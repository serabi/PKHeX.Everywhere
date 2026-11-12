# Read-only save import plan

Status: proposal only. No application changes, package builds, dependency
installation, or submodule updates are part of this task.

Starting upstream commit: `6373c4c7de5405cdeafc72ecbe826390c4622b17`.
The fork's `main` stays at upstream. This plan lives on
`serabi/read-only-import-plan`.

## Outcome

A player selects a supported game save in Just One More Catch. The browser
reads its caught-species flags, previews a matching game and caught count,
and creates a new playthrough with those species marked caught. The player
can cancel before creating anything. Existing playthroughs remain intact.

The browser processes the save locally. The website stores its usual
playthrough state, with no raw save upload, save-file storage, or new parsing
server. Guests use localStorage; signed-in players use the existing account
storage. Loading a file into the parser does not need to change the file.

Support official games through the parser's existing format detection,
including Switch games as fixtures establish compatibility. Keep an extension
point for popular ROM hacks. The website can import only games for which it
has a game pack. Parser support alone does not add a website game pack.

The reader exposes no save editing, save export, legality checking,
auto-legality, encounter search, transfers, plugin execution, or editor UI.
Internal parsing dependencies may still contain unused writing code. Removing
every internal writer is not a condition for shipping a read-only interface.

## What the current source already provides

| Source | Current behavior | Consequence |
| --- | --- | --- |
| [Facade Game](../src/PKHeX.Facade/Game.cs) | Loads bytes through registered formats or PKHeX detection; exposes progress and game identity | Reuse the loader and its format-choice behavior |
| [Facade progress](../src/PKHeX.Facade/Progress.cs) | Returns seen/caught totals | Add a species-level query; totals cannot mark individual species |
| [Engine game handlers](../src/PKHeX.Everywhere.Engine/Handlers/GameHandlers.cs) | Exposes `game.load`, `game.progress`, formats, and editing/export operations | Preserve existing calls; give the reader its own allowed operations |
| [Engine dispatcher](../src/PKHeX.Everywhere.Engine/Dispatcher.cs) | Dispatches generated handlers and registered session handlers | Do not expose the unrestricted dispatcher through the reader |
| [Save format interface](../src/PKHeX.Facade/Abstractions/ISaveFormat.cs) | Owns format detection, loading, capabilities, and game-data access | Extend format support without hard-coding hack names in generic layers |
| [Host startup](../src/PKHeX.Everywhere.Engine.Host/Program.cs) | Registers hack formats and starts the plugin host | Reuse format registrations in a reader host; omit plugins |
| [Host project](../src/PKHeX.Everywhere.Engine.Host/PKHeX.Everywhere.Engine.Host.csproj) | Publishes .NET WASM with partial trimming | Measure release output before choosing further trimming changes |
| [Facade project](../src/PKHeX.Facade/PKHeX.Facade.csproj) | References Core.AutoMod in the plugin submodule | Omitting the plugin host alone may leave auto-legality dependencies |
| [Engine SDK](../src/PKHeX.Everywhere.Engine.Sdk/packages/engine/package.json) | Already has a framework-independent TypeScript package and Vite helper | Reuse packaging/runtime patterns without introducing React into the website |
| [Submodule configuration](../.gitmodules) | Pins the upstream maintainer's Core and plugin forks | Keep both URLs and pins unchanged for the first implementation |

Follow [ADR 0004](../docs/adr/0004-domain-logic-lives-in-the-facade.md)
for domain logic and [ADR 0008](../docs/adr/0008-rom-hacks-are-converted-at-the-save-boundary.md)
for hack-specific behavior. Respect public API compatibility and
[ADR 0009](../docs/adr/0009-a-breaking-change-to-the-facades-api-bumps-the-plug-in-sdk.md).

The website currently uses plain TypeScript and Vite. Its `client/game.ts`
stores caught species in a `Set<number>` and serializes them with the rest
of the run. `client/types.ts` defines the Bridge; `client/main.ts` owns the
Clerk and Convex connections. Its current packs cover Crystal, FRLG, HGSS,
and USUM. Check those seams again before implementation.

## API contract

Add a generic, read-only Pokédex snapshot to the Facade, then expose it as
an additive Engine query, provisionally `game.pokedex`. Keep `game.progress`
and its existing DTO constructors unchanged. Update public API manifests
and regenerate the TypeScript client/types through the existing generator.
Do not edit generated files by hand.

The snapshot needs these fields:

| Field | Meaning |
| --- | --- |
| `status` | `available` or `unavailable` |
| `coverage` | For available data, `complete` or `partial` for the declared tracked species |
| `speciesNamespace` | `national-dex` for canonical official IDs; a stable format-specific namespace for custom IDs |
| `trackedSpecies` | IDs whose caught flags this parser can interpret, independent of the regional dex display |
| `seenSpecies`, `caughtSpecies` | Sorted, unique IDs in that namespace, restricted to the tracked set |
| `reason` | A stable reason when unavailable, such as `not-supported` or `not-readable` |
| `warnings` | Stable codes explaining partial coverage or mapping limitations |

Use a discriminated union in the generated contract. Unavailable results
must not contain empty arrays that a caller can mistake for a successfully
read empty Pokédex. An available snapshot with zero caught species is valid.
Do not infer caught history solely from the current party and boxes. Released,
traded, and evolved Pokémon can still have caught flags.

For official formats, read through Core's supported per-species accessors
after verifying their semantics at the pinned revision. Establish the valid
ID range and tracked set per game. Do not treat a regional dex number as a
National Dex number, or derive the full set solely from personal-data presence.
Verify flags across multiple dexes, DLC, forms, and unusual Switch storage
layouts with fixtures. Species-level caught state is the first contract;
form-specific completion remains outside the first website importer.

For hacks, keep decoding and ID conversion inside the existing format/data
boundary. Add an optional provider contract or compatible default interface
member after checking the repository's API rules. Do not add a required member
that breaks every existing format implementation. Only the RomHacks project
knows individual hacks. Its provider may emit canonical National Dex IDs only
when a tested mapping exists. Custom species retain their format namespace.
An unsupported hack must return unavailable or partial data with a reason,
never pretend its base game's flags are correct.

The proposed browser package has a small independent contract:

```ts
const reader = await createSaveReader();
const result = await reader.inspect(bytes, { fileName, formatId });
reader.dispose();
```

This is a proposal, not an implemented API. `inspect` returns:

- A schema version and parser build revision.
- Detected game/version identity, format ID, and any unresolved game choices.
- The Pokédex snapshot described above.
- Optional trainer name and play time for the preview, kept out of persistence
  unless the player chooses a corresponding website field.

Keep this JSON contract independent of website pack names and account IDs.
Use a stable error union for invalid save, unsupported format, format choice
required, unsupported Pokédex, oversized input, and parser failure. Preserve
the existing format-choice candidates and base-game hint. Do not silently
retry a hack as its base game. Each inspection owns isolated state and must
not return a previous file's result after an error or overlapping request.

The API accepts extracted saves in supported formats. Emulator save states,
ROM files, and encrypted console backup containers need separate handling;
the importer must explain an unsupported input without claiming to decrypt it.

## Delivery sequence

### 1. Establish fixtures and dependency costs

During the later implementation task, initialize the pinned submodules and
use the repository's documented toolchain. Inventory the read path's references,
especially `Game` construction, Facade's AutoMod reference, generated handler
registration, and the WASM host. No such build runs during this planning task.

Measure the existing release package and a minimal reader prototype on the
same revision. Record compressed transfer bytes, loaded bytes, startup time,
peak memory, and parse time on desktop and a representative mobile browser.
Set an acceptable budget from those measurements. Package archive size alone
does not establish browser startup cost, and no reduction percentage is promised.

Choose permitted, redistributable fixtures with independently checked species
flags. Avoid committing personal saves with trainer identifiers. Include an
empty caught set, released Pokémon still marked caught, and at least one save
where box contents differ from caught history.

Exit when the implementation has a fixture matrix and a dependency report
that identifies which costs a separate host can actually remove.

### 2. Add the reusable Pokédex API

Keep the first implementation change small and suitable for upstream review:

- Add the snapshot model and reading logic under `src/PKHeX.Facade/`.
- Add a thin DTO mapping/query under `src/PKHeX.Everywhere.Engine/`.
- Add format-specific providers under `src/PKHeX.Everywhere.RomHacks/` as their
  fixtures establish support. Implement the extension point before promising
  any additional hack.
- Extend the existing Facade and Engine test projects. Use the same fixtures
  to check domain results and the public JSON contract.
- Update `PublicAPI.Unshipped.txt`, generated SDK files, and a changeset where
  required by the existing release policy.

Retain all existing APIs and behavior. The generic query must also work in
upstream's full engine. Upstream contribution remains a separate decision;
this task does not open an upstream PR or issue.

### 3. Add a separate reader host and package

Add a sibling host, provisionally `PKHeX.Everywhere.Reader.Host`, and a small
TypeScript entry point. Keep the existing editor host and packages unchanged.
Reuse crypto/runtime initialization, save loading, game identity, the new
Pokédex logic, and the existing ROM-hack registrations where possible.

Expose inspection and resource disposal through a dedicated interop entry
point. Do not route arbitrary call names to the existing dispatcher. Exclude
plugin loading, plugins supplied by callers, mutation commands, and export
commands. A TypeScript wrapper that hides methods while leaving a generic
editing dispatcher callable does not meet this requirement.

Start with existing Facade/Core dependencies. Use linker output to determine
whether unused AutoMod, legality, editor, and plugin code remains reachable.
Prefer a small shared read component if needed to break that dependency.
Place any shared domain logic in the Facade layer, with generic adapters in
the host. Avoid copying parser code or moving many upstream files just to
reduce the first bundle. Treat deeper project splitting as a later change
with its own compatibility tests. Do not modify Core or plugin submodule
source or pins without revisiting the plan.

Package the reader with its required WASM assets and notices. Initially consume
a versioned local tarball in the website integration branch. A public fork
package needs a confirmed Serabi-owned publishing destination; never publish
under upstream's package identity. Do not enable upstream's deployment or
release workflows for this purpose.

Exit when the reader passes the fixture matrix, rejects editing/export calls,
and has measured size/startup results against the same-revision baseline.

### 4. Integrate Just One More Catch

Make this a separate website change. Lazy-load the reader only after the player
chooses import, and load its assets from the site's own static deployment.
Run parsing in a dedicated worker if the runtime supports it; measure mobile
responsiveness before accepting main-thread parsing. Bound input size according
to supported formats and provide cancel/retry behavior.

Add an adapter between parser results and website game packs. A save's numeric
version enum is not a website version ID. Resolve ambiguous pairs through an
explicit player choice. Require a matching pack and tested namespace mapping.
Do not map unsupported custom species by name or numerical coincidence.
The preview shows the matched game, recognized caught count, and unsupported
species or incomplete coverage. The player explicitly creates the new run.

Use normal default run state and set its caught IDs to the validated intersection
with the pack. Leave route progress, story events, notes, chosen starter,
encounter provenance, and started date at their defaults unless separate
evidence and an approved field mapping justify them. Caught flags alone do
not establish those facts. For partial data, explain what the import covers
before the player continues. An unavailable snapshot cannot create a run.

Keep backend access inside `client/main.ts` and expose it through the Bridge.
The current `createRun(version)` followed by `saveRun(id, state)` can leave an
empty run if the second operation fails. Plan an additive Bridge operation
that creates a run with its validated initial state in one operation. For an
account, implement it through an authenticated Convex mutation with the existing
ownership, run-limit, and state-size validation. For guests, implement the same
contract in the guest adapter with localStorage failure handling. Read the
project's generated Convex guidelines before designing or changing that mutation.
This stores ordinary tracker JSON only. It is not a file-upload endpoint.

Flush pending edits on the active run before switching. Do not create anything
on a failed parse, cancelled preview, unsupported game, or failed persistence.
Prevent duplicate submissions. Handle sign-in changes and stale async parse
results without saving to the wrong account or replacing another run. Preserve
the existing migration of guest runs on sign-in and guest use when Clerk fails.

Exit when browser checks prove the complete flow for guests and accounts,
including failure recovery, without sending save bytes over the network.

### 5. Release and maintain

The repository and engine package declare GPL-3.0-or-later. Retain license and
dependency notices, record the exact source revision, and settle compatibility
and source-distribution requirements for the combined website before release.
This plan does not establish a legal conclusion about that combination.

Use the existing static Cloudflare hosting. Before deployment, check the
actual asset limits, WASM MIME type, compression, CSP, cache behavior, and
versioned asset URLs against the measured package. Do not choose a new host
or introduce a parsing service to solve an unmeasured bundle-size concern.

Keep parser version, generated types, and WASM assets synchronized. Test the
reader against the fixture matrix after each upstream update. A new upstream
format is not automatically a verified website import target. New game packs
and new hacks get their own source/fixture work.

## Verification required during implementation

| Area | Required evidence |
| --- | --- |
| Current website packs | Crystal, FRLG, HGSS, and USUM fixtures produce the expected canonical caught IDs |
| Switch | Representative supported formats, including LGPE, SWSH, BDSP, PLA, and SV where the pinned parser supports them; no promise for untested titles or revisions |
| ROM hacks | One explicitly supported hack proves the provider and mapping; other registered formats return honest availability until tested |
| Pokédex semantics | Empty set, released/traded/evolved history, regional versus national IDs, multiple dexes, and partial coverage |
| Damaged input | Truncation, random bytes of a plausible size, unsupported containers, format ambiguity, and checksum/active-slot behavior follow established loader policy |
| Read-only behavior | Input bytes hash identically before/after inspection; no write/export path or arbitrary dispatcher/plugin entry point is exposed |
| Isolation | Consecutive files, overlapping requests, cancel, and failed parses cannot leak earlier results |
| Runtime | Release WASM parses the same fixtures as .NET tests; trimming does not remove format readers or crypto dependencies |
| Website | Guest/account import, Clerk unavailable, storage failure, account change, cancel, duplicate click, pack mismatch, and network inspection |
| Upstream compatibility | Existing engine/editor checks still pass; public APIs retain their prior signatures |

Use Facade and Engine unit tests for the contract, browser tests for the
published reader, and the website's required `pnpm check` plus focused browser
flows for integration. Run each repository's required checks when its code
changes. This planning task needs only documentation,
Git state, and hook verification; it does not run application checks or builds.

## Keep upstream updates manageable

Maintain four logical changes: fork documentation/guards, the generic query,
the reader host/package, and the website adapter. Keep the website-specific
mapping out of the parser. Prefer new files and compatible members over
conditional branches throughout the editor. Keep existing package names,
submodules, workflows, and formatting intact except for changes that the
reader actually requires.

Keep `main` identical to upstream and merge its updates into the fork's work
branches. The [fork guide](README.md) contains commands and hook setup.
Record upstream and submodule revisions with each released artifact. Resolve
conflicts in generated output by regenerating from source. Run compatibility
checks before adopting a new upstream revision in the website.

## Decisions and limits

- Build the browser reader as a separate package/host; preserve the editor.
- Add a reusable caught-species API before website integration.
- Keep official-game breadth; verify Switch formats instead of removing their
  readers as a size shortcut.
- Keep hack decoding optional and format-owned. Per-hack caught support requires
  tested layouts and mappings.
- Measure before choosing deeper dependency changes. The final bundle size,
  mobile startup cost, and degree of AutoMod removal remain unknown.
- Confirm a fork package destination and license compatibility before publishing
  an application artifact. Neither is needed to keep the plan in this fork.
- Do not implement, build, deploy, or publish packages until Sarah starts that
  next task. This branch contains only the plan and the requested Git guard.
