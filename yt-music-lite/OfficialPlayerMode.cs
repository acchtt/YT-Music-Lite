using System;
using System.Reflection;
using System.Windows.Forms;

namespace YTMusicLite
{
    public static class OfficialPlayerMode
    {
        public static void Attach(MainForm main)
        {
            if (main == null) return;

            Panel playerBar = GetPrivateField<Panel>(main, "playerBar");
            if (playerBar != null)
            {
                playerBar.Visible = false;
                playerBar.Height = 0;
                playerBar.TabStop = false;
            }

            main.PerformLayout();
        }

        private static T GetPrivateField<T>(object instance, string name) where T : class
        {
            try
            {
                FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null) return null;
                return field.GetValue(instance) as T;
            }
            catch
            {
                return null;
            }
        }
    }
}
