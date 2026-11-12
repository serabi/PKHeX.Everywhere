using PKHeX.Core;

namespace PKHeX.Facade.Tests;

public class PokedexSnapshotTests
{
    [Fact]
    public void ReadsSeenAndCaughtIndependentlyOfStorage()
    {
        var save = new SAV2(LanguageID.English, GameVersion.C);
        save.SetCaught(25, true);
        save.SetSeen(133, true);
        var snapshot = new Game(save).Pokedex;

        Assert.Equal("available", snapshot.Status);
        Assert.Equal("national-dex", snapshot.SpeciesNamespace);
        Assert.Equal(new[] { 25 }, snapshot.CaughtSpecies!);
        Assert.Equal(new[] { 133 }, snapshot.SeenSpecies!);
        Assert.Contains(251, snapshot.TrackedSpecies!);
    }

    [Fact]
    public void AvailableSnapshotsNormalizeIdsAndPreserveCoverage()
    {
        var snapshot = PokedexSnapshot.Available([133, 25, 25], [133], [25, 25], coverage: "partial");
        Assert.Equal(new[] { 25, 133 }, snapshot.TrackedSpecies!);
        Assert.Equal(new[] { 25 }, snapshot.CaughtSpecies!);
        Assert.Equal("partial", snapshot.Coverage);
        Assert.Throws<ArgumentException>(() => PokedexSnapshot.Available([25], [], [133]));
        Assert.Throws<ArgumentException>(() => PokedexSnapshot.Available([0], [], []));
    }

    [Fact]
    public void UnavailableHasNoSpeciesArrays()
    {
        var snapshot = PokedexSnapshot.Unavailable("Unknown layout.");
        Assert.Equal("unavailable", snapshot.Status);
        Assert.Null(snapshot.TrackedSpecies);
        Assert.Null(snapshot.SeenSpecies);
        Assert.Null(snapshot.CaughtSpecies);
    }

    [Fact]
    public void FireRedAndLeafGreenRequireATitleChoice()
    {
        var game = new Game(new SAV3FRLG());
        Assert.Equal(GameVersion.FRLG, game.DetectedVersion.Version);
        Assert.Equal(new[] { GameVersion.FR, GameVersion.LG }, game.DetectedVersions.Select(version => version.Version));
    }
}
