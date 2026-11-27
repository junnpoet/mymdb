using System.Text.Json.Serialization;

namespace MyMdb.Core.Parsing;

internal sealed class EmdbRawDatabase
{
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("collections")]
    public List<EmdbRawCollection>? Collections { get; init; }

    [JsonPropertyName("movies")]
    public List<EmdbRawMovie>? Movies { get; init; }

    [JsonPropertyName("actors")]
    public List<EmdbRawActor>? Actors { get; init; }

    [JsonPropertyName("directors")]
    public List<EmdbRawPerson>? Directors { get; init; }

    [JsonPropertyName("writers")]
    public List<EmdbRawPerson>? Writers { get; init; }

    [JsonPropertyName("composers")]
    public List<EmdbRawPerson>? Composers { get; init; }
}

internal sealed class EmdbRawCollection
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("sort-string")]
    public string? SortString { get; init; }
}

internal sealed class EmdbRawActor
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class EmdbRawPerson
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed class EmdbRawMovie
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("genres")]
    public string? Genres { get; init; }

    [JsonPropertyName("audio")]
    public string? Audio { get; init; }

    [JsonPropertyName("cast")]
    public string? Cast { get; init; }

    [JsonPropertyName("plot")]
    public string? Plot { get; init; }

    [JsonPropertyName("tagline")]
    public string? Tagline { get; init; }

    [JsonPropertyName("rating")]
    public string? Rating { get; init; }

    [JsonPropertyName("comments")]
    public string? Comments { get; init; }
}
