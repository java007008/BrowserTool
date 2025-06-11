using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CefSharp;
using CefSharp.Wpf;

namespace BrowserTool.Controls
{
    /// <summary>
    /// SearchBar.xaml 的交互逻辑
    /// </summary>
    public partial class SearchBar : UserControl
    {
        private ChromiumWebBrowser _browser;
        private int _currentMatchIndex = 0;
        private int _totalMatches = 0;
        private bool _isMatchCase = false;

        public event EventHandler CloseRequested;

        public bool IsMatchCase
        {
            get { return _isMatchCase; }
            set
            {
                _isMatchCase = value;
                UpdateMatchCaseButtonStyle();
            }
        }

        public SearchBar()
        {
            InitializeComponent();
            UpdateUI();
        }

        public void SetBrowser(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }

        public void FocusSearchBox()
        {
            SearchTextBox.Focus();
            SearchTextBox.SelectAll();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text;
            
            if (string.IsNullOrEmpty(searchText))
            {
                StopFind();
                return;
            }

            StartFind(searchText);
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Enter:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        FindPrevious();
                    }
                    else
                    {
                        FindNext();
                    }
                    e.Handled = true;
                    break;
                case Key.Escape:
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            FindPrevious();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            FindNext();
        }

        private void MatchCaseButton_Click(object sender, RoutedEventArgs e)
        {
            IsMatchCase = !IsMatchCase;
            
            // 重新搜索
            var searchText = SearchTextBox.Text;
            if (!string.IsNullOrEmpty(searchText))
            {
                StartFind(searchText);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void StartFind(string searchText)
        {
            if (_browser == null || !_browser.IsBrowserInitialized)
                return;

            try
            {
                _browser.Find(searchText, forward: true, matchCase: IsMatchCase, findNext: false);
                
                // 简单的UI更新，显示搜索中状态
                ResultCountText.Text = "搜索中...";
                SearchTextBox.Background = System.Windows.Media.Brushes.DarkSlateGray;
                
                // 启用按钮
                PreviousButton.IsEnabled = true;
                NextButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ResultCountText.Text = "搜索失败";
                SearchTextBox.Background = System.Windows.Media.Brushes.DarkRed;
            }
        }

        private void FindNext()
        {
            var searchText = SearchTextBox.Text;
            if (string.IsNullOrEmpty(searchText) || _browser == null || !_browser.IsBrowserInitialized)
                return;

            try
            {
                _browser.Find(searchText, forward: true, matchCase: IsMatchCase, findNext: true);
            }
            catch (Exception ex)
            {
                // 忽略错误
            }
        }

        private void FindPrevious()
        {
            var searchText = SearchTextBox.Text;
            if (string.IsNullOrEmpty(searchText) || _browser == null || !_browser.IsBrowserInitialized)
                return;

            try
            {
                _browser.Find(searchText, forward: false, matchCase: IsMatchCase, findNext: true);
            }
            catch (Exception ex)
            {
                // 忽略错误
            }
        }

        private void StopFind()
        {
            if (_browser == null || !_browser.IsBrowserInitialized)
                return;

            try
            {
                _browser.StopFinding(clearSelection: true);
            }
            catch (Exception ex)
            {
                // 忽略错误
            }
            
            _currentMatchIndex = 0;
            _totalMatches = 0;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_totalMatches > 0)
            {
                ResultCountText.Text = $"{_currentMatchIndex}/{_totalMatches}";
                SearchTextBox.Background = System.Windows.Media.Brushes.DarkSlateGray;
            }
            else if (!string.IsNullOrEmpty(SearchTextBox.Text))
            {
                ResultCountText.Text = "0/0";
                SearchTextBox.Background = System.Windows.Media.Brushes.DarkRed;
            }
            else
            {
                ResultCountText.Text = "0/0";
                SearchTextBox.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
            }

            PreviousButton.IsEnabled = !string.IsNullOrEmpty(SearchTextBox.Text);
            NextButton.IsEnabled = !string.IsNullOrEmpty(SearchTextBox.Text);
        }

        private void UpdateMatchCaseButtonStyle()
        {
            // 触发数据绑定更新
            var binding = MatchCaseButton.GetBindingExpression(Button.StyleProperty);
            binding?.UpdateTarget();
        }

        public void Clear()
        {
            SearchTextBox.Clear();
            StopFind();
        }
    }
}
