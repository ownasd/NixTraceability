using System;
using System.Windows;

namespace NixTraceability
{
    public static class ThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            var app = Application.Current;
            if (app == null) return;

            string path = "Themes/DarkTheme.xaml";
            if (themeName == "Light Theme")
            {
                path = "Themes/LightTheme.xaml";
            }

            var dict = new ResourceDictionary { Source = new Uri(path, UriKind.Relative) };
            
            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(dict);
        }
    }
}
