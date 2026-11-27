using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyMdb.app.Services;
using MyMdb.Core.Models;

namespace MyMdb.app.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly LocalEmdbCatalogService _catalogService;
    private List<MediaItemViewModel> _allItems = new();
    private IReadOnlyList<CollectionInfo> _collections = Array.Empty<CollectionInfo>();
    private bool _loaded;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "All";

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = true;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Loading catalog...";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public ObservableCollection<MediaItemViewModel> FilteredEntries { get; } = new();

    public HomeViewModel()
        : this(new LocalEmdbCatalogService())
    {
    }

    public HomeViewModel(LocalEmdbCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public async Task InitializeAsync()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var catalog = await _catalogService.LoadCatalogAsync();
            _collections = catalog.Collections;
            _allItems = catalog.Entries
                .OrderByDescending(e => e.DateAdded ?? DateOnly.MinValue)
                .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(e => new MediaItemViewModel(e, _catalogService))
                .ToList();

            ApplyFilters();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StatusText = "Failed to load catalog";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public DetailViewModel CreateDetailViewModel(MediaItemViewModel item) =>
        new(item.Entry, _collections, _catalogService);

    [RelayCommand]
    private void Search()
    {
        ApplyFilters();
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        if (SelectedFilter == filter)
        {
            return;
        }

        SelectedFilter = filter;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
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

        if (!string.IsNullOrEmpty(query))
        {
            matches = matches.Where(m =>
                m.SearchIndex.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredEntries.Clear();
        foreach (var item in matches)
        {
            FilteredEntries.Add(item);
        }

        StatusText = FilteredEntries.Count == _allItems.Count
            ? $"{_allItems.Count} titles"
            : $"{FilteredEntries.Count} of {_allItems.Count} titles";
    }
}
