using web3script.Handler;
using web3script.Mode;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using web3script.Handler;
using web3script.Mode;
using web3script.Views;

namespace web3script.Views
{
    public partial class SSR : UserControl
    {
        private ObservableCollection<ProfileItem> _servers = new ObservableCollection<ProfileItem>();
        private ObservableCollection<ClashProxyPort> _clashProxyPorts = new ObservableCollection<ClashProxyPort>();
        private CoreHandler _coreHandler;
        private Config _config = new Config();
        private Dictionary<string, bool> _runningStatus = new Dictionary<string, bool>();
        private Dictionary<int, (bool isActive, DateTime lastCheck)> _portStatusCache = new Dictionary<int, (bool, DateTime)>();
        private const int PORT_CHECK_CACHE_DURATION_MS = 2000; // 缓存2秒

        public SSR()
        {
            InitializeComponent();

            // 确保bin目录存在
            EnsureBinFolderExists();

            // 初始化配置
            ConfigHandler.LoadConfig(ref _config);

            // 初始化日志处理器
            LogHandler.AddLog("SimpleV2ray 启动成功");

            // 初始化核心处理器
            _coreHandler = new CoreHandler();
            LogHandler.UpdateFunc += UpdateHandler;
            // 注册CoreHandler的日志事件到UpdateHandler
            _coreHandler.OutputDataReceived += UpdateHandler;

            // 初始化状态指示器
            UpdateRunningStatus(false);

            // 绑定服务器列表
            dgServers.ItemsSource = _servers;

            // 绑定Clash代理端口列表
            dgClashPorts.ItemsSource = _clashProxyPorts;

            // 双击事件
            dgServers.MouseDoubleClick += DgServers_MouseDoubleClick;
            dgServers.SelectionChanged += DgServers_SelectionChanged;

            // 加载服务器列表
            RefreshServers();

            // 显示版本信息 
            LogHandler.AddLog($"配置目录：{Utils.GetConfigPath()}");
            LogHandler.AddLog($"运行目录：{AppDomain.CurrentDomain.BaseDirectory}");
            LogHandler.AddLog($"核心目录：{Utils.GetBinPath()}");
        }

        private async Task<bool> IsPortListeningAsync(int port)
        {
            // 检查缓存
            if (_portStatusCache.TryGetValue(port, out var cache))
            {
                if ((DateTime.Now - cache.lastCheck).TotalMilliseconds < PORT_CHECK_CACHE_DURATION_MS)
                {
                    return cache.isActive;
                }
            }

            try
            {
                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var connectTask = client.ConnectAsync("127.0.0.1", port);
                    var timeoutTask = Task.Delay(100); // 100ms超时
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    bool isActive = completedTask == connectTask && client.Connected;

                    // 更新缓存
                    _portStatusCache[port] = (isActive, DateTime.Now);

                    return isActive;
                }
            }
            catch
            {
                // 更新缓存
                _portStatusCache[port] = (false, DateTime.Now);
                return false;
            }
        }

        private async void DgServers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgServers.SelectedItem is ProfileItem selectedItem)
            {
                // 清空当前显示的端口列表
                _clashProxyPorts.Clear();

                if (selectedItem.coreType?.ToLower()?.Contains("clash") == true)
                {
                    // 显示Clash端口面板
                    if (gridClashPorts != null)
                        gridClashPorts.Visibility = Visibility.Visible;

                    // 确保端口列表已初始化
                    if (selectedItem.clashProxyPorts == null)
                    {
                        selectedItem.clashProxyPorts = new ObservableCollection<ClashProxyPort>();
                    }

                    // 异步更新端口状态
                    await UpdatePortStatusesAsync(selectedItem);
                }
                else
                {
                    // 隐藏Clash端口面板
                    if (gridClashPorts != null)
                        gridClashPorts.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async Task UpdatePortStatusesAsync(ProfileItem selectedItem)
        {
            try
            {
                var updateTasks = new List<Task>();

                foreach (var port in selectedItem.clashProxyPorts)
                {
                    // 添加到显示列表
                    _clashProxyPorts.Add(port);

                    // 异步检查端口状态
                    var checkTask = Task.Run(async () =>
                    {
                        bool isActive = await IsPortListeningAsync(port.port);
                        await Dispatcher.InvokeAsync(() =>
                        {
                            port.isActive = isActive;
                            LogHandler.AddLog($"端口 {port.port} ({port.name}) 状态: {(isActive ? "活跃" : "未激活")}");
                        });
                    });

                    updateTasks.Add(checkTask);
                }

                // 等待所有端口检查完成
                await Task.WhenAll(updateTasks);

                LogHandler.AddLog($"已加载 {_clashProxyPorts.Count} 个代理端口配置");

                // 刷新UI
                await Dispatcher.InvokeAsync(() => dgClashPorts.Items.Refresh());
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"更新端口状态时出错: {ex.Message}");
            }
        }

        private void EnsureBinFolderExists()
        {
            string binPath = Utils.GetBinPath();
            if (!Directory.Exists(binPath))
            {
                try
                {
                    Directory.CreateDirectory(binPath);
                    LogHandler.AddLog($"已创建核心目录：{binPath}");
                    LogHandler.AddLog("请在此目录下放置v2ray/xray等核心文件");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建核心目录失败：{ex.Message}");
                }
            }
        }

        private void DgServers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgServers.SelectedItem is ProfileItem selectedItem)
            {
                EditServer(selectedItem);
            }
        }

        private void EditServer(ProfileItem item)
        {
            var window = new AddServerWindow(item);
            if (window.ShowDialog() == true)
            {
                RefreshServers();
            }
        }

        private void UpdateRunningStatus(bool isRunning)
        {
            // 确保在UI线程执行
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateRunningStatus(isRunning));
                return;
            }

            if (isRunning)
            {
                txtStatus.Text = "正在运行";
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            else
            {
                txtStatus.Text = "未运行";
                txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        private async void UpdateHandler(object sender, LogEventArgs e)
        {
            // 确保在UI线程执行，但避免过多的Invoke导致阻塞
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => UpdateHandler(sender, e));
                return;
            }

            string content = e.content;
            if (txtLog.LineCount > 100)
            {
                txtLog.Clear();
            }
            // 使用更高效的方式添加日志，避免每次都滚动
            bool shouldScroll = txtLog.VerticalOffset >= txtLog.ExtentHeight - txtLog.ViewportHeight - 10;
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + " " + content + Environment.NewLine);
            if (shouldScroll)
            {
                txtLog.ScrollToEnd();
            }

            // 获取当前选定的服务器
            var selectedServer = dgServers.SelectedItem as ProfileItem;

            // 根据日志内容更新运行状态
            if (selectedServer != null)
            {
                // 对于Clash日志，当检测到端口监听时标记为运行状态
                if (content.Contains("proxy listening at"))
                {
                    // 提取端口号
                    var portMatch = System.Text.RegularExpressions.Regex.Match(content, @"proxy listening at.*?:(\d+)");
                    if (portMatch.Success && int.TryParse(portMatch.Groups[1].Value, out int port))
                    {
                        // 检查这个端口是否属于当前选中的服务器
                        bool isPortBelongsToServer = selectedServer.clashProxyPorts.Any(p => p.port == port);

                        if (isPortBelongsToServer && !selectedServer.isRunning)
                        {
                            selectedServer.isRunning = true;
                            if (_runningStatus.ContainsKey(selectedServer.indexId))
                            {
                                _runningStatus[selectedServer.indexId] = true;
                            }
                            else
                            {
                                _runningStatus.Add(selectedServer.indexId, true);
                            }

                            // 更新全局状态指示器
                            UpdateRunningStatus(true);

                            // 刷新UI
                            dgServers.Items.Refresh();

                            // 异步刷新Clash端口状态
                            await Task.Run(() => RefreshClashPortStatus());
                        }
                    }
                }
                else if (content.Contains("start service") || content.Contains("started") || content.Contains("listening on"))
                {
                    // 检查是否包含当前服务器的端口信息
                    bool isServerPort = false;
                    if (selectedServer.clashProxyPorts != null && selectedServer.clashProxyPorts.Any())
                    {
                        foreach (var proxyPort in selectedServer.clashProxyPorts)
                        {
                            if (content.Contains($"port {proxyPort.port}") || content.Contains($":{proxyPort.port}"))
                            {
                                isServerPort = true;
                                break;
                            }
                        }
                    }

                    if (isServerPort && !selectedServer.isRunning)
                    {
                        selectedServer.isRunning = true;
                        if (_runningStatus.ContainsKey(selectedServer.indexId))
                        {
                            _runningStatus[selectedServer.indexId] = true;
                        }
                        else
                        {
                            _runningStatus.Add(selectedServer.indexId, true);
                        }

                        // 更新全局状态指示器
                        UpdateRunningStatus(true);

                        // 刷新UI
                        dgServers.Items.Refresh();
                    }
                }
                else if (content.Contains("stop") || content.Contains("stopped") || content.Contains("exit") || content.Contains("error"))
                {
                    // 对于bolt.Close()错误，这是Clash的正常现象，不应标记为停止
                    if (content.Contains("bolt.Close()") || content.Contains("funlock error"))
                    {
                        // 忽略这些特定的Clash错误，它们不影响服务的运行状态
                        return;
                    }
                    else if (content.Contains("failed") || content.Contains("error"))
                    {
                        // 重大错误才标记为停止
                        if (selectedServer.isRunning)
                        {
                            selectedServer.isRunning = false;
                            if (_runningStatus.ContainsKey(selectedServer.indexId))
                            {
                                _runningStatus[selectedServer.indexId] = false;
                            }

                            // 更新全局状态指示器
                            UpdateRunningStatus(false);

                            // 刷新UI
                            dgServers.Items.Refresh();
                        }
                    }
                    else if (content.Contains("stop") || content.Contains("stopped") || content.Contains("exit"))
                    {
                        if (selectedServer.isRunning)
                        {
                            selectedServer.isRunning = false;
                            if (_runningStatus.ContainsKey(selectedServer.indexId))
                            {
                                _runningStatus[selectedServer.indexId] = false;
                            }

                            // 更新全局状态指示器
                            UpdateRunningStatus(false);

                            // 刷新UI
                            dgServers.Items.Refresh();
                        }
                    }
                }
            }
        }

        private void RefreshServers()
        {
            _servers.Clear();
            var profiles = ConfigHandler.GetProfiles();

            foreach (var item in profiles)
            {
                _servers.Add(item);
            }

            LogHandler.AddLog($"已加载 {profiles.Count} 个服务器配置");

            // 根据选择的服务器高亮显示
            if (!string.IsNullOrEmpty(_config.indexId) && dgServers.Items.Count > 0)
            {
                foreach (var item in dgServers.Items)
                {
                    if (item is ProfileItem profile && profile.indexId == _config.indexId)
                    {
                        dgServers.SelectedItem = item;
                        dgServers.ScrollIntoView(item);
                        break;
                    }
                }
            }
        }

        private void btnAddCustomServer_Click(object sender, RoutedEventArgs e)
        {
            var profileItem = new ProfileItem
            {
                configType = "custom"
            };

            var window = new AddServerWindow(profileItem);
            if (window.ShowDialog() == true)
            {
                RefreshServers();
            }
        }

        private void btnDeleteServer_Click(object sender, RoutedEventArgs e)
        {
            if (dgServers.SelectedItem is ProfileItem selectedItem)
            {
                MessageBoxResult result = MessageBox.Show(
                    $"确定要删除服务器 '{selectedItem.remarks}' 吗？此操作不可撤销。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DeleteServer(selectedItem);
                }
            }
        }

        private void DeleteServer(ProfileItem item)
        {
            try
            {
                // 如果服务器正在运行，先停止它
                if (item.isRunning)
                {
                    StopServer(item);
                }

                // 从配置中移除
                if (_config.profileItems != null)
                {
                    _config.profileItems.RemoveAll(p => p.indexId == item.indexId);
                    ConfigHandler.SaveConfig(ref _config);
                }

                // 从列表中移除
                _servers.Remove(item);

                // 从运行状态字典中移除
                if (_runningStatus.ContainsKey(item.indexId))
                {
                    _runningStatus.Remove(item.indexId);
                }

                LogHandler.AddLog($"已删除服务器: {item.remarks}");
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"删除服务器时出错: {ex.Message}");
                MessageBox.Show($"删除服务器时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            var selectedServer = dgServers.SelectedItem as ProfileItem;
            if (selectedServer != null)
            {
                // 已经在运行状态则不重复启动
                if (selectedServer.isRunning)
                {
                    LogHandler.AddLog($"服务器 {selectedServer.remarks} 已经在运行中");
                    return;
                }

                // 禁用所有按钮，防止操作冲突
                btnStart.IsEnabled = false;
                btnStop.IsEnabled = false;
                btnAddCustomServer.IsEnabled = false;
                btnDeleteServer.IsEnabled = false;

                try
                {
                    LogHandler.AddLog($"选中服务器: ID={selectedServer.indexId}, 名称={selectedServer.remarks}, 配置类型={selectedServer.configType}");

                    // 检查核心文件是否存在
                    string coreType = string.IsNullOrEmpty(selectedServer.coreType) ? "v2fly" : selectedServer.coreType;
                    string exeName = GetCoreExeName(coreType);
                    string exePath = Path.Combine(Utils.GetBinPath(), exeName);

                    LogHandler.AddLog($"核心类型: {coreType}, 可执行文件: {exeName}, 路径: {exePath}");

                    if (!File.Exists(exePath))
                    {
                        string errorMsg = $"找不到核心文件：{exePath}\n请先下载并放置核心文件到bin目录";
                        MessageBox.Show(errorMsg, "启动失败");
                        LogHandler.AddLog(errorMsg);
                        selectedServer.isRunning = false;
                        return;
                    }

                    // 预先将状态设置为"连接中"
                    txtStatus.Text = "正在连接...";
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);

                    // 确保所有操作完成后才启动服务
                    await Task.Delay(100); // 给UI刷新的时间

                    // 异步启动核心进程
                    await Task.Run(() => StartServer(selectedServer));

                    // 如果是Clash配置，刷新端口列表
                    if (selectedServer.coreType?.ToLower()?.Contains("clash") == true)
                    {
                        // 刷新UI显示端口列表
                        RefreshClashPortStatus();
                        LogHandler.AddLog("已显示Clash端口配置");
                    }

                    // 更新全局状态指示器
                    UpdateRunningStatus(selectedServer.isRunning);
                }
                catch (Exception ex)
                {
                    LogHandler.AddLog($"启动服务时出错: {ex.Message}");
                    MessageBox.Show($"启动服务时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    selectedServer.isRunning = false;
                    UpdateRunningStatus(false);
                }
                finally
                {
                    // 恢复按钮状态
                    btnStart.IsEnabled = true;
                    btnStop.IsEnabled = true;
                    btnAddCustomServer.IsEnabled = true;
                    btnDeleteServer.IsEnabled = true;
                }
            }
        }

        private string GetCoreExeName(string coreType)
        {
            switch (coreType.ToLower())
            {
                case "clash_meta":
                    return "clashmeta.exe";
                case "clash":
                    return "clash-windows-amd64.exe";
                case "v2fly":
                default:
                    return "v2ray-windows-amd64.exe";
            }
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            // 禁用所有按钮，防止操作冲突
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = false;
            btnAddCustomServer.IsEnabled = false;
            btnDeleteServer.IsEnabled = false;

            try
            {
                var selectedServer = dgServers.SelectedItem as ProfileItem;
                if (selectedServer != null && selectedServer.isRunning)
                {
                    LogHandler.AddLog($"正在停止服务: {selectedServer.remarks}...");

                    // 预先将状态设置为正在停止
                    txtStatus.Text = "正在停止...";
                    txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Orange);

                    // 确保UI有时间更新
                    await Task.Delay(100);

                    // 使用超时机制确保停止操作不会永久阻塞
                    var stopTask = Task.Run(() => StopServer(selectedServer));

                    // 最多等待10秒
                    if (await Task.WhenAny(stopTask, Task.Delay(10000)) != stopTask)
                    {
                        LogHandler.AddLog("停止服务超时，可能有进程未正常关闭");
                    }

                    // 无论如何都更新UI状态为停止
                    selectedServer.isRunning = false;

                    // 更新全局状态指示器
                    UpdateRunningStatus(false);

                    // 简单刷新UI
                    if (selectedServer.coreType?.ToLower()?.Contains("clash") == true)
                    {
                        RefreshClashPortStatus();
                    }

                    // 确保有足够时间完成清理工作再启动新服务
                    await Task.Delay(500);
                }
                else
                {
                    LogHandler.AddLog("没有正在运行的服务需要停止");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"停止服务时出错: {ex.Message}");
                MessageBox.Show($"停止服务时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                btnStart.IsEnabled = true;
                btnStop.IsEnabled = true;
                btnAddCustomServer.IsEnabled = true;
                btnDeleteServer.IsEnabled = true;
            }
        }

        private void btnClearLog_Click(object sender, RoutedEventArgs e)
        {
            txtLog.Clear();
            LogHandler.AddLog("日志已清除");
        }

        // 启动特定配置的代理服务
        public void StartServer(ProfileItem server)
        {
            if (server == null)
                return;

            try
            {
                // 检查核心文件是否存在
                string coreType = string.IsNullOrEmpty(server.coreType) ? "v2fly" : server.coreType;
                string exeName = GetCoreExeName(coreType);
                string exePath = Path.Combine(Utils.GetBinPath(), exeName);

                if (!File.Exists(exePath))
                {
                    LogHandler.AddLog($"找不到核心文件：{exePath}，无法启动服务");
                    server.isRunning = false;
                    return;
                }

                // 确保配置中包含此服务器
                if (_config.profileItems == null)
                    _config.profileItems = new List<ProfileItem>();

                if (!_config.profileItems.Any(p => p.indexId == server.indexId))
                {
                    _config.profileItems.Add(server);
                    ConfigHandler.SaveConfig(ref _config);
                }

                // 设置当前配置的索引
                _config.indexId = server.indexId;
                ConfigHandler.SetDefaultServerIndex(ref _config, server.indexId);

                // 启动核心
                LogHandler.AddLog($"正在启动服务器: {server.remarks}");
                _coreHandler.LoadCore(_config);

                // 更新运行状态
                server.isRunning = true;
                _runningStatus[server.indexId] = true;

                LogHandler.AddLog($"服务器 {server.remarks} 启动成功");
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"启动服务器 {server.remarks} 时出错: {ex.Message}");
                server.isRunning = false;
                _runningStatus[server.indexId] = false;
            }
        }

        // 停止特定配置的代理服务
        public void StopServer(ProfileItem server)
        {
            if (server == null)
                return;

            try
            {
                LogHandler.AddLog($"正在停止服务器: {server.remarks}");

                // 停止核心进程
                _coreHandler.StopServer(server.indexId);

                // 更新当前服务器的运行状态
                server.isRunning = false;
                _runningStatus[server.indexId] = false;

                LogHandler.AddLog($"服务器 {server.remarks} 已停止");
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"停止服务器 {server.remarks} 时出错: {ex.Message}");
            }
        }

        // 这个方法会被主窗口调用，用于安全关闭所有代理服务
        public void CloseAllServices()
        {
            try
            {
                LogHandler.AddLog("正在安全关闭所有代理服务...");

                // 使用CoreHandler的方法安全停止所有服务
                if (_coreHandler != null)
                {
                    _coreHandler.CoreStop();
                    LogHandler.AddLog("所有代理服务已关闭");

                    // 更新所有服务器状态为已停止
                    foreach (var server in _servers)
                    {
                        server.isRunning = false;
                        if (_runningStatus.ContainsKey(server.indexId))
                        {
                            _runningStatus[server.indexId] = false;
                        }
                    }

                    // 更新UI状态
                    UpdateRunningStatus(false);
                }
                else
                {
                    LogHandler.AddLog("警告: CoreHandler为null，无法关闭代理服务");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"关闭代理服务时出错: {ex.Message}");
            }
        }

        private async void RefreshClashPortStatus()
        {
            // 确保在UI线程执行
            if (!Dispatcher.CheckAccess())
            {
                await Dispatcher.InvokeAsync(() => RefreshClashPortStatus());
                return;
            }

            try
            {
                if (dgServers.SelectedItem is ProfileItem selectedItem &&
                    selectedItem.coreType?.ToLower()?.Contains("clash") == true)
                {
                    // 清空当前显示
                    _clashProxyPorts.Clear();

                    // 确保端口列表已初始化
                    if (selectedItem.clashProxyPorts == null)
                    {
                        selectedItem.clashProxyPorts = new ObservableCollection<ClashProxyPort>();
                    }

                    // 异步更新端口状态
                    await UpdatePortStatusesAsync(selectedItem);
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"刷新端口配置显示时出错: {ex.Message}");
            }
        }
    }
}