using PKHeX.Core;
using PKHeX.Facade.Abstractions;

namespace PKHeX.Facade;

/// <summary>A species-level snapshot of saved Pokédex flags, independent of current storage.</summary>
public sealed class PokedexSnapshot
{
    private PokedexSnapshot(string status, string? coverage, string? speciesNamespace,
        IReadOnlyList<int>? trackedSpecies, IReadOnlyList<int>? seenSpecies,
        IReadOnlyList<int>? caughtSpecies, IReadOnlyList<string> warnings, string? reason)
    {
        Status = status;
        Coverage = coverage;
        SpeciesNamespace = speciesNamespace;
        TrackedSpecies = trackedSpecies;
        SeenSpecies = seenSpecies;
        CaughtSpecies = caughtSpecies;
        Warnings = warnings;
        Reason = reason;
    }

    public string Status { get; }
    public string? Coverage { get; }
    public string? SpeciesNamespace { get; }
    public IReadOnlyList<int>? TrackedSpecies { get; }
    public IReadOnlyList<int>? SeenSpecies { get; }
    public IReadOnlyList<int>? CaughtSpecies { get; }
    public IReadOnlyList<string> Warnings { get; }
    public string? Reason { get; }

    /// <summary>Creates an available snapshot. All ids use the declared namespace.</summary>
    public static PokedexSnapshot Available(IEnumerable<int> trackedSpecies, IEnumerable<int> seenSpecies,
        IEnumerable<int> caughtSpecies, string speciesNamespace = "national-dex", string coverage = "complete",
        IEnumerable<string>? warnings = null)
    {
        if (string.IsNullOrWhiteSpace(speciesNamespace)) throw new ArgumentException("A species namespace is required.", nameof(speciesNamespace));
        if (coverage is not ("complete" or "partial")) throw new ArgumentException("Unknown coverage.", nameof(coverage));
        var tracked = Normalize(trackedSpecies);
        var seen = Normalize(seenSpecies);
        var caught = Normalize(caughtSpecies);
        if (seen.Except(tracked).Any() || caught.Except(tracked).Any())
            throw new ArgumentException("Seen and caught species must belong to tracked species.");
        return new("available", coverage, speciesNamespace, tracked, seen, caught,
            Array.AsReadOnly(warnings?.ToArray() ?? []), null);
    }

    /// <summary>Creates an unavailable snapshot without species arrays.</summary>
    public static PokedexSnapshot Unavailable(string reason) =>
        new("unavailable", null, null, null, null, null, Array.Empty<string>(), reason);

    internal static PokedexSnapshot Read(SaveFile save, ISaveFormat? format)
    {
        if (format is IPokedexProvider provider) return provider.ReadPokedex(save);
        if (format is not null) return Unavailable("This save format has no verified Pokédex provider.");
        if (!save.HasPokeDex) return Unavailable("This save has no readable Pokédex.");

        var tracked = new List<int>();
        var seen = new List<int>();
        var caught = new List<int>();
        for (ushort species = 1; species <= save.MaxSpeciesID; species++)
        {
            var wasSeen = save.GetSeen(species);
            var wasCaught = save.GetCaught(species);
            if (save.Personal.IsSpeciesInGame(species) || wasSeen || wasCaught) tracked.Add(species);
            if (wasSeen) seen.Add(species);
            if (wasCaught) caught.Add(species);
        }
        return Available(tracked, seen, caught);
    }

    private static IReadOnlyList<int> Normalize(IEnumerable<int> species)
    {
        var ids = species.Distinct().Order().ToArray();
        if (ids.Any(id => id <= 0)) throw new ArgumentException("Species ids must be positive.", nameof(species));
        return Array.AsReadOnly(ids);
    }
}
