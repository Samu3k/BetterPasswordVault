using System;
using System.IO;
using System.Text.Json;

namespace BetterPasswordVault
{
    public class AppSettings
    {
        public string Theme    { get; set; } = "Scuro";
        public string Language { get; set; } = "Italiano";

        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterPasswordVault");
        private static readonly string SettingsFile = Path.Combine(AppDir, "settings.json");

        // Carica da file, se non esiste ritorna i valori di default
        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return new AppSettings();
                var json = File.ReadAllText(SettingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        // Salva su file
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDir);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this));
            }
            catch { }
        }
    }
}
