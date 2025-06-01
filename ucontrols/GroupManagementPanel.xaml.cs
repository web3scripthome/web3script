using web3script.Models;
using web3script.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace web3script.ucontrols
{
    /// <summary>
    /// GroupManagementPanel.xaml 的交互逻辑
    /// </summary>
    public partial class GroupManagementPanel : UserControl
    {
        // 钱包服务
        private WalletService walletService;
        
        // 当前选中的钱包分组
        private WalletGroup selectedWalletGroup;

        public GroupManagementPanel()
        {
            InitializeComponent();
            
            // 不再在这里初始化钱包服务，而是通过SetWalletService方法从MainWindow获取
            // walletService = new WalletService();
            
            // 初始化UI会在SetWalletService中调用
        }
        
        // 设置钱包服务实例（从MainWindow传入）
        public void SetWalletService(WalletService service)
        {
            // 如果已有服务，先取消订阅事件
            if (walletService != null)
            {
                walletService.WalletGroupsChanged -= WalletService_WalletGroupsChanged;
            }
            
            walletService = service;
            
            // 订阅钱包分组变更事件
            walletService.WalletGroupsChanged += WalletService_WalletGroupsChanged;
            
            // 重新加载钱包分组
            LoadWalletGroups();
        }
        
        // 当钱包分组变更时触发的事件处理
        private void WalletService_WalletGroupsChanged(object sender, EventArgs e)
        {
            // 在UI线程上更新界面
            this.Dispatcher.Invoke(() =>
            {
                LoadWalletGroups();
            });
        }
        
        private void InitializeUI()
        {
            try
            {
                // 加载钱包分组
                LoadWalletGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化UI时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 加载钱包分组
        private void LoadWalletGroups()
        {
            groupListBox.Items.Clear();
            
            foreach (var group in walletService.WalletGroups)
            {
                var item = new ListBoxItem
                {
                    Content = group.Name,
                    Tag = group.Id
                };
                groupListBox.Items.Add(item);
            }
            
            if (groupListBox.Items.Count > 0)
            {
                groupListBox.SelectedIndex = 0;
            }
            else
            {
                // 如果没有分组，清空详情
                groupNameTextBox.Text = "";
                groupMembersDataGrid.ItemsSource = null;
            }
        }
        
        // 添加分组按钮点击事件
        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            // 创建输入对话框
            var dialog = new InputDialog("添加分组", "请输入分组名称:", "新分组");
            
            if (dialog.ShowDialog() == true)
            {
                string groupName = dialog.InputText;
                if (!string.IsNullOrWhiteSpace(groupName))
                {
 // 检查是否已存在相同名称的分组
                    if (walletService.WalletGroups.Any(g => g.Name == groupName))
                    {
                        MessageBox.Show("已存在相同名称的分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                   
                    // 添加分组
                    var group = walletService.AddGroup(groupName);
                    
                    // 刷新分组列表
                    LoadWalletGroups();
                    
                    // 选中新添加的分组
                    for (int i = 0; i < groupListBox.Items.Count; i++)
                    {
                        var item = groupListBox.Items[i] as ListBoxItem;
                        if (item != null && item.Tag.ToString() == group.Id)
                        {
                            groupListBox.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
        
        // 分组列表选择变更事件
        private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = groupListBox.SelectedItem as ListBoxItem;
            if (selectedItem != null && selectedItem.Tag != null)
            {
                string groupId = selectedItem.Tag.ToString();
                selectedWalletGroup = walletService.WalletGroups.FirstOrDefault(g => g.Id == groupId);
                
                if (selectedWalletGroup != null)
                {
                    // 更新分组名称
                    groupNameTextBox.Text = selectedWalletGroup.Name;
                    
                    // 加载分组成员
                    var groupMembers = walletService.GetWalletsInGroup(groupId);
                    
                    // 重置选择状态
                    foreach (var wallet in groupMembers)
                    {
                        wallet.IsSelected = false;
                    }
                    
                    // 重置全选复选框
                    selectAllCheckBox.IsChecked = false;
                    
                    // 清空并重新设置数据源
                    groupMembersDataGrid.ItemsSource = null;
                    groupMembersDataGrid.Items.Clear();
                    groupMembersDataGrid.ItemsSource = groupMembers;
                    
                    // 刷新DataGrid
                    groupMembersDataGrid.Items.Refresh();
                }
            }
            else
            {
                // 如果没有选中项，清空详情
                selectedWalletGroup = null;
                groupNameTextBox.Text = "";
                groupMembersDataGrid.ItemsSource = null;
                groupMembersDataGrid.Items.Clear();
            }
        }
        
        // 保存分组按钮点击事件
        private void SaveGroup_Click(object sender, RoutedEventArgs e)
        {
            if (selectedWalletGroup != null)
            {
                // 更新分组名称
                selectedWalletGroup.Name = groupNameTextBox.Text;
                
                // 保存分组数据
                walletService.SaveWalletGroups();
                
                // 刷新分组列表
                LoadWalletGroups();
                
                MessageBox.Show("分组保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // 删除分组按钮点击事件
        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = groupListBox.SelectedItem as ListBoxItem;
            if (selectedItem == null || selectedItem.Tag == null)
            {
                MessageBox.Show("请先选择要删除的分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string groupId = selectedItem.Tag.ToString();
            string groupName = selectedItem.Content.ToString();
            
            // 确认删除
            MessageBoxResult result = MessageBox.Show(
                $"确定要删除分组 \"{groupName}\" 吗？分组中的钱包将不会被删除，但会从该分组中移除。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                // 删除分组
                walletService.DeleteGroup(groupId);
                
                // 刷新分组列表
                LoadWalletGroups();
                
                MessageBox.Show($"分组 \"{groupName}\" 已删除", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // 从分组中移除钱包按钮点击事件
        private void RemoveWalletsFromGroup_Click(object sender, RoutedEventArgs e)
        {
            if (selectedWalletGroup == null)
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var walletsInGroup = groupMembersDataGrid.ItemsSource as List<Wallet>;
            if (walletsInGroup == null || walletsInGroup.Count == 0)
            {
                MessageBox.Show("当前分组没有钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 获取选中的钱包
            var selectedWallets = walletsInGroup.Where(w => w.IsSelected).ToList();
            if (selectedWallets.Count == 0)
            {
                MessageBox.Show("请先选择要移除的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 确认移除
            MessageBoxResult result = MessageBox.Show(
                $"确定要从 \"{selectedWalletGroup.Name}\" 分组中移除 {selectedWallets.Count} 个钱包吗？",
                "确认移除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                // 获取选中的钱包ID列表
                var walletIds = selectedWallets.Select(w => w.Id).ToList();
                
                // 从分组中移除钱包
                walletService.RemoveWalletsFromGroup(selectedWalletGroup.Id, walletIds);
                
                // 刷新分组成员列表
                var updatedGroupMembers = walletService.GetWalletsInGroup(selectedWalletGroup.Id);
                
                // 清空并重新设置数据源
                groupMembersDataGrid.ItemsSource = null;
                groupMembersDataGrid.Items.Clear();
                groupMembersDataGrid.ItemsSource = updatedGroupMembers;
                
                // 刷新DataGrid
                groupMembersDataGrid.Items.Refresh();
                
                // 重置全选复选框
                selectAllCheckBox.IsChecked = false;
                
                MessageBox.Show($"已从 \"{selectedWalletGroup.Name}\" 分组中移除 {selectedWallets.Count} 个钱包", 
                    "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // 全选复选框选中事件
        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var walletsInGroup = groupMembersDataGrid.ItemsSource as List<Wallet>;
            if (walletsInGroup != null)
            {
                foreach (var wallet in walletsInGroup)
                {
                    wallet.IsSelected = true;
                }
                
                // 刷新DataGrid
                groupMembersDataGrid.Items.Refresh();
            }
        }
        
        // 全选复选框取消选中事件
        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var walletsInGroup = groupMembersDataGrid.ItemsSource as List<Wallet>;
            if (walletsInGroup != null)
            {
                foreach (var wallet in walletsInGroup)
                {
                    wallet.IsSelected = false;
                }
                
                // 刷新DataGrid
                groupMembersDataGrid.Items.Refresh();
            }
        }
        
        // 上下文菜单-从分组中移除
        private void ContextMenu_RemoveFromGroup_Click(object sender, RoutedEventArgs e)
        {
            if (selectedWalletGroup == null)
                return;
                
            // 获取右键选中的钱包
            var selectedWallet = groupMembersDataGrid.SelectedItem as Wallet;
            if (selectedWallet == null)
                return;
                
            // 确认移除
            MessageBoxResult result = MessageBox.Show(
                $"确定要从 \"{selectedWalletGroup.Name}\" 分组中移除钱包 \"{selectedWallet.Address}\" 吗？",
                "确认移除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                // 从分组中移除钱包
                walletService.RemoveWalletsFromGroup(selectedWalletGroup.Id, new List<string> { selectedWallet.Id });
                
                // 刷新分组成员列表
                var updatedGroupMembers = walletService.GetWalletsInGroup(selectedWalletGroup.Id);
                
                // 清空并重新设置数据源
                groupMembersDataGrid.ItemsSource = null;
                groupMembersDataGrid.Items.Clear();
                groupMembersDataGrid.ItemsSource = updatedGroupMembers;
                
                // 刷新DataGrid
                groupMembersDataGrid.Items.Refresh();
                
                MessageBox.Show($"已从 \"{selectedWalletGroup.Name}\" 分组中移除钱包", 
                    "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // 输入对话框
        public class InputDialog : Window
        {
            private TextBox textBox;
            
            public string InputText 
            { 
                get { return textBox.Text; }
            }
            
            public InputDialog(string title, string promptText, string defaultValue = "")
            {
                Title = title;
                Width = 350;
                Height = 150;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ResizeMode = ResizeMode.NoResize;
                
                Grid grid = new Grid();
                grid.Margin = new Thickness(10);
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // 提示文本
                TextBlock prompt = new TextBlock
                {
                    Text = promptText,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                Grid.SetRow(prompt, 0);
                
                // 输入框
                textBox = new TextBox
                {
                    Text = defaultValue,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(textBox, 1);
                
                // 按钮区域
                StackPanel buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                
                Button cancelButton = new Button
                {
                    Content = "取消",
                    Width = 60,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                cancelButton.Click += (s, e) => DialogResult = false;
                
                Button okButton = new Button
                {
                    Content = "确定",
                    Width = 60,
                    Margin = new Thickness(5, 0, 0, 0)
                };
                okButton.Click += (s, e) => DialogResult = true;
                
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(okButton);
                Grid.SetRow(buttonPanel, 2);
                
                grid.Children.Add(prompt);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);
                
                Content = grid;
            }
        }
    }
} 