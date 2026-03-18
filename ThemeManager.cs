using System;
using System.Windows;

namespace BetterPasswordVault
{
    public static class ThemeManager
    {
        public static bool IsDark { get; private set; } = true;

        public static void Apply(string theme)
        {
            IsDark = theme != "Chiaro";
            var uri = new Uri(IsDark ? "ThemeDark.xaml" : "ThemeLight.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);
        }
    }
}
