using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MyMdb.app.Services;
using MyMdb.Core.Models;

namespace MyMdb.app.ViewModels;

public sealed partial class DetailViewModel : ViewModelBase
{
    private readonly CatalogService _catalogService;

    public MediaEntry Entry { get; }

    [ObservableProperty]
    public partial Bitmap? Backdrop { get; set; }

    [ObservableProperty]
    public partial Bitmap? Poster { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingImages { get; set; } = true;

    public string Title => Entry.Title;
    public string? OriginalTitle => Entry.OriginalTitle;
    public bool HasOriginalTitle => !string.IsNullOrWhiteSpace(Entry.OriginalTitle)
        && !string.Equals(Entry.OriginalTitle, Entry.Title, StringComparison.OrdinalIgnoreCase);

    public string? Tagline => Entry.Tagline;
    public bool HasTagline => !string.IsNullOrWhiteSpace(Entry.Tagline);

    public string? Plot => Entry.Plot;
    public bool HasPlot => !string.IsNullOrWhiteSpace(Entry.Plot);

    public string? Comments => Entry.Comments;
    public bool HasComments => !string.IsNullOrWhiteSpace(Entry.Comments);

    public string? CollectionName { get; }
    public bool HasCollection => !string.IsNullOrWhiteSpace(CollectionName);

    public string SummaryMeta { get; }
    public string RatingsLine { get; }
    public bool HasRatings => !string.IsNullOrEmpty(RatingsLine);

    public string WatchStatusLine { get; }
    public string GenresText => string.Join(", ", Entry.Genres.Select(SpanishLabels.Genre));
    public bool HasGenres => Entry.Genres.Count > 0;

    public string LanguagesText => string.Join(", ", Entry.Languages.Select(SpanishLabels.Language));
    public bool HasLanguages => Entry.Languages.Count > 0;

    public string DirectorsText => string.Join(", ", Entry.Directors);
    public bool HasDirectors => Entry.Directors.Count > 0;

    public string WritersText => string.Join(", ", Entry.Writers);
    public bool HasWriters => Entry.Writers.Count > 0;

    public string ComposersText => string.Join(", ", Entry.Composers);
    public bool HasComposers => Entry.Composers.Count > 0;

    public string? Studio => Entry.Studio;
    public bool HasStudio => !string.IsNullOrWhiteSpace(Entry.Studio);

    public string? Certification => Entry.Certification;
    public bool HasCertification => !string.IsNullOrWhiteSpace(Entry.Certification);

    public string? WebLink => Entry.WebLink;
    public bool HasWebLink => !string.IsNullOrWhiteSpace(Entry.WebLink);

    public string? Location => Entry.Location;
    public bool HasLocation => !string.IsNullOrWhiteSpace(Entry.Location);

    public string? TrailerUrl => Entry.TrailerUrl;
    public bool HasTrailer => !string.IsNullOrWhiteSpace(Entry.TrailerUrl);

    public bool HasLinks => HasWebLink || HasLocation || HasTrailer;

    public IReadOnlyList<CastMemberViewModel> Cast { get; }
    public bool HasCast => Cast.Count > 0;

    public DetailViewModel(
        MediaEntry entry,
        IReadOnlyList<CollectionInfo> collections,
        CatalogService catalogService)
    {
        Entry = entry;
        _catalogService = catalogService;

        CollectionName = entry.CollectionId is null
            ? null
            : collections.FirstOrDefault(c => c.Id == entry.CollectionId)?.Name;

        var meta = new List<string>();
        if (entry.Year is { } year)
        {
            meta.Add(year.ToString());
        }

        if (entry.IsTvSeries)
        {
            var tvLabel = string.IsNullOrEmpty(entry.SeasonsRange)
                ? "Serie"
                : $"Serie · Temporadas {entry.SeasonsRange}";
            if (entry.EpisodeCount is { } eps)
            {
                tvLabel += $" ({eps} ep.)";
            }

            meta.Add(tvLabel);
        }
        else if (entry.RuntimeMinutes is { } runtime)
        {
            meta.Add($"{runtime} min");
        }

        if (!string.IsNullOrEmpty(entry.CountryCode))
        {
            meta.Add(entry.CountryCode);
        }

        SummaryMeta = string.Join(" · ", meta);

        var ratings = new List<string>();
        if (entry.ImdbRating is { } imdb)
        {
            var votes = entry.ImdbVotes is { } v ? $" ({v:N0} votos)" : string.Empty;
            ratings.Add($"IMDb ★ {imdb:F1}{votes}");
        }

        if (entry.Metascore is { } metaScore)
        {
            ratings.Add($"Metascore {metaScore}");
        }

        RatingsLine = string.Join("  ·  ", ratings);

        var status = new List<string>();
        status.Add(entry.SeenCount > 0 ? $"Vista ({entry.SeenCount}x)" : "Sin ver");
        if (entry.DateLastWatched is { } lastWatched)
        {
            status.Add($"Última vez: {lastWatched:yyyy-MM-dd}");
        }

        if (entry.DateAdded is { } added)
        {
            status.Add($"Agregada: {added:yyyy-MM-dd}");
        }

        if (entry.IsFavorite)
        {
            status.Add("Favorita");
        }

        WatchStatusLine = string.Join(" · ", status);

        Cast = entry.Cast
            .Select(c => new CastMemberViewModel(c, catalogService))
            .ToList();

        _ = LoadImagesAsync();
    }

    private async Task LoadImagesAsync()
    {
        try
        {
            var backdropTask = _catalogService.LoadImageAsync("Backdrops", Entry.MediaFileName, 760);
            var posterTask = _catalogService.LoadImageAsync("Posters", Entry.MediaFileName, 260);

            await Task.WhenAll(backdropTask, posterTask);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Backdrop = backdropTask.Result;
                Poster = posterTask.Result;
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsLoadingImages = false);
        }
    }
}
