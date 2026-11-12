using PKHeX.Core;

namespace PKHeX.Facade.Abstractions;

/// <summary>An optional save-format provider for verified Pokédex flag layouts.</summary>
public interface IPokedexProvider
{
    /// <summary>Reads flags without changing the save. Return unavailable when the layout cannot be verified.</summary>
    PokedexSnapshot ReadPokedex(SaveFile save);
}
