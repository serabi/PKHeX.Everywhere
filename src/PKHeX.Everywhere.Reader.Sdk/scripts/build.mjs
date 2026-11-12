import { execFileSync } from 'node:child_process'
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createHash } from 'node:crypto'
import { gzipSync, brotliCompressSync, constants } from 'node:zlib'
import { build } from 'esbuild'

const sdk = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repo = resolve(sdk, '../..')
const host = resolve(sdk, '../PKHeX.Everywhere.Reader.Host')
const pkg = JSON.parse(readFileSync(join(sdk, 'package.json'), 'utf8'))
const published = join(host, 'bin/Release/net10.0/publish')
if (existsSync(published)) execFileSync('trash', [published])
execFileSync('dotnet', ['publish', join(host, 'PKHeX.Everywhere.Reader.Host.csproj'), '-c', 'Release', '--nologo'], { stdio: 'inherit' })
const assets = join(sdk, 'assets')
if (existsSync(assets)) execFileSync('trash', [assets])
mkdirSync(assets)
cpSync(join(host, 'bin/Release/net10.0/publish/wwwroot/_framework'), join(assets, '_framework'), {
  recursive: true, filter: (path) => !/\.(br|gz)$/.test(path),
})
await build({ entryPoints: [join(sdk, 'src/worker.ts')], outfile: join(assets, 'worker.js'), bundle: true, format: 'esm', platform: 'browser', target: 'es2022', minify: true })
await build({ entryPoints: [join(sdk, 'src/index.ts')], outfile: join(sdk, 'dist/index.js'), bundle: true, format: 'esm', platform: 'browser', target: 'es2022' })
execFileSync(join(sdk, 'node_modules/.bin/tsc'), ['--emitDeclarationOnly'], { cwd: sdk, stdio: 'inherit' })
cpSync(join(repo, 'LICENSE'), join(sdk, 'LICENSE'))
const git = (...args) => execFileSync('git', args, { cwd: repo, encoding: 'utf8' }).trim()
const restored = JSON.parse(readFileSync(join(host, 'obj/project.assets.json'), 'utf8'))
const runtimeVersion = Object.keys(restored.libraries).find((name) => name.startsWith('Microsoft.NET.Sdk.WebAssembly.Pack/')).split('/')[1]
const sdkDirectory = execFileSync('dotnet', ['--list-sdks'], { encoding: 'utf8' }).trim().split('\n').at(-1).match(/\[(.+)\]/)[1]
const runtimePack = resolve(sdkDirectory, '../packs/Microsoft.NETCore.App.Runtime.Mono.browser-wasm', runtimeVersion)
const runtimeSpec = readFileSync(join(runtimePack, 'Microsoft.NETCore.App.Runtime.Mono.browser-wasm.nuspec'), 'utf8')
const runtimeCommit = runtimeSpec.match(/<repository[^>]*commit="([^"]+)"/)[1]
const notices = [
  { name: 'PKHeX.Everywhere reader', version: pkg.version, license: 'GPL-3.0-or-later', source: `https://github.com/serabi/PKHeX.Everywhere/tree/${git('rev-parse', 'HEAD')}`, text: readFileSync(join(repo, 'LICENSE'), 'utf8') },
  { name: 'PKHeX.Core', license: 'MIT', source: `https://github.com/arleypadua/PKHeX/tree/${git('-C', 'external/PKHeX', 'rev-parse', 'HEAD')}`, text: readFileSync(join(repo, 'external/PKHeX/LICENSE'), 'utf8') },
  { name: '.NET browser runtime', version: runtimeVersion, license: 'MIT', source: `https://github.com/dotnet/dotnet/tree/${runtimeCommit}`, text: readFileSync(join(runtimePack, 'LICENSE.TXT'), 'utf8'), thirdPartyNotices: readFileSync(join(runtimePack, 'THIRD-PARTY-NOTICES.TXT'), 'utf8') },
]
for (const file of readdirSync(join(assets, '_framework'))) {
  if (!/^(PKHeX\.(Core|Facade|Everywhere\.(Reader(\.Host)?|RomHacks))\.[a-z0-9]+\.wasm|System\..+\.wasm|dotnet(\..+)?\.js|dotnet\.native\.[a-z0-9]+\.wasm)$/.test(file))
    throw new Error(`Review dependency attribution for new framework asset: ${file}`)
}
notices.push({ name: 'CryptoJS', version: '4.2.0', license: 'MIT', source: 'https://github.com/brix/crypto-js/tree/4.2.0', text: readFileSync(join(sdk, 'node_modules/crypto-js/LICENSE'), 'utf8') })
writeFileSync(join(sdk, 'notices.json'), JSON.stringify(notices, null, 2) + '\n')
cpSync(join(sdk, 'notices.json'), join(assets, 'notices.json'))
const sourceArchives = ['reader-source.tar.gz', 'pkhex-source.tar.gz', 'plugins-source.tar.gz']
for (const [index, directory] of ['', 'external/PKHeX', 'external/PKHeX-Plugins'].entries()) {
  execFileSync('git', ['archive', '--format=tar.gz', `--prefix=PKHeX.Everywhere/${directory ? directory + '/' : ''}`, '-o', join(assets, sourceArchives[index]), 'HEAD'], { cwd: join(repo, directory) })
}
const files = readdirSync(assets, { recursive: true, withFileTypes: true }).filter((entry) => entry.isFile()).map((entry) => {
  const path = join(entry.parentPath, entry.name)
  const bytes = readFileSync(path)
  if (bytes.length > 25 * 1024 * 1024) throw new Error(`Asset exceeds the static host's 25 MiB limit: ${path}`)
  return { path: relative(assets, path), bytes: bytes.length, gzipBytes: gzipSync(bytes).length,
    brotliBytes: brotliCompressSync(bytes, path.endsWith('.tar.gz') ? { params: { [constants.BROTLI_PARAM_QUALITY]: 4 } } : {}).length,
    sha256: createHash('sha256').update(bytes).digest('hex') }
})
const manifest = {
  schemaVersion: 1, package: pkg.name, version: pkg.version, sourceCommit: git('rev-parse', 'HEAD'),
  sourceDirty: git('status', '--porcelain').length > 0,
  coreRevision: git('-C', 'external/PKHeX', 'rev-parse', 'HEAD'),
  pluginsRevision: git('-C', 'external/PKHeX-Plugins', 'rev-parse', 'HEAD'),
  sourceArchives,
  files,
  totals: files.reduce((total, file) => ({ bytes: total.bytes + file.bytes, gzipBytes: total.gzipBytes + file.gzipBytes, brotliBytes: total.brotliBytes + file.brotliBytes }), { bytes: 0, gzipBytes: 0, brotliBytes: 0 }),
}
writeFileSync(join(assets, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n')
console.log(JSON.stringify({ version: pkg.version, files: files.length, ...manifest.totals }))
