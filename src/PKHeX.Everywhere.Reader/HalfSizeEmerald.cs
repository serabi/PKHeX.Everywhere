using System.Buffers.Binary;
using PKHeX.Core;
using PKHeX.Facade;
using PKHeX.Facade.Abstractions;

namespace PKHeX.Everywhere.Reader;

internal static class HalfSizeEmerald
{
    public const string Warning = "This 64 KiB save contains one complete save slot. A newer save may be missing.";

    public static Game? Load(byte[] bytes, string? fileName, ISaveFormat? format)
    {
        if (bytes.Length != SaveUtil.SIZE_G3RAWHALF || (format is not null && format != SaveFormats.PKHeX))
            return null;
        if (!SAV3.IsAllMainSectorsPresent(bytes, 0, out _))
            return null;

        var counter = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xFFC));
        for (var offset = 0; offset < 14 * 0x1000; offset += 0x1000)
        {
            var sector = bytes.AsSpan(offset, 0x1000);
            if (BinaryPrimitives.ReadUInt32LittleEndian(sector[0xFF8..]) != 0x08012025 ||
                BinaryPrimitives.ReadUInt32LittleEndian(sector[0xFFC..]) != counter)
                throw new GameNotLoadedException();
        }

        // Only the inspection copy is extended: Core's autodetector requires 128 KiB.
        // Erased bytes leave the absent slot invalid and preserve the normal format-choice checks.
        var inspection = new byte[SaveUtil.SIZE_G3RAW];
        inspection.AsSpan().Fill(0xFF);
        bytes.CopyTo(inspection, 0);
        var game = Game.LoadFrom(inspection, fileName, format);
        if (game.Format is not null || game.SaveFile is not SAV3E save || !save.ChecksumsValid)
            throw new GameNotLoadedException();
        return game;
    }
}
