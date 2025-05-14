using web3script.ucontrols;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using static web3script.ucontrols.GroupManagementPanel; 
using System.Text; 
using System.Globalization;

namespace web3script.ucontrols
{
    /// <summary>
    /// 将布尔值转换为字符串的转换器
    /// </summary>
    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "否";
                
            bool boolValue = (bool)value;
            
            // 处理parameter为null的情况
            if (parameter == null)
                return boolValue ? "是" : "否";
            
            string[] options;
            try
            {
                options = parameter.ToString().Split('|');
                if (options.Length == 2)
                {
                    return boolValue ? options[0] : options[1];
                }
            }
            catch
            {
                // 如果Split失败，返回默认值
            }
            
            return boolValue ? "是" : "否";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public partial class ProxyConfigPanel : UserControl
    {
        private ObservableCollection<ProxyViewModel> _proxyList = new ObservableCollection<ProxyViewModel>();
        private ObservableCollection<ProxyViewModel> _filteredProxyList = new ObservableCollection<ProxyViewModel>();
        private readonly string _proxyConfigFile = "proxy_config.json";
        private readonly string _proxyGroupsFile = "proxy_groups.json";
        private List<ProxyGroup> _proxyGroups = new List<ProxyGroup>();
        
        // 分页相关属性
        private int _currentPage = 1;
        private int _pageSize = 20;
        private int _totalPages = 1;
        private string _searchText = "";
        private ICollectionView _proxyView;
        
        // 添加一个字段用于保存当前选择的分组
        private string _selectedGroupFilter = "全部";
        
        public ProxyConfigPanel()
        {
            InitializeComponent();
            
            // 确保在调用其他方法前初始化集合
            _proxyList = new ObservableCollection<ProxyViewModel>();
            _filteredProxyList = new ObservableCollection<ProxyViewModel>();
            
            // 初始化CollectionView用于排序、筛选和分页
            _proxyView = CollectionViewSource.GetDefaultView(_proxyList);
            proxyDataGrid.ItemsSource = _proxyView;
            
            // 设置默认筛选条件，但不应用
            _proxyView.Filter = item => true;
            
            // 加载数据
            LoadProxyConfig();
            LoadProxyGroups();
            
            // 初始化分组下拉框
            var groupsList = new List<string>{"全部"};
            groupsList.AddRange(_proxyGroups.Select(g => g.Name));
            
            proxyGroupsComboBox.ItemsSource = groupsList;
            cmbAssignGroup.ItemsSource = _proxyGroups.Select(g => g.Name);
            
            // 设置默认选择
            proxyGroupsComboBox.SelectedIndex = 0; // 默认选择"全部"
            
            if(_proxyGroups.Count > 0)
            {
                cmbAssignGroup.SelectedIndex = 0;
            }
            
            // 添加分组选择事件处理
            proxyGroupsComboBox.SelectionChanged += ProxyGroupsComboBox_SelectionChanged;
            
            // 初始化分页设置
            cmbPageSize.SelectedIndex = 1; // 默认选择20条/页
            UpdatePagination();
        }

        // 添加分组选择事件处理方法
        private void ProxyGroupsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (proxyGroupsComboBox.SelectedItem != null)
            {
                _selectedGroupFilter = proxyGroupsComboBox.SelectedItem.ToString();
                _currentPage = 1; // 切换分组时重置到第一页
                ApplyFilter();
            }
        }

        private void LoadProxyGroups()
        {
            try
            {
                _proxyGroups.Clear();
                
                if (File.Exists(_proxyGroupsFile))
                {
                    string json = File.ReadAllText(_proxyGroupsFile);
                    var groups = JsonConvert.DeserializeObject<List<ProxyGroup>>(json);
                    
                    if (groups != null)
                    {
                        foreach (var group in groups)
                        {
                            _proxyGroups.Add(group);
                        }
                    }
                }
                
                if (!_proxyGroups.Any(g => g.Name == "默认"))
                {
                    _proxyGroups.Add(new ProxyGroup("默认"));
                    SaveProxyGroups();
                }
                
                // 更新两个下拉框
                if (proxyGroupsComboBox != null)
                {
                    var groupsList = new List<string>{"全部"};
                    groupsList.AddRange(_proxyGroups.Select(g => g.Name));
                    proxyGroupsComboBox.ItemsSource = groupsList;
                    
                    // 保持当前选择
                    if (!string.IsNullOrEmpty(_selectedGroupFilter))
                    {
                        int index = groupsList.IndexOf(_selectedGroupFilter);
                        if (index >= 0)
                            proxyGroupsComboBox.SelectedIndex = index;
                        else
                            proxyGroupsComboBox.SelectedIndex = 0;
                    }
                }
                    
                if (cmbAssignGroup != null)
                    cmbAssignGroup.ItemsSource = _proxyGroups.Select(g => g.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载代理分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveProxyGroups()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_proxyGroups, Formatting.Indented);
                File.WriteAllText(_proxyGroupsFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存代理分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProxyConfig()
        {
            try
            {
                if (File.Exists(_proxyConfigFile))
                {
                    string json = File.ReadAllText(_proxyConfigFile);
                    var proxies = JsonConvert.DeserializeObject<List<ProxyViewModel>>(json) ?? new List<ProxyViewModel>();
                    
                    _proxyList.Clear();
                    int index = 1;
                    foreach (var proxy in proxies)
                    {
                        proxy.Index = index++;
                        if (string.IsNullOrEmpty(proxy.GroupName))
                        {
                            proxy.GroupName = "默认";
                        }
                        _proxyList.Add(proxy);
                    }
                    ApplyFilter();
                    UpdatePagination();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载代理配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProxyConfig()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_proxyList, Formatting.Indented);
                File.WriteAllText(_proxyConfigFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存代理配置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddProxy_Click(object sender, RoutedEventArgs e)
        {
            string proxyType = ((ComboBoxItem)cbProxyType.SelectedItem).Content.ToString();
            string serverAddress = txtServerAddress.Text.Trim();
            string portText = txtServerPort.Text.Trim();
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(serverAddress))
            {
                MessageBox.Show("请输入服务器地址", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("端口号必须为1-65535之间的数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string groupName = proxyGroupsComboBox.SelectedItem?.ToString() ?? "默认";

            var proxy = new ProxyViewModel
            {
                Index = _proxyList.Count + 1,
                ProxyType = proxyType,
                ServerAddress = serverAddress,
                Port = port,
                Username = username,
                Password = password,
                Status = ProxyStatus.Unknown,
                StatusText = "未测试",
                Latency = 0,
                IsSelected = false,
                GroupName = groupName
            };

            _proxyList.Add(proxy);
            
            // 清空输入框
            txtServerAddress.Clear();
            txtServerPort.Clear();
            txtUsername.Clear();
            txtPassword.Clear();
            
            // 立即测试新添加的代理
            TestProxy(proxy);
            SaveProxyConfig();
        }

        private void ImportProxy_Click(object sender, RoutedEventArgs e)
        {
            var importDialog = new ImportProxyDialog();
            importDialog.Owner = Window.GetWindow(this);
            
            if (importDialog.ShowDialog() == true)
            {
                var importedProxies = importDialog.ImportedProxies;
                int index = _proxyList.Count + 1;
                
                foreach (var proxy in importedProxies)
                {
                    proxy.Index = index++;
                    if (string.IsNullOrEmpty(proxy.GroupName))
                    {
                        proxy.GroupName = "默认";
                    }
                    _proxyList.Add(proxy);
                }
                
                // 保存更改并更新视图
                SaveProxyConfig();
                ApplyFilter();
                UpdatePagination();
                
                MessageBox.Show($"成功导入 {importedProxies.Count} 个代理", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void TestProxy_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ProxyViewModel proxy)
            {
                await TestProxy(proxy);
                SaveProxyConfig();
            }
        }

        private async void TestAllProxy_Click(object sender, RoutedEventArgs e)
        {
            if (_proxyList.Count == 0)
            {
                MessageBox.Show("没有可测试的代理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tasks = _proxyList.Select(proxy => TestProxy(proxy)).ToArray();
            await Task.WhenAll(tasks);
            SaveProxyConfig();
        }

        private async Task TestProxy(ProxyViewModel proxy)
        {
            proxy.Status = ProxyStatus.Testing;
            proxy.StatusText = "测试中...";
            
            var stopwatch = new Stopwatch();
            bool isConnected = false;
            
            try
            {
                // 根据代理类型和是否有身份验证创建适当的代理
                HttpClientHandler handler = null;

                if (proxy.ProxyType.Equals("SOCKS5", StringComparison.OrdinalIgnoreCase))
                {
                 //   Debug.WriteLine($" 使用.NET 6+原生支持的SOCKS5代理 socks5://{proxy.ServerAddress}:{proxy.Port}-{proxy.Username}-{proxy.Password} - {proxy.HasAuthentication}");
                    string proxyUrl = $"socks5://{proxy.ServerAddress}:{proxy.Port}";
                    
                    if (proxy.HasAuthentication)
                    {
                     //  Debug.WriteLine  ($"socks5://{proxy.ServerAddress}:{proxy.Port} 需要身份验证");
                        handler = new HttpClientHandler
                        {
                            Proxy = new WebProxy(proxyUrl)
                            {
                                Credentials = new NetworkCredential(proxy.Username, proxy.Password)
                            },
                            UseProxy = true
                        };
                    }
                    else
                    {
                     //  Debug.WriteLine($"socks5://{proxy.ServerAddress}:{proxy.Port} 不需要身份验证");
                        handler = new HttpClientHandler
                        {
                            Proxy = new WebProxy(proxyUrl),
                            UseProxy = true
                        };
                    }
                }
                else
                {
                    // HTTP或HTTPS代理
                    string proxyUrl = $"{proxy.ProxyType.ToLower()}://{proxy.ServerAddress}:{proxy.Port}";
                    
                    if (proxy.HasAuthentication)
                    {
                        handler = new HttpClientHandler
                        {
                            Proxy = new WebProxy(proxyUrl)
                            {
                                Credentials = new NetworkCredential(proxy.Username, proxy.Password)
                            },
                            UseProxy = true
                        };
                    }
                    else
                    {
                        handler = new HttpClientHandler
                        {
                            Proxy = new WebProxy(proxyUrl),
                            UseProxy = true
                        };
                    }
                }
                
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    stopwatch.Start();
                    var response = await client.GetAsync("https://api.ipify.org/");
                    stopwatch.Stop();
                    
                    if (response.IsSuccessStatusCode)
                    {
                        isConnected = true;
                    }
                }
            }
            catch (Exception ex)
            {
                isConnected = false;
                Debug.WriteLine($"代理测试失败: {ex.Message}");
            }
            finally
            {
                if (stopwatch.IsRunning)
                {
                    stopwatch.Stop();
                }
                
                proxy.Status = isConnected ? ProxyStatus.Connected : ProxyStatus.Failed;
                proxy.StatusText = isConnected ? "正常" : "失败";
                proxy.Latency = isConnected ? (int)stopwatch.ElapsedMilliseconds : 0;
            }
        }

        private void DeleteProxy_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ProxyViewModel proxy)
            {
                if (MessageBox.Show("确定要删除此代理吗?", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _proxyList.Remove(proxy);
                    
                    // 更新索引
                    for (int i = 0; i < _proxyList.Count; i++)
                    {
                        _proxyList[i].Index = i + 1;
                    }
                    
                    SaveProxyConfig();
                }
            }
        }

        private void DeleteSelectedProxy_Click(object sender, RoutedEventArgs e)
        {
            var selectedProxies = _proxyList.Where(p => p.IsSelected).ToList();
            
            if (selectedProxies.Count == 0)
            {
                MessageBox.Show("请先选择要删除的代理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (MessageBox.Show($"确定要删除选中的 {selectedProxies.Count} 个代理吗?", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var proxy in selectedProxies)
                {
                    _proxyList.Remove(proxy);
                }
                
                // 更新索引
                for (int i = 0; i < _proxyList.Count; i++)
                {
                    _proxyList[i].Index = i + 1;
                }
                
                SaveProxyConfig();
            }
        }

        //private void ProxyCheckBox_Click(object sender, RoutedEventArgs e)
        //{
        //    var checkBox = sender as CheckBox;
        //    if (checkBox != null && checkBox.DataContext is ProxyViewModel proxy)
        //    {
        //        proxy.IsSelected = checkBox.IsChecked ?? false;
        //    }
        //}

        private void SaveProxyConfig_Click(object sender, RoutedEventArgs e)
        {
            SaveProxyConfig();
            MessageBox.Show("代理配置已保存", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddProxyGroup_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialog("添加分组", "请输入代理分组名称:");
            if (dialog.ShowDialog() == true)
            {
                string groupName = dialog.InputText.Trim();
                
                if (string.IsNullOrEmpty(groupName))
                {
                    MessageBox.Show("分组名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (_proxyGroups.Any(g => g.Name == groupName))
                {
                    MessageBox.Show("已存在相同名称的分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                _proxyGroups.Add(new ProxyGroup(groupName));
                SaveProxyGroups();
                
                // 更新分组下拉框
                var groupsList = new List<string>{"全部"};
                groupsList.AddRange(_proxyGroups.Select(g => g.Name));
                
                // 暂时移除事件处理器，避免触发不必要的筛选
                proxyGroupsComboBox.SelectionChanged -= ProxyGroupsComboBox_SelectionChanged;
                
                proxyGroupsComboBox.ItemsSource = null;
                proxyGroupsComboBox.ItemsSource = groupsList;
                proxyGroupsComboBox.SelectedItem = groupName;
                _selectedGroupFilter = groupName;
                
                // 重新添加事件处理器
                proxyGroupsComboBox.SelectionChanged += ProxyGroupsComboBox_SelectionChanged;
                
                // 更新分配分组下拉框
                cmbAssignGroup.ItemsSource = null;
                cmbAssignGroup.ItemsSource = _proxyGroups.Select(g => g.Name);
                cmbAssignGroup.SelectedItem = groupName;
                
                // 应用筛选
                ApplyFilter();
            }
        }
        
        private void DeleteProxyGroup_Click(object sender, RoutedEventArgs e)
        {
            if (proxyGroupsComboBox.SelectedItem == null || proxyGroupsComboBox.SelectedItem.ToString() == "全部")
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string groupName = proxyGroupsComboBox.SelectedItem.ToString();
            
            if (groupName == "默认")
            {
                MessageBox.Show("不能删除默认分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show($"确定要删除分组 \"{groupName}\" 吗？该分组下的代理将移到默认分组","确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                // 将该分组下的代理移到默认分组
                foreach (var proxy in _proxyList.Where(p => p.GroupName == groupName))
                {
                    proxy.GroupName = "默认";
                }
                
                // 从列表中移除该分组
                var group = _proxyGroups.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                {
                    _proxyGroups.Remove(group);
                }
                
                SaveProxyGroups();
                SaveProxyConfig();
                
                // 更新分组下拉框
                var groupsList = new List<string>{"全部"};
                groupsList.AddRange(_proxyGroups.Select(g => g.Name));
                
                // 暂时移除事件处理器，避免触发不必要的筛选
                proxyGroupsComboBox.SelectionChanged -= ProxyGroupsComboBox_SelectionChanged;
                
                proxyGroupsComboBox.ItemsSource = null;
                proxyGroupsComboBox.ItemsSource = groupsList;
                proxyGroupsComboBox.SelectedIndex = 0;
                _selectedGroupFilter = "全部";
                
                // 重新添加事件处理器
                proxyGroupsComboBox.SelectionChanged += ProxyGroupsComboBox_SelectionChanged;
                
                // 更新分配分组下拉框
                cmbAssignGroup.ItemsSource = null;
                cmbAssignGroup.ItemsSource = _proxyGroups.Select(g => g.Name);
                cmbAssignGroup.SelectedIndex = 0;
                
                // 应用筛选
                ApplyFilter();
            }
        }
        
        private void AssignToGroup_Click(object sender, RoutedEventArgs e)
        {
            if (cmbAssignGroup.SelectedItem == null)
            {
                MessageBox.Show("请先选择一个分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            string groupName = cmbAssignGroup.SelectedItem.ToString();
            var selectedProxies = _proxyList.Where(p => p.IsSelected).ToList();
            
            if (selectedProxies.Count == 0)
            {
                MessageBox.Show("请至少选择一个代理", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // 将选中的代理添加到选定分组
            foreach (var proxy in selectedProxies)
            {
                proxy.GroupName = groupName;
            }
            
            SaveProxyConfig();
            
            MessageBox.Show($"已将选中的 {selectedProxies.Count} 个代理添加到分组 {groupName}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 添加获取配置好的代理的公共方法
        public static IWebProxy GetConfiguredProxy(ProxyViewModel proxy)
        {
            try
            {
                string proxyUrl;
                if (proxy.ProxyType.Equals("SOCKS5", StringComparison.OrdinalIgnoreCase))
                {
                    // 使用.NET 6+原生支持的SOCKS5代理
                    proxyUrl = $"socks5://{proxy.ServerAddress}:{proxy.Port}";
                }
                else
                {
                    // HTTP或HTTPS代理
                    proxyUrl = $"{proxy.ProxyType.ToLower()}://{proxy.ServerAddress}:{proxy.Port}";
                }
                
                WebProxy webProxy = new WebProxy(proxyUrl);
                
                if (proxy.HasAuthentication)
                {
                    webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
                }
                
                return webProxy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建代理失败: {ex.Message}");
                return null;
            }
        }

        // 获取随机代理的方法
        public static IWebProxy GetRandomProxy(string groupName = null)
        {
            try
            {
                string proxyConfigFile = "proxy_config.json";
                if (!File.Exists(proxyConfigFile))
                    return null;
                    
                string json = File.ReadAllText(proxyConfigFile);
                var allProxies = JsonConvert.DeserializeObject<List<ProxyViewModel>>(json);
                
                if (allProxies == null || allProxies.Count == 0)
                    return null;
                    
                // 只使用状态为"正常"的代理
                var availableProxies = allProxies.Where(p => p.Status == ProxyStatus.Connected).ToList();
                
                if (groupName != null)
                {
                    availableProxies = availableProxies.Where(p => p.GroupName == groupName).ToList();
                }
                
                if (availableProxies.Count == 0)
                    return null;
                    
                Random random = new Random();
                int randomIndex = random.Next(availableProxies.Count);
                
                return GetConfiguredProxy(availableProxies[randomIndex]);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取随机代理失败: {ex.Message}");
                return null;
            }
        }

        // 分页和过滤方法
        private void ApplyFilter()
        {
            if (_proxyView == null)
            {
                Debug.WriteLine("警告: _proxyView为空，无法应用过滤器");
                return;
            }
            
            _proxyView.Filter = item =>
            {
                if (item is ProxyViewModel proxy)
                {
                    // 分组过滤
                    if (!string.IsNullOrEmpty(_selectedGroupFilter) && _selectedGroupFilter != "全部")
                    {
                        if (proxy.GroupName != _selectedGroupFilter)
                            return false;
                    }
                    
                    // 搜索过滤
                    if (!string.IsNullOrEmpty(_searchText))
                    {
                        bool matchSearch = 
                            (proxy.ServerAddress != null && proxy.ServerAddress.Contains(_searchText)) ||
                            proxy.Port.ToString().Contains(_searchText) ||
                            (proxy.ProxyType != null && proxy.ProxyType.Contains(_searchText)) ||
                            (proxy.Username != null && proxy.Username.Contains(_searchText)) ||
                            (proxy.GroupName != null && proxy.GroupName.Contains(_searchText));
                        
                        if (!matchSearch) return false;
                    }
                    
                    // 分页过滤
                    if (_pageSize > 0) // 0表示显示全部
                    {
                        if (_proxyList == null)
                        {
                            return true; // 如果_proxyList为空，不进行分页过滤
                        }
                        
                        int itemIndex = _proxyList.IndexOf(proxy);
                        int startIndex = (_currentPage - 1) * _pageSize;
                        int endIndex = Math.Min(startIndex + _pageSize, _proxyList.Count);
                        
                        return itemIndex >= startIndex && itemIndex < endIndex;
                    }
                    
                    return true;
                }
                return false;
            };
            
            UpdatePagination();
        }

        private void UpdatePagination()
        {
            if (_proxyList == null || _proxyView == null)
            {
                Debug.WriteLine("警告: _proxyList或_proxyView为空，无法更新分页");
                return;
            }
            
            // 计算符合过滤条件的总记录数
            int totalItems = 0;
            
            foreach (ProxyViewModel proxy in _proxyList)
            {
                // 应用与ApplyFilter相同的分组和搜索过滤逻辑
                bool include = true;
                
                // 分组过滤
                if (!string.IsNullOrEmpty(_selectedGroupFilter) && _selectedGroupFilter != "全部")
                {
                    if (proxy.GroupName != _selectedGroupFilter)
                    {
                        include = false;
                    }
                }
                
                // 搜索过滤
                if (include && !string.IsNullOrEmpty(_searchText))
                {
                    bool matchSearch = 
                        (proxy.ServerAddress != null && proxy.ServerAddress.Contains(_searchText)) ||
                        proxy.Port.ToString().Contains(_searchText) ||
                        (proxy.ProxyType != null && proxy.ProxyType.Contains(_searchText)) ||
                        (proxy.Username != null && proxy.Username.Contains(_searchText)) ||
                        (proxy.GroupName != null && proxy.GroupName.Contains(_searchText));
                    
                    if (!matchSearch) include = false;
                }
                
                if (include) totalItems++;
            }
            
            if (_pageSize > 0)
            {
                _totalPages = (int)Math.Ceiling((double)totalItems / _pageSize);
            }
            else
            {
                _totalPages = 1;
            }
            
            // 确保当前页在有效范围内
            _currentPage = Math.Max(1, Math.Min(_currentPage, _totalPages));
            
            // 更新UI
            txtPageInfo.Text = $"第 {_currentPage}/{_totalPages} 页";
            txtItemCount.Text = $"共 {totalItems} 条记录";
            
            try
            {
                // 刷新视图
                _proxyView.Refresh();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新视图时出错: {ex.Message}");
            }
        }

        // 分页控制方法
        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage = 1;
                ApplyFilter();
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                ApplyFilter();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                ApplyFilter();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage = _totalPages;
                ApplyFilter();
            }
        }

        private void PageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPageSize.SelectedItem != null && IsInitialized)
            {
                string sizeText = ((ComboBoxItem)cmbPageSize.SelectedItem).Content.ToString();
                if (sizeText == "全部")
                {
                    _pageSize = 0; // 0表示显示全部
                }
                else
                {
                    int.TryParse(sizeText, out _pageSize);
                }
                
                _currentPage = 1; // 切换页大小时重置到第一页
                ApplyFilter();
            }
        }

        // 搜索功能
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = txtSearch.Text.Trim();
            _currentPage = 1; // 搜索时重置到第一页
            ApplyFilter();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
        }

        // 选择功能
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = chkSelectAll.IsChecked ?? false;
            
            foreach (ProxyViewModel proxy in _proxyView)
            {
                proxy.IsSelected = isChecked;
            }
        }

        private void ProxyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            // 更新全选复选框状态
            UpdateSelectAllCheckbox();
        }

        private void ProxyDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 此处可以添加选择变更的逻辑
            UpdateSelectAllCheckbox();
        }

        private void UpdateSelectAllCheckbox()
        {
            int totalCount = _proxyView.Cast<ProxyViewModel>().Count();
            if (totalCount == 0)
            {
                chkSelectAll.IsChecked = false;
                return;
            }
            
            int selectedCount = _proxyView.Cast<ProxyViewModel>().Count(p => p.IsSelected);
            
            if (selectedCount == 0)
                chkSelectAll.IsChecked = false;
            else if (selectedCount == totalCount)
                chkSelectAll.IsChecked = true;
            else
                chkSelectAll.IsChecked = null; // 部分选中状态
        }

        // 批量操作功能
        private void TestSelectedProxy_Click(object sender, RoutedEventArgs e)
        {
            var selectedProxies = _proxyView.Cast<ProxyViewModel>()
                .Where(p => p.IsSelected)
                .ToList();
            
            if (selectedProxies.Count == 0)
            {
                MessageBox.Show("请先选择需要测试的代理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 测试选中的代理
            var tasks = selectedProxies.Select(proxy => TestProxy(proxy)).ToArray();
            Task.WhenAll(tasks).ContinueWith(t => 
            {
                Dispatcher.Invoke(() => 
                {
                    SaveProxyConfig();
                    MessageBox.Show($"已完成 {selectedProxies.Count} 个代理的测试", "测试完成", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void ExportSelectedProxy_Click(object sender, RoutedEventArgs e)
        {
            var selectedProxies = _proxyView.Cast<ProxyViewModel>()
                .Where(p => p.IsSelected)
                .ToList();
            
            if (selectedProxies.Count == 0)
            {
                MessageBox.Show("请先选择需要导出的代理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 导出选中的代理
            var dialog = new SaveFileDialog
            {
                Filter = "文本文件 (*.txt)|*.txt",
                Title = "导出代理",
                FileName = "proxies.txt"
            };
            
            if (dialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(dialog.FileName))
                {
                    foreach (var proxy in selectedProxies)
                    {
                        if (proxy.HasAuthentication)
                        {
                            writer.WriteLine($"{proxy.ProxyType.ToLower()}://{proxy.Username}:{proxy.Password}@{proxy.ServerAddress}:{proxy.Port}");
                        }
                        else
                        {
                            writer.WriteLine($"{proxy.ProxyType.ToLower()}://{proxy.ServerAddress}:{proxy.Port}");
                        }
                    }
                }
                
                MessageBox.Show($"已成功导出 {selectedProxies.Count} 个代理", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EditProxy_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is ProxyViewModel proxy)
            {
                // 创建一个编辑对话框
                EditProxyDialog dialog = new EditProxyDialog(proxy);
                dialog.Owner = Window.GetWindow(this);
                
                if (dialog.ShowDialog() == true)
                {
                    // 应用更改
                    SaveProxyConfig();
                    MessageBox.Show("代理已成功更新", "编辑成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }

    public class ProxyViewModel : INotifyPropertyChanged
    {
        private int _index;
        private string _proxyType;
        private string _serverAddress;
        private int _port;
        private string _username;
        private string _password;
        private ProxyStatus _status;
        private string _statusText;
        private int _latency;
        private bool _isSelected;
        private string _groupName = "默认";

        public int Index
        {
            get => _index;
            set
            {
                if (_index != value)
                {
                    _index = value;
                    OnPropertyChanged(nameof(Index));
                }
            }
        }

        public string ProxyType
        {
            get => _proxyType;
            set
            {
                if (_proxyType != value)
                {
                    _proxyType = value;
                    OnPropertyChanged(nameof(ProxyType));
                }
            }
        }

        public string ServerAddress
        {
            get => _serverAddress;
            set
            {
                if (_serverAddress != value)
                {
                    _serverAddress = value;
                    OnPropertyChanged(nameof(ServerAddress));
                }
            }
        }

        public int Port
        {
            get => _port;
            set
            {
                if (_port != value)
                {
                    _port = value;
                    OnPropertyChanged(nameof(Port));
                }
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged(nameof(Username));
                }
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged(nameof(Password));
                }
            }
        }

        // 检查代理是否需要认证
        public bool HasAuthentication => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);

        public ProxyStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public int Latency
        {
            get => _latency;
            set
            {
                if (_latency != value)
                {
                    _latency = value;
                    OnPropertyChanged(nameof(Latency));
                }
            }
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

        public string GroupName
        {
            get => _groupName;
            set
            {
                if (_groupName != value)
                {
                    _groupName = value;
                    OnPropertyChanged(nameof(GroupName));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum ProxyStatus
    {
        Unknown = 0,
        Testing = 1,
        Connected = 2,
        Failed = 3
    }

    public class ProxyGroup
    {
        public string Name { get; set; }
        public List<string> ProxyIds { get; set; } = new List<string>();
        
        public ProxyGroup()
        {
        }
        
        public ProxyGroup(string name)
        {
            Name = name;
        }
    }

    public class ImportProxyDialog : Window
    {
        public ObservableCollection<ProxyViewModel> ImportedProxies { get; private set; } = new ObservableCollection<ProxyViewModel>();
        
        private TextBox txtProxyList;
        private ComboBox cbProxyType;
        private string selectedProxyType = "HTTP"; // 默认代理类型
        
        public ImportProxyDialog()
        {
            Title = "导入代理";
            Width = 600;
            Height = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            
            // 提示标签
            var label = new TextBlock
            {
                Text = "请输入代理列表，支持以下格式：",
                Margin = new Thickness(10, 10, 10, 5),
                FontWeight = FontWeights.Bold
            };
            grid.Children.Add(label);
            Grid.SetRow(label, 0);
            
            // 格式提示
            var formatLabel = new TextBlock
            {
                Text = "1. 类型://地址:端口 - 例如：HTTP://10.0.0.1:8080 (自动识别类型)\n" +
                      "2. 地址:端口 - 例如：10.0.0.1:8080 (使用选择的代理类型)\n" +
                      "3. 地址:端口:用户名:密码 - 例如：10.0.0.1:8080:admin:pass (使用选择的代理类型)",
                Margin = new Thickness(10, 0, 10, 10),
                TextWrapping = TextWrapping.Wrap
            };
            grid.Children.Add(formatLabel);
            Grid.SetRow(formatLabel, 1);
            
            // 代理类型选择面板
            var typePanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                Margin = new Thickness(10, 0, 10, 10) 
            };
            
            var typeLabel = new TextBlock
            {
                Text = "选择代理类型：",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                FontWeight = FontWeights.Bold
            };
            
            cbProxyType = new ComboBox
            {
                Width = 120,
                Height = 30,
                SelectedIndex = 0
            };
            cbProxyType.Items.Add("HTTP");
            cbProxyType.Items.Add("HTTPS");
            cbProxyType.Items.Add("SOCKS5");
            cbProxyType.SelectionChanged += (s, e) => 
            {
                if (cbProxyType.SelectedItem != null)
                    selectedProxyType = cbProxyType.SelectedItem.ToString();
            };
            
            typePanel.Children.Add(typeLabel);
            typePanel.Children.Add(cbProxyType);
            
            // 说明文本
            var infoText = new TextBlock
            {
                Text = "注意：对于不包含类型的格式 (格式2和3)，将使用上方选择的代理类型",
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.DarkSlateBlue,
                FontStyle = FontStyles.Italic
            };
            typePanel.Children.Add(infoText);
            
            grid.Children.Add(typePanel);
            Grid.SetRow(typePanel, 2);
            
            // 输入框
            txtProxyList = new TextBox
            {
                Margin = new Thickness(10),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            };
            grid.Children.Add(txtProxyList);
            Grid.SetRow(txtProxyList, 3);
            
            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            
            var btnCancel = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };
            
            var btnImport = new Button
            {
                Content = "导入",
                Width = 80,
                Height = 30,
                IsDefault = true
            };
            btnImport.Click += BtnImport_Click;
            
            buttonPanel.Children.Add(btnCancel);
            buttonPanel.Children.Add(btnImport);
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 4);
            
            Content = grid;
        }
        
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var lines = txtProxyList.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;
                
                // 尝试匹配 类型://地址:端口 格式
                var typeMatch = Regex.Match(trimmedLine, @"^(https?|socks5)://([^:]+):(\d+)$", RegexOptions.IgnoreCase);
                if (typeMatch.Success)
                {
                    string proxyType = typeMatch.Groups[1].Value.ToUpper();
                    string serverAddress = typeMatch.Groups[2].Value;
                    
                    if (int.TryParse(typeMatch.Groups[3].Value, out int port) && port > 0 && port <= 65535)
                    {
                        ImportedProxies.Add(new ProxyViewModel
                        {
                            ProxyType = proxyType,
                            ServerAddress = serverAddress,
                            Port = port,
                            Status = ProxyStatus.Unknown,
                            StatusText = "未测试",
                            Latency = 0
                        });
                        successCount++;
                        continue; // 成功匹配，处理下一行
                    }
                }
                
                // 尝试匹配 地址:端口:用户名:密码 格式
                var authMatch = Regex.Match(trimmedLine, @"^([^:]+):(\d+):([^:]+):(.+)$");
                if (authMatch.Success)
                {
                    string serverAddress = authMatch.Groups[1].Value;
                    string username = authMatch.Groups[3].Value;
                    string password = authMatch.Groups[4].Value;
                    
                    if (int.TryParse(authMatch.Groups[2].Value, out int port) && port > 0 && port <= 65535)
                    {
                        ImportedProxies.Add(new ProxyViewModel
                        {
                            ProxyType = selectedProxyType, // 使用选择的代理类型
                            ServerAddress = serverAddress,
                            Port = port,
                            Username = username,
                            Password = password,
                            Status = ProxyStatus.Unknown,
                            StatusText = "未测试",
                            Latency = 0
                        });
                        successCount++;
                        continue; // 成功匹配，处理下一行
                    }
                }
                
                // 尝试匹配简单的 地址:端口 格式
                var simpleMatch = Regex.Match(trimmedLine, @"^([^:]+):(\d+)$");
                if (simpleMatch.Success)
                {
                    string serverAddress = simpleMatch.Groups[1].Value;
                    
                    if (int.TryParse(simpleMatch.Groups[2].Value, out int port) && port > 0 && port <= 65535)
                    {
                        ImportedProxies.Add(new ProxyViewModel
                        {
                            ProxyType = selectedProxyType, // 使用选择的代理类型
                            ServerAddress = serverAddress,
                            Port = port,
                            Status = ProxyStatus.Unknown,
                            StatusText = "未测试",
                            Latency = 0
                        });
                        successCount++;
                        continue; // 成功匹配，处理下一行
                    }
                }
            }
            
            if (ImportedProxies.Count > 0)
            {
                DialogResult = true;
                MessageBox.Show($"成功导入 {successCount} 个代理", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("没有找到有效的代理格式，请检查输入格式", "导入失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // 编辑代理对话框
    public class EditProxyDialog : Window
    {
        private ProxyViewModel _proxy;
        
        public EditProxyDialog(ProxyViewModel proxy)
        {
            _proxy = proxy;
            
            Title = "编辑代理";
            Width = 400;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            // 创建主网格
            var grid = new Grid();
            grid.Margin = new Thickness(10);
            
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 代理类型
            var lblType = new TextBlock { Text = "代理类型:", Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblType, 0);
            Grid.SetColumn(lblType, 0);
            
            var cbType = new ComboBox { Margin = new Thickness(0, 5, 0, 5), SelectedIndex = 0 };
            cbType.Items.Add("HTTP");
            cbType.Items.Add("HTTPS");
            cbType.Items.Add("SOCKS5");
            
            switch (proxy.ProxyType.ToUpper())
            {
                case "HTTP": cbType.SelectedIndex = 0; break;
                case "HTTPS": cbType.SelectedIndex = 1; break;
                case "SOCKS5": cbType.SelectedIndex = 2; break;
                default: cbType.SelectedIndex = 0; break;
            }
            
            Grid.SetRow(cbType, 0);
            Grid.SetColumn(cbType, 1);
            
            // 服务器地址
            var lblServer = new TextBlock { Text = "服务器地址:", Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblServer, 1);
            Grid.SetColumn(lblServer, 0);
            
            var txtServer = new TextBox { Text = proxy.ServerAddress, Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(txtServer, 1);
            Grid.SetColumn(txtServer, 1);
            
            // 端口
            var lblPort = new TextBlock { Text = "端口:", Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblPort, 2);
            Grid.SetColumn(lblPort, 0);
            
            var txtPort = new TextBox { Text = proxy.Port.ToString(), Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(txtPort, 2);
            Grid.SetColumn(txtPort, 1);
            
            // 用户名
            var lblUsername = new TextBlock { Text = "用户名:", Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblUsername, 3);
            Grid.SetColumn(lblUsername, 0);
            
            var txtUsername = new TextBox { Text = proxy.Username, Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(txtUsername, 3);
            Grid.SetColumn(txtUsername, 1);
            
            // 密码
            var lblPassword = new TextBlock { Text = "密码:", Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblPassword, 4);
            Grid.SetColumn(lblPassword, 0);
            
            var txtPassword = new PasswordBox { Margin = new Thickness(0, 5, 0, 5) };
            txtPassword.Password = proxy.Password ?? "";
            Grid.SetRow(txtPassword, 4);
            Grid.SetColumn(txtPassword, 1);
            
            // 分组
            var lblGroup = new TextBlock { Text = "所属分组:", Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(lblGroup, 5);
            Grid.SetColumn(lblGroup, 0);
            
            var cbGroup = new ComboBox { Margin = new Thickness(0, 5, 0, 5) };
            
            // 加载分组
            string json = File.ReadAllText("proxy_groups.json");
            var groups = JsonConvert.DeserializeObject<List<ProxyGroup>>(json);
            
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    cbGroup.Items.Add(group.Name);
                }
                
                // 选择当前分组
                int groupIndex = cbGroup.Items.IndexOf(proxy.GroupName);
                if (groupIndex >= 0)
                    cbGroup.SelectedIndex = groupIndex;
                else
                    cbGroup.SelectedIndex = 0;
            }
            
            Grid.SetRow(cbGroup, 5);
            Grid.SetColumn(cbGroup, 1);
            
            // 按钮面板
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            
            Grid.SetRow(buttonPanel, 6);
            Grid.SetColumnSpan(buttonPanel, 2);
            
            var btnSave = new Button { Content = "保存", Width = 80, Height = 30, Margin = new Thickness(5, 0, 0, 0) };
            var btnCancel = new Button { Content = "取消", Width = 80, Height = 30, Margin = new Thickness(10, 0, 0, 0) };
            
            btnSave.Click += (s, e) =>
            {
                // 验证输入
                if (string.IsNullOrWhiteSpace(txtServer.Text))
                {
                    MessageBox.Show("请输入服务器地址", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!int.TryParse(txtPort.Text, out int port) || port <= 0 || port > 65535)
                {
                    MessageBox.Show("请输入有效的端口号(1-65535)", "验证错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 更新代理信息
                proxy.ProxyType = cbType.SelectedItem.ToString();
                proxy.ServerAddress = txtServer.Text.Trim();
                proxy.Port = port;
                proxy.Username = txtUsername.Text.Trim();
                proxy.Password = txtPassword.Password;
                proxy.GroupName = cbGroup.SelectedItem.ToString();
                
                DialogResult = true;
                Close();
            };
            
            btnCancel.Click += (s, e) =>
            {
                DialogResult = false;
                Close();
            };
            
            buttonPanel.Children.Add(btnSave);
            buttonPanel.Children.Add(btnCancel);
            
            // 添加控件到网格
            grid.Children.Add(lblType);
            grid.Children.Add(cbType);
            grid.Children.Add(lblServer);
            grid.Children.Add(txtServer);
            grid.Children.Add(lblPort);
            grid.Children.Add(txtPort);
            grid.Children.Add(lblUsername);
            grid.Children.Add(txtUsername);
            grid.Children.Add(lblPassword);
            grid.Children.Add(txtPassword);
            grid.Children.Add(lblGroup);
            grid.Children.Add(cbGroup);
            grid.Children.Add(buttonPanel);
            
            Content = grid;
        }
    }
} 
