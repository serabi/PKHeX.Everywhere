using System.Runtime.InteropServices.JavaScript;

namespace PKHeX.Everywhere.Reader.Host;

public static partial class ReaderExports
{
    [JSExport]
    public static string Inspect(byte[] bytes, string? fileName, string? formatId) => SaveReader.Inspect(bytes, fileName, formatId);
}
