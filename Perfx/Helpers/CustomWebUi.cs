namespace Perfx
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    using ColoredConsole;

    using Microsoft.IdentityModel.Clients.ActiveDirectory.Extensibility;
    using Microsoft.Toolkit.Wpf.UI.Controls;

    // Credit: https://techcommunity.microsoft.com/t5/windows-dev-appconsult/how-to-use-active-directory-authentication-library-adal-for-net/ba-p/400623#
    class CustomWebUi : ICustomWebUi
    {
        public async Task<Uri> AcquireAuthorizationCodeAsync(Uri authorizationUri, Uri redirectUri)
        {
            var tcs = new TaskCompletionSource<Uri>();
            var thread = new Thread(() => AcquireAuthorizationCodeAsync(authorizationUri, tcs));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return await tcs.Task;
        }

        private void AcquireAuthorizationCodeAsync(Uri authorizationUri, TaskCompletionSource<Uri> tcs)
        {
            var webView = new WebView();
            var w = new Window
            {
                Title = "Sign in to your account",
                WindowStyle = WindowStyle.ToolWindow,
                Content = webView,
                Width = 600,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            w.Loaded += (_, __) => webView.Navigate(authorizationUri);

            webView.NavigationCompleted += (_, e) =>
            {
                System.Diagnostics.Debug.WriteLine(e.Uri);
                if (e.Uri.Query.Contains("code="))
                {
                    tcs.SetResult(e.Uri);
                    w.DialogResult = true;
                    w.Close();
                }
                if (e.Uri.Query.Contains("error="))
                {
                    tcs.SetException(new Exception(e.Uri.Query));
                    w.DialogResult = false;
                    w.Close();
                }
            };
            webView.UnsupportedUriSchemeIdentified += (_, e) =>
            {
                if (e.Uri.Query.Contains("code="))
                {
                    tcs.SetResult(e.Uri);
                    w.DialogResult = true;
                    w.Close();
                }
                else
                {
                    tcs.SetException(new Exception($"Unknown error: {e.Uri}"));
                    w.DialogResult = false;
                    w.Close();
                }
            };

            w.Activate();
            w.BringIntoView();
            w.Topmost = true;
            w.Focus();
            if (w.ShowDialog() != true && !tcs.Task.IsCompleted)
            {
                tcs.SetException(new Exception("canceled"));
            }
        }
    }
}
