using System.Windows;

namespace BetterPasswordVault
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var settings = AppSettings.Load();
            ThemeManager.Apply(settings.Theme);
            LanguageManager.Apply(settings.Language);
        }
    }
}
