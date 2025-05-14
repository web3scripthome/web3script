using web3script.Models;
using web3script.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows;
using web3script.ContractScript;
using System.Diagnostics;
using Task = web3script.Models.Task;
using web3script.ConScript;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace web3script.Services
{
    /// <summary>
    /// 报表服务类，用于收集和处理报表数据
    /// </summary>
    public class ReportService
    {
        private readonly ProjectService _projectService;
        private readonly TaskService _taskService;
        private readonly WalletService _walletService;
        private MonadStaking _monadStaking;
        
        // 标志位，控制是否需要加载余额
        private bool _shouldLoadBalances = false;
        
        private static ReportService _instance;
        public static ReportService Instance => _instance ?? (_instance = new ReportService());
        
        private readonly string _balancesFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "balances.json");
        
        // 余额缓存，避免频繁RPC请求
        private ConcurrentDictionary<string, decimal> _balanceCache;
        private ConcurrentDictionary<string, Dictionary<string, decimal>> _tokenBalanceCache;
        
        private SemaphoreSlim _rpcSemaphore = new SemaphoreSlim(5, 5); // 限制并发RPC请求数
        
        public ReportService()
        {
            _monadStaking = new MonadStaking();
            _projectService = new ProjectService();
            _taskService = TaskService.Instance;
            _walletService = new WalletService();
            
            _balanceCache = new ConcurrentDictionary<string, decimal>();
            _tokenBalanceCache = new ConcurrentDictionary<string, Dictionary<string, decimal>>();
            
            // 创建数据目录
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
        }
        
        /// <summary>
        /// 设置是否应该加载余额
        /// </summary>
        public void SetShouldLoadBalances(bool shouldLoad)
        {
            _shouldLoadBalances = shouldLoad;
        }
        
        /// <summary>
        /// 获取所有报表数据
        /// </summary>
        public ObservableCollection<BaseReportData> GetAllReportData(
            string projectFilter = null, 
            string groupFilter = null, 
            string interactionTypeFilter = null,
            bool loadBalances = false)
        {
            var result = new ObservableCollection<BaseReportData>();
            
            // 检查是否有任务正在运行
            if (_taskService.IsAnyTaskRunning())
            {
                MessageBox.Show("当前有任务正在执行，无法获取最新报表数据。请等待任务完成后再试。", 
                                "任务运行中", MessageBoxButton.OK, MessageBoxImage.Information);
                return result;
            }
            
            // 获取所有项目
            var projects = _projectService.GetProjects();
            
            // 如果指定了项目过滤条件，则只选择匹配的项目
            if (!string.IsNullOrEmpty(projectFilter))
            {
                projects = projects.Where(p => p.Name == projectFilter).ToList();
            }
            
            // 获取所有钱包
            var wallets = _walletService.GetWallets();
            if (!string.IsNullOrEmpty(groupFilter) && groupFilter != "全部")
            {
                wallets = wallets.Where(w => _walletService.GetWalletGroup(w.Id) == groupFilter).ToList();
            }
            
            // 获取所有执行记录
            var allExecutionRecords = _taskService.GetAllExecutionRecords();
            
            // 按项目和钱包分组统计
            foreach (var project in projects)
            {
                foreach (var wallet in wallets)
                {
                    // 获取当前项目和钱包的执行记录
                    var records = allExecutionRecords.Where(r => 
                        r.ProjectName == project.Name && 
                        r.WalletAddress == wallet.Address).ToList();
                    
                    // 如果指定了交互类型筛选条件，且不是"全部"，则只选择匹配的交互类型记录
                    if (!string.IsNullOrEmpty(interactionTypeFilter) && interactionTypeFilter != "全部")
                    {
                        // 使用模糊匹配而非精确匹配
                        records = records.Where(r => r.TaskName != null && 
                            r.TaskName.IndexOf(interactionTypeFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                        // 如果筛选后没有记录，则跳过当前钱包
                        if (!records.Any())
                            continue;
                    }
                    
                    if (records.Any())
                    {
                        // 创建对应项目类型的报表数据
                        var reportData = ReportDataFactory.CreateReportData(project.Name);
                        
                        // 填充基本数据
                        reportData.ProjectName = project.Name;
                        reportData.WalletAddress = wallet.Address;
                        reportData.InteractionCount = records.Count;
                        reportData.SuccessCount = records.Count(r => r.Success == true);
                        reportData.FailedCount = records.Count(r => r.Success == false);
                        
                        // 初始化余额为0，仅当需要时才加载
                        reportData.CurrentBalance = 0;
                        
                        // 初始化任务余额字典
                        reportData.TaskBalances = new Dictionary<string, decimal>();
                        
                        // 如果是MonadReportData，则设置交互类型
                        if (reportData is MonadReportData monadData && 
                            !string.IsNullOrEmpty(interactionTypeFilter) && 
                            interactionTypeFilter != "全部")
                        {
                            // 根据筛选条件确定交互类型
                            string interactionType = DetermineInteractionTypeFromName(interactionTypeFilter);
                            monadData.InteractionType = interactionType;
                        }
                        
                        // 添加到结果中
                        result.Add(reportData);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 从任务名称确定交互类型
        /// </summary>
        private string DetermineInteractionTypeFromName(string taskName)
        {
            if (string.IsNullOrEmpty(taskName))
                return "gmon"; // 默认为gMon
                
            taskName = taskName.ToLower();
            
            // 精确匹配特定任务名称
            if (taskName == "自动质押aprmon(apriori)" || taskName == "自动质押aprmon(apriori)")
                return "aprmon"; 
            // 更精确的匹配，优先级：aprmon > smon > gmon
            if (taskName.Contains("aprmon") || taskName.Contains("apriori"))
                return "aprmon";
            else if (taskName.Contains("smon"))
                return "smon";
            else if (taskName.Contains("gmon"))
                return "gmon";
            
            // 其他关键字匹配
            if (taskName.Contains("质押aprmon") || taskName.Contains("自动质押aprmon") || 
                taskName.Contains("aprmon质押") || taskName.Contains("aprmon自动质押"))
                return "aprmon";
                
            return "gmon"; // 默认为gMon
        }
        
        /// <summary>
        /// 异步加载所有报表项的余额数据
        /// </summary>
        public async System.Threading.Tasks.Task LoadBalancesAsync(ObservableCollection<BaseReportData> reportData)
        {
            if (reportData == null || reportData.Count == 0)
                return;

            // 使用SemaphoreSlim控制并发，限制为1表示同一时刻只有一个请求执行
            using (var semaphore = new System.Threading.SemaphoreSlim(1, 1))
            {
                foreach (var data in reportData)
                {
                    try
                    {
                        // 等待获取信号量，确保同一时刻只有一个请求
                        await semaphore.WaitAsync();
                        
                        try
                        {
                            // 加载单个项目的所有余额（原生代币和特定代币）
                            await LoadAllBalancesForSingleItem(data);
                        }
                        finally
                        {
                            // 无论成功失败，都释放信号量
                            semaphore.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"加载余额出错: {ex.Message}");
                    }
                }
            }
        }
        
        /// <summary>
        /// 异步加载单个报表项的余额数据
        /// </summary>
        public async System.Threading.Tasks.Task LoadSingleItemBalancesAsync(BaseReportData reportData)
        {
            if (reportData == null)
                return;
                
            try
            {
                // 顺序加载所有余额
                await LoadAllBalancesForSingleItem(reportData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载单个项目余额出错: {ex.Message}");
                MessageBox.Show($"获取钱包 {reportData.WalletAddress} 余额失败: {ex.Message}", 
                            "余额获取错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        /// <summary>
        /// 加载单个项目的所有余额数据（包括原生代币和特定代币）
        /// </summary>
        private async System.Threading.Tasks.Task LoadAllBalancesForSingleItem(BaseReportData reportData)
        {
            try
            {
                // 获取RPC信号量，限制并发请求
                await _rpcSemaphore.WaitAsync();
                
                try
                {
                    // 根据项目类型获取余额
                    switch (reportData.ProjectName.ToLower())
                    {
                        case "monad":
                            await GetMonadBalances(reportData);
                            break;
                            
                        case "ethereum":
                            await GetEthereumBalances(reportData);
                            break;
                            
                        default:
                            // 对于未知项目，使用测试数据
                            await GetTestBalances(reportData);
                            break;
                    }
                    
                    // 更新最后刷新时间
                    reportData.LastBalanceUpdate = DateTime.Now;
                }
                finally
                {
                    // 释放信号量
                    _rpcSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载余额失败 - {reportData.WalletAddress}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取Monad项目的余额
        /// </summary>
        private async System.Threading.Tasks.Task GetMonadBalances(BaseReportData reportData)
        {
            // 获取原生代币余额（MON）
            reportData.CurrentBalance = await QueryNativeTokenBalance(reportData.WalletAddress, "monad");
            
            // 更新余额缓存
            string cacheKey = $"{reportData.ProjectName}_{reportData.WalletAddress}";
            _balanceCache[cacheKey] = reportData.CurrentBalance;
            
            // 根据交互类型查询特定代币余额
            var tokenBalances = await QueryTokenBalance(reportData.WalletAddress, reportData.InteractionType, "monad");
            reportData.TaskBalances = tokenBalances;
            
            // 更新代币余额缓存
            string tokenCacheKey = $"{reportData.ProjectName}_{reportData.WalletAddress}_{reportData.InteractionType}";
            _tokenBalanceCache[tokenCacheKey] = tokenBalances;
        }
        
        /// <summary>
        /// 获取Ethereum项目的余额
        /// </summary>
        private async System.Threading.Tasks.Task GetEthereumBalances(BaseReportData reportData)
        {
            // 获取原生代币余额（ETH）
            reportData.CurrentBalance = await QueryNativeTokenBalance(reportData.WalletAddress, "ethereum");
            
            // 更新余额缓存
            string cacheKey = $"{reportData.ProjectName}_{reportData.WalletAddress}";
            _balanceCache[cacheKey] = reportData.CurrentBalance;
            
            // 根据交互类型查询特定代币余额
            var tokenBalances = await QueryTokenBalance(reportData.WalletAddress, reportData.InteractionType, "ethereum");
            reportData.TaskBalances = tokenBalances;
            
            // 更新代币余额缓存
            string tokenCacheKey = $"{reportData.ProjectName}_{reportData.WalletAddress}_{reportData.InteractionType}";
            _tokenBalanceCache[tokenCacheKey] = tokenBalances;
        }
        
        /// <summary>
        /// 获取测试余额
        /// </summary>
        private async System.Threading.Tasks.Task GetTestBalances(BaseReportData reportData)
        {
            // 随机生成测试余额
            Random random = new Random();
            decimal nativeBalance = (decimal)(random.NextDouble() * 10);
            
            reportData.CurrentBalance = nativeBalance;
            
            // 更新余额缓存
            string cacheKey = $"{reportData.ProjectName}_{reportData.WalletAddress}";
            _balanceCache[cacheKey] = nativeBalance;
            
            // 生成测试代币余额
            var tokenBalances = new Dictionary<string, decimal>();
            
            switch (reportData.InteractionType?.ToLower())
            {
                case "gmon":
                    tokenBalances["gMON"] = (decimal)(random.NextDouble() * 100);
                    break;
                case "smon":
                    tokenBalances["sMON"] = (decimal)(random.NextDouble() * 50);
                    break;
                case "lp":
                    tokenBalances["LP"] = (decimal)(random.NextDouble() * 1);
                    break;
                default:
                    tokenBalances["代币"] = (decimal)(random.NextDouble() * 20);
                    break;
            }
            
            reportData.TaskBalances = tokenBalances;
            
            // 更新代币余额缓存
            string tokenCacheKey = $"{reportData.ProjectName}_{reportData.WalletAddress}_{reportData.InteractionType}";
            _tokenBalanceCache[tokenCacheKey] = tokenBalances;
            
            // 模拟网络延迟
            await System.Threading.Tasks.Task.Delay(200);
        }
        
        /// <summary>
        /// 查询原生代币余额
        /// </summary>
        private async System.Threading.Tasks.Task<decimal> QueryNativeTokenBalance(string walletAddress, string projectName)
        {
            // 这里实现实际的余额查询逻辑
            // 以下为测试实现
            Random random = new Random();
            decimal balance = 0;
            
            switch (projectName.ToLower())
            {
                case "monad":
                    balance = (decimal)(random.NextDouble() * 10);
                    break;
                case "ethereum":
                    balance = (decimal)(random.NextDouble() * 5);
                    break;
                default:
                    balance = (decimal)(random.NextDouble() * 100);
                    break;
            }
            
            // 模拟网络延迟
            await System.Threading.Tasks.Task.Delay(100);
            
            return balance;
        }
        
        /// <summary>
        /// 查询特定代币余额
        /// </summary>
        private async System.Threading.Tasks.Task<Dictionary<string, decimal>> QueryTokenBalance(string walletAddress, string interactionType, string projectName)
        {
            // 这里可以实现实际的代币余额查询逻辑
            // 根据交互类型和项目查询不同的代币余额
            
            var result = new Dictionary<string, decimal>();
            Random random = new Random();
            
            // 模拟网络延迟
            await System.Threading.Tasks.Task.Delay(100);
            
            switch (projectName.ToLower())
            {
                case "monad":
                    switch (interactionType?.ToLower())
                    {
                        case "gmon":
                            result["gMON"] = (decimal)(random.NextDouble() * 100);
                            break;
                        case "smon":
                            result["sMON"] = (decimal)(random.NextDouble() * 50);
                            break;
                        case "lp":
                            result["LP"] = (decimal)(random.NextDouble() * 1);
                            break;
                        default:
                            result["MON"] = (decimal)(random.NextDouble() * 20);
                            break;
                    }
                    break;
                    
                case "ethereum":
                    switch (interactionType?.ToLower())
                    {
                        case "eth交易":
                            result["ETH"] = (decimal)(random.NextDouble() * 2);
                            break;
                        case "质押eth":
                            result["stETH"] = (decimal)(random.NextDouble() * 3);
                            break;
                        case "usdc兑换":
                            result["USDC"] = (decimal)(random.NextDouble() * 1000);
                            break;
                        default:
                            result["ETH"] = (decimal)(random.NextDouble() * 1);
                            break;
                    }
                    break;
                    
                default:
                    result["代币"] = (decimal)(random.NextDouble() * 50);
                    break;
            }
            
            return result;
        }
        
        /// <summary>
        /// 获取所有项目名称列表
        /// </summary>
        public List<string> GetAllProjectNames()
        {
            var projects = _projectService.GetProjects();
            return projects.Select(p => p.Name).OrderBy(n => n).ToList();
        }
        
        /// <summary>
        /// 获取所有钱包分组名称列表
        /// </summary>
        public List<string> GetAllWalletGroups()
        {
            return _walletService.GetGroups().Select(g => g.Name).OrderBy(n => n).ToList();
        }
        
        /// <summary>
        /// 获取所有交互类型列表（来自任务名称）
        /// </summary>
        public List<string> GetAllInteractionTypes(string projectName = null)
        {
            var query = _taskService.Tasks.AsEnumerable();
            
            // Filter by project if specified
            if (!string.IsNullOrEmpty(projectName) && projectName != "全部")
            {
                Debug.WriteLine($"按项目筛选交互类型: {projectName}");
                query = query.Where(t => t.ProjectName == projectName);
            }
            else
            {
                Debug.WriteLine("获取所有项目的交互类型");
            }
            
            var result = query
                .Select(t => t.Name)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
                
            Debug.WriteLine($"找到 {result.Count} 个交互类型");
            return result;
        }

        /// <summary>
        /// 保存余额数据到本地文件
        /// </summary>
        public void SaveBalancesToLocal(ObservableCollection<BaseReportData> reportData)
        {
            if (reportData == null || reportData.Count == 0)
                return;
                
            try
            {
                // 创建一个字典，键为钱包地址，值为该地址的所有余额信息
                var balanceData = new Dictionary<string, object>();
                
                foreach (var item in reportData)
                {
                    // 跳过没有余额的项
                    if (item.CurrentBalance == 0 && (item.TaskBalances == null || !item.TaskBalances.Any()))
                        continue;
                        
                    // 创建余额信息对象
                    var balanceInfo = new
                    {
                        ProjectName = item.ProjectName,
                        CurrentBalance = item.CurrentBalance,
                        TaskBalances = item.TaskBalances?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, decimal>()
                    };
                    
                    // 保存到字典中
                    string balanceKey = $"{item.ProjectName}_{item.WalletAddress}";
                    balanceData[balanceKey] = balanceInfo;
                }
                
                // 如果没有余额数据，则跳过保存
                if (!balanceData.Any())
                    return;
                    
                // 序列化为JSON并保存到文件
                string json = JsonConvert.SerializeObject(balanceData, Formatting.Indented);
                File.WriteAllText(_balancesFilePath, json);
                
                Debug.WriteLine($"成功保存余额数据到本地文件，共 {balanceData.Count} 条记录");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存余额数据到本地文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从本地文件加载余额数据
        /// </summary>
        public void LoadBalancesFromLocal(ObservableCollection<BaseReportData> reportData)
        {
            if (reportData == null || reportData.Count == 0)
                return;
                
            try
            {
                // 检查文件是否存在
                if (!File.Exists(_balancesFilePath))
                {
                    Debug.WriteLine("余额数据文件不存在");
                    return;
                }
                    
                // 读取并反序列化JSON
                string json = File.ReadAllText(_balancesFilePath);
                var balanceData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                
                if (balanceData == null || !balanceData.Any())
                {
                    Debug.WriteLine("余额数据为空或格式错误");
                    return;
                }
                    
                // 为每个数据项应用余额信息
                foreach (var item in reportData)
                {
                    // 检查本地数据中是否有该钱包地址的余额信息
                    string balanceKey = $"{item.ProjectName}_{item.WalletAddress}";
                    if (balanceData.TryGetValue(balanceKey, out var balanceInfo))
                    {
                        // 应用余额信息
                        item.CurrentBalance = Convert.ToDecimal(balanceInfo);
                        
                        // 如果不存在TaskBalances字典，则创建一个
                        if (item.TaskBalances == null)
                            item.TaskBalances = new Dictionary<string, decimal>();
                            
                        // 复制任务余额
                        string tokenBalanceKey = $"{item.ProjectName}_{item.WalletAddress}_{item.InteractionType}";
                        if (balanceData.TryGetValue(tokenBalanceKey, out var tokenBalanceInfo))
                        {
                            var tokenDict = tokenBalanceInfo as Newtonsoft.Json.Linq.JObject;
                            if (tokenDict != null)
                            {
                                foreach (var prop in tokenDict.Properties())
                                {
                                    item.TaskBalances[prop.Name] = prop.Value.ToObject<decimal>();
                                }
                            }
                        }
                        
                        // 如果是Monad项目，还需要设置特定代币余额
                        if (item is MonadReportData monadData)
                        {
                            // 根据交互类型设置代币余额
                            switch (monadData.InteractionType?.ToLower())
                            {
                                case "smon":
                                    if (item.TaskBalances.TryGetValue("sMON", out decimal smonBalance))
                                    {
                                        monadData.SMonTokens = smonBalance;
                                        monadData.GMonTokens = 0;
                                    }
                                    break;
                                case "gmon":
                                    if (item.TaskBalances.TryGetValue("gMON", out decimal gmonBalance))
                                    {
                                        monadData.GMonTokens = gmonBalance;
                                        monadData.SMonTokens = 0;
                                    }
                                    break;
                                case "aprmon":
                                    if (item.TaskBalances.TryGetValue("MON", out decimal aprmonBalance))
                                    {
                                        monadData.GMonTokens = aprmonBalance;
                                        monadData.SMonTokens = 0;
                                    }
                                    break;
                            }
                        }
                    }
                }
                
                Debug.WriteLine($"成功从本地文件加载余额数据");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"从本地文件加载余额数据失败: {ex.Message}");
            }
        }
    }
} 
