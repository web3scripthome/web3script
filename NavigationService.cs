using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; 

namespace web3script
{
    public class NavigationService
    {
        // Panel references
        private UIElement _initialHint;
        private TextBlock _breadcrumbText;
        private Panel _projectListPanel;
        private Panel _executeListPanel;
        private Panel _reportInfoPanel;
        private Panel _groupConfigPanel;
        private Panel _walletConfigPanel;
        private Panel _emailConfigPanel;
        private Panel _otherConfigPanel;
        private Panel _proxyConfigPanel;
        private Panel _projectListGrid;
        private Panel _projectDetailGrid;

        // State
        private string _currentSelectedMenu = "";
        private bool _isLuMaoExpanded = true;
        private bool _isAccountExpanded = true;

        // Menu items
        private UIElement _projectListItem;
        private UIElement _executeListItem;
        private UIElement _reportInfoItem;
        private UIElement _accConfigItem;
        private UIElement _walletConfigItem;
        private UIElement _emailConfigItem;
        private UIElement _otherConfigItem;
        private UIElement _proxyConfigItem;
        private TextBlock _luMaoExpandIcon;
        private TextBlock _accountExpandIcon;

        public NavigationService(Dictionary<string, object> uiElements)
        {
            // Initialize UI elements from dictionary
            _initialHint = uiElements["initialHint"] as UIElement;
            _breadcrumbText = uiElements["breadcrumbText"] as TextBlock;
            _projectListPanel = uiElements["projectListPanel"] as Panel;
            _executeListPanel = uiElements["executeListPanel"] as Panel;
            _reportInfoPanel = uiElements["reportInfoPanel"] as Panel;
            _groupConfigPanel = uiElements["groupConfigPanel"] as Panel;
            _walletConfigPanel = uiElements["walletConfigPanel"] as Panel;
            _emailConfigPanel = uiElements["emailConfigPanel"] as Panel;
            _otherConfigPanel = uiElements["otherConfigPanel"] as Panel;
            _proxyConfigPanel = uiElements["proxyConfigPanel"] as Panel;
            _projectListGrid = uiElements["projectListGrid"] as Panel;
            _projectDetailGrid = uiElements["projectDetailGrid"] as Panel;

            // Initialize menu items
            _projectListItem = uiElements["projectListItem"] as UIElement;
            _executeListItem = uiElements["executeListItem"] as UIElement;
            _reportInfoItem = uiElements["reportInfoItem"] as UIElement;
            _accConfigItem = uiElements["accConfigItem"] as UIElement;
            _walletConfigItem = uiElements["walletConfigItem"] as UIElement;
            _emailConfigItem = uiElements["emailConfigItem"] as UIElement;
            _otherConfigItem = uiElements["otherConfigItem"] as UIElement;
            _proxyConfigItem = uiElements["proxyConfigItem"] as UIElement;
            _luMaoExpandIcon = uiElements["luMaoExpandIcon"] as TextBlock;
            _accountExpandIcon = uiElements["accountExpandIcon"] as TextBlock;

            // Initialize menu state
            UpdateMenuVisibility();
        }

        public string CurrentSelectedMenu => _currentSelectedMenu;

        // Navigate to a specific panel based on tag
        public void NavigateToPanel(string tag, UIElement sender)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            // Set current selected menu
            _currentSelectedMenu = tag;

            // Reset all menu item styles
            ResetMenuItemStyles();

            // Set selected menu item style
            if (sender is Grid grid)
            {
                SetSelectedMenuItem(grid);
            }

            // Hide all content panels
            HideAllContentPanels();

            // Hide initial hint
            if (_initialHint != null)
                _initialHint.Visibility = Visibility.Collapsed;

            // Show appropriate panel based on tag
            switch (tag)
            {
                case "项目清单":
                    _projectListPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "项目清单";
                    break;
                case "执行列表":
                    _executeListPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "执行列表";
                    break;
                case "报表信息":
                    _reportInfoPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "报表信息";
                    break;
                case "分组配置":
                    _groupConfigPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "分组配置";
                    break;
                case "钱包配置":
                    _walletConfigPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "钱包配置";
                    break;
                case "邮箱配置":
                    _emailConfigPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "邮箱配置";
                    break;
                case "其它配置":
                    _otherConfigPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "其它配置";
                    break;
                case "代理配置":
                    _proxyConfigPanel.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "代理配置";
                    break;
                default:
                    if (_initialHint != null)
                        _initialHint.Visibility = Visibility.Visible;
                    _breadcrumbText.Text = "";
                    break;
            }
        }

        // Toggle the expansion state of the LuMao menu
        public void ToggleLuMaoMenu()
        {
            _isLuMaoExpanded = !_isLuMaoExpanded;
            UpdateMenuVisibility();
        }

        // Toggle the expansion state of the Account Config menu
        public void ToggleAccountConfigMenu()
        {
            _isAccountExpanded = !_isAccountExpanded;
            UpdateMenuVisibility();
        }

        // Update breadcrumb text
        public void UpdateBreadcrumb(string breadcrumb)
        {
            _breadcrumbText.Text = breadcrumb;
        }

        // Navigate to project details
        public void NavigateToProjectDetails()
        {
            _projectListGrid.Visibility = Visibility.Collapsed;
            _projectDetailGrid.Visibility = Visibility.Visible;
        }

        // Navigate back to project list
        public void NavigateBackToProjectList()
        {
            _projectDetailGrid.Visibility = Visibility.Collapsed;
            _projectListGrid.Visibility = Visibility.Visible;
        }

        // Reset styles for all menu items
        private void ResetMenuItemStyles()
        {
            // For all menu items, reset their background to transparent
            List<UIElement> allMenuItems = new List<UIElement>
            {
                _projectListItem,
                _executeListItem,
                _reportInfoItem,
                _accConfigItem,
                _walletConfigItem,
                _emailConfigItem,
                _otherConfigItem,
                _proxyConfigItem
            };

            foreach (var item in allMenuItems)
            {
                if (item is Grid menuGrid)
                {
                    menuGrid.Background = new SolidColorBrush(Colors.Transparent);
                }
                else if (item is StackPanel menuPanel)
                {
                    menuPanel.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
        }

        // Set the style for the selected menu item
        private void SetSelectedMenuItem(UIElement menuItem)
        {
            if (menuItem is Grid grid)
            {
                grid.Background = new SolidColorBrush(Color.FromRgb(64, 74, 88)); // #404A58 暗蓝灰色
            }
            else if (menuItem is StackPanel panel)
            {
                panel.Background = new SolidColorBrush(Color.FromRgb(64, 74, 88)); // #404A58 暗蓝灰色
            }
        }

        // Hide all content panels
        private void HideAllContentPanels()
        {
            _projectListPanel.Visibility = Visibility.Collapsed;
            _executeListPanel.Visibility = Visibility.Collapsed;
            _reportInfoPanel.Visibility = Visibility.Collapsed;
            _groupConfigPanel.Visibility = Visibility.Collapsed;
            _walletConfigPanel.Visibility = Visibility.Collapsed;
            _emailConfigPanel.Visibility = Visibility.Collapsed;
            _otherConfigPanel.Visibility = Visibility.Collapsed;
            _proxyConfigPanel.Visibility = Visibility.Collapsed;
            
            // Show initial hint
            if (_initialHint != null)
                _initialHint.Visibility = Visibility.Visible;
        }

        // Update menu visibility based on expansion state
        private void UpdateMenuVisibility()
        {
            // LuMao submenu items
            _projectListItem.Visibility = _isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
            _executeListItem.Visibility = _isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
            _reportInfoItem.Visibility = _isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
            
            // Account config submenu items
            _accConfigItem.Visibility = _isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            _walletConfigItem.Visibility = _isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            _emailConfigItem.Visibility = _isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            _otherConfigItem.Visibility = _isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            _proxyConfigItem.Visibility = _isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            
            // Update expansion icons
            _luMaoExpandIcon.Text = _isLuMaoExpanded ? "▲" : "▼";
            _accountExpandIcon.Text = _isAccountExpanded ? "▲" : "▼";
        }
    }
} 
