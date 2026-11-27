namespace MyMdb.Core.Models;

public sealed record MediaEntry
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string? OriginalTitle { get; init; }
    public string? Studio { get; init; }
    public string? CollectionId { get; init; }

    public int? Year { get; init; }
    public int? RuntimeMinutes { get; init; }
    public string? CountryCode { get; init; }

    public bool IsTvSeries { get; init; }
    public string? SeasonsRange { get; init; }
    public int? EpisodeCount { get; init; }

    public required IReadOnlyList<string> Genres { get; init; }
    public required IReadOnlyList<string> Languages { get; init; }
    public string? Certification { get; init; }

    public DateOnly? DateAdded { get; init; }
    public DateOnly? DateLastWatched { get; init; }
    public int SeenCount { get; init; }
    public bool IsFavorite { get; init; }

    public required IReadOnlyList<string> Directors { get; init; }
    public required IReadOnlyList<string> Writers { get; init; }
    public required IReadOnlyList<string> Composers { get; init; }
    public required IReadOnlyList<CastMember> Cast { get; init; }

    public string? Plot { get; init; }
    public string? Tagline { get; init; }
    public string? Comments { get; init; }

    public double? ImdbRating { get; init; }
    public int? ImdbVotes { get; init; }
    public int? Metascore { get; init; }

    public string? ImdbId { get; init; }
    public string? TmdbId { get; init; }
    public string? RottenTomatoesSlug { get; init; }
    public string? TrailerUrl { get; init; }
    public string? WebLink { get; init; }
    public string? Location { get; init; }

    /// <summary>
    /// Standard 6-digit filename used in Posters/, Covers/, and Backdrops/ (e.g. "000358.jpg").
    /// </summary>
    public string MediaFileName => $"{Id:D6}.jpg";
}
