# Read-only save reader

The implementation lives on `serabi/read-only-reader`. Upstream's editor and
its APIs remain available. The browser reader has a separate host whose only
JavaScript export is `ReaderExports.Inspect`.

## Projects

- `PKHeX.Facade/PokedexSnapshot.cs` reads species flags and reports unavailable
  layouts. `Game.Pokedex` is the common API. `IPokedexProvider` is optional, so
  existing save formats do not need to change.
- `PKHeX.Everywhere.Engine` adds the thin `game.pokedex` query and generated SDK
  types. It does not change `game.progress` or existing DTO signatures.
- `PKHeX.Everywhere.Reader` parses an isolated copy and serializes a versioned
  result with source-generated JSON metadata.
- `PKHeX.Everywhere.Reader.Host` registers formats and the browser crypto
  providers. It references no Engine, plug-in host, or dispatcher. Its crypto
  adapters are linked from the existing host, so fixes remain shared.
- `PKHeX.Everywhere.Reader.Sdk` starts and terminates the worker and builds a
  private local package. Its `crypto.ts` matches the existing Engine SDK
  implementation; compare them when upstream changes its crypto bridge.

The full trimmer removes unreachable managed code, but Core's embedded data
remains substantial. A read-only public API does not mean every writer or
legality type disappears from dependencies. No source pruning or submodule
changes are used to reduce size.

## Build and validate

Use the .NET SDK in `global.json`, the `wasm-tools` workload, and Node 24.
Initialize submodules at their recorded commits. Do not update their pins as
part of a reader release unless the update is reviewed separately.

```sh
git submodule update --init --recursive
dotnet workload install wasm-tools
dotnet test src/PKHeX.Facade.Tests -c Release
dotnet test src/PKHeX.Everywhere.Engine.Tests -c Release
SERABI_FIXTURE_DIR=/tmp/serabi-reader-fixtures dotnet test src/PKHeX.Everywhere.Reader.Tests -c Release
dotnet build src/PKHeX.Everywhere.Engine.CodeGen -c Release

cd src/PKHeX.Everywhere.Engine.Sdk
npm ci --ignore-scripts
npm run typecheck
npm test
cd ../PKHeX.Everywhere.Reader.Sdk
npm ci --ignore-scripts
npm run typecheck
npm run build
npm test
SERABI_FIXTURE_DIR=/tmp/serabi-reader-fixtures npm run test:browser
npm pack --pack-destination /tmp
```

The browser test imports `playwright` from the development environment. Set
`SERABI_PLAYWRIGHT_MODULE` to an installed module's absolute path when needed.
It serves only local build assets and synthetic fixtures on an ephemeral port,
checks real WASM parsing, verifies input digests, and checks cancellation.
The test server records request methods to ensure no upload occurs.

Build sequentially with .NET tests: concurrent builds share upstream output
directories. The package build trashes only its generated assets and the reader
host's publish directory before replacing them, avoiding stale fingerprinted
files. It writes SHA-256 hashes, source revisions, a dirty-source marker, and
asset sizes to `assets/manifest.json`. Build a release from a clean commit and
check that `sourceDirty` is false. Never label an uncommitted build as a release.
The build bundles that commit's source and both pinned submodule sources in
three archives listed under `sourceArchives` in the manifest. Serve them with
the other assets so the distributed reader has matching source downloads.
Extract all three into the same folder to reconstruct the source tree. Separate
archives keep each asset below the static host's 25 MiB limit, which the build
checks. Runtime and CryptoJS notices also link to their exact upstream sources.

Copy the complete `assets/` tree under an immutable website path. Install the
matching local tarball in the website and record its SHA-256. Neither a registry
publish nor a server endpoint is needed. The website must keep raw save bytes
out of analytics, error reports, persistence, and backend mutation arguments.

## Fixture gate

`Reader.Tests` builds synthetic saves from pinned Core constructors and writes
only the `READER` trainer name. Small structural headers come from the pinned
Core's detection code. No personal or downloaded save is redistributed.
Each round-trip fixture has Pikachu (25) and Eevee (133) marked caught and no
stored Pokémon. This proves that released history survives without scanning
boxes. Facade tests independently set caught and seen flags and verify that a
seen-only species is excluded from caught ids. Tests also cover an empty dex,
unavailable providers, malformed files, unknown formats, and the size limit.

The end-to-end fixture matrix covers Crystal, both FRLG selections, HGSS,
USUM, Let's Go Pikachu, and Brilliant Diamond. FR and LG deliberately produce
the same candidate set; the file cannot recover a title selection. HGSS may
also remain ambiguous.

Sword and Scarlet currently have synthetic in-memory snapshot coverage, not
serialized browser fixture coverage. Core's blank block arrays do not serialize
to all the size/type combinations its autodetector accepts. Real supported
Switch files are still handled by pinned Core, but do not advertise new website
support from these snapshot tests alone. Add a permitted serialized fixture and
run it through the worker before enabling either game.

Browser results establish the tested Chromium path. Mobile performance, peak
worker memory, Safari, physical Switch extraction, and encrypted containers
need separate evidence. Measured compression sizes describe the artifacts,
not a guaranteed network transfer or startup time on another device.

## Add an official game

1. Check whether the pinned Core already detects the save and implements
   `HasPokeDex`, `GetSeen`, and `GetCaught` in National Pokédex ids. Inspect its
   actual implementation; do not infer this from the generation number.
2. Add a synthetic or licensed fixture with known flags, including a seen-only
   species, released history, empty storage, and an empty caught set. Include
   regional forms where they affect the getter. For new serialization families,
   test both native Core and the published browser worker.
3. Verify `Game.DetectedVersion` and `DetectedVersions`. Add a small Facade
   rule only when the stored format cannot distinguish games. Preserve the
   candidate list and require the website to resolve it.
4. Add the website's game-pack mapping and valid species universe. A parser
   release does not add encounter data. Follow that repository's pack sources
   and import validation rules. Ask for platform choice when multiple packs
   share a raw save format, such as GBA and Switch FRLG releases.
5. Rebuild, regenerate Engine SDK code if its contract changed, run the fixture
   gate, bump the private reader package and parser revision, and install the
   new local tarball and assets together. Keep the previous assets available.

## Add a ROM hack

Keep layout knowledge under `PKHeX.Everywhere.RomHacks`. A format's saved caught
flags and species mapping must be verified for named hack versions before it
implements `IPokedexProvider.ReadPokedex(SaveFile)`.

Return `PokedexSnapshot.Available(tracked, seen, caught, speciesNamespace,
coverage, warnings)`. Use `national-dex` only when every returned id has that
meaning. Use another namespace for custom species and add an explicit website
mapping. List only ids the provider can read, set `partial` when necessary,
and explain skipped ranges in warnings. Return `Unavailable(reason)` for
unrecognized revisions. Do not treat missing flags as a successful empty dex.
Never infer the caught set from party or boxes.

Add redistributable fixtures for each supported layout revision and at least
one rejected revision. Test detection ambiguity and prove no fallback to the
base game occurs. Register the format in the reader host, add the website's
pack and namespace mapping, and run the same browser gate. A format being
registered does not mean its Pokédex is supported. Existing registered hacks
have no reader providers yet and report unavailable honestly.

## Merge upstream updates

Keep `main` unchanged from upstream and merge it into the implementation branch.
Retain the installed pre-push hook and disabled upstream push URL described in
[README.md](README.md). Do not push upstream or publish its npm namespaces.

After merging, inspect changes to `SaveFile` getters, format detection,
`ISaveFormat`, browser cryptography, resource progress, and runtime loading.
Reconcile only the small Facade additions, Engine query, and host exemption.
Leave upstream shipped API manifests and generated code generation intact.
Record exact fork, Core, and Plugins revisions in the new artifact manifest.
Rebuild all fixtures and browser assets rather than reusing a previous package.
Review new dependency assets and licenses when the build's attribution check
stops on an unknown filename. Do not suppress that check to complete a build.

The package has no automated release or deployment workflow. Fork Actions stay
disabled and publishing remains a separate user-authorized action.

## Adventure-start date (reader 0.1.1)

Schema 1 adds optional top-level `startDate` (`YYYY-MM-DD`) on successful
snapshots. The allowlist is `SAV4HGSS`, `SAV7SM`, and `SAV7USUM` only.
At pinned Core `02c9ec108fe3c96735f6c2397eaebc10e29a049f`:

- `Saves/SAV4.cs` reads `SecondsToStart` at `AdventureInfo + 0x34`;
  `Saves/SAV4HGSS.cs` sets `AdventureInfo = 0`.
- `Saves/SAV7.cs` forwards `SecondsToStart` to `GameTime`;
  `Saves/Substructures/Gen7/GameTime7.cs` reads it at `0x28`.
- `Util/DateUtil.cs:GetDateTime2000` adds unsigned elapsed seconds to
  2000-01-01, without timezone conversion. Both `SAV_SimpleTrainer.cs` and
  `Gen7/SAV_Trainer7.cs` in the upstream WinForms save editors bind that
  conversion to the adventure-start date/time controls. Their corresponding
  `.Designer.cs` files define the format-specific date bounds.

The reader omits zero timestamps as uninitialized and limits dates to the
upstream editor ranges (HGSS 2000–2099; Gen7 2000–2050). It cannot establish whether a nonzero date
reflects the real-world start day, a changed console clock, or save editing.
Other layouts, including LeafGreen, omit the field. There is no file-date,
last-save, playtime, or Hall of Fame fallback. No upstream Core source changes
or new write/export APIs are required.

Generated serialized fixtures round-trip leap days and the maximum supported date for HG, SS,
SN, MN, US, and UM, plus zero/out-of-range omission and LeafGreen absence.
The browser gate parses the same fixtures through the published WASM worker
and checks input digests and absent-property semantics.

## Half-size Emerald recovery (reader 0.1.2)

`Reader/HalfSizeEmerald.cs` adds an inspection-only fallback for exactly 65,536
bytes. Pinned Core's `SaveUtil.IsG3` only autodetects 128 KiB, although `SAV3`
handles a single 56 KiB main slot. Before extending a private copy with erased
bytes, the fallback checks all 14 unique section IDs, the 0x08012025 signatures
and a consistent save counter. Normal `Game.LoadFrom` detection retains registered
format ambiguity and expanded-species checks; only a checksum-valid `SAV3E` with
no custom format can succeed. Neither Core nor its submodule pin changes.

The returned National Dex snapshot has complete flag coverage for the surviving
slot and an explicit warning that a newer save may be missing. It is not a claim
that the missing half was reconstructed. The website must display warnings before
creation. No raw save write/export API is exposed, and the caller's bytes stay
unchanged.

Native and WASM fixtures cover caught history with empty storage, an empty dex,
rotated sections, checksum errors, missing and duplicate sections, invalid
signatures, mixed counters, blank data, RS/FRLG layouts and an expanded-species
format choice. They are synthetic; a permitted real emulator export is a separate
check. To find one, request an existing 65,536-byte in-game `.sav` or `.srm` from
its owner. Reproducing a fresh save in a disposable emulator profile configured
for 64 KiB flash is another candidate, but needs a user-supplied ROM and verified
output; never change the player's active profile or truncate their only save.

## Checksum warnings (reader 0.1.3)

When Core reports invalid checksums on a loaded save, `pokedex.warnings` includes:

> This save's checksum doesn't match. It may have been edited or damaged.

Inspection still returns `status: "ok"` with the same caught flags and coverage.
Display the warning before confirming an import. Existing provider and recovery
warnings remain. Valid 64 KiB Emerald recovery passes Core's checksum check and
keeps only its missing-slot warning; damaged half-size saves remain rejected.
