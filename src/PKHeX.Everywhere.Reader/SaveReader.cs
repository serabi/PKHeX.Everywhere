using System.Text.Json;
using System.Text.Json.Serialization;
using PKHeX.Core;
using PKHeX.Facade;

namespace PKHeX.Everywhere.Reader;

public static class SaveReader
{
    public const int MaxInputBytes = 64 * 1024 * 1024;
    public const string ParserRevision = "serabi-reader-0.1.0+pkhex.02c9ec108fe3c96735f6c2397eaebc10e29a049f";

    public static string Inspect(byte[] bytes, string? fileName = null, string? formatId = null)
    {
        ReaderResult Error(string code, string message, FormatCandidate[]? candidateFormats = null, string? pkhexGame = null) =>
            new("error", 1, ParserRevision, Code: code, Message: message, CandidateFormats: candidateFormats, PkhexGame: pkhexGame);
        ReaderResult result;
        if (bytes.Length > MaxInputBytes) result = Error("input-too-large", "The save exceeds the 64 MiB input limit.");
        else if (bytes.Length == 0) result = Error("invalid-save", "The file is empty.");
        else if (bytes.AsSpan().StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }))
            result = Error("unsupported-format", "Extract the save file from the archive before importing it.");
        else
        {
            try
            {
                var format = formatId is null ? null : SaveFormats.Find(formatId);
                if (formatId is not null && format is null)
                    result = Error("unsupported-format", "This save format is not registered.");
                else
                {
                    var game = Game.LoadFrom((byte[])bytes.Clone(), fileName, format);
                    var pokedex = game.Pokedex;
                    if (pokedex.Status != "available") result = Error("unsupported-pokedex", pokedex.Reason ?? "This save has no verified Pokédex provider.");
                    else
                    {
                        var version = game.DetectedVersion.Version;
                        var versions = game.DetectedVersions.Select(candidate => candidate.Version.ToString()).ToArray();
                        result = new("ok", 1, ParserRevision,
                            new ReaderGame(version.ToString(), versions, game.Format?.Id ?? "pkhex",
                                game.Format?.Name ?? game.DetectedVersion.Name),
                            new ReaderPokedex(pokedex.Status, pokedex.Coverage!, pokedex.SpeciesNamespace!,
                                pokedex.TrackedSpecies!.ToArray(), pokedex.SeenSpecies!.ToArray(),
                                pokedex.CaughtSpecies!.ToArray(), pokedex.Warnings.ToArray()));
                    }
                }
            }
            catch (FormatChoiceRequiredException e)
            {
                result = Error("format-choice-required", "Choose the save's format before reading it.",
                    e.Candidates.Select(candidate => new FormatCandidate(candidate.Id, candidate.Name)).ToArray(), e.PKHeXGame);
            }
            catch (GameNotLoadedException)
            {
                result = Error("invalid-save", "This file could not be read as a supported game save.");
            }
            catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException)
            {
                result = Error("parser-failure", "The parser could not inspect this file.");
            }
        }
        return JsonSerializer.Serialize(result, ReaderJsonContext.Default.ReaderResult);
    }
}

internal sealed record ReaderResult(string Status, int SchemaVersion, string ParserRevision,
    ReaderGame? Game = null, ReaderPokedex? Pokedex = null, string? Code = null, string? Message = null,
    FormatCandidate[]? CandidateFormats = null, string? PkhexGame = null);
internal sealed record ReaderGame(string Version, string[] Versions, string FormatId, string Name);
internal sealed record ReaderPokedex(string Status, string Coverage, string SpeciesNamespace,
    int[] TrackedSpecies, int[] SeenSpecies, int[] CaughtSpecies, string[] Warnings);
internal sealed record FormatCandidate(string Id, string Name);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReaderResult))]
internal partial class ReaderJsonContext : JsonSerializerContext;
