using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MyMdb.app.Android;

[Activity(
    NoHistory = true,
    LaunchMode = LaunchMode.SingleTop,
    Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = AndroidOAuthBuildConfig.RedirectScheme,
    DataPath = "/oauth2redirect")]
public sealed class OAuthCallbackActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        AndroidOAuthBroker.CompleteFromIntent(Intent?.Data);

        var returnIntent = new Intent(this, typeof(MainActivity));
        returnIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
        StartActivity(returnIntent);
        Finish();
    }
}
