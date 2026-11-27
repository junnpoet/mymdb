using System.IO;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using MyMdb.app.Services;
using MyMdb.Core.GoogleDrive;

namespace MyMdb.app.Android;

[Activity(
    Label = "MyMDB",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        AndroidOAuthBroker.SetCurrentActivity(this);

        var filesDir = FilesDir?.AbsolutePath ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
        var cacheDir = CacheDir?.AbsolutePath ?? Path.Combine(filesDir, "cache");
        var tokenPath = Path.Combine(filesDir, "gdrive-token.json");
        var mediaCacheDir = Path.Combine(cacheDir, "MyMDB");

        CatalogService.RegisterPlatformFactory(() => CatalogService.CreateGoogleDrive(
            new GoogleOAuthConfig { ClientId = AndroidOAuthBuildConfig.ClientId },
            CatalogService.DefaultDriveFolderId,
            tokenPath,
            mediaCacheDir,
            AndroidOAuthBroker.AuthorizeAsync));

        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume()
    {
        base.OnResume();
        AndroidOAuthBroker.SetCurrentActivity(this);
        AndroidOAuthBroker.OnMainActivityResumed();
    }
}
