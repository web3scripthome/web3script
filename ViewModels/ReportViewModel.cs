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
            
            StartDate = DateTime.Now.AddDays(-30);
            EndDate = DateTime.Now;
            
            _taskService.ExecutionRecordsChanged += TaskService_ExecutionRecordsChanged;
            
            InitCommands();
            InitializeAsync();
        }

        ~ReportViewModel()
        {
            Cleanup();
        }

        public void Cleanup()
        {
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
            
            if (projectName == "全部")
            {
                var allProjects = _projectService.GetProjects();
                HashSet<string> allTaskNames = new HashSet<string>();
                
                var allExecutionRecords = _taskService.GetAllExecutionRecords();
                var recordTaskNames = allExecutionRecords.Select(r => r.TaskName)
                                                         .Where(name => !string.IsNullOrEmpty(name))
                                                         .Distinct();
                
                foreach (var name in recordTaskNames)
                {
                    allTaskNames.Add(name);
                }
                 
                foreach (var taskName in allTaskNames.OrderBy(n => n))
                {
                    InteractionTypes.Add(taskName);
                }
            }
            else
            {
                var project = _projectService.GetProjects().FirstOrDefault(p => p.Name == projectName);
                
                var projectExecutionRecords = _taskService.GetAllExecutionRecords()
                                                       .Where(r => r.ProjectName == projectName)
                                                       .Select(r => r.TaskName)
                                                       .Where(name => !string.IsNullOrEmpty(name))
                                                       .Distinct();
                
                foreach (var name in projectExecutionRecords)
                {   
                    foreach (var item in name.Split(','))
                    {
                        if (!InteractionTypes.Contains(item))
                        {
                            InteractionTypes.Add(item);
                        }
                    }
                }
            }

            try
            {
                SelectedInteractionType = InteractionTypes.First();
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
                var projectFilter = _selectedProject == "全部" ? null : _selectedProject;
                var groupFilter = _selectedGroup == "全部" ? null : _selectedGroup;
                var typeFilter = _selectedInteractionType == "全部" ? null : _selectedInteractionType;
                
                var allRecords = _taskService.GetAllExecutionRecords();
                
                var filteredRecords = allRecords.Where(r => 
                    r.OperationTime >= StartDate && 
                    r.OperationTime <= EndDate.AddDays(1)).ToList();
                
                if (!string.IsNullOrEmpty(projectFilter))
                {
                    filteredRecords = filteredRecords.Where(r => r.ProjectName == projectFilter).ToList();
                }
                
                if (!string.IsNullOrEmpty(typeFilter))
                {
                    var beforeCount = filteredRecords.Count;
                    
                    var availableTaskNames = filteredRecords.Select(r => r.TaskName).Distinct().OrderBy(n => n).ToList();
                    
                    filteredRecords = filteredRecords.Where(r => {
                        if (r.TaskName == null) return false;
                        
                        bool isMatch = r.TaskName.Equals(typeFilter, StringComparison.OrdinalIgnoreCase) || 
                                       r.TaskName.IndexOf(typeFilter, StringComparison.OrdinalIgnoreCase) >= 0 || 
                                       typeFilter.IndexOf(r.TaskName, StringComparison.OrdinalIgnoreCase) >= 0;
                        
                        return isMatch;
                    }).ToList();
                    
                    var taskNames = filteredRecords.Select(r => r.TaskName).Distinct().OrderBy(n => n).ToList();
                }
                
                if (!string.IsNullOrEmpty(groupFilter))
                {
                    var groupWallets = _walletService.GetWalletsInGroup(_walletService.WalletGroups.First(g => g.Name == groupFilter).Id);
                    var groupAddresses = groupWallets.Select(w => w.Address).ToList();
                    var beforeCount = filteredRecords.Count;
                    filteredRecords = filteredRecords.Where(r => groupAddresses.Contains(r.WalletAddress)).ToList();
                }
                
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
                        Records = g.ToList()
                    })
                    .ToList();
                
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
                    
                    await GetTokenBalance(reportData);
                    
                    ReportData.Add(reportData);
                }
                
                TotalInteractions = filteredRecords.Count;
                TotalSuccess = filteredRecords.Count(r => r.Success == true);
                var rate = TotalInteractions > 0 ? (double)TotalSuccess / TotalInteractions * 100 : 0;
                SuccessRate = $"{rate:F2}%";
            }
            catch (Exception ex)
            {
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
                
                switch (reportData.ProjectName.ToLower())
                {
                    case "monad":
                        reportData.CurrentBalance = await GetMonadNativeBalance(reportData.WalletAddress);
                        await GetTaskTokenBalance(reportData);
                        break;
                        
                    case "ethereum":
                         
                        await GetTaskTokenBalance(reportData);
                        break;
                        
                    default:
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
                if (reportData.TaskBalances == null)
                {
                    reportData.TaskBalances = new Dictionary<string, decimal>();
                }
                else
                {
                    reportData.TaskBalances.Clear();
                }
                
                string tokenName = ExtractTokenName(reportData.InteractionType);
                
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
                            break;
                    }
                });
                
                reportData.LastBalanceUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
            }
        }
        
        private string ExtractTokenName(string interactionType)
        {
            if (string.IsNullOrEmpty(interactionType))
                return null;
            
            int startIndex = interactionType.IndexOf('(');
            int endIndex = interactionType.IndexOf(')', startIndex);
            
            if (startIndex >= 0 && endIndex > startIndex)
            {
                string tokenName = interactionType.Substring(startIndex + 1, endIndex - startIndex - 1);
                return !string.IsNullOrWhiteSpace(tokenName) ? tokenName : null;
            }
            
            return null;
        }
        
        private async Task<decimal> GetMonadNativeBalance(string walletAddress)
        {
            var balance =await GetEVMBalance.GetEthBalanceAsync("https://testnet-rpc.monad.xyz", walletAddress);
            return balance;
        }
        
        private async Task<decimal> GetEthereumNativeBalance(string walletAddress)
        {
            await Task.Delay(100);
            return GetRandomDecimal(0.01m, 1m);
        }
        
        private decimal GetRandomDecimal(decimal min, decimal max)
        {
            Random random = new Random();
            double range = (double)(max - min);
            double sample = random.NextDouble();
            double scaled = (sample * range) + (double)min;
            return (decimal)scaled;
        }
        
        private void ExportData()
        {
            MessageBox.Show("导出数据功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        public void ReloadInteractionTypes()
        {
            string currentInteractionType = SelectedInteractionType;
            
            LoadInteractionTypes(_selectedProject);
            
            if (!string.IsNullOrEmpty(currentInteractionType) && 
                InteractionTypes.Contains(currentInteractionType))
            {
                SelectedInteractionType = currentInteractionType;
            }
            else if (InteractionTypes.Count > 0)
            {
                SelectedInteractionType = InteractionTypes.First();
            }
        }
        
        private void TaskService_ExecutionRecordsChanged(object sender, EventArgs e)
        {
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
