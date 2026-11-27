namespace MyMdb.Core.Models;

public sealed record CollectionInfo
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? SortString { get; init; }
}
