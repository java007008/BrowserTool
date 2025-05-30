using System.Windows;
using System.Windows.Input;
using BrowserTool.Utils;

namespace BrowserTool
{
    public partial class LoginWindow : Window
    {
        private const string DEFAULT_PASSWORD = "qwe123";

        public LoginWindow()
        {
            InitializeComponent();
            txtPassword.Focus();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            ValidatePassword();
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
                ValidatePassword();
            }
        }

        private void ValidatePassword()
        {
            string password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("密码不能为空！");
                return;
            }

            if (PasswordHelper.VerifyOrSetStartupPassword(password))
            {
                KeyManager.SetPassword(password);
                DialogResult = true;
                Close();
            }
            else
            {
                ShowError("密码错误！");
                txtPassword.Password = string.Empty;
                txtPassword.Focus();
            }
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }
    }
} 