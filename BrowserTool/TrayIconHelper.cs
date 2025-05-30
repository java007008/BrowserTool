using System.Windows.Controls;

namespace BrowserTool
{
    public class TrayIconHelper
    {
        private void ShowChangePasswordWindow()
        {
            var win = new ChangePasswordWindow();
            win.ShowDialog();
        }
    }
} 