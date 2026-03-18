using System;
using System.Windows;

namespace BetterPasswordVault
{
    public static class LanguageManager
    {
        public static bool IsItalian { get; private set; } = true;

        public static void Apply(string language)
        {
            IsItalian = language != "English";
            var uri = new Uri(IsItalian ? "LanguageIT.xaml" : "LanguageEN.xaml", UriKind.Relative);
            var dict = new ResourceDictionary { Source = uri };

            // Rimuovi vecchio dizionario lingua e aggiungi il nuovo
            var merged = Application.Current.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var src = merged[i].Source?.OriginalString ?? "";
                if (src.Contains("Language"))
                    merged.RemoveAt(i);
            }
            merged.Add(dict);
        }
    }
}
