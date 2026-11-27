using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using AndroidX.Browser.CustomTabs;
using MyMdb.Core.GoogleDrive;

namespace MyMdb.app.Android;

internal static class AndroidOAuthBroker
{
    private static readonly object SyncRoot = new();
    private static WeakReference<Activity>? _currentActivity;
    private static TaskCompletionSource<OAuthAuthorizationResult>? _pendingTcs;
    private static string? _expectedState;
    private static string? _activeRedirectUri;
    private static bool _awaitingBrowserReturn;

    public static void SetCurrentActivity(Activity activity)
    {
        lock (SyncRoot)
        {
            _currentActivity = new WeakReference<Activity>(activity);
        }
    }

    public static void OnMainActivityResumed()
    {
        TaskCompletionSource<OAuthAuthorizationResult>? tcs = null;

        lock (SyncRoot)
        {
            if (_awaitingBrowserReturn && _pendingTcs is not null)
            {
                tcs = _pendingTcs;
                _pendingTcs = null;
                _expectedState = null;
                _activeRedirectUri = null;
                _awaitingBrowserReturn = false;
            }
        }

        // User closed the Custom Tab or navigated back without completing sign-in.
        tcs?.TrySetCanceled();
    }

    public static Task<OAuthAuthorizationResult> AuthorizeAsync(
        Uri placeholderAuthUri,
        string expectedState,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(AndroidOAuthBuildConfig.ClientId))
        {
            throw new InvalidOperationException(
                "Android Google OAuth Client ID is not configured at build time.");
        }

        var redirectUri = $"{AndroidOAuthBuildConfig.RedirectScheme}:/oauth2redirect";
        var finalAuthUrl = placeholderAuthUri.AbsoluteUri.Replace(
            Uri.EscapeDataString("{REDIRECT_URI}"),
            Uri.EscapeDataString(redirectUri),
            StringComparison.Ordinal);

        var tcs = new TaskCompletionSource<OAuthAuthorizationResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Activity? activity = null;
        lock (SyncRoot)
        {
            _pendingTcs?.TrySetCanceled();
            _pendingTcs = tcs;
            _expectedState = expectedState;
            _activeRedirectUri = redirectUri;
            _awaitingBrowserReturn = true;
            _currentActivity?.TryGetTarget(out activity);
        }

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        }

        var androidUri = global::Android.Net.Uri.Parse(finalAuthUrl)!;
        LaunchInCustomTab(activity, androidUri);

        return tcs.Task;
    }

    private static void LaunchInCustomTab(Activity? activity, global::Android.Net.Uri uri)
    {
        try
        {
            var customTabsIntent = new CustomTabsIntent.Builder()
                .SetShowTitle(true)?
                .Build();

            if (customTabsIntent is not null)
            {
                if (activity is not null)
                {
                    customTabsIntent.LaunchUrl(activity, uri);
                    return;
                }

                customTabsIntent.Intent?.AddFlags(ActivityFlags.NewTask);
                customTabsIntent.LaunchUrl(global::Android.App.Application.Context, uri);
                return;
            }
        }
        catch
        {
            // Fall back to standard browser intent below.
        }

        var fallbackIntent = new Intent(Intent.ActionView, uri);
        fallbackIntent.AddFlags(ActivityFlags.NewTask);
        global::Android.App.Application.Context.StartActivity(fallbackIntent);
    }

    public static void CompleteFromIntent(global::Android.Net.Uri? data)
    {
        TaskCompletionSource<OAuthAuthorizationResult>? tcs;
        string? expectedState;
        string? redirectUri;

        lock (SyncRoot)
        {
            tcs = _pendingTcs;
            expectedState = _expectedState;
            redirectUri = _activeRedirectUri;
            _pendingTcs = null;
            _expectedState = null;
            _activeRedirectUri = null;
            _awaitingBrowserReturn = false;
        }

        if (tcs is null)
        {
            return;
        }

        if (data is null)
        {
            tcs.TrySetException(new InvalidOperationException("Empty OAuth callback URI."));
            return;
        }

        var error = data.GetQueryParameter("error");
        if (!string.IsNullOrEmpty(error))
        {
            tcs.TrySetException(new InvalidOperationException($"Google OAuth error: {error}"));
            return;
        }

        var returnedState = data.GetQueryParameter("state");
        var stateValid = !string.IsNullOrEmpty(returnedState)
            && !string.IsNullOrEmpty(expectedState)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(returnedState),
                Encoding.UTF8.GetBytes(expectedState));

        if (!stateValid)
        {
            tcs.TrySetException(new InvalidOperationException("OAuth state mismatch."));
            return;
        }

        var code = data.GetQueryParameter("code");
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(redirectUri))
        {
            tcs.TrySetException(new InvalidOperationException("Missing authorization code in OAuth callback."));
            return;
        }

        tcs.TrySetResult(new OAuthAuthorizationResult(code, redirectUri));
    }
}
