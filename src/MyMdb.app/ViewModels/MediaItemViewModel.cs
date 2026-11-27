using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MyMdb.app.Services;
using MyMdb.Core.Models;

namespace MyMdb.app.ViewModels;

public sealed partial class MediaItemViewModel : ViewModelBase
{
    private readonly CatalogService _catalogService;
    private Task? _posterLoadTask;

    public MediaEntry Entry { get; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    public string Title => Entry.Title;
    public bool IsTvSeries => Entry.IsTvSeries;
    public bool IsWatched => Entry.SeenCount > 0 || Entry.DateLastWatched is not null;
    public bool IsFavorite => Entry.IsFavorite;
    public bool HasRating => Entry.ImdbRating is not null;

    public string RatingText => Entry.ImdbRating is { } rating ? $"★ {rating:F1}" : string.Empty;

    public string MetaLine { get; }
    public string GenresLine { get; }
    public string SearchIndex { get; }

    public MediaItemViewModel(MediaEntry entry, CatalogService catalogService)
    {
        Entry = entry;
        _catalogService = catalogService;

        var metaParts = new List<string>(3);
        if (Entry.Year is { } year)
        {
            metaParts.Add(year.ToString());
        }

        if (Entry.IsTvSeries)
        {
            metaParts.Add(string.IsNullOrEmpty(Entry.SeasonsRange)
                ? "TV Series"
                : $"Seasons {Entry.SeasonsRange}");
        }
        else if (Entry.RuntimeMinutes is { } minutes)
        {
            metaParts.Add($"{minutes} min");
        }

        if (!string.IsNullOrEmpty(Entry.CountryCode))
        {
            metaParts.Add(Entry.CountryCode);
        }

        MetaLine = string.Join(" · ", metaParts);
        GenresLine = string.Join(", ", Entry.Genres.Take(3));

        var searchable = new List<string> { Entry.Title };
        if (!string.IsNullOrEmpty(Entry.OriginalTitle))
        {
            searchable.Add(Entry.OriginalTitle);
        }

        searchable.AddRange(Entry.Directors);
        searchable.AddRange(Entry.Cast.Select(c => c.Name));
        SearchIndex = string.Join(' ', searchable);
    }

    public Task EnsurePosterLoadedAsync(CancellationToken cancellationToken = default)
    {
        _posterLoadTask ??= LoadPosterAsync(cancellationToken);
        return _posterLoadTask;
    }

    private async Task LoadPosterAsync(CancellationToken cancellationToken)
    {
        var bitmap = await _catalogService.LoadImageAsync("Posters", Entry.MediaFileName, 140, cancellationToken);
        if (bitmap is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Poster = bitmap);
        }
    }
}
