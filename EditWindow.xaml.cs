using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace BetterPasswordVault
{
    public partial class EditWindow : Window
    {
        private readonly VaultWindow.VaultItem _item;
        private bool _passwordVisible = false;
        public bool Saved { get; private set; } = false;

        public EditWindow(VaultWindow.VaultItem item)
        {
            InitializeComponent();
            _item = item;

            SiteBox.Text     = item.Site;
            UserBox.Text     = item.Username;
            PassBox.Password = item.Password;
            NoteBox.Text     = item.Note;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var site = SiteBox.Text.Trim();
            var user = UserBox.Text.Trim();
            var pass = _passwordVisible ? PassBoxVisible.Text : PassBox.Password;

            if (string.IsNullOrEmpty(site) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                StatusText.Text = "Sito, username e password sono obbligatori.";
                return;
            }

            _item.Site     = site;
            _item.Username = user;
            _item.Password = pass;
            _item.Note     = NoteBox.Text.Trim();

            Saved = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => this.Close();

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            _passwordVisible = !_passwordVisible;
            if (_passwordVisible)
            {
                PassBoxVisible.Text       = PassBox.Password;
                PassBox.Visibility        = Visibility.Collapsed;
                PassBoxVisible.Visibility = Visibility.Visible;
                ToggleIcon.Text           = "\uD83D\uDE48";
            }
            else
            {
                PassBox.Password          = PassBoxVisible.Text;
                PassBoxVisible.Visibility = Visibility.Collapsed;
                PassBox.Visibility        = Visibility.Visible;
                ToggleIcon.Text           = "\uD83D\uDC41";
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            var generated = GeneratePassword(16);
            PassBox.Password    = generated;
            PassBoxVisible.Text = generated;
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
    }
}
