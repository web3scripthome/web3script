using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using web3script.Data;
using web3script.Models;
using System.Windows.Input;
using System.Diagnostics;
using web3script.ViewModels.Common;

namespace web3script.ViewModels
{
    public class TaskDetailsViewModel : INotifyPropertyChanged
    {
        private string _lastRecordsHash = string.Empty;

        public string TaskId { get; set; }
        public string ProjectName { get => _projectName; set { _projectName = value; OnPropertyChanged(nameof(ProjectName)); } }
        public string GroupName { get => _groupName; set { _groupName = value; OnPropertyChanged(nameof(GroupName)); } }
        public string CreateTime { get => _createTime; set { _createTime = value; OnPropertyChanged(nameof(CreateTime)); } }
        public int ThreadCount { get => _threadCount; set { _threadCount = value; OnPropertyChanged(nameof(ThreadCount)); } }

        public string Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public Brush StatusColor { get => _statusColor; set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); } }
        public int Progress { get => _progress; set { _progress = value; OnPropertyChanged(nameof(Progress)); } }

        public int TotalCount { get => _totalCount; set { _totalCount = value; OnPropertyChanged(nameof(TotalCount)); } }
        public int SuccessCount { get => _successCount; set { _successCount = value; OnPropertyChanged(nameof(SuccessCount)); } }
        public int FailedCount { get => _failedCount; set { _failedCount = value; OnPropertyChanged(nameof(FailedCount)); } }

        public ObservableCollection<string> ExecutionItems { get; set; } = new();
        public ObservableCollection<ExecutionRecordViewModel> ExecutionRecords { get; set; } = new();

        private DispatcherTimer _syncTimer;

        /// <summary>
        /// 设置当前项目名称，并更新所有执行记录中的项目名称
        /// </summary>
        /// <param name="projectName">项目名称</param>
        public void SetProjectName(string projectName)
        {
            // 更新ViewModel的ProjectName属性
            this.ProjectName = projectName;
            
            // 更新所有现有记录的项目名称
            foreach (var record in ExecutionRecords)
            {
                record.ProjectName = projectName;
            }
        }

        public void StartSync()
        {
            if (_syncTimer == null)
            {
                _syncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
                _syncTimer.Tick += (s, e) => SyncFromStore();
            }
            _syncTimer.Start();
            SyncFromStore();
        }

        public void StopSync()
        {
            _syncTimer?.Stop();
        }
        
        private void SyncFromStore()
        {
            if (string.IsNullOrEmpty(TaskId))
                return;

            var records = ExecutionStore.GetRecords(TaskId);
            string currentHash = JsonSerializer.Serialize(records);

            // 如果数据没变，就直接返回，不刷新
            if (currentHash == _lastRecordsHash)
                return;

            _lastRecordsHash = currentHash;

            // 更新计数和统计信息
            TotalCount = records.Count;
            SuccessCount = records.Count(x => x.Success == true);
            FailedCount = records.Count(x => x.Success == false);
            Progress = TotalCount > 0 ? (int)((SuccessCount + FailedCount) / (double)TotalCount * 100) : 0;

            // 智能更新记录列表，而不是清空再添加
            if (ExecutionRecords.Count == 0)
            {
                // 如果列表为空，直接添加所有记录
                for (int i = 0; i < records.Count; i++)
                {
                    var r = records[i];
                    
                    // 确保状态显示正确，而不是简单地显示"已暂停"
                    string displayStatus = r.Status;
                    if (r.Status == "已暂停" && r.Success.HasValue)
                    {
                        // 如果已经有执行结果但状态显示为"已暂停"，使用实际执行结果的状态
                        displayStatus = r.Success.Value ? "交互成功" : "交互失败";
                    }
                    
                    ExecutionRecords.Add(new ExecutionRecordViewModel
                    {
                        Index = i + 1,
                        WalletAddress = r.WalletAddress,
                        OperationTime = r.OperationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        Status = displayStatus,
                        Success = r.Success,
                        ErrorMessage = r.ErrorMessage,
                        TransactionHash = r.TransactionHash,
                        ProjectName = !string.IsNullOrEmpty(r.ProjectName) ? r.ProjectName : this.ProjectName
                    });
                }
            }
            else
            {
                // 处理记录数量变化的情况
                if (ExecutionRecords.Count > records.Count)
                {
                    // 如果记录减少了，移除多余的项
                    while (ExecutionRecords.Count > records.Count)
                    {
                        ExecutionRecords.RemoveAt(ExecutionRecords.Count - 1);
                    }
                }
                else if (ExecutionRecords.Count < records.Count)
                {
                    // 如果记录增加了，添加新项
                    for (int i = ExecutionRecords.Count; i < records.Count; i++)
                    {
                        var r = records[i];
                        
                        // 确保状态显示正确
                        string displayStatus = r.Status;
                        if (r.Status == "已暂停" && r.Success.HasValue)
                        {
                            displayStatus = r.Success.Value ? "交互成功" : "交互失败";
                        }
                        
                        ExecutionRecords.Add(new ExecutionRecordViewModel
                        {
                            Index = i + 1,
                            WalletAddress = r.WalletAddress,
                            OperationTime = r.OperationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            Status = displayStatus,
                            Success = r.Success,
                            ErrorMessage = r.ErrorMessage,
                            TransactionHash = r.TransactionHash,
                            ProjectName = !string.IsNullOrEmpty(r.ProjectName) ? r.ProjectName : this.ProjectName
                        });
                    }
                }

                // 更新现有项的值（不改变对象引用，从而保持UI选择状态）
                for (int i = 0; i < records.Count && i < ExecutionRecords.Count; i++)
                {
                    var record = records[i];
                    var viewModel = ExecutionRecords[i];
                    
                    // 确保状态显示正确
                    string displayStatus = record.Status;
                    if (record.Status == "已暂停" && record.Success.HasValue)
                    {
                        displayStatus = record.Success.Value ? "交互成功" : "交互失败";
                    }
                    
                    viewModel.Index = i + 1;
                    viewModel.WalletAddress = record.WalletAddress;
                    viewModel.OperationTime = record.OperationTime.ToString("yyyy-MM-dd HH:mm:ss");
                    viewModel.Status = displayStatus;
                    viewModel.Success = record.Success;
                    viewModel.ErrorMessage = record.ErrorMessage;
                    viewModel.TransactionHash = record.TransactionHash;
                    viewModel.ProjectName = !string.IsNullOrEmpty(record.ProjectName) ? record.ProjectName : this.ProjectName;
                }
            }
        }

        private string _projectName;
        private string _groupName;
        private string _createTime;
        private int _threadCount;

        private string _status;
        private Brush _statusColor = Brushes.Gray;
        private int _progress;

        private int _totalCount;
        private int _successCount;
        private int _failedCount;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public class ExecutionRecordViewModel : INotifyPropertyChanged
    {
        private int _index;
        private string _walletAddress;
        private string _operationTime;
        private string _status;
        private bool? _success;
        private string _errorMessage;
        private string _transactionHash;
        private bool _isSelected;
        private string _projectName;

        // 存储项目名称，用于生成区块浏览器链接
        public string ProjectName
        {
            get => _projectName;
            set
            {
                if (_projectName != value)
                {
                    _projectName = value;
                    OnPropertyChanged(nameof(ProjectName));
                    // 更新探索器命令的可执行状态
                    (OpenExplorerCommand as Common.RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

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

        public string WalletAddress
        {
            get => _walletAddress;
            set
            {
                if (_walletAddress != value)
                {
                    _walletAddress = value;
                    OnPropertyChanged(nameof(WalletAddress));
                    // 地址变更时更新命令可执行状态
                    (OpenExplorerCommand as Common.RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string OperationTime
        {
            get => _operationTime;
            set
            {
                if (_operationTime != value)
                {
                    _operationTime = value;
                    OnPropertyChanged(nameof(OperationTime));
                }
            }
        }

        public string Status
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

        public bool? Success
        {
            get => _success;
            set
            {
                if (_success != value)
                {
                    _success = value;
                    OnPropertyChanged(nameof(Success));
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        public string TransactionHash
        {
            get => _transactionHash;
            set
            {
                if (_transactionHash != value)
                {
                    _transactionHash = value;
                    OnPropertyChanged(nameof(TransactionHash));
                    OnPropertyChanged(nameof(HasTxHash));
                }
            }
        }

        public bool HasTxHash => !string.IsNullOrWhiteSpace(TransactionHash);

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

        // 打开区块浏览器命令
        private ICommand _openExplorerCommand;
        public ICommand OpenExplorerCommand => _openExplorerCommand ?? (_openExplorerCommand = new Common.RelayCommand(
            execute: () => OpenExplorer(),
            canExecute: () => CanOpenExplorer()
        ));

        private bool CanOpenExplorer()
        {
            // 检查是否有有效的钱包地址和项目名称
            return !string.IsNullOrEmpty(WalletAddress) && 
                   !string.IsNullOrEmpty(ProjectName) &&
                   ProjectExplorerMappings.IsExplorerSupported(ProjectName);
        }

        private void OpenExplorer()
        {
            try
            {
                // 获取对应项目的区块浏览器URL
                string explorerUrl = ProjectExplorerMappings.GetExplorerUrl(ProjectName, WalletAddress);
                
                if (!string.IsNullOrEmpty(explorerUrl))
                {
                    // 使用默认浏览器打开URL
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = explorerUrl,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                // 错误处理
                System.Windows.MessageBox.Show($"无法打开区块浏览器: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
