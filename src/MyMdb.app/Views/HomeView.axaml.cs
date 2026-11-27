using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MyMdb.app.ViewModels;

namespace MyMdb.app.Views;

public partial class HomeView : ContentPage
{
    public HomeView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is HomeViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is HomeViewModel vm)
        {
            vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void CatalogList_ContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (e.Container.DataContext is MediaItemViewModel item)
        {
            item.EnsurePosterLoaded();
        }
    }

    private async void CatalogList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox
            || listBox.SelectedItem is not MediaItemViewModel selected
            || DataContext is not HomeViewModel vm
            || Navigation is null)
        {
            return;
        }

        listBox.SelectedItem = null;

        await Navigation.PushAsync(new DetailView
        {
            DataContext = vm.CreateDetailViewModel(selected),
        });
    }
}
