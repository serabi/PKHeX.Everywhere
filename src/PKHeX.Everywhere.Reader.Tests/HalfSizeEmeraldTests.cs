using System.Text.Json;
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Everywhere.Reader.Tests;

public class HalfSizeEmeraldTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ReadsCompleteSlotWithWarning(bool empty, bool rotated)
    {
        var bytes = Fixture(empty, rotated);
        var before = bytes.ToArray();
        foreach (var format in new string?[] { null, "pkhex" })
        {
            var json = SaveReader.Inspect(bytes, "not-an-emerald-name.srm", format);
            using var result = JsonDocument.Parse(json);
            Assert.Equal("ok", result.RootElement.GetProperty("status").GetString());
            Assert.Equal("E", result.RootElement.GetProperty("game").GetProperty("version").GetString());
            var dex = result.RootElement.GetProperty("pokedex");
            Assert.Equal(empty ? [] : new[] { 25, 133, 252, 386 }, dex.GetProperty("caughtSpecies").EnumerateArray().Select(n => n.GetInt32()));
            Assert.Equal(empty ? [] : new[] { 1, 25, 133, 252, 386 }, dex.GetProperty("seenSpecies").EnumerateArray().Select(n => n.GetInt32()));
            Assert.Equal("complete", dex.GetProperty("coverage").GetString());
            Assert.Equal(1, dex.GetProperty("warnings").GetArrayLength());
            Assert.Contains("newer save may be missing", dex.GetProperty("warnings")[0].GetString());
            Assert.False(result.RootElement.TryGetProperty("startDate", out _));
            Assert.Equal(before, bytes);
            Export($"E-half-{(empty ? "empty" : "caught")}-{(rotated ? "rotated" : "ordered")}", bytes, json);
        }
    }

    [Theory]
    [InlineData("checksum")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("signature")]
    [InlineData("counter")]
    [InlineData("blank")]
    [InlineData("ruby-sapphire")]
    [InlineData("firered-leafgreen")]
    public void RejectsDamagedOrDifferentGameSlots(string defect)
    {
        var bytes = Fixture(false, false);
        switch (defect)
        {
            case "checksum": bytes[0x28] ^= 1; break;
            case "missing": bytes.AsSpan(0x1000, 0x1000).Fill(0xFF); break;
            case "duplicate": WriteUInt16LittleEndian(bytes.AsSpan(0x1FF4), 0); break;
            case "signature": bytes[0x1FF8] ^= 1; break;
            case "counter": bytes[0x1FFC] ^= 1; break;
            case "blank": bytes.AsSpan().Clear(); break;
            case "ruby-sapphire":
                bytes.AsSpan(0xAC, 4).Clear();
                SetChecksum(bytes);
                break;
            case "firered-leafgreen":
                WriteUInt32LittleEndian(bytes.AsSpan(0xAC), 1);
                SetChecksum(bytes);
                break;
        }
        var before = bytes.ToArray();
        foreach (var format in new string?[] { null, "pkhex" })
        {
            var json = SaveReader.Inspect(bytes, "Emerald.sav", format);
            using var result = JsonDocument.Parse(json);
            Assert.Equal("error", result.RootElement.GetProperty("status").GetString());
            Assert.Equal("invalid-save", result.RootElement.GetProperty("code").GetString());
            Assert.False(result.RootElement.TryGetProperty("pokedex", out _));
            Assert.Equal(before, bytes);
            Export($"E-half-invalid-{defect}", bytes, json);
        }
    }

    [Fact]
    public void PreservesHackChoiceForExpandedSpecies()
    {
        var bytes = Fixture(false, false);
        var save = new SAV3E(bytes);
        var pokemon = (PK3)save.BlankPKM;
        pokemon.SpeciesInternal = 1000;
        save.SetBoxSlotAtIndex(pokemon, 0, EntityImportSettings.None);
        bytes = save.Write().ToArray()[..0x10000];
        var json = SaveReader.Inspect(bytes);
        using var result = JsonDocument.Parse(json);
        Assert.Equal("format-choice-required", result.RootElement.GetProperty("code").GetString());
        Export("E-half-hack-choice", bytes, json);
    }

    [Fact]
    public void FullSizeSaveDoesNotCarryRecoveryWarning()
    {
        var bytes = FullFixture(false);
        var json = SaveReader.Inspect(bytes, formatId: "pkhex");
        using var result = JsonDocument.Parse(json);
        Assert.Equal("ok", result.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, result.RootElement.GetProperty("pokedex").GetProperty("warnings").GetArrayLength());
    }

    private static byte[] FullFixture(bool empty)
    {
        var data = new byte[SaveUtil.SIZE_G3RAW];
        for (var sector = 0; sector < 28; sector++)
        {
            var footer = data.AsSpan(sector * 0x1000 + 0xFF4);
            WriteUInt16LittleEndian(footer, (ushort)(sector % 14));
            WriteUInt32LittleEndian(footer[4..], 0x08012025);
            WriteUInt32LittleEndian(footer[8..], sector < 14 ? 5u : 4u);
        }
        var save = new SAV3E(data) { OT = "READER" };
        save.SmallBlock.SecurityKey = 0x12345678;
        save.Small[0x890] = 1;
        if (!empty)
        {
            foreach (ushort species in new ushort[] { 25, 133, 252, 386 })
            {
                var pokemon = save.BlankPKM;
                pokemon.Species = species;
                save.SetBoxSlotAtIndex(pokemon, 0);
                save.SetBoxSlotAtIndex(save.BlankPKM, 0, EntityImportSettings.None);
            }
            save.SetSeen(1, true);
        }
        var bytes = save.Write().ToArray();
        save.WriteBothSaveSlots(bytes);
        return bytes;
    }

    private static byte[] Fixture(bool empty, bool rotated)
    {
        var bytes = FullFixture(empty)[..0x10000];
        if (rotated)
        {
            var source = bytes.ToArray();
            for (var sector = 0; sector < 14; sector++)
                source.AsSpan(sector * 0x1000, 0x1000).CopyTo(bytes.AsSpan(((sector + 3) % 14) * 0x1000));
        }
        Assert.True(new SAV3E(bytes).ChecksumsValid);
        return bytes;
    }

    private static void SetChecksum(byte[] bytes) =>
        WriteUInt16LittleEndian(bytes.AsSpan(0xFF6), Checksums.CheckSum32(bytes.AsSpan(0, 0xF80)));

    private static void Export(string name, byte[] bytes, string json)
    {
        if (Environment.GetEnvironmentVariable("SERABI_FIXTURE_DIR") is not { } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".sav"), bytes);
        File.WriteAllText(Path.Combine(directory, name + ".json"), json);
    }
}
