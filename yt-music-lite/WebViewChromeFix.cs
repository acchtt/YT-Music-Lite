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
                const oldStyle = document.getElementById('ytmlite-native-player-hide');
                if (oldStyle) oldStyle.remove();

                const roots = [
                    document.documentElement,
                    document.body,
                    document.querySelector('ytmusic-app'),
                    document.querySelector('ytmusic-app-layout')
                ];
                roots.forEach(node => {
                    if (!node || !node.style) return;
                    node.style.removeProperty('--ytmusic-player-bar-height');
                });

                const bar = document.querySelector('ytmusic-player-bar');
                const background = document.querySelector('#player-bar-background');
                [bar, background].forEach(node => {
                    if (!node || !node.style) return;
                    node.style.removeProperty('display');
                    node.style.removeProperty('visibility');
                    node.style.removeProperty('height');
                    node.style.removeProperty('min-height');
                    node.style.removeProperty('max-height');
                    node.style.removeProperty('opacity');
                });
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
