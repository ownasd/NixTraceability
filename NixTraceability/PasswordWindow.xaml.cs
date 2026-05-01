using System.Windows;
using System.Windows.Input;

namespace NixTraceability
{
    public partial class PasswordWindow : Window
    {
        public bool IsAuthenticated { get; private set; } = false;

        public PasswordWindow()
        {
            InitializeComponent();
            txtPassword.Focus();
        }

        private void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            CheckPassword();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void txtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CheckPassword();
            }
        }

        private void CheckPassword()
        {
            if (txtPassword.Password == "admin123")
            {
                IsAuthenticated = true;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Incorrect Password!", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }
    }
}
