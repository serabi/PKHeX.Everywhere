# PKHeX Everywhere (Serabi fork)

This is a fork of [arleypadua/PKHeX.Everywhere](https://github.com/arleypadua/PKHeX.Everywhere).
We use it to build a small, read-only save reader for
[Just One More Catch](https://github.com/Interactive-Buffoonery/just-one-more-catch),
a Pokémon catch tracker.

For the web editor, the CLI and the `@pkhex-everywhere` npm packages, head to
upstream. That's where they're built and released. This fork is only for code used
on justonemorecatch.app.

## The save reader

The reader opens an in-game save in the browser and reports which species it has
marked as caught, plus a few details like the trainer name, play time and, for some
games, the day the adventure started. It runs on [PKHeX](https://github.com/kwsch/PKHeX)
in .NET WebAssembly, so the save never leaves the player's device. It doesn't edit or
export saves, and it can't run plug-ins.

The reader lives alongside upstream's code, which we keep as unchanged as we can so
updates merge cleanly:

- `src/PKHeX.Everywhere.Reader`: reads a save and returns a versioned result
- `src/PKHeX.Everywhere.Reader.Host`: the WebAssembly host, which can only inspect saves
- `src/PKHeX.Everywhere.Reader.Sdk`: the `@serabi/pokemon-save-reader` package, built
  as a local tarball rather than published to npm

[.serabi/READER.md](.serabi/READER.md) covers building, testing and adding games.

## License

GPL-3.0-or-later, matching PKHeX.Core and upstream.
