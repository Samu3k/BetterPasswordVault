using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace BetterPasswordVault
{
    public partial class VaultWindow : Window
    {
        private static readonly string AppDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BetterPasswordVault");
        private static readonly string VaultFile = Path.Combine(AppDir, "vault.dat");

        private readonly byte[] _vaultKey;
        private readonly string _username;
        private ObservableCollection<VaultItem> Items;
        private bool _passwordVisible = false;
        private bool _hasUnsavedChanges = false;

        // ── AUTO-LOCK ─────────────────────────────────────────────────────
        private readonly DispatcherTimer _lockTimer;
        private static readonly TimeSpan LockTimeout = TimeSpan.FromMinutes(10);

        public VaultWindow(string username, byte[] vaultKey)
        {
            InitializeComponent();
            _vaultKey  = vaultKey;
            _username  = username;
            WelcomeText.Text = $"Benvenuto, {username}";
            Items = new ObservableCollection<VaultItem>();
            VaultCards.ItemsSource = Items;

            _lockTimer = new DispatcherTimer { Interval = LockTimeout };
            _lockTimer.Tick += OnLockTimerTick;
            _lockTimer.Start();

            this.MouseMove += ResetLockTimer;
            this.KeyDown   += ResetLockTimer;
            this.MouseDown += ResetLockTimer;

            this.Closing += VaultWindow_Closing;

            LoadVault();
        }

        private void VaultWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_hasUnsavedChanges) return;

            var result = MessageBox.Show(
                "Hai modifiche non salvate. Vuoi salvare prima di uscire?",
                "Modifiche non salvate",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                Save_Click(this, new RoutedEventArgs());
            else if (result == MessageBoxResult.Cancel)
                e.Cancel = true;
        }

        private void ResetLockTimer(object sender, EventArgs e)
        {
            _lockTimer.Stop();
            _lockTimer.Start();
        }

        private void OnLockTimerTick(object? sender, EventArgs e)
        {
            _lockTimer.Stop();
            LockApp();
        }

        private void LockApp()
        {
            var lockWindow = new LockWindow();
            lockWindow.Owner = this;
            this.Hide();
            lockWindow.ShowDialog();

            if (lockWindow.Unlocked)
            {
                this.Show();
                _lockTimer.Start();
            }
            else
            {
                var login = new MainWindow();
                login.Show();
                this.Close();
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var site = SiteBox.Text.Trim();
            var user = UserBox.Text.Trim();
            var pass = _passwordVisible ? PassBoxVisible.Text : PassBox.Password;

            if (string.IsNullOrEmpty(site) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                StatusText.Text = "Compila tutti i campi obbligatori.";
                return;
            }

            Items.Add(new VaultItem { Site = site, Username = user, Password = pass, Note = "" });

            SiteBox.Text = "";
            UserBox.Text = "";
            PassBox.Password = "";
            PassBoxVisible.Text = "";
            StatusText.Text = "Aggiunto ✅ (premi Salva)";
            _hasUnsavedChanges = true;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            ClearSearchBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(query))
            {
                VaultCards.ItemsSource = Items;
                return;
            }

            VaultCards.ItemsSource = Items.Where(i =>
                i.DisplaySite.ToLower().Contains(query.ToLower()) ||
                i.Site.ToLower().Contains(query.ToLower())).ToList();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settings = new SettingsWindow(_vaultKey);
            settings.Owner = this;
            settings.ShowDialog();
        }

        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VaultItem item)
            {
                var editWindow = new EditWindow(item);
                editWindow.Owner = this;
                editWindow.ShowDialog();

                if (editWindow.Saved)
                {
                    StatusText.Text = "Credenziale modificata ✅ (premi Salva)";
                    _hasUnsavedChanges = true;
                }
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VaultItem item)
            {
                Items.Remove(item);
                StatusText.Text = "Voce eliminata (premi Salva)";
                _hasUnsavedChanges = true;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VaultItem item)
            {
                Clipboard.SetText(item.Password);
                StatusText.Text = $"Password di {item.Site} copiata ✅";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var json = JsonSerializer.Serialize(Items);
                var plainBytes = Encoding.UTF8.GetBytes(json);
                byte[] iv = RandomNumberGenerator.GetBytes(16);

                using var aes = Aes.Create();
                aes.Key = _vaultKey;
                aes.IV  = iv;

                using var ms = new MemoryStream();
                ms.Write(iv, 0, iv.Length);
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    cs.Write(plainBytes);

                File.WriteAllBytes(VaultFile, ms.ToArray());
                StatusText.Text = "Salvato ✅";
                _hasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Errore salvataggio: {ex.Message}";
            }
        }

        private void LoadVault()
        {
            try
            {
                if (!File.Exists(VaultFile)) return;
                var fileBytes = File.ReadAllBytes(VaultFile);
                if (fileBytes.Length < 16) return;

                byte[] iv = fileBytes[..16];
                byte[] cipherBytes = fileBytes[16..];

                using var aes = Aes.Create();
                aes.Key = _vaultKey;
                aes.IV  = iv;

                using var ms = new MemoryStream(cipherBytes);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
                using var sr = new StreamReader(cs, Encoding.UTF8);

                var loaded = JsonSerializer.Deserialize<ObservableCollection<VaultItem>>(sr.ReadToEnd());
                if (loaded == null) return;

                Items.Clear();
                foreach (var it in loaded) Items.Add(it);
            }
            catch
            {
                StatusText.Text = "Impossibile decifrare il vault.";
            }
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _passwordVisible = !_passwordVisible;
            if (_passwordVisible)
            {
                PassBoxVisible.Text = PassBox.Password;
                PassBox.Visibility = Visibility.Collapsed;
                PassBoxVisible.Visibility = Visibility.Visible;
                TogglePassBtn.Content = "🙈";
            }
            else
            {
                PassBox.Password = PassBoxVisible.Text;
                PassBoxVisible.Visibility = Visibility.Collapsed;
                PassBox.Visibility = Visibility.Visible;
                TogglePassBtn.Content = "👁";
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            var generated = GeneratePassword(16);
            PassBox.Password = generated;
            PassBoxVisible.Text = generated;
            StatusText.Text = "Password generata 🔐";
        }

        private static string GeneratePassword(int length)
        {
            const string chars =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-_=+[]{};:,.?/";
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
                result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
            return new string(result);
        }

        public class VaultItem : INotifyPropertyChanged
        {
            private string _site = "", _username = "", _password = "", _note = "";

            public string Site
            {
                get => _site;
                set { _site = value; OnPropertyChanged(nameof(Site)); OnPropertyChanged(nameof(FaviconUrl)); OnPropertyChanged(nameof(DisplaySite)); }
            }
            public string Username
            {
                get => _username;
                set { _username = value; OnPropertyChanged(nameof(Username)); }
            }
            public string Password
            {
                get => _password;
                set { _password = value; OnPropertyChanged(nameof(Password)); }
            }
            public string Note
            {
                get => _note;
                set { _note = value; OnPropertyChanged(nameof(Note)); OnPropertyChanged(nameof(HasNote)); }
            }

            [JsonIgnore]
            public string FaviconUrl => string.IsNullOrWhiteSpace(Site) ? ""
                : $"https://www.google.com/s2/favicons?domain={Site}&sz=32";

            [JsonIgnore]
            public string DisplaySite
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(Site)) return Site;
                    try
                    {
                        var url = Site.Contains("://") ? Site : "https://" + Site;
                        var host = new Uri(url).Host;
                        return host.StartsWith("www.") ? host.Substring(4) : host;
                    }
                    catch { return Site; }
                }
            }

            [JsonIgnore]
            public bool HasNote => !string.IsNullOrWhiteSpace(Note);

            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string name) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
