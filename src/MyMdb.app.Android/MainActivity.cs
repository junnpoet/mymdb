using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;

namespace MyMdb.app.Android;

[Activity(
    Label = "MyMdb.app.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
