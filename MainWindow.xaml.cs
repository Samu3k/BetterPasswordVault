using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BetterPasswordVault
{
    public partial class MainWindow : Window
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterPasswordVault");
        private static readonly string AuthFile = Path.Combine(AppDir, "auth.json");

        private bool _isFirstRun;

        public MainWindow()
        {
            InitializeComponent();
            Directory.CreateDirectory(AppDir);

            _isFirstRun = !File.Exists(AuthFile);

            if (_isFirstRun)
            {
                // Prima volta: mostra registrazione
                SubtitleText.Text = Application.Current.TryFindResource("AppSubtitleRegister") as string
                                    ?? "Crea il tuo account per iniziare";
                RegisterPanel.Visibility = Visibility.Visible;
                RegPasswordBox.PasswordChanged += PasswordStrength_Changed;
            }
            else
            {
                // Account già esistente: mostra solo login
                SubtitleText.Text = Application.Current.TryFindResource("AppSubtitleLogin") as string
                                    ?? "Bentornato! Inserisci le tue credenziali";
                LoginPanel.Visibility = Visibility.Visible;
            }
        }

        // ── REGISTRAZIONE ────────────────────────────────────────────────

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var username = RegUsernameBox.Text.Trim();
            var password = RegPasswordBox.Password;
            var confirm  = RegPasswordConfirmBox.Password;

            if (username.Length < 3)
            {
                RegStatusText.Text = "Username deve avere almeno 3 caratteri.";
                return;
            }

            if (password.Length < 8)
            {
                RegStatusText.Text = "La password deve avere almeno 8 caratteri.";
                return;
            }

            if (password != confirm)
            {
                RegStatusText.Text = "Le password non coincidono.";
                return;
            }

            if (File.Exists(AuthFile))
            {
                RegStatusText.Text = "Account già esistente su questo PC.";
                return;
            }

            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = HashPassword(password, salt);

            var auth = new AuthRecord
            {
                Username = username,
                SaltB64  = Convert.ToBase64String(salt),
                HashB64  = Convert.ToBase64String(hash)
            };

            File.WriteAllText(AuthFile, JsonSerializer.Serialize(auth));

            // Dopo la registrazione apri direttamente il vault
            byte[] vaultKey = DeriveVaultKey(password, salt);
            var vault = new VaultWindow(username, vaultKey);
            vault.Show();
            this.Close();
        }

        // ── LOGIN ─────────────────────────────────────────────────────────

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var username = LoginUsernameBox.Text.Trim();
            var password = LoginPasswordBox.Password;

            if (!File.Exists(AuthFile))
            {
                LoginStatusText.Text = "Nessun account trovato.";
                return;
            }

            var auth = JsonSerializer.Deserialize<AuthRecord>(File.ReadAllText(AuthFile));
            if (auth == null)
            {
                LoginStatusText.Text = "File di accesso corrotto.";
                return;
            }

            if (!string.Equals(auth.Username, username, StringComparison.Ordinal))
            {
                LoginStatusText.Text = "Username errato.";
                return;
            }

            byte[] salt         = Convert.FromBase64String(auth.SaltB64);
            byte[] expectedHash = Convert.FromBase64String(auth.HashB64);
            byte[] givenHash    = HashPassword(password, salt);

            if (!CryptographicOperations.FixedTimeEquals(expectedHash, givenHash))
            {
                LoginStatusText.Text = "Password errata.";
                return;
            }

            byte[] vaultKey = DeriveVaultKey(password, salt);
            var vault = new VaultWindow(username, vaultKey);
            vault.Show();
            this.Close();
        }

        // ── INDICATORE FORZA PASSWORD ─────────────────────────────────────

        private void PasswordStrength_Changed(object sender, RoutedEventArgs e)
        {
            var pw = RegPasswordBox.Password;
            int score = 0;

            if (pw.Length >= 8)  score++;
            if (pw.Length >= 12) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(pw, @"[A-Z]") &&
                System.Text.RegularExpressions.Regex.IsMatch(pw, @"[0-9]")) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(pw, @"[^a-zA-Z0-9]")) score++;

            var colors = new[]
            {
                new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), // vuoto
                new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)), // debole
                new SolidColorBrush(Color.FromRgb(0xF9, 0x73, 0x16)), // media
                new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)), // buona
                new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // ottima
            };

            StrBar1.Background = score >= 1 ? colors[score] : colors[0];
            StrBar2.Background = score >= 2 ? colors[score] : colors[0];
            StrBar3.Background = score >= 3 ? colors[score] : colors[0];
            StrBar4.Background = score >= 4 ? colors[score] : colors[0];
        }

        // ── CRYPTO HELPERS ────────────────────────────────────────────────

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

        // ── MODELLO ───────────────────────────────────────────────────────

        private class AuthRecord
        {
            public string Username { get; set; } = "";
            public string SaltB64  { get; set; } = "";
            public string HashB64  { get; set; } = "";
        }
    }
}
