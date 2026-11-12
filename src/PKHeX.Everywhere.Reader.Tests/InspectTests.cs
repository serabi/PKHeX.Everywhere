using System.Text.Json;
using PKHeX.Core;
using PKHeX.Facade;
using PKHeX.Facade.Abstractions;
using PKHeX.Facade.Repositories;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Everywhere.Reader.Tests;

public class InspectTests
{
    [Theory]
    [InlineData(GameVersion.C)]
    [InlineData(GameVersion.FR)]
    [InlineData(GameVersion.LG)]
    [InlineData(GameVersion.HG)]
    [InlineData(GameVersion.SS)]
    [InlineData(GameVersion.US)]
    [InlineData(GameVersion.UM)]
    [InlineData(GameVersion.GP)]
    [InlineData(GameVersion.BD)]
    public void ReadsSavedHistoryWithoutChangingInput(GameVersion version)
    {
        var save = SyntheticSave(version);
        foreach (ushort species in new ushort[] { 25, 133 })
        {
            var pokemon = save.BlankPKM;
            pokemon.Species = species;
            pokemon.CurrentLevel = 5;
            save.SetBoxSlotAtIndex(pokemon, 0);
            save.SetBoxSlotAtIndex(save.BlankPKM, 0, EntityImportSettings.None);
        }
        switch (save)
        {
            case SAV2 gen2: gen2.SetSeen(1, true); break;
            case SAV3 gen3: gen3.SetSeen(1, true); break;
            case SAV4 gen4: gen4.Dex.SetSeen(1); break;
            case SAV7 gen7: gen7.Zukan.SetSeen(1, 0, true); break;
        }
        var bytes = WriteFixture(save, version);
        var before = (byte[])bytes.Clone();
        Assert.NotNull(SaveUtil.GetSaveFile((byte[])bytes.Clone()));
        _ = Game.LoadFrom((byte[])bytes.Clone(), format: SaveFormats.PKHeX).Pokedex;
        var json = SaveReader.Inspect(bytes, "synthetic.sav", "pkhex");
        using var result = JsonDocument.Parse(json);
        Assert.True(result.RootElement.GetProperty("status").GetString() == "ok", json);
        var dex = result.RootElement.GetProperty("pokedex");
        Assert.Equal(new[] { 25, 133 }, dex.GetProperty("caughtSpecies").EnumerateArray().Select(id => id.GetInt32()));
        if (save is SAV2 or SAV3 or SAV4 or SAV7)
            Assert.Contains(1, dex.GetProperty("seenSpecies").EnumerateArray().Select(id => id.GetInt32()));
        Assert.Equal(before, bytes);
        var versions = result.RootElement.GetProperty("game").GetProperty("versions").EnumerateArray().Select(item => item.GetString());
        Assert.Contains(version.ToString(), versions);
        Assert.DoesNotContain(Game.LoadFrom((byte[])bytes.Clone(), format: SaveFormats.PKHeX).Trainer.PokemonBox.All, slot => !slot.IsEmpty);
        ExportFixture(version.ToString(), bytes, json);
    }

    [Theory]
    [InlineData(GameVersion.HG)]
    [InlineData(GameVersion.SS)]
    [InlineData(GameVersion.SN)]
    [InlineData(GameVersion.MN)]
    [InlineData(GameVersion.US)]
    [InlineData(GameVersion.UM)]
    public void StartDatesRoundTripAsCalendarDates(GameVersion version)
    {
        var save = SyntheticSave(version);
        foreach (var date in new[] { new DateTime(2000, 2, 29), new DateTime(2024, 2, 29), new DateTime(save is SAV4HGSS ? 2099 : 2050, 12, 31) })
        {
            save.SecondsToStart = (uint)(date.AddHours(23).AddMinutes(59) - new DateTime(2000, 1, 1)).TotalSeconds;
            var bytes = WriteFixture(save, version);
            var before = (byte[])bytes.Clone();
            var json = SaveReader.Inspect(bytes, formatId: "pkhex");
            using var result = JsonDocument.Parse(json);
            Assert.Equal("ok", result.RootElement.GetProperty("status").GetString());
            Assert.Equal(date.ToString("yyyy-MM-dd"), result.RootElement.GetProperty("startDate").GetString());
            Assert.Equal(before, bytes);
            ExportFixture($"{version}-date-{date:yyyy-MM-dd}", bytes, json);
        }
    }

    [Theory]
    [InlineData(GameVersion.HG, 0u)]
    [InlineData(GameVersion.US, 0u)]
    [InlineData(GameVersion.HG, uint.MaxValue)]
    [InlineData(GameVersion.US, uint.MaxValue)]
    [InlineData(GameVersion.LG, 123456789u)]
    public void UnavailableStartDatesAreOmitted(GameVersion version, uint seconds)
    {
        var save = SyntheticSave(version);
        save.SecondsToStart = seconds;
        var bytes = WriteFixture(save, version);
        var json = SaveReader.Inspect(bytes, formatId: "pkhex");
        using var result = JsonDocument.Parse(json);
        Assert.Equal("ok", result.RootElement.GetProperty("status").GetString());
        Assert.False(result.RootElement.TryGetProperty("startDate", out _));
        ExportFixture($"{version}-date-absent-{seconds}", bytes, json);
    }

    private static byte[] WriteFixture(SaveFile save, GameVersion version)
    {
        var bytes = save.Write().ToArray();
        if (version == GameVersion.C)
        {
            bytes[0x2866] = 0xFF;
            bytes[0x2D11] = 0xFF;
        }
        if (version == GameVersion.GP)
        {
            WriteUInt32LittleEndian(bytes.AsSpan(0xB8800 - 0x1F0), 0x42454546);
            WriteUInt16LittleEndian(bytes.AsSpan(0xB8800 - 0x200 + 0xB0), 0x13);
        }
        return bytes;
    }

    private static void ExportFixture(string name, byte[] bytes, string json)
    {
        var fixtureDirectory = Environment.GetEnvironmentVariable("SERABI_FIXTURE_DIR");
        if (fixtureDirectory is not null)
        {
            Directory.CreateDirectory(fixtureDirectory);
            File.WriteAllBytes(Path.Combine(fixtureDirectory, name + ".sav"), bytes);
            File.WriteAllText(Path.Combine(fixtureDirectory, name + ".json"), json);
        }
    }

    private static SaveFile SyntheticSave(GameVersion version)
    {
        if (version is GameVersion.FR or GameVersion.LG)
        {
            var data = new byte[SaveUtil.SIZE_G3RAW];
            for (var sector = 0; sector < 28; sector++)
            {
                var footer = data.AsSpan(sector * 0x1000 + 0xFF4);
                WriteUInt16LittleEndian(footer, (ushort)(sector % 14));
                WriteUInt32LittleEndian(footer[4..], 0x08012025);
            }
            var save = new SAV3FRLG(data);
            WriteUInt32LittleEndian(save.Small[0xAC..], 1);
            save.Version = version;
            return save;
        }
        if (version is GameVersion.HG or GameVersion.SS)
        {
            var data = new byte[SaveUtil.SIZE_G4RAW];
            foreach (var offset in new[] { 0, 0x40000 })
            {
                var footer = data.AsSpan(offset + SAV4HGSS.GeneralSize - 0xC);
                WriteUInt32LittleEndian(footer, SAV4HGSS.GeneralSize);
                WriteUInt32LittleEndian(footer[4..], SAV4.MAGIC_JAPAN_INTL);
            }
            return new SAV4HGSS(data) { Version = version };
        }
        return Game.EmptyOf(GameVersionRepository.Instance.Get(version), "READER").SaveFile;
    }

    [Theory]
    [InlineData(GameVersion.SW)]
    [InlineData(GameVersion.SL)]
    public void SwitchSnapshotsReadNationalIds(GameVersion version)
    {
        var save = BlankSaveFile.Get(version, "READER");
        var pokemon = save.BlankPKM;
        pokemon.Species = 133;
        save.SetBoxSlotAtIndex(pokemon, 0);
        save.SetBoxSlotAtIndex(save.BlankPKM, 0, EntityImportSettings.None);
        var snapshot = new Game(save).Pokedex;
        Assert.Equal("national-dex", snapshot.SpeciesNamespace);
        Assert.Equal(new[] { 133 }, snapshot.CaughtSpecies!);
    }

    [Fact]
    public void EmptyPokedexIsAvailable()
    {
        var game = new Game(BlankSaveFile.Get(GameVersion.C));
        Assert.Equal("available", game.Pokedex.Status);
        Assert.Empty(game.Pokedex.CaughtSpecies!);
    }

    [Fact]
    public void EmptySavedPokedexIsSuccessful()
    {
        var bytes = WriteFixture(SyntheticSave(GameVersion.C), GameVersion.C);
        var json = SaveReader.Inspect(bytes);
        using var result = JsonDocument.Parse(json);
        Assert.Equal("ok", result.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, result.RootElement.GetProperty("pokedex").GetProperty("caughtSpecies").GetArrayLength());
        ExportFixture("C-empty", bytes, json);
    }

    [Fact]
    public void RejectsArchivesBeforeTheArchiveReaderRuns()
    {
        using var result = JsonDocument.Parse(SaveReader.Inspect([0x50, 0x4B, 0x03, 0x04]));
        Assert.Equal("unsupported-format", result.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void UnknownFormatsAndUnavailableProvidersUseDistinctErrors()
    {
        SaveFormats.Register(new TestFormat());
        using var unknown = JsonDocument.Parse(SaveReader.Inspect([1, 2, 3], formatId: "reader-unknown"));
        using var unavailable = JsonDocument.Parse(SaveReader.Inspect([1, 2, 3], formatId: "reader-test"));
        Assert.Equal("unsupported-format", unknown.RootElement.GetProperty("code").GetString());
        Assert.Equal("unsupported-pokedex", unavailable.RootElement.GetProperty("code").GetString());
        Assert.False(unavailable.RootElement.TryGetProperty("pokedex", out _));
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 1, 2, 3 })]
    public void RejectsMalformedFiles(byte[] bytes)
    {
        using var result = JsonDocument.Parse(SaveReader.Inspect(bytes));
        Assert.Equal("invalid-save", result.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void RejectsOversizedInputBeforeParsing()
    {
        using var result = JsonDocument.Parse(SaveReader.Inspect(new byte[SaveReader.MaxInputBytes + 1]));
        Assert.Equal("input-too-large", result.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void AFormatNeedsAnExplicitPokedexProvider()
    {
        var save = BlankSaveFile.Get(GameVersion.C);
        var snapshot = new Game(save, new TestFormat()).Pokedex;
        Assert.Equal("unavailable", snapshot.Status);
        Assert.Null(snapshot.CaughtSpecies);
        Assert.Null(snapshot.TrackedSpecies);
        Assert.Equal("partial", new Game(save, new ProviderFormat()).Pokedex.Coverage);
    }

    private class TestFormat : ISaveFormat
    {
        public string Id => "reader-test";
        public string Name => "Reader Test";
        public GameVersion BaseGame => GameVersion.C;
        public IReadOnlySet<Capability> Capabilities { get; } = new HashSet<Capability>();
        public SaveFormatMatch Detect(ReadOnlySpan<byte> data) => SaveFormatMatch.No;
        public SaveFile Load(byte[] data) => BlankSaveFile.Get(GameVersion.C);
    }

    private sealed class ProviderFormat : TestFormat, IPokedexProvider
    {
        public PokedexSnapshot ReadPokedex(SaveFile save) =>
            PokedexSnapshot.Available([25], [], [25], "test-dex", "partial", ["Synthetic provider."]);
    }
}
