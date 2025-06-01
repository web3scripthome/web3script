using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using web3script.ConScript;
using web3script.ContractScript;
using web3script.Models;
using web3script.Services;
using Monad_TestNet_Script;
using Task = System.Threading.Tasks.Task;

namespace web3script.ViewModels
{
    public class ReportViewModel : INotifyPropertyChanged
    {
        private readonly ProjectService _projectService;
        private readonly TaskService _taskService;
        private readonly WalletService _walletService;
        
        private ObservableCollection<string> _projects;
        private ObservableCollection<string> _groups;
        private ObservableCollection<string> _interactionTypes;
        private ObservableCollection<BaseReportData> _reportData;
        
        private string _selectedProject;
        private string _selectedGroup;
        private string _selectedInteractionType;
        private BaseReportData _selectedReportItem;
        
        private DateTime _startDate;
        private DateTime _endDate;
        
        private bool _isLoading;
        private int _totalInteractions;
        private int _totalSuccess;
        private string _successRate;
        
        private Dictionary<string, List<string>> _projectInteractionTypes;

        public ReportViewModel()
        {
            _projectService = new ProjectService();
            _taskService = TaskService.Instance;
            _walletService = new WalletService();
            
            Projects = new ObservableCollection<string>();
            Groups = new ObservableCollection<string>();
            InteractionTypes = new ObservableCollection<string>();
            ReportData = new ObservableCollection<BaseReportData>();
            
            _projectInteractionTypes = new Dictionary<string, List<string>>();
            
            // 默认筛选时间为过去30天
            StartDate = DateTime.Now.AddDays(-30);
            EndDate = DateTime.Now;
            
            // 订阅执行记录更新事件
            _taskService.ExecutionRecordsChanged += TaskService_ExecutionRecordsChanged;
            
            InitCommands();
            InitializeAsync();
        }

        // 析构函数
        ~ReportViewModel()
        {
            Cleanup();
        }

        // 清理资源
        public void Cleanup()
        {
            // 取消订阅事件，防止内存泄漏
            if (_taskService != null)
            {
                _taskService.ExecutionRecordsChanged -= TaskService_ExecutionRecordsChanged;
            }
        }

        private void InitCommands()
        {
            RefreshCommand = new Common.RelayCommand(
                execute: async () => await RefreshDataAsync(), 
                canExecute: () => CanRefresh
            );
            
            ExportCommand = new Common.RelayCommand(
                execute: () => ExportData(),
                canExecute: null
            );
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            
            try
            {
                LoadProjects();
                LoadGroups();
                
                if (Projects.Count > 0)
                {
                    SelectedProject = Projects.First();
                }
                
                if (Groups.Count > 0)
                {
                    SelectedGroup = Groups.First();
                }
                
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化报表数据时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void LoadProjects()
        {
            Projects.Clear();
            
            
            foreach (var project in _projectService.GetProjects())
            {
                Projects.Add(project.Name);
            }
        }
        
        private void LoadGroups()
        {
            Groups.Clear(); 
            
            foreach (var group in _walletService.WalletGroups)
            {
                Groups.Add(group.Name);
            }
        }
        
        private void LoadInteractionTypes(string projectName)
        {
            InteractionTypes.Clear();
           // InteractionTypes.Add("全部");
            
            Debug.WriteLine($"DEBUG - 开始加载交互类型，项目名称: {projectName ?? "全部"}");
            
            if (projectName == "全部")
            {
                // 获取所有项目的所有执行任务
                var allProjects = _projectService.GetProjects();
                HashSet<string> allTaskNames = new HashSet<string>();
                
                // 从任务服务获取所有任务名称（这是实际存在的记录）
                var allExecutionRecords = _taskService.GetAllExecutionRecords();
                var recordTaskNames = allExecutionRecords.Select(r => r.TaskName)
                                                         .Where(name => !string.IsNullOrEmpty(name))
                                                         .Distinct();
                
                Debug.WriteLine($"DEBUG - 从执行记录中找到 {recordTaskNames.Count()} 个任务名称");
                foreach (var name in recordTaskNames)
                {
                    allTaskNames.Add(name);
                }
                 
                // 按字母顺序添加所有唯一的任务名称
                foreach (var taskName in allTaskNames.OrderBy(n => n))
                {
                    InteractionTypes.Add(taskName);
                }
                
                Debug.WriteLine($"DEBUG - 加载了 {InteractionTypes.Count - 1} 个交互类型（不包括全部）");
            }
            else
            {
                // 获取特定项目的执行任务
                var project = _projectService.GetProjects().FirstOrDefault(p => p.Name == projectName);
                
                // 从任务服务获取该项目的所有任务名称（这是实际存在的记录）
                var projectExecutionRecords = _taskService.GetAllExecutionRecords()
                                                       .Where(r => r.ProjectName == projectName)
                                                       .Select(r => r.TaskName)
                                                       .Where(name => !string.IsNullOrEmpty(name))
                                                       .Distinct();
                
                Debug.WriteLine($"DEBUG - 项目 '{projectName}' 从执行记录中找到 {projectExecutionRecords.Count()} 个任务名称");
                foreach (var name in projectExecutionRecords)
                {
                    // 添加任务名称    
                    foreach (var item in name.Split(','))
                    {
                        if (!InteractionTypes.Contains(item))
                        {
                            InteractionTypes.Add(item);
                            Debug.WriteLine($"DEBUG - 添加任务名称: {item}");
                        }
                    }
                   
                }

                 
            }
            
            // 如果除了"全部"外没有找到任何交互类型，添加一些默认值
            //if (InteractionTypes.Count <= 1)
            //{
            //    MessageBox.Show($"{projectName}还未交互过任何项目");
            //    return;
            //}
            
            // 输出最终的交互类型列表
            Debug.WriteLine($"DEBUG - 最终交互类型列表 ({InteractionTypes.Count} 项): {string.Join(", ", InteractionTypes)}");

            // 默认选择第一项
            try
            {
                SelectedInteractionType = InteractionTypes.First();
                Debug.WriteLine($"DEBUG - 默认选择交互类型: {SelectedInteractionType}");
            }
            catch (Exception)
            {

                
            }
          
        }
        
        private async Task RefreshDataAsync()
        {
            if (IsLoading) return;
            
            IsLoading = true;
            ReportData.Clear();
            
            try
            {
                // 获取筛选条件
                var projectFilter = _selectedProject == "全部" ? null : _selectedProject;
                var groupFilter = _selectedGroup == "全部" ? null : _selectedGroup;
                var typeFilter = _selectedInteractionType == "全部" ? null : _selectedInteractionType;
                
                Debug.WriteLine($"DEBUG - 筛选条件: 项目={projectFilter ?? "全部"}, 分组={groupFilter ?? "全部"}, 交互类型={typeFilter ?? "全部"}");
                
                // 获取所有执行记录
                var allRecords = _taskService.GetAllExecutionRecords();
                Debug.WriteLine($"DEBUG - 获取到总执行记录数: {allRecords.Count}");
                
                // 根据日期进行筛选
                var filteredRecords = allRecords.Where(r => 
                    r.OperationTime >= StartDate && 
                    r.OperationTime <= EndDate.AddDays(1)).ToList();
                Debug.WriteLine($"DEBUG - 按日期筛选后的记录数: {filteredRecords.Count}");
                
                // 根据项目进行筛选
                if (!string.IsNullOrEmpty(projectFilter))
                {
                    filteredRecords = filteredRecords.Where(r => r.ProjectName == projectFilter).ToList();
                    Debug.WriteLine($"DEBUG - 按项目 '{projectFilter}' 筛选后的记录数: {filteredRecords.Count}");
                }
                
                // 根据交互类型进行筛选
                if (!string.IsNullOrEmpty(typeFilter))
                {
                    var beforeCount = filteredRecords.Count;
                    
                    // 为了调试，先打印所有任务名称
                    var availableTaskNames = filteredRecords.Select(r => r.TaskName).Distinct().OrderBy(n => n).ToList();
                    Debug.WriteLine($"DEBUG - 筛选前可用的任务名称 ({availableTaskNames.Count}): {string.Join(", ", availableTaskNames)}");
                    
                    // 修改为模糊匹配，检查交互类型是否包含在TaskName中，或TaskName是否包含在交互类型中
                    filteredRecords = filteredRecords.Where(r => {
                        if (r.TaskName == null) return false;
                        
                        // 尝试多种匹配方式
                        bool isMatch = r.TaskName.Equals(typeFilter, StringComparison.OrdinalIgnoreCase) || // 完全匹配
                                       r.TaskName.IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0 || // 包含筛选词
                                       typeFilter.IndexOf(r.TaskName, StringComparison.OrdinalIgnoreCase) >= 0; // 筛选词包含任务名
                        
                        // 输出匹配结果（当匹配失败时）
                        if (!isMatch && beforeCount <= 20) // 仅在记录较少时输出详细匹配信息
                        {
                            Debug.WriteLine($"DEBUG - 匹配失败: TaskName='{r.TaskName}', TypeFilter='{typeFilter}'");
                        }
                        
                        return isMatch;
                    }).ToList();
                    
                    Debug.WriteLine($"DEBUG - 按交互类型 '{typeFilter}' 筛选前记录数: {beforeCount}, 筛选后记录数: {filteredRecords.Count}");
                    
                    // 输出所有筛选后剩余的任务名称，用于调试
                    var taskNames = filteredRecords.Select(r => r.TaskName).Distinct().OrderBy(n => n).ToList();
                    Debug.WriteLine($"DEBUG - 筛选后剩余的任务名称 ({taskNames.Count}): {string.Join(", ", taskNames)}");
                }
                
                // 根据钱包分组进行筛选
                if (!string.IsNullOrEmpty(groupFilter))
                {
                    var groupWallets = _walletService.GetWalletsInGroup(_walletService.WalletGroups.First(g => g.Name == groupFilter).Id);
                    var groupAddresses = groupWallets.Select(w => w.Address).ToList();
                    var beforeCount = filteredRecords.Count;
                    filteredRecords = filteredRecords.Where(r => groupAddresses.Contains(r.WalletAddress)).ToList();
                    Debug.WriteLine($"DEBUG - 按分组 '{groupFilter}' 筛选前记录数: {beforeCount}, 筛选后记录数: {filteredRecords.Count}");
                }
                
                // 输出所有执行记录的TaskName
                if (filteredRecords.Count == 0)
                {
                    var allTaskNames = allRecords.Select(r => r.TaskName).Distinct().ToList();
                    Debug.WriteLine($"DEBUG - 所有可用的任务名称: {string.Join(", ", allTaskNames)}");
                }
                
                // 按钱包地址和项目名称分组统计
                var groupedData = filteredRecords
                    .GroupBy(r => new { r.WalletAddress, r.ProjectName })
                    .Select(g => new
                    {
                        WalletAddress = g.Key.WalletAddress,
                        ProjectName = g.Key.ProjectName,
                        InteractionCount = g.Count(),
                        SuccessCount = g.Count(r => r.Success == true),
                        FailedCount = g.Count(r => r.Success == false || r.Success == null),
                        InteractionType = typeFilter,
                        Records = g.ToList() // 保存原始记录用于调试
                    })
                    .ToList();
                
                Debug.WriteLine($"DEBUG - 分组后的数据项数: {groupedData.Count}");
                
                // 创建报表数据
                foreach (var item in groupedData)
                {
                    var reportData = ReportDataFactory.CreateReportData(item.ProjectName);
                    reportData.WalletAddress = item.WalletAddress;
                    reportData.ProjectName = item.ProjectName;
                    reportData.InteractionCount = item.InteractionCount;
                    reportData.SuccessCount = item.SuccessCount;
                    reportData.FailedCount = item.FailedCount;
                    reportData.InteractionType = item.InteractionType;
                    reportData.TaskBalances = new Dictionary<string, decimal>();
                    
                    // 输出每个报表项的详细信息
                    Debug.WriteLine($"DEBUG - 创建报表项: 项目={item.ProjectName}, 钱包={item.WalletAddress}, 交互次数={item.InteractionCount}, 交互类型={item.InteractionType}");
                    
                    // 异步加载余额
                    await GetTokenBalance(reportData);
                    
                    ReportData.Add(reportData);
                }
                
                // 更新统计数据
                TotalInteractions = filteredRecords.Count;
                TotalSuccess = filteredRecords.Count(r => r.Success == true);
                var rate = TotalInteractions > 0 ? (double)TotalSuccess / TotalInteractions * 100 : 0;
                SuccessRate = $"{rate:F2}%";
                
                Debug.WriteLine($"DEBUG - 最终报表项数量: {ReportData.Count}, 总交互次数: {TotalInteractions}, 成功率: {SuccessRate}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR - 刷新数据时出错: {ex.Message}");
                Debug.WriteLine($"ERROR - 堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"刷新数据时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task GetTokenBalance(BaseReportData reportData)
        {
            try
            {
                reportData.LastBalanceUpdate = DateTime.Now;
                
                // 根据项目类型获取原生代币余额
                switch (reportData.ProjectName.ToLower())
                {
                    case "monad":
                        Debug.WriteLine("获取原生代币余额================");
                        reportData.CurrentBalance = await GetMonadNativeBalance(reportData.WalletAddress);
                        Debug.WriteLine(" 根据交互任务类型获取特定代币余额================"); 
                        await GetTaskTokenBalance(reportData);
                        break;
                        
                    case "ethereum":
                         
                        await GetTaskTokenBalance(reportData);
                        break;
                        
                    default:
                        // 测试数据 - 随机生成余额
                      
                        await GetTaskTokenBalance(reportData);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取钱包 {reportData.WalletAddress} 的余额时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async Task GetTaskTokenBalance(BaseReportData reportData)
        {
            try
            {
                // 初始化或清空余额字典
                if (reportData.TaskBalances == null)
                {
                    reportData.TaskBalances = new Dictionary<string, decimal>();
                }
                else
                {
                    reportData.TaskBalances.Clear();
                }
                
                // 提取交互类型中的代币名称（如果有括号）
                string tokenName = ExtractTokenName(reportData.InteractionType);
                
                // 测试方法，根据交互类型获取不同代币的余额
                await Task.Run(async () => {
                    switch (reportData.InteractionType?.ToLower())
                    {
                        case var type when type?.Contains("gmon") == true:
                            MonadStaking monadStaking = new MonadStaking();
                            var balance =  await monadStaking.GetBalanceAsync(reportData.WalletAddress);
                            reportData.TaskBalances[tokenName ?? "gMON"] = balance.Balance;
                            break;
                            
                        case var type when type?.Contains("Nad.fun") == true:
                            
                            break;
                            
                        case var type when type?.Contains("aprmon") == true || type?.Contains("apriori") == true:
                            aprstaking _aprstaking = new aprstaking();
                            var aprbalance =await _aprstaking.GetBalanceOfSharesAsync(reportData.WalletAddress);
                            reportData.TaskBalances[tokenName ?? "aprMON"] =decimal.Parse(aprbalance.ToString());
                            break;
                            
                        case var type when type?.Contains("lp") == true:
                            
                            break; 
                        default:
                            // 对于未知交互类型，使用从交互类型名称中提取的代币名称
                            
                            break;
                    }
                });
                
                reportData.LastBalanceUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取任务代币余额时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从交互任务名称中提取代币名称（括号内的内容）
        /// </summary>
        private string ExtractTokenName(string interactionType)
        {
            if (string.IsNullOrEmpty(interactionType))
                return null;
            
            // 查找左括号和右括号的位置
            int startIndex = interactionType.IndexOf('(');
            int endIndex = interactionType.IndexOf(')', startIndex);
            
            // 如果找到了括号并且位置有效
            if (startIndex >= 0 && endIndex > startIndex)
            {
                // 提取括号内的内容
                string tokenName = interactionType.Substring(startIndex + 1, endIndex - startIndex - 1);
                return !string.IsNullOrWhiteSpace(tokenName) ? tokenName : null;
            }
            
            return null;
        }
        
        // 测试方法：获取Monad原生代币余额
        private async Task<decimal> GetMonadNativeBalance(string walletAddress)
        {

            var balance =await GetEVMBalance.GetEthBalanceAsync("https://testnet-rpc.monad.xyz", walletAddress);
            return balance;
        }
        
        // 测试方法：获取Ethereum原生代币余额
        private async Task<decimal> GetEthereumNativeBalance(string walletAddress)
        {
            await Task.Delay(100); // 模拟网络延迟
            return GetRandomDecimal(0.01m, 1m);
        }
        
        // 生成随机小数，用于测试数据
        private decimal GetRandomDecimal(decimal min, decimal max)
        {
            Random random = new Random();
            double range = (double)(max - min);
            double sample = random.NextDouble();
            double scaled = (sample * range) + (double)min;
            return (decimal)scaled;
        }
        
        // 导出数据到CSV文件
        private void ExportData()
        {
            // 这里实现导出数据的逻辑
            MessageBox.Show("导出数据功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        /// <summary>
        /// 重新加载交互类型列表
        /// </summary>
        public void ReloadInteractionTypes()
        {
            Debug.WriteLine("手动触发交互类型列表刷新");
            
            // 保存当前选中的交互类型
            string currentInteractionType = SelectedInteractionType;
            
            // 重新加载交互类型列表
            LoadInteractionTypes(_selectedProject);
            
            // 尝试恢复选中的交互类型
            if (!string.IsNullOrEmpty(currentInteractionType) && 
                InteractionTypes.Contains(currentInteractionType))
            {
                SelectedInteractionType = currentInteractionType;
                Debug.WriteLine($"已恢复之前选中的交互类型: {currentInteractionType}");
            }
            else if (InteractionTypes.Count > 0)
            {
                SelectedInteractionType = InteractionTypes.First();
                Debug.WriteLine($"已选择第一个交互类型: {SelectedInteractionType}");
            }
        }
        
        // 当执行记录更新时处理
        private void TaskService_ExecutionRecordsChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("检测到执行记录已更新，准备刷新交互类型列表");
            
            // 使用UI线程调度器执行刷新
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ReloadInteractionTypes();
            });
        }
        
        #region 属性
        
        public ObservableCollection<string> Projects
        {
            get => _projects;
            set
            {
                _projects = value;
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<string> Groups
        {
            get => _groups;
            set
            {
                _groups = value;
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<string> InteractionTypes
        {
            get => _interactionTypes;
            set
            {
                _interactionTypes = value;
                OnPropertyChanged();
            }
        }
        
        public ObservableCollection<BaseReportData> ReportData
        {
            get => _reportData;
            set
            {
                _reportData = value;
                OnPropertyChanged();
            }
        }
        
        public string SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (_selectedProject != value)
                {
                    _selectedProject = value;
                    OnPropertyChanged();
                    
                    // 当选择项目变化时，重新加载交互类型
                    LoadInteractionTypes(_selectedProject);
                }
            }
        }
        
        public string SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _selectedGroup = value;
                OnPropertyChanged();
            }
        }
        
        public string SelectedInteractionType
        {
            get => _selectedInteractionType;
            set
            {
                _selectedInteractionType = value;
                OnPropertyChanged();
            }
        }
        
        public BaseReportData SelectedReportItem
        {
            get => _selectedReportItem;
            set
            {
                _selectedReportItem = value;
                OnPropertyChanged();
            }
        }
        
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged();
            }
        }
        
        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                _endDate = value;
                OnPropertyChanged();
            }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
        
        public bool CanRefresh => !IsLoading;
        
        public int TotalInteractions
        {
            get => _totalInteractions;
            set
            {
                _totalInteractions = value;
                OnPropertyChanged();
            }
        }
        
        public int TotalSuccess
        {
            get => _totalSuccess;
            set
            {
                _totalSuccess = value;
                OnPropertyChanged();
            }
        }
        
        public string SuccessRate
        {
            get => _successRate;
            set
            {
                _successRate = value;
                OnPropertyChanged();
            }
        }
        
        public ICommand RefreshCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        
        #endregion
        
        #region INotifyPropertyChanged
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
} 