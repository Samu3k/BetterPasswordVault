using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace BetterPasswordVault
{
    public partial class LockWindow : Window
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterPasswordVault");
        private static readonly string AuthFile = Path.Combine(AppDir, "auth.json");

        public bool Unlocked { get; private set; } = false;
        public byte[]? VaultKey { get; private set; }

        public LockWindow()
        {
            InitializeComponent();
            PasswordBox.Focus();
        }

        private void Unlock_Click(object sender, RoutedEventArgs e) => TryUnlock();

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) TryUnlock();
        }

        private void TryUnlock()
        {
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(password))
            {
                StatusText.Text = "Inserisci la password.";
                return;
            }

            try
            {
                var auth = JsonSerializer.Deserialize<AuthRecord>(File.ReadAllText(AuthFile));
                if (auth == null) { StatusText.Text = "Errore lettura account."; return; }

                byte[] salt         = Convert.FromBase64String(auth.SaltB64);
                byte[] expectedHash = Convert.FromBase64String(auth.HashB64);
                byte[] givenHash    = HashPassword(password, salt);

                if (!CryptographicOperations.FixedTimeEquals(expectedHash, givenHash))
                {
                    StatusText.Text = "Password errata.";
                    PasswordBox.Clear();
                    return;
                }

                VaultKey = DeriveVaultKey(password, salt);
                Unlocked = true;
                this.Close();
            }
            catch
            {
                StatusText.Text = "Errore durante la verifica.";
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
