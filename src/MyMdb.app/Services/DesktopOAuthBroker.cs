using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyMdb.Core.GoogleDrive;

namespace MyMdb.app.Services;

public static class DesktopOAuthBroker
{
    private const string SuccessHtml =
        "<html><body style='font-family:sans-serif;text-align:center;padding:48px;'>"
        + "<h2>MyMDB authorized</h2><p>You can close this tab and return to the app.</p>"
        + "</body></html>";

    private const string ErrorHtml =
        "<html><body style='font-family:sans-serif;text-align:center;padding:48px;'>"
        + "<h2>Authorization failed</h2><p>Please close this tab and try again in MyMDB.</p>"
        + "</body></html>";

    public static async Task<OAuthAuthorizationResult> AuthorizeAsync(
        Uri placeholderAuthUri,
        string expectedState,
        CancellationToken cancellationToken)
    {
        var port = GetAvailableLoopbackPort();
        var redirectUri = $"http://127.0.0.1:{port}/";

        var finalAuthUrl = placeholderAuthUri.AbsoluteUri.Replace(
            Uri.EscapeDataString("{REDIRECT_URI}"),
            Uri.EscapeDataString(redirectUri),
            StringComparison.Ordinal);

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        listener.Start();

        OpenBrowser(finalAuthUrl);

        var contextTask = listener.GetContextAsync();
        using var cancelReg = cancellationToken.Register(() => listener.Stop());

        var context = await contextTask;
        var query = context.Request.QueryString;
        var error = query["error"];
        var code = query["code"];
        var returnedState = query["state"];

        var stateValid = !string.IsNullOrEmpty(returnedState)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(returnedState),
                Encoding.UTF8.GetBytes(expectedState));

        var ok = string.IsNullOrEmpty(error) && !string.IsNullOrEmpty(code) && stateValid;
        await WriteHtmlResponseAsync(context.Response, ok ? SuccessHtml : ErrorHtml, ok ? 200 : 400);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"Google OAuth error: {error}");
        }

        if (!stateValid)
        {
            throw new InvalidOperationException("OAuth state mismatch.");
        }

        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException("Missing authorization code in OAuth callback.");
        }

        return new OAuthAuthorizationResult(code, redirectUri);
    }

    private static int GetAvailableLoopbackPort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        return ((IPEndPoint)tcp.LocalEndpoint).Port;
    }

    private static void OpenBrowser(string url)
    {
        if (OperatingSystem.IsLinux())
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
        }
        else
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private static async Task WriteHtmlResponseAsync(HttpListenerResponse response, string html, int statusCode)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = statusCode;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.OutputStream.Close();
    }
}
