using System.Windows;

namespace BrowserTool
{
    public partial class GroupEditDialog : Window
    {
        public string GroupName { get; set; }
        public bool IsDefaultExpanded { get; set; }

        public GroupEditDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public GroupEditDialog(string groupName, bool isDefaultExpanded = false) : this()
        {
            GroupName = groupName;
            IsDefaultExpanded = isDefaultExpanded;
            chkDefaultExpanded.IsChecked = isDefaultExpanded;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GroupName))
            {
                MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 更新IsDefaultExpanded属性
            IsDefaultExpanded = chkDefaultExpanded.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}