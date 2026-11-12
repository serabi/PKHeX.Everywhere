import { crypto } from './crypto'
import type { InspectOptions } from './index'

interface ReaderExports { Inspect(bytes: Uint8Array, fileName: string | null, formatId: string | null): string }
let reader: ReaderExports | undefined
let initialized = false

globalThis.addEventListener('message', async ({ data }: MessageEvent) => {
  if (data.type === 'init' && !initialized) {
    initialized = true
    try {
      const url = new URL('_framework/dotnet.js', data.baseUrl).href
      const { dotnet } = await import(/* @vite-ignore */ url)
      const runtime = await dotnet.withModuleConfig({
        onDownloadResourceProgress(loaded: number, total: number) {
          globalThis.postMessage({ type: 'progress', progress: { stage: 'preparing', loaded, total } })
        },
      }).create()
      runtime.setModuleImports('pkhex-crypto', crypto)
      await runtime.runMain()
      const exports = await runtime.getAssemblyExports('PKHeX.Everywhere.Reader.Host')
      reader = exports.PKHeX.Everywhere.Reader.Host.ReaderExports
      globalThis.postMessage({ type: 'ready' })
    } catch {
      globalThis.postMessage({ type: 'failure' })
    }
  } else if (data.type === 'inspect' && reader) {
    const options = data.options as InspectOptions
    try {
      const result = JSON.parse(reader.Inspect(data.bytes, options.fileName ?? null, options.formatId ?? null))
      globalThis.postMessage({ type: 'result', id: data.id, result })
    } catch {
      globalThis.postMessage({ type: 'result', id: data.id, result: {
        status: 'error', schemaVersion: 1, parserRevision: 'serabi-reader-0.1.0',
        code: 'parser-failure', message: 'The parser could not inspect this file.',
      } })
    }
  }
})
