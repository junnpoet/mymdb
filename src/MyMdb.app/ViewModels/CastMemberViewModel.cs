using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MyMdb.app.Services;
using MyMdb.Core.Models;

namespace MyMdb.app.ViewModels;

public sealed partial class CastMemberViewModel : ViewModelBase
{
    private readonly LocalEmdbCatalogService _catalogService;
    private readonly CastMember _member;

    [ObservableProperty]
    public partial Bitmap? Photo { get; set; }

    public string Name => _member.Name;
    public string CharacterText => _member.EpisodeCount is { } eps
        ? $"{_member.Character} ({eps} ep.)"
        : _member.Character ?? string.Empty;

    public bool HasCharacter => !string.IsNullOrWhiteSpace(_member.Character);

    public CastMemberViewModel(CastMember member, LocalEmdbCatalogService catalogService)
    {
        _member = member;
        _catalogService = catalogService;
        _ = LoadPhotoAsync();
    }

    private async Task LoadPhotoAsync()
    {
        var bitmap = await _catalogService.LoadImageAsync("Actors", _member.PhotoFileName, 160);
        if (bitmap is not null)
        {
            await Dispatcher.UIThread.InvokeAsync(() => Photo = bitmap);
        }
    }
}
