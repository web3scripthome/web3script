using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using web3script.Models;
using System.Linq;
using web3script.Services;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using Task = System.Threading.Tasks.Task;
using System.Threading;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json; 
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace web3script.ucontrols
{
    public class IndexToNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
                return (index + 1).ToString();
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class BalanceInfoPanel : UserControl, INotifyPropertyChanged
    {
        private List<Project> _projects = new List<Project>();
        private List<string> _groups = new List<string>();
        private BalanceQueryService _balanceService = new BalanceQueryService();
        private ObservableCollection<EnhancedWalletBalanceInfo> _observableBalances = new ObservableCollection<EnhancedWalletBalanceInfo>();
        private ObservableCollection<CoinTypeNode> _coinTypeNodes = new ObservableCollection<CoinTypeNode>();
        ProjectService projectService = new ProjectService();
        WalletService walletService = new WalletService();
        private bool _isLoading;
        private CancellationTokenSource _cancellationTokenSource;
        
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public BalanceInfoPanel()
        {
            InitializeComponent();
            this.DataContext = this;
            lvBalances.ItemsSource = _observableBalances;
            tvCoinTypes.ItemsSource = _coinTypeNodes;
            LoadProjects();
        }

        private void LoadProjects()
        {
            // TODO: 实际应从服务加载
            _projects = projectService.GetProjects() ?? new List<Project>();
            cbProject.ItemsSource = _projects.Select(p => p.Name).ToList();
            if (_projects.Count > 0)
                cbProject.SelectedIndex = 0;
        }

        private void cbProject_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbProject.SelectedItem == null) return;
            string projectName = cbProject.SelectedItem.ToString();
            // TODO: 实际应从服务加载分组
            _groups = walletService.WalletGroups.Select(g => g.Name).ToList() ?? new List<string>();
            cbGroup.ItemsSource = _groups;
            if (_groups.Count > 0)
                cbGroup.SelectedIndex = 0;
            LoadCoinTypes(projectName);
        }

        private void cbGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbGroup.SelectedItem == null) return;
            string projectName = cbProject.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(projectName))
                LoadCoinTypes(projectName);
        }

        private void LoadCoinTypes(string projectName)
        {
            // 清空现有币种树
            _coinTypeNodes.Clear();
            
            try
            {
                // 使用ChainConfigService的配置
                var jsonObj = ChainConfigService.CoinConfig;
                
                // 创建币种树节点
                foreach (var prop in jsonObj.Properties())
                {
                    var parentNode = new CoinTypeNode 
                    { 
                        Name = prop.Name, 
                        TokenType = prop.Name,
                        IsSelected = false 
                    };
                    
                    // 处理子节点，跳过rpcUrls属性
                    if (prop.Value.Type == JTokenType.Object)
                    {
                        var childObject = prop.Value as JObject;
                        if (childObject != null)
                        {
                            foreach (var childProp in childObject.Properties())
                            {
                                // 跳过rpcUrls属性，不创建为币种节点
                                if (childProp.Name.Equals("rpcUrls", StringComparison.OrdinalIgnoreCase))
                                    continue;
                                
                                parentNode.Children.Add(new CoinTypeNode 
                                { 
                                    Name = childProp.Name, 
                                    TokenType = $"{prop.Name}.{childProp.Name}",
                                    IsSelected = false,
                                    Parent = parentNode
                                });
                            }
                        }
                    }
                    
                    _coinTypeNodes.Add(parentNode);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"解析币种配置出错: {ex.Message}");
                // 如果解析失败，则使用简单的币种列表
                foreach (var coin in CoinList.coins)
                {
                    _coinTypeNodes.Add(new CoinTypeNode 
                    { 
                        Name = coin, 
                        TokenType = coin,
                        IsSelected = false 
                    });
                }
            }
            
            // 默认选中第一个币种
            if (_coinTypeNodes.Count > 0)
                _coinTypeNodes[0].IsSelected = true;
        }
        
        // 币种选择变更事件
        private void CoinType_CheckChanged(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is CoinTypeNode node)
            {
                // 若状态变化是由父节点引起，不再触发本事件以避免循环
                if (node.IsChangingByParent)
                    return;
                
                // 不再自动更新子节点状态
                // 让用户能够单独选择父节点和子节点
                
                // 更新父节点状态
                UpdateParentNodeState(node);
            }
        }
        
        // 更新父节点状态
        private void UpdateParentNodeState(CoinTypeNode node)
        {
            if (node.Parent != null)
            {
                // 检查所有兄弟节点
                bool allSelected = node.Parent.Children.All(c => c.IsSelected);
                bool anySelected = node.Parent.Children.Any(c => c.IsSelected);
                
                node.Parent.IsChangingByParent = true;
                
                // 只有当所有子节点都被选中时，父节点才被选中
                // 这里不再强制父子节点状态同步
                if (allSelected)
                    node.Parent.IsSelected = true;
                
                node.Parent.IsChangingByParent = false;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshBalances();
        }

        private async Task RefreshBalances()
        {
            // 检查是否选择了项目和分组
            var projectName = cbProject.SelectedItem?.ToString();
            var groupName = cbGroup.SelectedItem?.ToString();
            if (projectName == null || groupName == null) return;
            
            // 获取选中的币种
            var selectedTokenTypes = GetSelectedTokenTypes();
            if (selectedTokenTypes.Count == 0)
            {
                MessageBox.Show("请选择至少一种币种", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 取消之前未完成的查询
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            
            // 清空当前结果
            _observableBalances.Clear();
            
            // 显示加载状态
            IsLoading = true;
            
            try
            {
                // 获取选中分组的钱包列表
                string groupId = null;
                if (!string.IsNullOrEmpty(groupName))
                {
                    var group = walletService.WalletGroups.FirstOrDefault(g => g.Name == groupName);
                    if (group != null)
                        groupId = group.Id;
                }
                
                List<web3script.Models.Wallet> wallets = new List<web3script.Models.Wallet>();
                if (!string.IsNullOrEmpty(groupId))
                    wallets = walletService.GetWalletsInGroup(groupId);
                
                // 创建钱包信息字典，用于汇总每个钱包的多种余额
                var walletInfoDict = new Dictionary<string, EnhancedWalletBalanceInfo>();
                
                // 为每个钱包创建一个初始的余额信息对象
                foreach (var wallet in wallets)
                {
                    var walletInfo = new EnhancedWalletBalanceInfo
                    {
                        WalletAddress = wallet.Address,
                        PrivateKey = wallet.PrivateKey,
                        Mnemonic = wallet.Mnemonic,
                        Remark = wallet.Remark,
                        BalanceItems = new ObservableCollection<BalanceItem>()
                    };
                    
                    walletInfoDict[wallet.Address] = walletInfo;
                    _observableBalances.Add(walletInfo);
                }
                
                // 分别查询每种币种的余额
                foreach (var tokenInfo in selectedTokenTypes)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    // 创建临时结果集
                    var tempCollection = new ObservableCollection<WalletBalanceInfo>();
                    
                    // 如果是二级币种，则需要使用合约查询
                    if (tokenInfo.TokenType.Contains('.'))
                    {
                        // 解析tokenInfo获取主链和合约信息
                        string[] parts = tokenInfo.TokenType.Split('.');
                        string chain = parts[0];
                        string token = parts[1];
                        
                        // 获取币种元数据
                        var coinData = ChainConfigService.GetCoinTypeData(tokenInfo.TokenType);
                        string contract = coinData?["contract"]?.ToString();
                        int decimals = coinData?["decimals"]?.Value<int>() ?? 18;
                        
                        // 使用统一的查询服务查询ERC20余额，与查询原生代币使用相同的方法
                        await _balanceService.QueryBalancesAsync(projectName, groupName, tokenInfo.TokenType, tempCollection);

                    }
                    else
                    {
                        // 查询原生代币余额
                        await _balanceService.QueryBalancesAsync(projectName, groupName, tokenInfo.TokenType, tempCollection);
                        Debug.WriteLine($"查询 {tokenInfo.TokenType} 余额完成");
                    }
                    
                    // 添加到对应钱包的余额信息列表中
                    foreach (var balanceInfo in tempCollection)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;
                        
                        if (walletInfoDict.TryGetValue(balanceInfo.WalletAddress, out var walletInfo))
                        {
                            // 对于子币种，显示完整的类型（如ETH.USDT）
                            string displayType = tokenInfo.TokenType.Contains('.') ? tokenInfo.TokenType : balanceInfo.TokenType;
                            
                            walletInfo.BalanceItems.Add(new BalanceItem
                            {
                                Balance = balanceInfo.Balance,
                                TokenType = displayType
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"查询余额时出错: {ex.Message}");
               
            }
            finally
            {
                // 关闭加载状态
                IsLoading = false;
            }
        }
        
        // 获取选中的币种类型
        private List<(string TokenType, JToken Data)> GetSelectedTokenTypes()
        {
            var result = new List<(string TokenType, JToken Data)>();
            
            // 解析配置获取币种元数据
            JObject coinConfig = null;
            try
            {
                coinConfig = JObject.Parse(ChainConfigService.GetCoinTypesJson());
            }
            catch
            {
                // 解析失败时使用空对象
                coinConfig = new JObject();
            }
            
            // 遍历所有被选中的节点
            foreach (var parentNode in _coinTypeNodes)
            {
                // 如果父节点被选中且没有选中的子节点，添加父节点
                if (parentNode.IsSelected && !parentNode.Children.Any(c => c.IsSelected))
                {
                    result.Add((parentNode.TokenType, null));
                }
                
                // 检查选中的子节点
                foreach (var childNode in parentNode.Children)
                {
                    if (childNode.IsSelected)
                    {
                        // 获取币种的元数据
                        JToken tokenData = null;
                        string[] parts = childNode.TokenType.Split('.');
                        if (parts.Length == 2 && coinConfig[parts[0]] != null && coinConfig[parts[0]][parts[1]] != null)
                        {
                            tokenData = coinConfig[parts[0]][parts[1]];
                        }
                        
                        result.Add((childNode.TokenType, tokenData));
                    }
                }
            }
            
            return result;
        }
        
        // 查询ERC20代币余额
        private async Task<decimal> GetErc20BalanceAsync(string chain, string address, string contract, int decimals, ProxyViewModel proxy = null, int retry = 3)
        {
            // 使用ChainConfigService获取RPC URL
            string rpcUrl = ChainConfigService.GetChainRpcUrl(chain);
            
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    // TODO: 实现实际的ERC20余额查询
                    // 这里是示例代码，需要根据实际情况修改
                    // var web3 = proxy == null ? new Web3(rpcUrl) : new Web3(sHttpHandler.GetRpcClient(sHttpHandler.GetHttpClientHandler(proxy), rpcUrl));
                    // var erc20 = web3.Eth.ERC20.GetContractService(contract);
                    // var balance = await erc20.BalanceOfQueryAsync(address);
                    // return Web3.Convert.FromWei(balance, decimals);
                    
                    // 模拟查询结果
                    await Task.Delay(100);
                    return 100.5m;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"查询ERC20余额失败 (尝试 {i+1}/{retry}): {ex.Message}");
                    
                    if (i == retry - 1) throw;
                    
                    // 如果当前RPC失败，尝试使用另一个RPC
                    try
                    {
                        var allRpcUrls = ChainConfigService.GetChainRpcUrls(chain);
                        if (allRpcUrls.Count > 1)
                        {
                            // 从列表中移除当前失败的RPC
                            allRpcUrls.Remove(rpcUrl);
                            
                            // 随机选择另一个RPC
                            Random random = new Random();
                            int index = random.Next(allRpcUrls.Count);
                            rpcUrl = allRpcUrls[index];
                            
                            Debug.WriteLine($"切换到备用RPC: {rpcUrl}");
                        }
                    }
                    catch { }
                    
                    await Task.Delay(1000);
                }
            }
            
            throw new Exception("查询ERC20余额失败");
        }

        private void CopyAddress_Click(object sender, RoutedEventArgs e)
        {
            if (lvBalances.SelectedItem is EnhancedWalletBalanceInfo info && !string.IsNullOrEmpty(info.WalletAddress))
                Clipboard.SetText(info.WalletAddress);
        }
        
        private void CopyPrivateKey_Click(object sender, RoutedEventArgs e)
        {
            if (lvBalances.SelectedItem is EnhancedWalletBalanceInfo info && !string.IsNullOrEmpty(info.PrivateKey))
                Clipboard.SetText(info.PrivateKey);
            else
                MessageBox.Show("无私钥信息", "提示");
        }
        
        private void CopyMnemonic_Click(object sender, RoutedEventArgs e)
        {
            if (lvBalances.SelectedItem is EnhancedWalletBalanceInfo info && !string.IsNullOrEmpty(info.Mnemonic))
                Clipboard.SetText(info.Mnemonic);
            else
                MessageBox.Show("无助记词信息", "提示");
        }
    }
    
    // 币种节点类，用于树形显示
    public class CoinTypeNode : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isChangingByParent;
        
        public string Name { get; set; }
        public string TokenType { get; set; }
        public ObservableCollection<CoinTypeNode> Children { get; set; } = new ObservableCollection<CoinTypeNode>();
        public CoinTypeNode Parent { get; set; }
        
        public bool IsChangingByParent
        {
            get => _isChangingByParent;
            set => _isChangingByParent = value;
        }
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    // 余额项，用于表示一种币种的余额
    public class BalanceItem
    {
        public string Balance { get; set; }
        public string TokenType { get; set; }
    }
    
    // 增强的钱包余额信息，包含多种币种的余额
    public class EnhancedWalletBalanceInfo
    {
        public string WalletAddress { get; set; }
        public string Remark { get; set; }
        public string PrivateKey { get; set; }
        public string Mnemonic { get; set; }
        public ObservableCollection<BalanceItem> BalanceItems { get; set; } = new ObservableCollection<BalanceItem>();
    }
    
    // 用于兼容现有API的钱包余额信息
    public class WalletBalanceInfo
    {
        public string WalletAddress { get; set; }
        public string Balance { get; set; }
        public string TokenType { get; set; }
        public string PrivateKey { get; set; }
        public string Mnemonic { get; set; }
    }
} 