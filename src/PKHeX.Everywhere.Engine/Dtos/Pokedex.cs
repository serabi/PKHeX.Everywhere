using PKHeX.Facade;

namespace PKHeX.Everywhere.Engine.Dtos;

/// <summary>Saved species flags returned by <c>game.pokedex</c>. Unavailable snapshots have no species arrays.</summary>
/// <param name="Status">Available or unavailable, as lowercase strings.</param>
/// <param name="Coverage">Complete or partial, or null when unavailable.</param>
/// <param name="SpeciesNamespace">The id namespace, usually national-dex, or null when unavailable.</param>
/// <param name="TrackedSpecies">Species whose flags can be read, or null when unavailable.</param>
/// <param name="SeenSpecies">Species marked seen, or null when unavailable.</param>
/// <param name="CaughtSpecies">Species marked caught, including released Pokémon, or null when unavailable.</param>
/// <param name="Warnings">Limits reported by the provider.</param>
/// <param name="Reason">Why the snapshot is unavailable, or null when available.</param>
public record GamePokedex(string Status, string? Coverage, string? SpeciesNamespace,
    int[]? TrackedSpecies, int[]? SeenSpecies, int[]? CaughtSpecies, string[] Warnings, string? Reason);

public static class PokedexMapping
{
    public static GamePokedex ToDto(this PokedexSnapshot snapshot) => new(
        snapshot.Status, snapshot.Coverage, snapshot.SpeciesNamespace,
        snapshot.TrackedSpecies?.ToArray(), snapshot.SeenSpecies?.ToArray(), snapshot.CaughtSpecies?.ToArray(),
        snapshot.Warnings.ToArray(), snapshot.Reason);
}
