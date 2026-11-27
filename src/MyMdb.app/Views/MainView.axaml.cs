
using Avalonia;
using Avalonia.Controls;
using MyMdb.app.ViewModels;
using System;

namespace MyMdb.app.Views;

public partial class MainView : NavigationPage
{
    public MainView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (CurrentPage == null)
            await PushAsync(new HomeView()
            {
                DataContext = new HomeViewModel()
            });
    }
}