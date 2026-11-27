using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyMdb.app.Services;
using MyMdb.Core.Models;

namespace MyMdb.app.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private const int PageSize = 20;
    private const int MaxConcurrentPosterDownloads = 6;
    private const string AllGenresOption = "Todos los géneros";

    private readonly CatalogService _catalogService;
    private List<MediaItemViewModel> _allItems = new();
    private List<MediaItemViewModel> _filteredAll = new();
    private IReadOnlyList<CollectionInfo> _collections = Array.Empty<CollectionInfo>();
    private CancellationTokenSource? _pageLoadCts;
    private bool _loaded;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "All";

    [ObservableProperty]
    public partial string SelectedGenre { get; set; } = AllGenresOption;

    [ObservableProperty]
    public partial bool NeedsGoogleDriveAuth { get; set; }

    [ObservableProperty]
    public partial bool IsConnecting { get; set; }

    [ObservableProperty]
    public partial bool IsSignOutConfirmOpen { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial bool HasMoreItems { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Cargando catálogo...";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool ShowLoadMoreButton => HasMoreItems && !IsLoadingMore;
    public bool SupportsSignOut => _catalogService.SupportsSignOut;

    public ObservableCollection<string> AvailableGenres { get; } = new() { AllGenresOption };
    public ObservableCollection<MediaItemViewModel> FilteredEntries { get; } = new();

    public HomeViewModel()
        : this(CatalogService.CreateDefault())
    {
    }

    public HomeViewModel(CatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    partial void OnHasMoreItemsChanged(bool value) => OnPropertyChanged(nameof(ShowLoadMoreButton));
    partial void OnIsLoadingMoreChanged(bool value) => OnPropertyChanged(nameof(ShowLoadMoreButton));

    partial void OnSelectedGenreChanged(string value)
    {
        if (_loaded && !IsLoading && !string.IsNullOrEmpty(value))
        {
            _ = ApplyFiltersAsync();
        }
    }

    public async Task InitializeAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        if (_catalogService.RequiresAuthentication)
        {
            NeedsGoogleDriveAuth = true;
            IsLoading = false;
            return;
        }

        await LoadCoreAsync(forceRefresh: false);
    }

    [RelayCommand]
    private async Task ConnectGoogleDriveAsync()
    {
        if (IsConnecting)
        {
            return;
        }

        IsConnecting = true;
        ErrorMessage = null;

        try
        {
            var succeeded = await LoadCoreAsync(forceRefresh: true);
            if (succeeded && !_catalogService.RequiresAuthentication)
            {
                NeedsGoogleDriveAuth = false;
            }
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    private void RequestSignOut()
    {
        IsSignOutConfirmOpen = true;
    }

    [RelayCommand]
    private void CancelSignOut()
    {
        IsSignOutConfirmOpen = false;
    }

    [RelayCommand]
    private void ConfirmSignOut()
    {
        IsSignOutConfirmOpen = false;
        _pageLoadCts?.Cancel();
        _catalogService.SignOut();
        ErrorMessage = null;
        NeedsGoogleDriveAuth = true;
    }

    [RelayCommand]
    private Task SyncAsync() => LoadCoreAsync(forceRefresh: true);

    [RelayCommand]
    private Task SearchAsync() => ApplyFiltersAsync();

    [RelayCommand]
    private async Task SetFilterAsync(string filter)
    {
        if (SelectedFilter == filter)
        {
            return;
        }

        SelectedFilter = filter;
        await ApplyFiltersAsync();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoadingMore || !HasMoreItems)
        {
            return;
        }

        var token = _pageLoadCts?.Token ?? CancellationToken.None;
        await LoadNextPageAsync(token);
    }

    public DetailViewModel CreateDetailViewModel(MediaItemViewModel item) =>
        new(item.Entry, _collections, _catalogService);

    private async Task<bool> LoadCoreAsync(bool forceRefresh)
    {
        IsLoading = true;
        ErrorMessage = null;
        StatusText = forceRefresh
            ? $"Sincronizando con {_catalogService.SourceLabel}..."
            : $"Cargando desde {_catalogService.SourceLabel}...";

        try
        {
            var catalog = await _catalogService.LoadCatalogAsync(forceRefresh);
            _collections = catalog.Collections;
            _allItems = catalog.Entries
                .OrderByDescending(e => e.DateAdded ?? DateOnly.MinValue)
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new MediaItemViewModel(e, _catalogService))
                .ToList();

            var genres = _allItems
                .SelectMany(e => e.Entry.Genres)
                .Select(SpanishLabels.Genre)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            AvailableGenres.Clear();
            AvailableGenres.Add(AllGenresOption);
            foreach (var genre in genres)
            {
                AvailableGenres.Add(genre);
            }

            if (!AvailableGenres.Contains(SelectedGenre))
            {
                SelectedGenre = AllGenresOption;
            }

            await ApplyFiltersAsync();
            return true;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Autenticación cancelada.";
            StatusText = "Autenticación cancelada";
            return false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Error al cargar el catálogo";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyFiltersAsync()
    {
        _pageLoadCts?.Cancel();
        _pageLoadCts?.Dispose();
        _pageLoadCts = new CancellationTokenSource();
        var token = _pageLoadCts.Token;

        var query = SearchText.Trim();
        IEnumerable<MediaItemViewModel> matches = _allItems;

        matches = SelectedFilter switch
        {
            "Movies" => matches.Where(m => !m.IsTvSeries),
            "Series" => matches.Where(m => m.IsTvSeries),
            "Watched" => matches.Where(m => m.IsWatched),
            "Unwatched" => matches.Where(m => !m.IsWatched),
            _ => matches,
        };

        if (!string.IsNullOrEmpty(SelectedGenre)
            && !string.Equals(SelectedGenre, AllGenresOption, StringComparison.Ordinal))
        {
            matches = matches.Where(m =>
                m.Entry.Genres.Any(g => string.Equals(SpanishLabels.Genre(g), SelectedGenre, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrEmpty(query))
        {
            matches = matches.Where(m =>
                m.SearchIndex.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        _filteredAll = matches.ToList();
        FilteredEntries.Clear();
        HasMoreItems = _filteredAll.Count > 0;
        UpdateStatusText();

        if (_filteredAll.Count > 0)
        {
            await LoadNextPageAsync(token);
        }
    }

    private async Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        var startIndex = FilteredEntries.Count;
        var batch = _filteredAll.Skip(startIndex).Take(PageSize).ToList();
        if (batch.Count == 0)
        {
            HasMoreItems = false;
            return;
        }

        IsLoadingMore = true;
        await Task.Yield();
        try
        {
            using var semaphore = new SemaphoreSlim(MaxConcurrentPosterDownloads);
            var tasks = batch.Select(async item =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    await item.EnsurePosterLoadedAsync(cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            foreach (var item in batch)
            {
                FilteredEntries.Add(item);
            }

            HasMoreItems = FilteredEntries.Count < _filteredAll.Count;
            UpdateStatusText();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer filter/search request.
        }
        finally
        {
            IsLoadingMore = false;
        }
    }

    private void UpdateStatusText()
    {
        StatusText = _filteredAll.Count == _allItems.Count
            ? $"{FilteredEntries.Count} de {_allItems.Count} títulos"
            : $"{FilteredEntries.Count} de {_filteredAll.Count} resultados ({_allItems.Count} en total)";
    }
}
