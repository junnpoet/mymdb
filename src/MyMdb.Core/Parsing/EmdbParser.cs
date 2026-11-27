using System.Globalization;
using System.Text;
using System.Text.Json;
using MyMdb.Core.Models;

namespace MyMdb.Core.Parsing;

public sealed class EmdbParser
{
    private const char RecordSeparator = '\u001e';
    private const int ImdbRatingOffset = 0x3E8; // 1000 => 0.0
    private const int FavoriteFlagMask = 32;

    public Catalog Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(
            stream,
            Encoding.Unicode,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: true);

        var rawText = reader.ReadToEnd();
        var sanitizedJson = rawText.Replace("\u001e", "\\u001e", StringComparison.Ordinal);

        var rawDb = JsonSerializer.Deserialize<EmdbRawDatabase>(sanitizedJson)
            ?? throw new InvalidDataException("Unable to deserialize EMDB database JSON.");

        var actors = rawDb.Actors ?? [];
        var directors = rawDb.Directors ?? [];
        var writers = rawDb.Writers ?? [];
        var composers = rawDb.Composers ?? [];

        var collections = (rawDb.Collections ?? [])
            .Select(c => new CollectionInfo
            {
                Id = c.Id ?? string.Empty,
                Name = c.Name ?? string.Empty,
                SortString = NullIfEmpty(c.SortString),
            })
            .ToList();

        var entries = (rawDb.Movies ?? [])
            .Select(m => MapMovie(m, actors, directors, writers, composers))
            .ToList();

        return new Catalog
        {
            Version = rawDb.Version ?? string.Empty,
            Entries = entries,
            Collections = collections,
        };
    }

    private static MediaEntry MapMovie(
        EmdbRawMovie raw,
        IReadOnlyList<EmdbRawActor> actors,
        IReadOnlyList<EmdbRawPerson> directors,
        IReadOnlyList<EmdbRawPerson> writers,
        IReadOnlyList<EmdbRawPerson> composers)
    {
        var titleParts = SplitParts(raw.Title);
        var yearParts = SplitParts(raw.Year);
        var genreParts = SplitParts(raw.Genres);
        var castParts = SplitParts(raw.Cast);
        var taglineParts = SplitParts(raw.Tagline);
        var ratingParts = SplitParts(raw.Rating);

        var (isTvSeries, seasonsRange, episodeCount) = ParseSeriesInfo(GetPart(genreParts, 4));
        var flags = ParseInt(GetPart(yearParts, 9)) ?? 0;

        return new MediaEntry
        {
            Id = ParseInt(GetPart(yearParts, 14)) ?? 0,
            Title = CleanText(GetPart(titleParts, 0)) ?? string.Empty,
            OriginalTitle = ParseOriginalTitle(GetPart(titleParts, 1)),
            Studio = CleanText(GetPart(titleParts, 2)),
            CollectionId = ParseCollectionId(GetPart(titleParts, 3)),

            Year = ParseInt(GetPart(yearParts, 0)),
            RuntimeMinutes = ParsePositiveInt(GetPart(yearParts, 2)),
            CountryCode = ParseCountryCode(GetPart(yearParts, 3)),

            IsTvSeries = isTvSeries,
            SeasonsRange = seasonsRange,
            EpisodeCount = episodeCount,

            Genres = EmdbLookupTables.DecodeGenres(GetPart(genreParts, 0)),
            Languages = EmdbLookupTables.DecodeLanguages(GetPart(ratingParts, 5)),
            Certification = NullIfEmpty(GetPart(genreParts, 3)),

            DateAdded = ParseCompactDate(GetPart(genreParts, 2)),
            DateLastWatched = ParseCompactDate(GetPart(genreParts, 8)),
            SeenCount = flags >> 8,
            IsFavorite = (flags & FavoriteFlagMask) != 0,

            Directors = ResolvePeople(GetPart(yearParts, 1), directors),
            Writers = ResolvePeople(GetPart(yearParts, 15), writers),
            Composers = ResolvePeople(GetPart(yearParts, 16), composers),
            Cast = ResolveCast(GetPart(castParts, 0), GetPart(castParts, 1), actors),

            Plot = CleanText(raw.Plot),
            Tagline = CleanText(GetPart(taglineParts, 0)),
            Comments = CleanText(raw.Comments),

            ImdbRating = ParseImdbRating(GetPart(ratingParts, 0)),
            ImdbVotes = ParsePositiveInt(GetPart(ratingParts, 7)),
            Metascore = ParseMetascore(GetPart(ratingParts, 9)),

            ImdbId = ParseImdbId(GetPart(genreParts, 1)),
            TmdbId = NullIfEmpty(GetPart(genreParts, 10)),
            RottenTomatoesSlug = NullIfEmpty(GetPart(genreParts, 9)),
            TrailerUrl = NullIfEmpty(GetPart(genreParts, 7)),
            WebLink = NullIfEmpty(GetPart(taglineParts, 2)),
            Location = NullIfEmpty(GetPart(yearParts, 7)),
        };
    }

    private static string[] SplitParts(string? value) =>
        string.IsNullOrEmpty(value) ? [] : value.Split(RecordSeparator);

    private static string? GetPart(string[] parts, int index) =>
        index < parts.Length ? parts[index] : null;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? CleanText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Replace("<DQ>", "\"", StringComparison.Ordinal).Trim();

    private static string? ParseOriginalTitle(string? value)
    {
        var cleaned = CleanText(value);
        if (cleaned is null)
        {
            return null;
        }

        return cleaned.StartsWith("#O", StringComparison.Ordinal)
            ? NullIfEmpty(cleaned[2..])
            : cleaned;
    }

    private static string? ParseCollectionId(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "-1" ? null : value.Trim();

    private static string? ParseCountryCode(string? value)
    {
        var trimmed = NullIfEmpty(value);
        if (trimmed is null)
        {
            return null;
        }

        return trimmed.StartsWith('#') ? NullIfEmpty(trimmed[1..]) : trimmed;
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static int? ParsePositiveInt(string? value) =>
        ParseInt(value) is > 0 and var parsed ? parsed : null;

    private static DateOnly? ParseCompactDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static (bool IsTvSeries, string? SeasonsRange, int? EpisodeCount) ParseSeriesInfo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return (false, null, null);
        }

        var parts = value.Split('@');
        var isTvSeries = parts.Length > 1 && parts[1] == "1";
        if (!isTvSeries)
        {
            return (false, null, null);
        }

        var seasonsRange = NullIfEmpty(parts[0]);
        var episodeCount = parts.Length > 2 ? ParsePositiveInt(parts[2]) : null;
        return (true, seasonsRange, episodeCount);
    }

    private static double? ParseImdbRating(string? hexValue)
    {
        if (!int.TryParse(hexValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var raw))
        {
            return null;
        }

        var scaled = raw - ImdbRatingOffset;
        return scaled > 0 ? Math.Round(scaled / 10.0, 1) : null;
    }

    private static int? ParseMetascore(string? packedRatings)
    {
        if (string.IsNullOrEmpty(packedRatings) || packedRatings.Length < 3)
        {
            return null;
        }

        var suffix = packedRatings[^3..];
        return ParsePositiveInt(suffix);
    }

    private static string? ParseImdbId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var rawId = value.Split('|')[0].Trim();
        if (string.IsNullOrEmpty(rawId) || rawId == "0")
        {
            return null;
        }

        return rawId.StartsWith("tt", StringComparison.OrdinalIgnoreCase) ? rawId : $"tt{rawId}";
    }

    private static IReadOnlyList<string> ResolvePeople(string? csvIndices, IReadOnlyList<EmdbRawPerson> table)
    {
        if (string.IsNullOrWhiteSpace(csvIndices))
        {
            return [];
        }

        var result = new List<string>();
        foreach (var token in csvIndices.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx)
                && idx >= 0
                && idx < table.Count
                && !string.IsNullOrWhiteSpace(table[idx].Name))
            {
                result.Add(table[idx].Name!);
            }
        }

        return result;
    }

    private static IReadOnlyList<CastMember> ResolveCast(
        string? csvIndices,
        string? charactersRaw,
        IReadOnlyList<EmdbRawActor> actors)
    {
        if (string.IsNullOrWhiteSpace(csvIndices))
        {
            return [];
        }

        var indices = csvIndices.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var characters = string.IsNullOrEmpty(charactersRaw) ? [] : charactersRaw.Split('|');
        var result = new List<CastMember>(indices.Length);

        for (var i = 0; i < indices.Length; i++)
        {
            if (!int.TryParse(indices[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var actorIdx)
                || actorIdx < 0
                || actorIdx >= actors.Count)
            {
                continue;
            }

            var actor = actors[actorIdx];
            var (character, episodeCount) = ParseCharacterEntry(i < characters.Length ? characters[i] : null);

            result.Add(new CastMember
            {
                Id = NullIfEmpty(actor.Id),
                Name = actor.Name ?? string.Empty,
                Character = character,
                EpisodeCount = episodeCount,
            });
        }

        return result;
    }

    private static (string? Character, int? EpisodeCount) ParseCharacterEntry(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return (null, null);
        }

        var hashIndex = raw.LastIndexOf('#');
        if (hashIndex > 0 && int.TryParse(raw[(hashIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            return (CleanText(raw[..hashIndex]), count);
        }

        return (CleanText(raw), null);
    }
}
