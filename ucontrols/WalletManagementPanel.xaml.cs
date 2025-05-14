using web3script.Models;
using web3script.Services;
using Nethereum.HdWallet;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wallet = web3script.Models.Wallet;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Concurrent;
using Task = System.Threading.Tasks.Task;
using System.Diagnostics;
using NBitcoin;
using System.Windows.Data;

namespace web3script.ucontrols
{
    // 字符串到可见性转换器
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 缩进边距转换器
    public class IndentMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int indentLevel)
            {
                int leftMargin = indentLevel * 20; // 每级缩进20像素
                return new Thickness(leftMargin, 0, 0, 0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Boolean到FontWeight转换器
    public class BoolToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isGroupHeader && isGroupHeader)
                return FontWeights.Bold;
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Boolean到Visibility的反转转换器
    public class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool invert = parameter != null && parameter.ToString().ToLower() == "inverse";
                if (invert)
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                else
                    return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// WalletManagementPanel.xaml 的交互逻辑
    /// </summary>
    public partial class WalletManagementPanel : UserControl
    {
        // 钱包服务
        private WalletService walletService;
        
        // 当前选中的钱包分组
        private WalletGroup selectedWalletGroup;
        
        // 钱包分页相关属性
        private int currentPage = 1;
        private int pageSize = 100; // 每页显示100条
        private int totalPages = 1;
        private string walletSearchText = "";
        
        // 保存所有选中的钱包ID列表（用于跨页面选择）
        private HashSet<string> selectedWalletIds = new HashSet<string>();

        public WalletManagementPanel()
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
            
            // 更新分组下拉列表和钱包列表
            UpdateGroupSelectionComboBox();
            UpdateFilterGroupComboBox(); // 添加初始化筛选下拉框
            LoadWallets();
        }
        
        // 当钱包分组变更时触发的事件处理
        private void WalletService_WalletGroupsChanged(object sender, EventArgs e)
        {
            // 在UI线程上更新界面
            this.Dispatcher.Invoke(() =>
            {
                UpdateGroupSelectionComboBox();
                UpdateFilterGroupComboBox(); // 更新筛选下拉框
                LoadWallets();
            });
        }
        
        private void InitializeUI()
        {
            try
            {
                // 初始化分页大小下拉框
                InitializePageSizeComboBox();
                
                // 确保pageButtonsPanel已初始化
                if (pageButtonsPanel != null)
                {
                    pageButtonsPanel.ItemsSource = new List<PageButtonModel>();
                }
                
                // 更新分组下拉列表
                UpdateGroupSelectionComboBox();
                
                // 加载钱包列表
                LoadWallets();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化UI时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 初始化分页大小下拉框
        private void InitializePageSizeComboBox()
        {
            // 根据初始pageSize设置默认选中项
            switch (pageSize)
            {
                case 100:
                    pageSizeComboBox.SelectedIndex = 0;
                    break;
                case 200:
                    pageSizeComboBox.SelectedIndex = 1;
                    break;
                case 500:
                    pageSizeComboBox.SelectedIndex = 2;
                    break;
                case 1000:
                    pageSizeComboBox.SelectedIndex = 3;
                    break;
                default:
                    // 默认每页100条
                    pageSize = 1000;
                    pageSizeComboBox.SelectedIndex = 2;
                    break;
            }
        }
        
        // 更新分组下拉列表
        private void UpdateGroupSelectionComboBox()
        {
            try
            {
                var groups = walletService.WalletGroups;
                
                // 清空当前列表
                groupSelectionComboBox.Items.Clear();
                
                // 添加所有分组，并设置Tag为分组ID
                foreach (var group in groups)
                {
                    var item = new ComboBoxItem
                    {
                        Content = group.Name,
                        Tag = group.Id
                    };
                    groupSelectionComboBox.Items.Add(item);
                }
                
                // 如果没有分组，添加一个提示
                if (groups.Count == 0)
                {
                    var item = new ComboBoxItem
                    {
                        Content = "请先创建分组",
                        IsEnabled = false
                    };
                    groupSelectionComboBox.Items.Add(item);
                }
                
                // 默认选中第一项
                if (groupSelectionComboBox.Items.Count > 0)
                {
                    groupSelectionComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载钱包分组时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 初始化分组筛选下拉框
        private void UpdateFilterGroupComboBox()
        {
            if (filterGroupComboBox == null || walletService == null)
                return;
            
            // 保存当前选中项的Tag值
            string currentSelectedTag = null;
            if (filterGroupComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                currentSelectedTag = selectedItem.Tag?.ToString();
            }
            
            // 清空现有项，保留"显示所有"项
            filterGroupComboBox.Items.Clear();
            filterGroupComboBox.Items.Add(new ComboBoxItem { Content = "显示所有钱包", Tag = "all" });
            
            // 添加"显示未分组"选项
            filterGroupComboBox.Items.Add(new ComboBoxItem { Content = "显示未分组钱包", Tag = "unassigned" });
            
            // 添加所有分组
            foreach (var group in walletService.WalletGroups)
            {
                filterGroupComboBox.Items.Add(new ComboBoxItem 
                { 
                    Content = $"{group.Name} ({group.WalletIds.Count})", 
                    Tag = group.Id 
                });
            }
            
            // 尝试恢复之前的选择
            if (!string.IsNullOrEmpty(currentSelectedTag))
            {
                foreach (ComboBoxItem item in filterGroupComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentSelectedTag)
                    {
                        filterGroupComboBox.SelectedItem = item;
                        return;
                    }
                }
            }
            
            // 如果没有找到或没有之前的选择，默认选择"显示所有"
            filterGroupComboBox.SelectedIndex = 0;
        }
        
        // 处理分组筛选变更事件
        private void FilterGroup_Changed(object sender, SelectionChangedEventArgs e)
        {
            // 如果选择了分组，则根据分组筛选钱包
            LoadWallets();
        }
        
        // 加载钱包列表
        private void LoadWallets()
        {
            // 检查walletService是否已初始化
            if (walletService == null)
            {
                return;
            }
            
            // 获取所有钱包
            var wallets = walletService.Wallets;
            
            // 应用分组筛选
            if (filterGroupComboBox.SelectedItem is ComboBoxItem selectedFilterItem && 
                selectedFilterItem.Tag != null)
            {
                string selectedTag = selectedFilterItem.Tag.ToString();
                
                if (selectedTag == "unassigned")
                {
                    // 查找所有没有被分配到任何分组的钱包
                    var allGroupWalletIds = new HashSet<string>();
                    foreach (var group in walletService.WalletGroups)
                    {
                        foreach (var walletId in group.WalletIds)
                        {
                            allGroupWalletIds.Add(walletId);
                        }
                    }
                    
                    // 过滤出未分组的钱包
                    wallets = wallets.Where(w => !allGroupWalletIds.Contains(w.Id)).ToList();
                }
                else if (selectedTag != "all")
                {
                    // 根据特定分组筛选钱包
                    string groupId = selectedTag;
                    var group = walletService.WalletGroups.FirstOrDefault(g => g.Id == groupId);
                    if (group != null)
                    {
                        wallets = wallets.Where(w => group.WalletIds.Contains(w.Id)).ToList();
                    }
                }
            }
            
            // 应用搜索过滤
            if (!string.IsNullOrWhiteSpace(walletSearchText))
            {
                wallets = wallets.Where(w => 
                    w.Address.IndexOf(walletSearchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            
            // 计算总页数
            totalPages = (int)Math.Ceiling(wallets.Count / (double)pageSize);
            if (totalPages == 0) totalPages = 1; // 至少有一页
            
            // 确保当前页在有效范围内
            if (currentPage > totalPages)
            {
                currentPage = totalPages;
            }
            
            // 分页获取当前页的数据
            int skip = (currentPage - 1) * pageSize;
            var currentPageWallets = pageSize == int.MaxValue 
                ? wallets.ToList() // 显示全部
                : wallets.Skip(skip).Take(pageSize).ToList(); // 分页显示
            
            // 转换为视图模型
            var walletViewModels = new List<WalletViewModel>();
            for (int i = 0; i < currentPageWallets.Count; i++)
            {
                var wallet = currentPageWallets[i];
                
                // 获取钱包所属的分组名称
                var groupNames = GetWalletGroupNames(wallet.Id);
                
                // 创建视图模型
                walletViewModels.Add(new WalletViewModel
                {
                    Id = wallet.Id,
                    Address = wallet.Address,
                    Mnemonic = wallet.Mnemonic,
                    PrivateKey = wallet.PrivateKey,
                    Remark = wallet.Remark,
                    IsSelected = selectedWalletIds.Contains(wallet.Id),
                    RowIndex = skip + i + 1, // 从1开始计数
                    Groups = groupNames
                });
            }
            
            // 设置列表数据源
            walletListView.ItemsSource = walletViewModels;
            
            // 更新"全选"复选框状态
            selectAllWallets.IsChecked = walletViewModels.Count > 0 && walletViewModels.All(w => w.IsSelected);
            
            // 更新总记录数显示
            totalRecordsText.Text = wallets.Count.ToString();
            
            // 更新分页控件
            UpdatePaginationControls();
            
            // 显示分页控件（在按分组显示时会隐藏）
            if (pageSize == int.MaxValue)
            {
                // 如果是"显示全部"，隐藏分页控件
                pageButtonsPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                pageButtonsPanel.Visibility = Visibility.Visible;
            }
        }
        
        // 页码数据模型
        public class PageButtonModel : INotifyPropertyChanged
        {
            private int _pageNumber;
            private bool _isCurrentPage;
            
            public int PageNumber 
            { 
                get { return _pageNumber; }
                set
                {
                    if (_pageNumber != value)
                    {
                        _pageNumber = value;
                        OnPropertyChanged("PageNumber");
                    }
                }
            }
            
            public bool IsCurrentPage 
            { 
                get { return _isCurrentPage; }
                set
                {
                    if (_isCurrentPage != value)
                    {
                        _isCurrentPage = value;
                        OnPropertyChanged("IsCurrentPage");
                    }
                }
            }
            
            public event PropertyChangedEventHandler PropertyChanged;
            
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            
            public override string ToString()
            {
                return PageNumber.ToString();
            }
        }
        
        // 更新分页按钮
        private void UpdatePaginationControls()
        {
            // 检查walletService是否已初始化
            if (walletService == null)
            {
                return;
            }
            
            // 计算总页数
            int totalRecords = walletService.Wallets.Count;
            int filteredCount = totalRecords;
            
            // 如果有搜索条件，计算过滤后的总数
            if (!string.IsNullOrWhiteSpace(walletSearchText))
            {
                filteredCount = walletService.Wallets.Count(w => 
                    w.Address.IndexOf(walletSearchText, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            
            totalPages = (int)Math.Ceiling(filteredCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1; // 至少有一页
            
            // 更新总记录数显示
            totalRecordsText.Text = filteredCount.ToString();
            
            // 确保当前页在有效范围内
            if (currentPage > totalPages)
            {
                currentPage = totalPages;
            }
            else if (currentPage < 1)
            {
                currentPage = 1;
            }
            
            // 生成页码按钮
            GeneratePageButtons();
            
            // 更新文本框中的页码
            pageNumberTextBox.Text = currentPage.ToString();
            
          
           
        }
        
        // 生成页码按钮
        private void GeneratePageButtons()
        {
            // 清空现有按钮
            List<PageButtonModel> pageButtons = new List<PageButtonModel>();
            
            // 最多显示5个页码按钮
            int maxVisibleButtons = 5;
            int startPage = Math.Max(1, currentPage - maxVisibleButtons / 2);
            int endPage = Math.Min(totalPages, startPage + maxVisibleButtons - 1);
            
            // 调整起始页，确保显示足够的按钮
            if (endPage - startPage + 1 < maxVisibleButtons && startPage > 1)
            {
                startPage = Math.Max(1, endPage - maxVisibleButtons + 1);
            }
            
            // 生成页码按钮
            for (int i = startPage; i <= endPage; i++)
            {
                pageButtons.Add(new PageButtonModel 
                { 
                    PageNumber = i, 
                    IsCurrentPage = i == currentPage 
                });
            }
            
            // 设置页码按钮列表
            pageButtonsPanel.ItemsSource = pageButtons;
        }
        
        // 页码按钮点击事件
        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag is PageButtonModel model)
            {
                currentPage = model.PageNumber;
                LoadWallets();
            }
        }
        
        // 首页按钮点击事件
        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage != 1)
            {
                currentPage = 1;
                LoadWallets();
            }
        }
        
        // 末页按钮点击事件
        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage != totalPages)
            {
                currentPage = totalPages;
                LoadWallets();
            }
        }
        
        // 每页显示数量变更事件
        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem selectedItem = pageSizeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string content = selectedItem.Content.ToString();
                
                // 处理"显示全部"选项
                if (content == "显示全部")
                {
                    pageSize = int.MaxValue; // 使用一个足够大的值
                }
                else if (int.TryParse(content, out int newPageSize))
                {
                    pageSize = newPageSize;
                }
                
                currentPage = 1; // 重置到第一页
                LoadWallets();
            }
        }
        
        // 验证输入的页码是否为数字
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
        
        // 获取钱包所属的分组名称列表
        private List<string> GetWalletGroupNames(string walletId)
        {
            var groupNames = new List<string>();
            
            foreach (var group in walletService.WalletGroups)
            {
                if (group.WalletIds.Contains(walletId))
                {
                    groupNames.Add(group.Name);
                }
            }
            
            return groupNames;
        }
        
        // 搜索钱包按钮点击事件
        private void SearchWallet_Click(object sender, RoutedEventArgs e)
        {
            walletSearchText = searchWalletTextBox.Text.Trim();
            currentPage = 1; // 重置到第一页
            LoadWallets();
        }
        
        // 清空搜索按钮点击事件
        private void ClearWalletSearch_Click(object sender, RoutedEventArgs e)
        {
            searchWalletTextBox.Text = "";
            walletSearchText = "";
            currentPage = 1; // 重置到第一页
            LoadWallets();
        }
        
        // 钱包视图模型
        public class WalletViewModel
        {
            public string Id { get; set; }
            public string Address { get; set; }
            public string Mnemonic { get; set; }
            public string PrivateKey { get; set; }
            public string Remark { get; set; }
            public bool IsSelected { get; set; }
            public int RowIndex { get; set; }
            public List<string> Groups { get; set; } = new List<string>();
            public bool IsGroupHeader { get; set; } = false;
            public string GroupId { get; set; } = string.Empty;
            public int IndentLevel { get; set; } = 0;
        }
        
        // 导入钱包对话框
        public class ImportWalletDialog : Window
        {
            private TextBox walletDataTextBox;
            private TextBox filePathTextBox;
            private ProgressBar progressBar;
            private TextBlock statusTextBlock;
            private Button importButton;
            private Button cancelButton;
            
            public string WalletData
            {
                get { return walletDataTextBox.Text; }
            }
            
            // 任务取消标志
            private CancellationTokenSource cancellationTokenSource;
            
            // 导入结果
            public int ImportedCount { get; private set; }
            public int FailedCount { get; private set; }
            public int DuplicateCount { get; private set; }
            public List<(string Input, string Address, bool IsMnemonic)> ValidWallets { get; private set; }
            
            public ImportWalletDialog()
            {
                Title = "导入钱包";
                Width = 500;
                Height = 500; // 增加高度以容纳进度条
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                
                Grid mainGrid = new Grid
                {
                    Margin = new Thickness(20)
                };
                
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 进度条
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 状态文本
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 按钮区域
                
                // 顶部指导文本
                TextBlock instructionText = new TextBlock
                {
                    Text = "请在下方输入钱包数据（每行一个钱包，支持助记词和私钥）",
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(instructionText, 0);
                
                // 文件选择区域
                Grid fileSelectGrid = new Grid
                {
                    Margin = new Thickness(0, 0, 0, 10)
                };
                fileSelectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                fileSelectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fileSelectGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                TextBlock fileLabel = new TextBlock
                {
                    Text = "从文件导入:",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(fileLabel, 0);
                
                filePathTextBox = new TextBox
                {
                    IsReadOnly = true,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(filePathTextBox, 1);
                
                Button browseButton = new Button
                {
                    Content = "浏览...",
                    Padding = new Thickness(10, 3, 10, 3)
                };
                browseButton.Click += BrowseButton_Click;
                Grid.SetColumn(browseButton, 2);
                
                fileSelectGrid.Children.Add(fileLabel);
                fileSelectGrid.Children.Add(filePathTextBox);
                fileSelectGrid.Children.Add(browseButton);
                Grid.SetRow(fileSelectGrid, 1);
                
                // 编辑区域标签
                TextBlock editAreaLabel = new TextBlock
                {
                    Text = "或直接粘贴钱包数据:",
                    Margin = new Thickness(0, 0, 0, 5)
                };
                Grid.SetRow(editAreaLabel, 2);
                
                // 钱包数据输入框
                walletDataTextBox = new TextBox
                {
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(walletDataTextBox, 3);
                
                // 进度条
                progressBar = new ProgressBar
                {
                    Height = 20,
                    Margin = new Thickness(0, 0, 0, 5),
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(progressBar, 4);
                
                // 状态文本
                statusTextBlock = new TextBlock
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = Visibility.Collapsed
                };
                Grid.SetRow(statusTextBlock, 5);
                
                // 底部按钮区域
                Grid buttonGrid = new Grid();
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                
                cancelButton = new Button
                {
                    Content = "取消",
                    Padding = new Thickness(20, 5, 20, 5),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                cancelButton.Click += (s, e) => 
                {
                    // 如果正在处理，则取消任务
                    if (cancellationTokenSource != null && !cancellationTokenSource.IsCancellationRequested)
                    {
                        cancellationTokenSource.Cancel();
                        statusTextBlock.Text = "正在取消操作...";
                        return;
                    }
                    
                    DialogResult = false;
                };
                Grid.SetColumn(cancelButton, 1);
                
                importButton = new Button
                {
                    Content = "导入",
                    Padding = new Thickness(20, 5, 20, 5),
                    Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    Foreground = Brushes.White
                };
                importButton.Click += ImportButton_Click;
                Grid.SetColumn(importButton, 2);
                
                buttonGrid.Children.Add(cancelButton);
                buttonGrid.Children.Add(importButton);
                Grid.SetRow(buttonGrid, 6);
                
                // 添加所有元素到主网格
                mainGrid.Children.Add(instructionText);
                mainGrid.Children.Add(fileSelectGrid);
                mainGrid.Children.Add(editAreaLabel);
                mainGrid.Children.Add(walletDataTextBox);
                mainGrid.Children.Add(progressBar);
                mainGrid.Children.Add(statusTextBlock);
                mainGrid.Children.Add(buttonGrid);
                
                Content = mainGrid;
                
                // 初始化取消令牌
                cancellationTokenSource = new CancellationTokenSource();
                
                // 初始化结果
                ValidWallets = new List<(string Input, string Address, bool IsMnemonic)>();
            }
            
            private void BrowseButton_Click(object sender, RoutedEventArgs e)
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    Title = "选择钱包文件"
                };
                
                if (openFileDialog.ShowDialog() == true)
                {
                    filePathTextBox.Text = openFileDialog.FileName;
                    try
                    {
                        // 读取文件内容并显示在文本框中
                        string fileContent = System.IO.File.ReadAllText(openFileDialog.FileName);
                        walletDataTextBox.Text = fileContent;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"读取文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            
            private async void ImportButton_Click(object sender, RoutedEventArgs e)
            {
                string walletData = walletDataTextBox.Text;
                if (string.IsNullOrWhiteSpace(walletData))
                {
                    MessageBox.Show("请输入钱包数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 显示进度条和状态
                importButton.IsEnabled = false;
                progressBar.Visibility = Visibility.Visible;
                statusTextBlock.Visibility = Visibility.Visible;
                statusTextBlock.Text = "正在验证钱包数据...";
                
                // 重置结果计数
                ImportedCount = 0;
                FailedCount = 0;
                DuplicateCount = 0;
                ValidWallets.Clear();
                
                try
                {
                    // 重置取消令牌
                    if (cancellationTokenSource != null)
                    {
                        cancellationTokenSource.Dispose();
                    }
                    cancellationTokenSource = new CancellationTokenSource();
                    
                    // 获取所有行
                    string[] lines = walletData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int totalLines = lines.Length;
                    progressBar.Maximum = totalLines;
                    
                    // 异步验证所有行
                    await Task.Run(() => ValidateWallets(lines, cancellationTokenSource.Token));
                    
                    // 如果取消了操作
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        statusTextBlock.Text = "操作已取消";
                        progressBar.Value = 0;
                        importButton.IsEnabled = true;
                        cancelButton.Content = "关闭";
                        return;
                    }
                    
                    // 处理成功，关闭对话框
                    DialogResult = true;
                }
                catch (Exception ex)
                {
                    statusTextBlock.Text = $"处理钱包数据时出错: {ex.Message}";
                    MessageBox.Show($"处理钱包数据时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    importButton.IsEnabled = true;
                }
            }

            private void ValidateWallets(string[] lines, CancellationToken cancellationToken)
            {
                int processedCount = 0;
                int localFailedCount = 0;

                foreach (var rawLine in lines)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // 每行作为一个钱包数据处理
                    ProcessWalletData(line, ref localFailedCount);

                    processedCount++;
                    UpdateProgress(processedCount, lines.Length);
                }

                FailedCount = localFailedCount;
            }

            // 处理单个钱包数据
            private void ProcessWalletData(string walletData, ref int failedCount)
            {
                try
                {
                    Debug.WriteLine($"开始处理钱包数据: {walletData.Substring(0, Math.Min(50, walletData.Length))}...");
                    
                    // 尝试从格式化文本中提取钱包信息
                    var (privateKey, mnemonic, address) = ExtractWalletInfo(walletData);
                    
                    Debug.WriteLine($"提取的信息 - 地址: {address?.Substring(0, Math.Min(10, address?.Length ?? 0))}..., " +
                                    $"私钥: {(privateKey != null ? "有" : "无")}, " +
                                    $"助记词: {(mnemonic != null ? "有" : "无")}");
                    
                    bool walletAdded = false;
                    
                    // 如果直接找到了地址和私钥或助记词，则直接添加
                    if (!string.IsNullOrEmpty(address) && (!string.IsNullOrEmpty(privateKey) || !string.IsNullOrEmpty(mnemonic)))
                    {
                        Debug.WriteLine("地址和凭证都存在，直接添加");
                        // 优先使用助记词
                        if (!string.IsNullOrEmpty(mnemonic))
                        {
                            lock (ValidWallets)
                            {
                                ValidWallets.Add((mnemonic, address, true));
                                Debug.WriteLine($"添加成功: 使用助记词和地址 {address?.Substring(0, Math.Min(10, address?.Length ?? 0))}...");
                            }
                            walletAdded = true;
                        }
                        else if (!string.IsNullOrEmpty(privateKey))
                        {
                            lock (ValidWallets)
                            {
                                ValidWallets.Add((privateKey, address, false));
                                Debug.WriteLine($"添加成功: 使用私钥和地址 {address?.Substring(0, Math.Min(10, address?.Length ?? 0))}...");
                            }
                            walletAdded = true;
                        }
                    }
                    
                    // 如果没有直接添加，则尝试验证助记词或私钥
                    if (!walletAdded)
                    {
                        // 首先尝试验证助记词，但即使验证失败也继续尝试私钥
                        bool mnemonicSuccess = false;
                        if (!string.IsNullOrEmpty(mnemonic))
                        {
                            try
                            {
                                Debug.WriteLine($"尝试验证助记词: {mnemonic.Substring(0, Math.Min(20, mnemonic.Length))}...");
                                var wallet = new Nethereum.HdWallet.Wallet(mnemonic, null);
                                var generatedAddress = wallet.GetAccount(0).Address;
                                lock (ValidWallets)
                                {
                                    ValidWallets.Add((mnemonic, generatedAddress, true));
                                    Debug.WriteLine($"助记词验证成功，生成地址: {generatedAddress.Substring(0, Math.Min(10, generatedAddress.Length))}...");
                                }
                                mnemonicSuccess = true;
                                walletAdded = true;
                            }
                            catch (Exception ex)
                            {
                                // 助记词无效，继续尝试私钥
                                Debug.WriteLine($"助记词验证失败: {ex.Message}");
                            }
                        }
                        
                        // 如果助记词失败或未提供，尝试私钥
                        if (!mnemonicSuccess && !string.IsNullOrEmpty(privateKey))
                        {
                            try
                            {
                                Debug.WriteLine($"尝试验证私钥: {privateKey.Substring(0, Math.Min(10, privateKey.Length))}...");
                                // 确保私钥格式正确
                                if (privateKey.StartsWith("0x"))
                                {
                                    privateKey = privateKey.Substring(2);
                                }
                                
                                // 尝试创建账户
                                var account = new Nethereum.Web3.Accounts.Account(privateKey);
                                var generatedAddress = account.Address;
                                
                                lock (ValidWallets)
                                {
                                    // 存储时保留0x前缀
                                    ValidWallets.Add(("0x" + privateKey, generatedAddress, false));
                                    Debug.WriteLine($"私钥验证成功，生成地址: {generatedAddress.Substring(0, Math.Min(10, generatedAddress.Length))}...");
                                }
                                walletAdded = true;
                            }
                            catch (Exception ex)
                            {
                                // 输出错误信息以便调试
                                Debug.WriteLine($"私钥验证失败: {ex.Message}, 私钥: {privateKey.Substring(0, Math.Min(10, privateKey.Length))}...");
                            }
                        }
                    }
                    
                    // 如果上述方法都失败，尝试将整个文本作为助记词或私钥
                    if (!walletAdded)
                    {
                        Debug.WriteLine("尝试将原始文本作为凭证处理");
                        string rawData = walletData.Replace("\r", " ").Replace("\n", " ").Trim();
                        
                        // 首先尝试作为助记词
                        bool rawMnemonicSuccess = false;
                        if (IsValidMnemonic(rawData))
                        {
                            try
                            {
                                Debug.WriteLine($"尝试将原始文本作为助记词: {rawData.Substring(0, Math.Min(20, rawData.Length))}...");
                                var wallet = new Nethereum.HdWallet.Wallet(rawData, null);
                                var generatedAddress = wallet.GetAccount(0).Address;
                                lock (ValidWallets)
                                {
                                    ValidWallets.Add((rawData, generatedAddress, true));
                                    Debug.WriteLine($"原始文本作为助记词验证成功，生成地址: {generatedAddress.Substring(0, Math.Min(10, generatedAddress.Length))}...");
                                }
                                rawMnemonicSuccess = true;
                                walletAdded = true;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"原始文本作为助记词验证失败: {ex.Message}");
                            }
                        }
                        
                        // 然后尝试作为私钥
                        if (!rawMnemonicSuccess && IsValidPrivateKey(rawData))
                        {
                            try
                            {
                                Debug.WriteLine($"尝试将原始文本作为私钥: {rawData.Substring(0, Math.Min(10, rawData.Length))}...");
                                // 确保私钥格式正确
                                string cleanPrivateKey = CleanPrivateKey(rawData);
                                
                                var account = new Nethereum.Web3.Accounts.Account(cleanPrivateKey);
                                var generatedAddress = account.Address;
                                lock (ValidWallets)
                                {
                                    ValidWallets.Add(("0x" + cleanPrivateKey, generatedAddress, false));
                                    Debug.WriteLine($"原始文本作为私钥验证成功，生成地址: {generatedAddress.Substring(0, Math.Min(10, generatedAddress.Length))}...");
                                }
                                walletAdded = true;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"原始文本作为私钥验证失败: {ex.Message}");
                            }
                        }
                    }
                    
                    // 如果所有尝试都失败，增加失败计数
                    if (!walletAdded)
                    {
                        Debug.WriteLine("所有验证方法都失败，标记为失败");
                        Interlocked.Increment(ref failedCount);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"处理钱包数据时出错: {ex.Message}\n{ex.StackTrace}");
                    Interlocked.Increment(ref failedCount);
                }
            }
            
            // 从格式化文本中提取钱包信息
            private (string PrivateKey, string Mnemonic, string Address) ExtractWalletInfo(string data)
            {
                string privateKey = null;
                string mnemonic = null;
                string address = null;
                
                // 分行处理
                string[] lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine)) continue;
                    
                    // 提取地址
                    if (trimmedLine.StartsWith("地址:") || trimmedLine.StartsWith("地址：") || 
                        trimmedLine.StartsWith("Address:") || trimmedLine.ToLower().StartsWith("address:"))
                    {
                        address = ExtractValueFromLine(trimmedLine);
                    }
                    // 提取私钥
                    else if (trimmedLine.StartsWith("私钥:") || trimmedLine.StartsWith("私钥：") || 
                             trimmedLine.StartsWith("Private Key:") || trimmedLine.ToLower().StartsWith("private key:") ||
                             trimmedLine.ToLower().StartsWith("privatekey:"))
                    {
                        privateKey = ExtractValueFromLine(trimmedLine);
                    }
                    // 提取助记词
                    else if (trimmedLine.StartsWith("助记词:") || trimmedLine.StartsWith("助记词：") || 
                             trimmedLine.StartsWith("Mnemonic:") || trimmedLine.ToLower().StartsWith("mnemonic:"))
                    {
                        string value = ExtractValueFromLine(trimmedLine);
                        // 检查是否是N/A或其他表示不适用的占位符
                        if (!IsPlaceholderText(value))
                        {
                            mnemonic = value;
                        }
                    }
                    // 尝试检测未标记的地址
                    else if (trimmedLine.StartsWith("0x") && trimmedLine.Length == 42 && IsValidEthereumAddress(trimmedLine))
                    {
                        address = trimmedLine;
                    }
                    // 尝试检测未标记的私钥
                    else if (IsValidPrivateKey(trimmedLine))
                    {
                        privateKey = CleanPrivateKey(trimmedLine);
                    }
                    // 尝试检测未标记的助记词
                    else
                    {
                        string extractedMnemonic = ExtractValidMnemonic(trimmedLine);
                        if (!string.IsNullOrEmpty(extractedMnemonic))
                        {
                            mnemonic = extractedMnemonic;
                        }
                    }
                }
                
                return (privateKey, mnemonic, address);
            }
            
            // 检查文本是否是N/A或其他表示不适用的占位符
            private bool IsPlaceholderText(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return true;
                
                // 常见的占位符文本
                string[] placeholders = { "N/A", "n/a", "NA", "na", "无", "没有", "None", "none", "-", "null", "undefined" };
                
                return placeholders.Contains(text.Trim());
            }
            
            // 从一行文本中提取值部分
            private string ExtractValueFromLine(string line)
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex < 0) colonIndex = line.IndexOf('：');
                
                if (colonIndex >= 0 && colonIndex < line.Length - 1)
                {
                    return line.Substring(colonIndex + 1).Trim();
                }
                return line.Trim();
            }
            
            // 判断是否是有效的以太坊地址
            private bool IsValidEthereumAddress(string address)
            {
                if (string.IsNullOrEmpty(address)) return false;
                if (!address.StartsWith("0x")) return false;
                if (address.Length != 42) return false;
                
                // 简单检查地址格式
                return address.Substring(2).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
            }
            
            // 清理私钥格式
            private string CleanPrivateKey(string privateKey)
            {
                privateKey = privateKey.Trim();
                // 移除可能的0x前缀
                if (privateKey.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    privateKey = privateKey.Substring(2);
                }
                return privateKey;
            }
            
            // 判断是否是有效的助记词
            private bool IsValidMnemonic(string input)
            {
                if (string.IsNullOrEmpty(input)) return false;
                
                // 清理输入文本
                input = input.Trim();
                
                // 检查是否是带标签的助记词
                if (input.StartsWith("助记词:") || input.StartsWith("助记词：") || 
                    input.StartsWith("Mnemonic:") || input.ToLower().StartsWith("mnemonic:"))
                {
                    input = ExtractValueFromLine(input);
                }
                
                // 简单的空格分隔检查
                var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 12 && words.Length <= 24 && 
                    (words.Length == 12 || words.Length == 15 || words.Length == 18 || words.Length == 21 || words.Length == 24))
                {
                    try
                    {
                        // 使用NBitcoin验证助记词是否有效
                        Wordlist wordlist = Wordlist.English;
                        if (words.All(word => wordlist.WordExists(word, out _)))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // 验证过程中发生错误，不是有效的助记词
                    }
                }
                
                return false;
            }
            
            // 提取文本中的有效助记词
            private string ExtractValidMnemonic(string input)
            {
                if (string.IsNullOrEmpty(input)) return null;
                
                // 清理输入文本，替换多余的标点符号和特殊字符为空格
                string cleanedInput = System.Text.RegularExpressions.Regex.Replace(
                    input, 
                    @"[^\w\s]|_", 
                    " "
                ).Trim();
                
                // 替换多个连续空格为单个空格
                cleanedInput = System.Text.RegularExpressions.Regex.Replace(cleanedInput, @"\s+", " ");
                
                // 拆分为单词
                string[] allWords = cleanedInput.Split(' ');
                
                // 获取英语助记词词汇表
                Wordlist wordlist = Wordlist.English;
                
                // 支持的助记词长度
                int[] validLengths = { 24, 21, 18, 15, 12 };
                
                // 尝试在字符串中找到有效的助记词序列
                for (int startIndex = 0; startIndex < allWords.Length; startIndex++)
                {
                    // 对于每个起始位置，尝试不同长度的助记词
                    foreach (int length in validLengths)
                    {
                        // 确保从当前起始位置开始有足够的单词
                        if (startIndex + length <= allWords.Length)
                        {
                            string[] potentialMnemonic = new string[length];
                            bool isValid = true;
                            
                            // 检查每个单词是否在助记词词汇表中
                            for (int i = 0; i < length; i++)
                            {
                                string word = allWords[startIndex + i].ToLower();
                                if (!wordlist.WordExists(word, out _))
                                {
                                    isValid = false;
                                    break;
                                }
                                potentialMnemonic[i] = word;
                            }
                            
                            // 如果所有单词都有效，返回找到的助记词
                            if (isValid)
                            {
                                return string.Join(" ", potentialMnemonic);
                            }
                        }
                    }
                }
                
                return null;
            }
            
            // 判断是否是有效的私钥（简单判断：64个16进制字符，可能有0x前缀）
            private bool IsValidPrivateKey(string input)
            {
                if (string.IsNullOrEmpty(input)) return false;
                
                // 清理输入文本
                input = input.Trim();
                
                // 检查是否是带标签的私钥
                if (input.StartsWith("私钥:") || input.StartsWith("私钥：") || 
                    input.StartsWith("Private Key:") || input.ToLower().StartsWith("private key:") ||
                    input.ToLower().StartsWith("privatekey:"))
                {
                    input = ExtractValueFromLine(input);
                }
                
                // 移除可能的0x前缀
                if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    input = input.Substring(2);
                }
                
                // 检查长度和格式
                return input.Length == 64 && input.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
            }
            
            private void UpdateProgress(int current, int total)
            {
                // 在UI线程更新进度
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = current;
                    double percentage = (double)current / total * 100;
                    statusTextBlock.Text = $"正在验证... {current}/{total} ({percentage:F1}%)";
                });
            }
        }
        
        // 导入钱包按钮点击事件
        private async void ImportWallets_Click(object sender, RoutedEventArgs e)
        {
            var importDialog = new ImportWalletDialog();
            if (importDialog.ShowDialog() == true)
            {
                // 获取验证后的有效钱包列表
                var validWallets = importDialog.ValidWallets;
                
                if (validWallets.Count == 0)
                {
                    MessageBox.Show("没有找到有效的钱包数据", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 显示导入进度对话框
                var progressDialog = new ProgressDialog("导入钱包", "正在导入钱包...");
                progressDialog.Owner = Window.GetWindow(this);
                
                // 异步导入钱包
                var importTask = Task.Run(() => 
                {
                    int importedCount = 0;
                    int failedCount = 0;
                    int duplicateCount = 0;
                    
                    // 处理重复地址，优先保留助记词生成的钱包
                    var uniqueWallets = new Dictionary<string, (string Input, bool IsMnemonic)>();
                    
                    foreach (var walletInfo in validWallets)
                    {
                        Debug.WriteLine($"地址: {walletInfo.Address}，类型: {(walletInfo.IsMnemonic ? "助记词" : "私钥")}");

                        if (uniqueWallets.TryGetValue(walletInfo.Address, out var existingWallet))
                        {
                            // 如果当前是助记词，而已存在的是私钥，则替换
                            if (walletInfo.IsMnemonic && !existingWallet.IsMnemonic)
                            {
                                uniqueWallets[walletInfo.Address] = (walletInfo.Input, walletInfo.IsMnemonic);
                            }
                            duplicateCount++;
                        }
                        else
                        {
                            // 添加新的唯一钱包
                            uniqueWallets.Add(walletInfo.Address, (walletInfo.Input, walletInfo.IsMnemonic));
                        }
                        
                        // 更新进度
                        progressDialog.Dispatcher.Invoke(() => 
                        {
                            progressDialog.SetProgress((double)(uniqueWallets.Count + duplicateCount) / validWallets.Count * 50);
                            progressDialog.SetStatus($"正在处理重复地址... {uniqueWallets.Count + duplicateCount}/{validWallets.Count}");
                        });
                    }
                    
                    // 导入唯一的钱包
                    int current = 0;
                    foreach (var wallet in uniqueWallets)
                    {
                        current++;
                        
                        try
                        {
                            Wallet importedWallet; // 创建局部变量接收out参数
                            if (walletService.TryImportWallet(wallet.Value.Input, out importedWallet))
                            {
                                importedCount++;
                            }
                            else
                            {
                                failedCount++;
                            }
                        }
                        catch
                        {
                            failedCount++;
                        }
                        
                        // 更新进度
                        progressDialog.Dispatcher.Invoke(() => 
                        {
                            progressDialog.SetProgress(50 + (double)current / uniqueWallets.Count * 50);
                            progressDialog.SetStatus($"正在导入钱包... {current}/{uniqueWallets.Count}");
                        });
                    }
                    
                    // 保存结果
                    return (importedCount, failedCount, duplicateCount);
                });
                
                // 显示进度对话框
                progressDialog.Show();
                
                try
                {
                    // 等待导入完成
                    (int importedCount, int failedCount, int duplicateCount) = await importTask;
                    
                    // 关闭进度对话框
                    progressDialog.Close();
                    
                    // 确保所有钱包都被保存到文件
                    walletService.SaveWallets();
                    
                    // 刷新钱包列表
                    LoadWallets();
                    
                    // 显示导入结果
                    if (importedCount > 0)
                    {
                        var message = $"成功导入 {importedCount} 个钱包";
                        if (duplicateCount > 0)
                        {
                            message += $"，发现 {duplicateCount} 个重复地址（已优先保留助记词生成的钱包）";
                        }
                        if (failedCount > 0)
                        {
                            message += $"，{failedCount} 个导入失败";
                        }
                        MessageBox.Show(message, "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (importedCount == 0 && failedCount > 0)
                    {
                        MessageBox.Show($"所有 {failedCount} 个钱包导入失败", "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    progressDialog.Close();
                    MessageBox.Show($"导入钱包时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        // 进度对话框
        public class ProgressDialog : Window
        {
            private ProgressBar progressBar;
            private TextBlock statusTextBlock;
            
            public ProgressDialog(string title, string initialStatus)
            {
                Title = title;
                Width = 400;
                Height = 150;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.ToolWindow;
                
                Grid grid = new Grid
                {
                    Margin = new Thickness(20)
                };
                
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                // 状态文本
                statusTextBlock = new TextBlock
                {
                    Text = initialStatus,
                    Margin = new Thickness(0, 0, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(statusTextBlock, 0);
                
                // 进度条
                progressBar = new ProgressBar
                {
                    Height = 20,
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };
                Grid.SetRow(progressBar, 1);
                
                grid.Children.Add(statusTextBlock);
                grid.Children.Add(progressBar);
                
                Content = grid;
            }
            
            public void SetProgress(double value)
            {
                progressBar.Value = value;
            }
            
            public void SetStatus(string status)
            {
                statusTextBlock.Text = status;
            }
        }
        
        // 全选按钮点击事件
        private void SelectAllWallets_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                bool isChecked = checkBox.IsChecked ?? false;
                
                // 更新当前显示的所有钱包项的选中状态
                if (walletListView.ItemsSource != null)
                {
                    foreach (var item in walletListView.ItemsSource)
                    {
                        var wallet = item as WalletViewModel;
                        if (wallet != null && !wallet.IsGroupHeader) // 跳过组标题项
                        {
                            wallet.IsSelected = isChecked;
                            
                            // 更新全局选中集合
                            if (isChecked)
                            {
                                selectedWalletIds.Add(wallet.Id);
                            }
                            else
                            {
                                selectedWalletIds.Remove(wallet.Id);
                            }
                        }
                    }
                }
                
                // 刷新列表，使UI更新
                walletListView.Items.Refresh();
            }
        }
        
        // 删除钱包按钮点击事件
        private void DeleteWallets_Click(object sender, RoutedEventArgs e)
        {
            var walletVMs = walletListView.ItemsSource as List<WalletViewModel>;
            if (walletVMs == null || walletVMs.Count == 0)
            {
                MessageBox.Show("请先选择要删除的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var selectedWallets = walletVMs.Where(w => w.IsSelected).ToList();
            if (selectedWallets.Count == 0)
            {
                MessageBox.Show("请先选择要删除的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 确认删除
            MessageBoxResult result = MessageBox.Show(
                $"确定要删除选中的 {selectedWallets.Count} 个钱包吗？此操作不可撤销。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                // 收集要删除的钱包ID
                var walletIdsToDelete = selectedWallets.Select(w => w.Id).ToList();
                
                // 从钱包服务中删除钱包
                walletService.Wallets.RemoveAll(w => walletIdsToDelete.Contains(w.Id));
                
                // 从分组中移除这些钱包ID
                foreach (var group in walletService.WalletGroups)
                {
                    group.WalletIds.RemoveAll(id => walletIdsToDelete.Contains(id));
                }
                
                // 从已选择的集合中移除
                foreach (var id in walletIdsToDelete)
                {
                    selectedWalletIds.Remove(id);
                }
                
                // 保存更改
                walletService.SaveWallets();
                walletService.SaveWalletGroups();
                
                // 刷新钱包列表
                LoadWallets();
                
                MessageBox.Show($"已删除 {walletIdsToDelete.Count} 个钱包", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        // 处理钱包选择状态变更
        private void WalletCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                var viewModel = checkBox.DataContext as WalletViewModel;
                if (viewModel != null)
                {
                    // 对于组标题项，不处理选择事件
                    if (viewModel.IsGroupHeader)
                    {
                        return;
                    }
                    
                    // 更新视图模型的选中状态
                    viewModel.IsSelected = checkBox.IsChecked ?? false;
                    
                    // 更新全局选中集合
                    if (viewModel.IsSelected)
                    {
                        selectedWalletIds.Add(viewModel.Id);
                    }
                    else
                    {
                        selectedWalletIds.Remove(viewModel.Id);
                    }
                    
                    // 更新"全选"复选框状态
                    bool allSelected = true;
                    foreach (var item in walletListView.ItemsSource)
                    {
                        // 跳过组标题项
                        var wallet = item as WalletViewModel;
                        if (wallet != null && !wallet.IsGroupHeader && !wallet.IsSelected)
                        {
                            allSelected = false;
                            break;
                        }
                    }
                    
                    selectAllWallets.IsChecked = allSelected;
                }
            }
        }
        
        // 导出选中钱包
        private void ExportedWallets_Click(object sender, RoutedEventArgs e)
        {
            var walletVMs = walletListView.ItemsSource as List<WalletViewModel>;
            if (walletVMs == null || walletVMs.Count == 0)
            {
                MessageBox.Show("没有可导出的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var selectedWallets = walletVMs.Where(w => w.IsSelected).ToList();
            if (selectedWallets.Count == 0)
            {
                MessageBox.Show("请先选择要导出的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 选择保存文件路径
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                Title = "导出钱包",
                FileName = $"钱包导出_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                StringBuilder exportData = new StringBuilder(); 
                
                // 添加选中的钱包数据，使用格式化输出
                foreach (var walletVM in selectedWallets)
                {
                    // 添加地址
                    exportData.AppendLine($"地址: {walletVM.Address}");
                    
                    // 添加私钥（如果有）
                    if (!string.IsNullOrEmpty(walletVM.PrivateKey))
                    {
                        exportData.AppendLine($"私钥: {(walletVM.PrivateKey.StartsWith("0x") ? walletVM.PrivateKey : "0x" + walletVM.PrivateKey)}");
                    }
                    
                    // 添加助记词（如果有）
                    if (!string.IsNullOrEmpty(walletVM.Mnemonic))
                    {
                        exportData.AppendLine($"助记词: {walletVM.Mnemonic}");
                    }
                    
                    // 添加分隔线
                    exportData.AppendLine("--------------------------------------------------");
                }
                
                try
                {
                    // 写入文件
                    System.IO.File.WriteAllText(saveFileDialog.FileName, exportData.ToString());
                    MessageBox.Show($"成功导出 {selectedWallets.Count} 个钱包", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出钱包时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        // 刷新/导出所有钱包
        private void RefreshValidatingWallets_Click(object sender, RoutedEventArgs e)
        { 
            var wallets = walletService.Wallets;
            if (wallets.Count == 0)
            {
                MessageBox.Show("没有可导出的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 选择保存文件路径
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                Title = "导出所有钱包",
                FileName = $"所有钱包导出_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            
            if (saveFileDialog.ShowDialog() == true)
            {
                StringBuilder exportData = new StringBuilder();
                
                // 添加所有钱包数据，使用格式化输出
                foreach (var wallet in wallets)
                {
                    // 添加地址
                    exportData.AppendLine($"地址: {wallet.Address}");
                    
                    // 添加私钥（如果有）
                    if (!string.IsNullOrEmpty(wallet.PrivateKey))
                    {
                        exportData.AppendLine($"私钥: {(wallet.PrivateKey.StartsWith("0x") ? wallet.PrivateKey : "0x" + wallet.PrivateKey)}");
                    }
                    
                    // 添加助记词（如果有）
                    if (!string.IsNullOrEmpty(wallet.Mnemonic))
                    {
                        exportData.AppendLine($"助记词: {wallet.Mnemonic}");
                    }
                    
                    // 添加分隔线
                    exportData.AppendLine("--------------------------------------------------");
                }
                
                try
                {
                    // 写入文件
                    System.IO.File.WriteAllText(saveFileDialog.FileName, exportData.ToString());
                    MessageBox.Show($"成功导出 {wallets.Count} 个钱包", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出钱包时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        // 将选中的钱包添加到分组
        private void AddWalletsToGroup_Click(object sender, RoutedEventArgs e)
        {
            var walletVMs = walletListView.ItemsSource as List<WalletViewModel>;
            if (walletVMs == null || walletVMs.Count == 0)
            {
                MessageBox.Show("没有可添加的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var selectedWallets = walletVMs.Where(w => w.IsSelected).ToList();
            if (selectedWallets.Count == 0)
            {
                MessageBox.Show("请先选择要添加到分组的钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 获取选中的分组
            var selectedItem = groupSelectionComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem == null || selectedItem.Tag == null)
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string groupId = selectedItem.Tag.ToString();
            
            // 获取选中的钱包ID列表
            var walletIds = selectedWallets.Select(w => w.Id).ToList();
            
            // 将钱包添加到分组
            walletService.AddWalletsToGroup(groupId, walletIds);
            
            // 刷新钱包列表以更新分组信息
            LoadWallets();
            
            MessageBox.Show($"已将 {selectedWallets.Count} 个钱包添加到分组", "操作成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        // 添加钱包到分组上下文菜单点击事件
        private void AddToGroup_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;
            if (menuItem == null || menuItem.Tag == null)
                return;
            
            string walletId = menuItem.Tag.ToString();
            if (string.IsNullOrEmpty(walletId))
                return;
            
            // 创建分组选择对话框
            var dialog = new GroupSelectionDialog(walletService.WalletGroups);
            
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.SelectedGroupId))
            {
                // 将钱包添加到选中的分组
                walletService.AddWalletsToGroup(dialog.SelectedGroupId, new List<string> { walletId });
                
                // 刷新钱包列表以更新分组信息
                LoadWallets();
            }
        }
        
        // 生成新钱包按钮点击事件
        private void GenerateWallets_Click(object sender, RoutedEventArgs e)
        {
            // 创建输入对话框
            var dialog = new InputDialog("生成钱包", "请输入生成数量:", "1");
            
            if (dialog.ShowDialog() == true)
            {
                if (int.TryParse(dialog.InputText, out int count) && count > 0 && count <= 100)
                {
                    // 生成钱包
                    try
                    {
                        walletService.GenerateWallets(count);
                        
                        // 刷新钱包列表
                        currentPage = 1; // 重置到第一页
                        walletSearchText = ""; // 清除搜索条件
                        searchWalletTextBox.Text = "";
                        LoadWallets();
                        
                        MessageBox.Show($"成功生成 {count} 个钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"生成钱包失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("请输入有效的数量 (1-100)", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        
        // 上一页按钮点击事件
        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage > 1)
            {
                currentPage--;
                LoadWallets();
            }
        }
        
        // 下一页按钮点击事件
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (currentPage < totalPages)
            {
                currentPage++;
                LoadWallets();
            }
        }
        
        // 跳转到指定页码
        private void JumpToPage_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(pageNumberTextBox.Text, out int pageNumber) && pageNumber > 0 && pageNumber <= totalPages)
            {
                currentPage = pageNumber;
                LoadWallets();
            }
            else
            {
                MessageBox.Show($"请输入有效的页码 (1-{totalPages})", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                pageNumberTextBox.Text = currentPage.ToString();
            }
        }
        
        // 复制钱包地址
        private void CopyWalletAddress_Click(object sender, RoutedEventArgs e)
        {
            var wallet = GetWalletFromMenuItem(sender as MenuItem);
            if (wallet != null)
            {
                Clipboard.SetText(wallet.Address);
                ShowCopySuccessMessage("地址");
            }
        }
        
        // 复制钱包助记词
        private void CopyWalletMnemonic_Click(object sender, RoutedEventArgs e)
        {
            var wallet = GetWalletFromMenuItem(sender as MenuItem);
            if (wallet != null)
            {
                Clipboard.SetText(wallet.Mnemonic);
                ShowCopySuccessMessage("助记词");
            }
        }
        
        // 复制钱包私钥
        private void CopyWalletPrivateKey_Click(object sender, RoutedEventArgs e)
        {
            var wallet = GetWalletFromMenuItem(sender as MenuItem);
            if (wallet != null)
            {
                Clipboard.SetText(wallet.PrivateKey);
                ShowCopySuccessMessage("私钥");
            }
        }
        
        // 从菜单项获取钱包对象
        private Wallet GetWalletFromMenuItem(MenuItem menuItem)
        {
            if (menuItem == null || menuItem.Tag == null)
                return null;
            
            string walletId = menuItem.Tag.ToString();
            if (string.IsNullOrEmpty(walletId))
                return null;
            
            return walletService.Wallets.FirstOrDefault(w => w.Id == walletId);
        }
        
        // 显示复制成功消息
        private void ShowCopySuccessMessage(string itemType)
        {
            MessageBox.Show($"已复制{itemType}到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
        
        // 分组选择对话框
        public class GroupSelectionDialog : Window
        {
            private ComboBox groupComboBox;
            
            public string SelectedGroupId { get; private set; }
            
            public GroupSelectionDialog(List<WalletGroup> groups)
            {
                Title = "选择分组";
                Width = 300;
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
                    Text = "请选择要添加到的分组:",
                    Margin = new Thickness(0, 0, 0, 5)
                };
                Grid.SetRow(prompt, 0);
                
                // 分组下拉框
                groupComboBox = new ComboBox
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    DisplayMemberPath = "Content"
                };
                
                // 添加分组项
                foreach (var group in groups)
                {
                    var item = new ComboBoxItem
                    {
                        Content = group.Name,
                        Tag = group.Id
                    };
                    groupComboBox.Items.Add(item);
                }
                
                // 默认选中第一项
                if (groupComboBox.Items.Count > 0)
                {
                    groupComboBox.SelectedIndex = 0;
                }
                
                Grid.SetRow(groupComboBox, 1);
                
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
                okButton.Click += (s, e) =>
                {
                    var selectedItem = groupComboBox.SelectedItem as ComboBoxItem;
                    if (selectedItem != null && selectedItem.Tag != null)
                    {
                        SelectedGroupId = selectedItem.Tag.ToString();
                        DialogResult = true;
                    }
                    else
                    {
                        MessageBox.Show("请选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(okButton);
                Grid.SetRow(buttonPanel, 2);
                
                grid.Children.Add(prompt);
                grid.Children.Add(groupComboBox);
                grid.Children.Add(buttonPanel);
                
                Content = grid;
            }
        }
        
        private void BatchTransfer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建批量转账对话框
                var batchTransferDialog = new BatchTransferDialog(walletService);
                
                // 设置所有者窗口
                batchTransferDialog.Owner = Window.GetWindow(this);
                
                // 显示对话框
                batchTransferDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开批量转账对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BBatchTransfer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建批量转账对话框
                var batchTransferDialog = new contractBatchTransferDialog(walletService);

                // 设置所有者窗口
                batchTransferDialog.Owner = Window.GetWindow(this);

                // 显示对话框
                batchTransferDialog.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开批量转账对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
         
        
        // 按分组加载钱包列表
        private void LoadWalletsByGroup()
        {
            // 检查walletService是否已初始化
            if (walletService == null)
            {
                return;
            }
            
            // 获取所有分组和钱包
            var groups = walletService.WalletGroups;
            var wallets = walletService.Wallets;
            
            // 创建分组的钱包视图模型列表
            var groupedWalletViewModels = new List<WalletViewModel>();
            
            // 为每个分组创建一个组标题项
            foreach (var group in groups)
            {
                // 应用搜索过滤
                var groupWallets = wallets.Where(w => 
                    group.WalletIds.Contains(w.Id) && 
                    (string.IsNullOrWhiteSpace(walletSearchText) || 
                     w.Address.IndexOf(walletSearchText, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                
                // 如果分组没有钱包或者所有钱包都被搜索过滤掉了，就跳过这个分组
                if (groupWallets.Count == 0)
                {
                    continue;
                }
                
                // 添加分组标题
                groupedWalletViewModels.Add(new WalletViewModel
                {
                    Id = $"group_{group.Id}",
                    Address = $"▼ {group.Name} ({groupWallets.Count}个钱包)",
                    IsGroupHeader = true,
                    GroupId = group.Id
                });
                
                // 添加分组中的钱包
                int walletIndex = 1;
                foreach (var wallet in groupWallets)
                {
                    // 获取钱包所属的分组名称
                    var groupNames = GetWalletGroupNames(wallet.Id);
                    
                    // 创建视图模型
                    groupedWalletViewModels.Add(new WalletViewModel
                    {
                        Id = wallet.Id,
                        Address = wallet.Address,
                        Mnemonic = wallet.Mnemonic,
                        PrivateKey = wallet.PrivateKey,
                        Remark = wallet.Remark,
                        IsSelected = selectedWalletIds.Contains(wallet.Id),
                        RowIndex = walletIndex++,
                        Groups = groupNames,
                        IndentLevel = 1 // 子项缩进
                    });
                }
            }
            
            // 设置列表数据源
            walletListView.ItemsSource = groupedWalletViewModels;
            
            // 更新"全选"复选框状态 - 仅考虑非组标题项
            var nonHeaderItems = groupedWalletViewModels.Where(w => !w.IsGroupHeader).ToList();
            selectAllWallets.IsChecked = nonHeaderItems.Count > 0 && nonHeaderItems.All(w => w.IsSelected);
            
            // 更新总记录数显示
            totalRecordsText.Text = wallets.Count.ToString();
            
            // 分组视图模式下不显示分页控件
            pageButtonsPanel.Visibility = Visibility.Collapsed;
            totalPages = 1;
        }
    }
} 

