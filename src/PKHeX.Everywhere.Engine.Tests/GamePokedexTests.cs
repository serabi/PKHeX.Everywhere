using AwesomeAssertions;
using PKHeX.Facade.Tests.Base;
using static PKHeX.Everywhere.Engine.Tests.EngineCalls;
using static PKHeX.Everywhere.Engine.Tests.EngineResults;

namespace PKHeX.Everywhere.Engine.Tests;

public class GamePokedexTests
{
    [Fact]
    public void ReturnsSavedSpeciesIds()
    {
        var session = Loaded(SaveFilePath.Crystal);
        var dex = Value(Dispatch(session, "game.pokedex", "[]"))!;
        dex["status"]!.GetValue<string>().Should().Be("available");
        dex["speciesNamespace"]!.GetValue<string>().Should().Be("national-dex");
        dex["caughtSpecies"]!.AsArray().Select(id => id!.GetValue<int>()).Should()
            .Equal(session.Game!.Pokedex.CaughtSpecies!);
    }

    [Fact]
    public void NoSaveUsesTheExistingErrorContract() =>
        Error(Dispatch(new Session(), "game.pokedex", "[]")).Should().Be("no-save");

    [Fact]
    public void UnverifiedFormatsDoNotReportAnEmptyDex()
    {
        var dex = Value(Dispatch(Loaded(SaveFilePath.Unbound), "game.pokedex", "[]"))!;
        dex["status"]!.GetValue<string>().Should().Be("unavailable");
        dex["caughtSpecies"].Should().BeNull();
    }
}
