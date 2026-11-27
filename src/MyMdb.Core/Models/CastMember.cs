namespace MyMdb.Core.Models;

public sealed record CastMember
{
    public string? Id { get; init; }
    public required string Name { get; init; }
    public string? Character { get; init; }
    public int? EpisodeCount { get; init; }

    public string? PhotoFileName => string.IsNullOrWhiteSpace(Id) ? null : $"{Id}.jpg";
}
