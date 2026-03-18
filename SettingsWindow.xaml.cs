using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BetterPasswordVault
{
    public partial class SettingsWindow : Window
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterPasswordVault");
        private static readonly string AuthFile  = Path.Combine(AppDir, "auth.json");
        private static readonly string VaultFile = Path.Combine(AppDir, "vault.dat");

        private readonly byte[] _currentVaultKey;
        private readonly AppSettings _appSettings;

        public SettingsWindow(byte[] currentVaultKey)
        {
            InitializeComponent();
            _currentVaultKey = currentVaultKey;

            // Carica impostazioni salvate e applica ai ComboBox
            _appSettings = AppSettings.Load();
            ThemeCombo.SelectedIndex = _appSettings.Theme == "Chiaro" ? 1 : 0;
            LangCombo.SelectedIndex  = _appSettings.Language == "English" ? 1 : 0;

            _settingsLoaded = true;
        }

        // ── CAMBIO PASSWORD ───────────────────────────────────────────────

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            var oldPw  = OldPasswordBox.Password;
            var newPw  = NewPasswordBox.Password;
            var confPw = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(oldPw) || string.IsNullOrEmpty(newPw))
            {
                PasswordStatusText.Text = "Compila tutti i campi.";
                return;
            }

            if (newPw.Length < 8)
            {
                PasswordStatusText.Text = "La nuova password deve avere almeno 8 caratteri.";
                return;
            }

            if (newPw != confPw)
            {
                PasswordStatusText.Text = "Le password non coincidono.";
                return;
            }

            // Verifica password attuale
            var auth = LoadAuth();
            if (auth == null) return;

            byte[] oldSalt = Convert.FromBase64String(auth.SaltB64);
            byte[] oldHash = Convert.FromBase64String(auth.HashB64);
            byte[] givenHash = HashPassword(oldPw, oldSalt);

            if (!CryptographicOperations.FixedTimeEquals(oldHash, givenHash))
            {
                PasswordStatusText.Text = "Password attuale errata.";
                return;
            }

            try
            {
                // Decifra vault con la chiave vecchia
                byte[] vaultBytes = File.ReadAllBytes(VaultFile);
                byte[] oldIv = vaultBytes[..16];
                byte[] cipher = vaultBytes[16..];

                using var aesOld = Aes.Create();
                aesOld.Key = _currentVaultKey;
                aesOld.IV  = oldIv;

                byte[] plainBytes;
                using (var ms = new MemoryStream(cipher))
                using (var cs = new CryptoStream(ms, aesOld.CreateDecryptor(), CryptoStreamMode.Read))
                using (var mem = new MemoryStream())
                {
                    cs.CopyTo(mem);
                    plainBytes = mem.ToArray();
                }

                // Nuovo salt, hash e chiave
                byte[] newSalt = RandomNumberGenerator.GetBytes(16);
                byte[] newHash = HashPassword(newPw, newSalt);
                byte[] newKey  = DeriveVaultKey(newPw, newSalt);

                // Riscifra vault con nuova chiave
                byte[] newIv = RandomNumberGenerator.GetBytes(16);
                using var aesNew = Aes.Create();
                aesNew.Key = newKey;
                aesNew.IV  = newIv;

                using var outMs = new MemoryStream();
                outMs.Write(newIv, 0, newIv.Length);
                using (var cs = new CryptoStream(outMs, aesNew.CreateEncryptor(), CryptoStreamMode.Write))
                    cs.Write(plainBytes);

                File.WriteAllBytes(VaultFile, outMs.ToArray());

                // Aggiorna auth.json
                auth.SaltB64 = Convert.ToBase64String(newSalt);
                auth.HashB64 = Convert.ToBase64String(newHash);
                File.WriteAllText(AuthFile, JsonSerializer.Serialize(auth));

                PasswordStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                PasswordStatusText.Text = "Password aggiornata ✅";

                OldPasswordBox.Clear();
                NewPasswordBox.Clear();
                ConfirmPasswordBox.Clear();
            }
            catch (Exception ex)
            {
                PasswordStatusText.Text = $"Errore: {ex.Message}";
            }
        }

        // ── ELIMINA ACCOUNT ───────────────────────────────────────────────

        private void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Sei sicuro di voler eliminare l'account e tutti i dati salvati?\nQuesta operazione è irreversibile.",
                "Elimina account",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (File.Exists(AuthFile))  File.Delete(AuthFile);
                if (File.Exists(VaultFile)) File.Delete(VaultFile);

                MessageBox.Show("Account eliminato. L'applicazione verrà chiusa.", "Eliminato",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante l'eliminazione: {ex.Message}");
            }
        }

        // ── ESPORTA VAULT ─────────────────────────────────────────────────

        private void ExportVault_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(VaultFile))
            {
                MessageBox.Show("Nessun vault da esportare.", "Esporta",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title      = "Esporta vault",
                FileName   = $"vault_backup_{DateTime.Now:yyyyMMdd_HHmmss}.dat",
                Filter     = "Vault file (*.dat)|*.dat|Tutti i file (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.Copy(VaultFile, dialog.FileName, overwrite: true);
                    MessageBox.Show("Backup esportato con successo ✅", "Esporta",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore esportazione: {ex.Message}");
                }
            }
        }

        // ── TEMA ──────────────────────────────────────────────────────────

        private bool _settingsLoaded = false;

        private void ThemeCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_settingsLoaded) return;
        }

        private void LangCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!_settingsLoaded) return;
        }

        private void SaveTheme_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is not ComboBoxItem item) return;
            // Salva sempre in italiano indipendentemente dalla lingua UI
            var content = item.Content.ToString()!;
            var theme = (content == "Scuro" || content == "Dark") ? "Scuro" : "Chiaro";
            _appSettings.Theme = theme;
            _appSettings.Save();
            ThemeManager.Apply(theme);
        }

        private void SaveLanguage_Click(object sender, RoutedEventArgs e)
        {
            if (LangCombo.SelectedItem is not ComboBoxItem item) return;
            var content = item.Content.ToString()!;
            var lang = (content == "Italiano" || content == "Italian") ? "Italiano" : "English";
            _appSettings.Language = lang;
            _appSettings.Save();
            LanguageManager.Apply(lang);
        }

        // ── HELPERS ───────────────────────────────────────────────────────

        private AuthRecord? LoadAuth()
        {
            try
            {
                return JsonSerializer.Deserialize<AuthRecord>(File.ReadAllText(AuthFile));
            }
            catch
            {
                PasswordStatusText.Text = "Errore lettura file di autenticazione.";
                return null;
            }
        }

        private static byte[] HashPassword(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 200_000, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32);
        }

        private static byte[] DeriveVaultKey(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 200_001, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(32);
        }

        private class AuthRecord
        {
            public string Username { get; set; } = "";
            public string SaltB64  { get; set; } = "";
            public string HashB64  { get; set; } = "";
        }
    }
}
