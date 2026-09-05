using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace YTMusicLite
{
    internal static class MenuStyler
    {
        public static void Attach(MainForm form)
        {
            if (form == null) return;

            try
            {
                FieldInfo field = typeof(MainForm).GetField(
                    "tray",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) return;

                NotifyIcon tray = field.GetValue(form) as NotifyIcon;
                if (tray == null || tray.ContextMenuStrip == null) return;

                ContextMenuStrip menu = tray.ContextMenuStrip;
                menu.Renderer = new LiteMenuRenderer();
                menu.BackColor = Color.FromArgb(24, 24, 24);
                menu.ForeColor = Color.FromArgb(235, 235, 235);
                menu.ShowImageMargin = false;
                menu.ShowCheckMargin = true;
                menu.Padding = new Padding(5, 6, 5, 6);
                menu.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
                menu.MinimumSize = new Size(188, 0);

                foreach (ToolStripItem item in menu.Items)
                {
                    if (item is ToolStripSeparator)
                    {
                        item.Margin = new Padding(3, 5, 3, 5);
                    }
                    else
                    {
                        item.Padding = new Padding(8, 3, 8, 3);
                        item.Margin = new Padding(0, 1, 0, 1);
                    }
                }
            }
            catch
            {
            }
        }
    }
}
