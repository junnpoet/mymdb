namespace MyMdb.Core.Models;

public sealed record Catalog
{
    public required string Version { get; init; }
    public required IReadOnlyList<MediaEntry> Entries { get; init; }
    public required IReadOnlyList<CollectionInfo> Collections { get; init; }
}
