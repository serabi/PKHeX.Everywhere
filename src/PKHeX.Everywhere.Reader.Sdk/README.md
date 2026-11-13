# Pokémon save reader

`@serabi/pokemon-save-reader` reads saved Pokédex flags inside a dedicated
browser worker. It creates no requests containing save bytes and exposes no
save editing, export, plug-in loading, or generic Engine dispatch methods.
Runtime assets are fetched from the URL supplied by the host website.

This package is distributed as a local tarball. Registry publishing is disabled
by `private: true`.

```ts
import { createSaveReader } from '@serabi/pokemon-save-reader'

const controller = new AbortController()
const reader = await createSaveReader({
  baseUrl: '/save-reader/0.1.3/',
  signal: controller.signal,
  onProgress: ({ loaded, total }) => {
    // Resource counts from .NET, not transferred byte counts.
  },
})
try {
  const result = await reader.inspect(new Uint8Array(await file.arrayBuffer()), {
    fileName: file.name,
  })
} finally {
  reader.dispose()
}
```

Copy the entire package `assets/` directory to the versioned URL above before
using it. It contains `worker.js`, `_framework/`, `notices.json`, and
`manifest.json`. It also includes three source archives containing the release's
fork source and both pinned submodule sources. Extract them into the same folder
to restore the source tree. Keep every version immutable.
Keep `dist/index.js` in the
website's normal bundle; the runtime is loaded only when `createSaveReader`
is called. Serve JavaScript modules and WASM with their correct MIME types.
Use normal HTTP compression. The build records raw, gzip, and Brotli sizes
without shipping duplicate compressed files.

`createSaveReader` resolves after the worker is ready. Its optional signal can
cancel preparation as well as inspection. Aborting or calling `dispose()`
terminates the worker and rejects pending work with `AbortError`. A new import
can create a new worker. The caller's `Uint8Array` stays attached and unchanged.
`MAX_INPUT_BYTES` is 64 MiB; check `File.size` before reading it into memory too.

`onProgress` first receives `{stage: 'preparing'}`. Later events may include
`loaded` and `total` resource counts reported by .NET. Show indeterminate
progress when no total is available. Parsing has no measured percentage.

An inspection resolves to a schema version 1 result. Successful results include
the parser revision, detected game, and an available Pokédex snapshot. Species
arrays are sorted and unique. `caughtSpecies` comes from saved caught flags,
including released Pokémon. Party and box contents do not define caught history.
Forms share a species id. Official saves use `national-dex` ids.

Successful snapshots optionally include top-level `startDate` as `YYYY-MM-DD`.
This is the saved adventure-start calendar date, with no timezone conversion.
Version 0.1.1 verifies HGSS and Sun/Moon/Ultra Sun/Ultra Moon only. Zero
(uninitialized) timestamps and dates outside the upstream editor ranges (HGSS 2000–2099; Gen7 2000–2050) are omitted, as are all
other layouts, including LeafGreen. Missing dates are not inferred from file
metadata, play time, last save, or Hall of Fame records.

Game values use PKHeX enum names, such as `C`, `US`, and `UM`. `game.versions`
lists candidate individual games. FireRed and LeafGreen saves have no reliable
stored title discriminator: both return `version: 'FRLG'` and candidates
`['FR', 'LG']`. HGSS may also need a choice. The website must ask before choosing
among supported games or platforms. A parser-supported title still needs a
website game pack and mapping before a playthrough can be created.

Errors have `status: 'error'`, `code`, and `message`. Codes are `invalid-save`,
`unsupported-format`, `format-choice-required`, `unsupported-pokedex`,
`input-too-large`, and `parser-failure`. A format choice may include
`candidateFormats` and `pkhexGame`. Pass an explicitly selected candidate id
as `formatId` to inspect again. Do not silently retry a hack as its base game.
An unavailable Pokédex returns an error, never a successful empty caught set.
Preparation errors reject the creation promise.

Version 0.1.2 also reads 64 KiB Emerald saves when all 14 main sections are
present, their signatures and save counters agree, and Core validates their
checksums and Emerald identity. A private inspection copy is extended with erased
bytes for Core's 128 KiB autodetector, preserving registered-format choices and
expanded-species checks. No repaired save is returned or written. Successful
results include a `pokedex.warnings` message: the surviving slot may be older
than a missing slot. Display this warning before confirming the import.
Missing or corrupt sections and 64 KiB Ruby/Sapphire or FRLG layouts are rejected.
This does not establish support for emulator save states or ROM hacks.

Use extracted game saves. ZIP archives are rejected before Core's archive
handler runs. Emulator save states, ROMs, and encrypted console backup
containers are not supported inputs. Unknown hacks can resemble an official
save; detection cannot prove a file's origin. Known formats without a verified
Pokédex provider report `unsupported-pokedex`.

See [reader maintenance](https://github.com/serabi/PKHeX.Everywhere/blob/serabi/read-only-reader/.serabi/READER.md) for builds, fixtures, new games,
ROM hack providers, upstream updates, and the verification limits.

The package retains GPL-3.0-or-later licensing. Ship its license and
`notices.json` with the assets, and provide the corresponding fork source at
the release commit. Notices include PKHeX.Core, .NET and its third-party
notices, and the bundled CryptoJS license.

## Checksum warnings (reader 0.1.3)

When Core reports invalid checksums on a loaded save, `pokedex.warnings` includes:

> This save's checksum doesn't match. It may have been edited or damaged.

Inspection still returns `status: "ok"` with the same caught flags and coverage.
Display the warning before confirming an import. Existing provider and recovery
warnings remain. Valid 64 KiB Emerald recovery passes Core's checksum check and
keeps only its missing-slot warning; damaged half-size saves remain rejected.
