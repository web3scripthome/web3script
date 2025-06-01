using web3script.ContractScript;
using web3script.Models;
using web3script.Services;
using web3script.ucontrols;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing; 
using MessageBox = System.Windows.MessageBox;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes; 
using web3script.Views;  
using System.IO;
using System.Windows.Navigation;
using System.Reflection;

namespace web3script
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // 记录当前菜单展开状态
        private bool isLuMaoExpanded = true; // 默认展开
        private bool isAccountExpanded = true; // 默认展开
        
        // 当前选中的菜单项
        private string currentSelectedMenu = "项目清单";
        
        // 用于记录所有菜单项的集合
        private UIElement[] menuItems;
        
        // 基础服务，可以传递给子控件
        private ProjectService projectService;
        private WalletService walletService;

        private TrayHelper _trayHelper;
        public MainWindow()
        {
            InitializeComponent();

          

            // 初始化基础服务
            projectService = new ProjectService();
            walletService = new WalletService();
            // 初始化菜单项集合
            menuItems = new UIElement[] { 
                projectListItem, 
                executeListItem,
                balanceInfoItem,
                reportInfoItem,
                accConfigItem,
                walletConfigItem,
                emailConfigItem,
                otherConfigItem,
                proxyConfigItem,
                proxySSRItem,
            };
            
             
            InitializeUI();
            LogService.ShowLogWindow();
            LogService.hideLogWindow();
            
            SetupUserControlCommunication();
            _trayHelper = new TrayHelper(this, "web3script");
    
        } 
        private void InitializeUI()
        {
            try
            {
                
                UpdateMenuVisibility();
                
               
                HideAllContentPanels();
                
                
                ResetMenuItemStyles();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化UI时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SetupUserControlCommunication()
        {
            
            if (GroupManagementPanel != null)
            {
                GroupManagementPanel.SetWalletService(walletService);
            }
            
            if (WalletManagementPanel != null)
            {
                WalletManagementPanel.SetWalletService(walletService);
            }
            
            if (ExecuteListPanel != null)
            {
                ExecuteListPanel.SetWalletService(walletService);
            }
            
            if (ProxyConfigPanel != null && ProxyConfigPanel.DataContext != null)
            {
                
            }
 

            if (ProjectListPanel != null)
            {
                ProjectListPanel.CreateTaskRequested += (groupId, groupName) =>
                {
                    ShowPanel("执行列表");
                    ExecuteListPanel.CreateNewTask(groupId, groupName);
                };
            }
        }
        
        
        private void ResetMenuItemStyles()
        {
            foreach (var item in menuItems)
            {
                if (item is Grid grid)
                {
                    grid.Background = Brushes.Transparent;
                }
                else if (item is StackPanel panel)
                {
                    panel.Background = Brushes.Transparent;
                }
            }
        }
        
       
        private void SetSelectedMenuItem(UIElement menuItem)
        {
            if (menuItem is Grid grid)
            {
                grid.Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // #808080 浅灰色
            }
            else if (menuItem is StackPanel panel)
            {
                panel.Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)); // #808080 浅灰色
            }
        }
        
       
        private void HideAllContentPanels()
        {
           // initialHint.Visibility = Visibility.Collapsed;
            ProjectListPanel.Visibility = Visibility.Collapsed;
            ExecuteListPanel.Visibility = Visibility.Collapsed;
            BalanceInfoPanel.Visibility = Visibility.Collapsed;
            ReportPanel.Visibility = Visibility.Collapsed;
            GroupManagementPanel.Visibility = Visibility.Collapsed;
            WalletManagementPanel.Visibility = Visibility.Collapsed;
            EmailConfigPanel.Visibility = Visibility.Collapsed;
            OtherConfigPanel.Visibility = Visibility.Collapsed;
            ProxyConfigPanel.Visibility = Visibility.Collapsed;
            ProxySSRConfigPanel.Visibility = Visibility.Collapsed;
        }
        
      
        private void UpdateMenuVisibility()
        {
           
            projectListItem.Visibility = isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
            executeListItem.Visibility = isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
            reportInfoItem.Visibility = isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
            balanceInfoItem.Visibility = isLuMaoExpanded ? Visibility.Visible : Visibility.Collapsed;
           
            accConfigItem.Visibility = isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            walletConfigItem.Visibility = isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            emailConfigItem.Visibility = isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            otherConfigItem.Visibility = isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            proxyConfigItem.Visibility = isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            proxySSRItem.Visibility = isAccountExpanded ? Visibility.Visible : Visibility.Collapsed;
            
            luMaoExpandIcon.Text = isLuMaoExpanded ? "▲" : "▼";
            accountExpandIcon.Text = isAccountExpanded ? "▲" : "▼";
        }
        
         
        private async void TopButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                string content = button.Content.ToString();
                switch (content)
                {
                    case "↻": 
                        break;
                   
                    case "⛶":
                        ToggleFullscreen();
                        break;
                }
            }
        }
        
      
        private void ToggleFullscreen()
        {
            if (WindowState == WindowState.Maximized && WindowStyle == WindowStyle.None)
            {
                // 恢复窗口
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.SingleBorderWindow;
            }
            else
            {
                // 设置全屏
                WindowState = WindowState.Maximized;
                WindowStyle = WindowStyle.None;
            }
        }
        
       
        private void LuMaoMenu_Click(object sender, RoutedEventArgs e)
        {
            isLuMaoExpanded = !isLuMaoExpanded;
            UpdateMenuVisibility();
        }
        
        
        private void AccountConfigMenu_Click(object sender, RoutedEventArgs e)
        {
            isAccountExpanded = !isAccountExpanded;
            UpdateMenuVisibility();
        }
        
        
        private void SubMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is UIElement menuItem && menuItem.GetType().GetProperty("Tag") != null)
            {
               
                string menuTag = menuItem.GetValue(FrameworkElement.TagProperty) as string;
                
                if (!string.IsNullOrEmpty(menuTag))
            {
                 
                    currentSelectedMenu = menuTag;
                
                
                ResetMenuItemStyles();
                
                     
                    SetSelectedMenuItem(menuItem);
                    
                    
                    ShowPanel(menuTag);
                }
            }
        }
         
        
        
        private void ShowPanel(string selectedMenuTag)
        {
            HideAllContentPanels();
            switch (selectedMenuTag)
                {
                    case "项目清单":
                    ProjectListPanel.Visibility = Visibility.Visible;
                        break;
                    case "执行列表":
                    ExecuteListPanel.Visibility = Visibility.Visible;
                        break;
                    case "报表信息": 
                    ReportPanel.Visibility = Visibility.Visible;
                        break;
                    case "钱包信息":
                    BalanceInfoPanel.Visibility = Visibility.Visible;
                    break;
                case "分组配置":
                    GroupManagementPanel.Visibility = Visibility.Visible;
                        break;
               case "钱包配置":
                    WalletManagementPanel.Visibility = Visibility.Visible;
                        break;
              case "邮箱配置":
                    EmailConfigPanel.Visibility = Visibility.Visible;
                        break;
                case "其他配置":
                    OtherConfigPanel.Visibility = Visibility.Visible;
                        break;
               case "代理配置":
                    ProxyConfigPanel.Visibility = Visibility.Visible;
                        break;
                case "代理工具":
                    ProxySSRConfigPanel.Visibility = Visibility.Visible;
                    break;
                default:
                   // initialHint.Visibility = Visibility.Visible;
                        break;
                }
            }
       
        
        
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!e.Cancel)
            {
                e.Cancel = true;
                this.Hide();
            }
        }


        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
    }
}
 