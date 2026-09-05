using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YTMusicLite
{
    internal static class WebViewChromeFix
    {
        public static void Attach(Control root)
        {
            WebView2 web = FindWebView(root);
            if (web == null) return;

            web.CoreWebView2InitializationCompleted += delegate(object sender, CoreWebView2InitializationCompletedEventArgs e)
            {
                if (!e.IsSuccess || web.CoreWebView2 == null) return;
                web.CoreWebView2.NavigationCompleted += async delegate
                {
                    await ApplyAsync(web);
                };
                ApplyAsync(web);
            };

            if (web.CoreWebView2 != null)
            {
                web.CoreWebView2.NavigationCompleted += async delegate
                {
                    await ApplyAsync(web);
                };
                ApplyAsync(web);
            }
        }

        private static WebView2 FindWebView(Control root)
        {
            if (root == null) return null;
            WebView2 direct = root as WebView2;
            if (direct != null) return direct;

            foreach (Control child in root.Controls)
            {
                WebView2 found = FindWebView(child);
                if (found != null) return found;
            }
            return null;
        }

        private static async Task ApplyAsync(WebView2 web)
        {
            if (web == null || web.CoreWebView2 == null) return;

            const string script = @"(() => {
                let style = document.getElementById('ytmlite-native-player-hide');
                if (!style) {
                    style = document.createElement('style');
                    style.id = 'ytmlite-native-player-hide';
                    document.documentElement.appendChild(style);
                }
                style.textContent = `
                    ytmusic-player-bar,
                    #player-bar-background {
                        display: none !important;
                        visibility: hidden !important;
                        height: 0 !important;
                        min-height: 0 !important;
                        max-height: 0 !important;
                    }
                    html,
                    body,
                    ytmusic-app,
                    ytmusic-app-layout {
                        --ytmusic-player-bar-height: 0px !important;
                    }
                `;
            })();";

            try
            {
                await web.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch
            {
            }
        }
    }
}
