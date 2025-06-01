using web3script.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TaskStatus = web3script.Models.TaskStatus;
using System.Threading;
using web3script.ContractScript;
using Monad_TestNet_Script;
using web3script.ConScript.MitScript;
using Detest.MitScript;
using NadFun.Api;
using NadFunTrading;
using Task = System.Threading.Tasks.Task;
using System.Diagnostics; 
using Nethereum.Web3.Accounts;
using System.Windows.Controls;
using ADRaffy.ENSNormalize;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Account = Nethereum.Web3.Accounts.Account;
using Nethereum.Web3;
using System.Numerics;
using System.Net;
using System.Collections.Concurrent;
using web3script.ucontrols;
using System.Net.Http;
using Nethereum.Signer;
using Solnet.Wallet;
using System.Diagnostics.Metrics;
using System.Windows.Documents;

namespace web3script.Services
{
    public class TaskService : INotifyPropertyChanged
    {
        private static TaskService _instance;
        private readonly string _tasksFilePath = "tasks.json";
        private readonly string _executionRecordsFilePath = "execution_records.json";
        private ObservableCollection<Models.Task> _tasks;
        private Dictionary<string, List<TaskExecutionRecord>> _executionRecords;
        private DispatcherTimer _scheduledTaskTimer;
        
        public static TaskService Instance => _instance ?? (_instance = new TaskService());
        
        public ObservableCollection<Models.Task> Tasks => _tasks;
        
        // 添加一个事件来通知执行记录已更新
        public event EventHandler ExecutionRecordsChanged;
        
        private TaskService()
        {
            _tasks = new ObservableCollection<Models.Task>();
            _executionRecords = new Dictionary<string, List<TaskExecutionRecord>>();
            
            LoadTasks();
            LoadExecutionRecords();
            
            // 加载执行记录详情
            Data.ExecutionStore.LoadRecords();
            
            // 确保执行记录完整性
            EnsureExecutionRecordsIntegrity();
            
            // 启动定时任务检查
            _scheduledTaskTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _scheduledTaskTimer.Tick += CheckScheduledTasks;
            _scheduledTaskTimer.Start();
        }
        
        private void CheckScheduledTasks(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            var tasksToStart = _tasks.Where(t => 
                t.Status == TaskStatus.Pending && 
                t.ScheduleSettings != null && 
                t.ScheduleSettings.IsScheduled && 
                t.ScheduleSettings.ScheduledTime.HasValue && 
                t.ScheduleSettings.ScheduledTime.Value <= now).ToList();
                
            foreach (var task in tasksToStart)
            {
                StartTask(task.Id);
                
                // 如果是重复任务，更新下次执行时间
                if (task.ScheduleSettings.IsRecurring)
                {
                    DateTime nextRunTime = DateTime.Now;
                    
                    switch (task.ScheduleSettings.RecurrenceType)
                    {
                        case RecurrenceType.Hourly:
                            nextRunTime = nextRunTime.AddHours(task.ScheduleSettings.RecurrenceInterval);
                            break;
                        case RecurrenceType.Daily:
                            nextRunTime = nextRunTime.AddDays(task.ScheduleSettings.RecurrenceInterval);
                            break;
                        case RecurrenceType.Weekly:
                            nextRunTime = nextRunTime.AddDays(7 * task.ScheduleSettings.RecurrenceInterval);
                            break;
                        case RecurrenceType.Monthly:
                            nextRunTime = nextRunTime.AddMonths(task.ScheduleSettings.RecurrenceInterval);
                            break;
                    }
                    
                    task.ScheduleSettings.ScheduledTime = nextRunTime;
                }
                else
                {
                    // 非重复任务执行后取消定时
                    task.ScheduleSettings.IsScheduled = false;
                    task.ScheduleSettings.ScheduledTime = null;
                }
            }
            
            if (tasksToStart.Any())
            {
                SaveTasks();
            }
        }
        
        public void LoadTasks()
        {
            try
            {
                // 先加载执行记录详情
                Data.ExecutionStore.LoadRecords();
                
                if (File.Exists(_tasksFilePath))
                {
                    string json = File.ReadAllText(_tasksFilePath);
                    var tasks = JsonConvert.DeserializeObject<List<Models.Task>>(json);
                    
                    if (tasks != null)
                    {
                        _tasks.Clear();
                        foreach (var task in tasks)
                        {
                            // 如果任务在上次程序关闭时正在运行，将其状态设置为暂停
                            if (task.Status == TaskStatus.Running)
                            {
                                task.Status = TaskStatus.Paused;
                                
                                // 同步更新ExecutionStore中相应任务的记录状态
                                var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                foreach (var record in taskRecords)
                                {
                                    // 仅当记录没有成功/失败结果时才更新状态为"已暂停"
                                    if (!record.Success.HasValue)
                                    {
                                        record.Status = "已暂停";
                                    }
                                }
                                
                                Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                                    MessageBox.Show($"任务 \"{task.ProjectName}\" 在程序退出前正在执行，已自动暂停。您可以点击继续按钮从上次中断处继续执行。", 
                                        "任务恢复提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                                }));
                            }
                            
                            _tasks.Add(task);
                        }
                        
                        // 保存更新后的任务状态
                        if (tasks.Any(t => t.Status == TaskStatus.Paused))
                        {
                            try
                            {
                                SaveTasks();
                            }
                            catch (Exception saveEx)
                            {
                                Debug.WriteLine($"保存恢复的任务状态时出错: {saveEx.Message}");
                            }
                        }
                        
                        // 确保执行记录完整性
                        EnsureExecutionRecordsIntegrity();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载任务数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void LoadExecutionRecords()
        {
            try
            {
                if (File.Exists(_executionRecordsFilePath))
                {
                    string json = File.ReadAllText(_executionRecordsFilePath);
                    _executionRecords = JsonConvert.DeserializeObject<Dictionary<string, List<TaskExecutionRecord>>>(json) 
                        ?? new Dictionary<string, List<TaskExecutionRecord>>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载执行记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void SaveTasks()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_tasks, Formatting.Indented);
                File.WriteAllText(_tasksFilePath, json);
                
                // 同时保存执行记录详情
                Data.ExecutionStore.SaveRecords();
                
                OnPropertyChanged(nameof(Tasks));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 添加新方法：确保执行记录完整性
        private void EnsureExecutionRecordsIntegrity()
        {
            try
            {
                foreach (var task in _tasks)
                {
                    // 无论任务状态如何，都检查其执行记录完整性
                    // 获取分组中的钱包
                    var walletService = new Services.WalletService();
                    List<Models.Wallet> wallets = new List<Models.Wallet>();
                    
                    // 尝试解析分组名获取ID
                    if (!string.IsNullOrEmpty(task.GroupName))
                    {
                        var group = walletService.WalletGroups.FirstOrDefault(g => g.Name == task.GroupName);
                        if (group != null)
                        {
                            wallets = walletService.GetWalletsInGroup(group.Id);
                            
                            // 获取任务名称的具体功能
                            string[] taskActions = task.Name.Split(',');
                            
                            // 获取现有执行记录
                            var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                            
                            // 只有当记录完全为空时才重新初始化
                            if (taskRecords == null || taskRecords.Count == 0)
                            {
                                Debug.WriteLine($"任务 {task.Id} ({task.ProjectName}) 的执行记录为空，重新初始化");
                                // 创建记录集合
                                Data.ExecutionStore.ClearRecords(task.Id);
                                taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                
                                // 为每个钱包创建初始记录
                                foreach (var wallet in wallets)
                                {
                                    int walletIndex = wallets.IndexOf(wallet);
                                    foreach (var action in taskActions)
                                    {
                                        string trimmedAction = action.Trim();
                                        var initialRecord = new Data.ExecutionRecord
                                        {
                                            TaskId = task.Id,
                                            WalletAddress = wallet.Address,
                                            OperationTime = DateTime.Now,
                                            Status = task.Status == TaskStatus.Paused ? "已暂停" : "准备中",
                                            Success = null,
                                            ErrorMessage = null,
                                            TransactionHash = null,
                                            // 设置新增属性
                                            TaskName = task.Name,
                                            ProjectName = task.ProjectName,
                                            WalletIndex = walletIndex,
                                            ActionName = trimmedAction
                                        };
                                        
                                        taskRecords.Add(initialRecord);
                                    }
                                }
                                
                                // 保存记录
                                Data.ExecutionStore.SaveRecords();
                            }
                            else
                            {
                                // 记录已存在，检查是否完整
                                Debug.WriteLine($"任务 {task.Id} ({task.ProjectName}) 的执行记录存在，检查完整性");
                                
                                // 确保所有钱包和操作都有对应的记录
                                int expectedRecordCount = wallets.Count * taskActions.Length;
                                
                                if (taskRecords.Count < expectedRecordCount)
                                {
                                    Debug.WriteLine($"记录不完整: 当前 {taskRecords.Count} 条，应有 {expectedRecordCount} 条");
                                    
                                    // 创建已存在记录的索引
                                    Dictionary<string, Dictionary<string, Data.ExecutionRecord>> existingRecords = new Dictionary<string, Dictionary<string, Data.ExecutionRecord>>();
                                    
                                    foreach (var record in taskRecords)
                                    {
                                        // 使用钱包地址和操作作为键
                                        if (!existingRecords.ContainsKey(record.WalletAddress))
                                        {
                                            existingRecords[record.WalletAddress] = new Dictionary<string, Data.ExecutionRecord>();
                                        }
                                        
                                        // 确保ActionName有值
                                        string actionKey = !string.IsNullOrEmpty(record.ActionName) ? record.ActionName : "默认操作";
                                        existingRecords[record.WalletAddress][actionKey] = record;
                                    }
                                    
                                    // 添加缺失的记录
                                    foreach (var wallet in wallets)
                                    {
                                        int walletIndex = wallets.IndexOf(wallet);
                                        foreach (var action in taskActions)
                                        {
                                            string trimmedAction = action.Trim();
                                            
                                            // 检查记录是否存在
                                            bool recordExists = existingRecords.ContainsKey(wallet.Address) && 
                                                              existingRecords[wallet.Address].ContainsKey(trimmedAction);
                                            
                                            if (!recordExists)
                                            {
                                                var newRecord = new Data.ExecutionRecord
                                                {
                                                    TaskId = task.Id,
                                                    WalletAddress = wallet.Address,
                                                    OperationTime = DateTime.Now,
                                                    Status = task.Status == TaskStatus.Paused ? "已暂停" : "准备中",
                                                    Success = null,
                                                    ErrorMessage = null,
                                                    TransactionHash = null,
                                                    TaskName = task.Name,
                                                    ProjectName = task.ProjectName,
                                                    WalletIndex = walletIndex,
                                                    ActionName = trimmedAction
                                                };
                                                
                                                taskRecords.Add(newRecord);
                                                
                                                // 更新索引
                                                if (!existingRecords.ContainsKey(wallet.Address))
                                                {
                                                    existingRecords[wallet.Address] = new Dictionary<string, Data.ExecutionRecord>();
                                                }
                                                existingRecords[wallet.Address][trimmedAction] = newRecord;
                                            }
                                        }
                                    }
                                    
                                    // 保存更新后的记录
                                    Data.ExecutionStore.SaveRecords();
                                }
                                else if (taskRecords.Count > expectedRecordCount)
                                {
                                    Debug.WriteLine($"记录数量异常: 当前 {taskRecords.Count} 条，应有 {expectedRecordCount} 条");
                                }
                                
                                // 确保所有记录都有ProjectName和TaskName
                                bool needUpdate = false;
                                foreach (var record in taskRecords)
                                {
                                    if (string.IsNullOrEmpty(record.ProjectName) || string.IsNullOrEmpty(record.TaskName))
                                    {
                                        record.ProjectName = task.ProjectName;
                                        record.TaskName = task.Name;
                                        needUpdate = true;
                                    }
                                    
                                    // 确保已完成任务的状态是正确的，不是"已暂停"
                                    if (task.Status == TaskStatus.Completed && record.Status == "已暂停" && record.Success.HasValue)
                                    {
                                        record.Status = record.Success.Value ? "交互成功" : "交互失败";
                                        needUpdate = true;
                                    }
                                }
                                
                                if (needUpdate)
                                {
                                    Data.ExecutionStore.SaveRecords();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"确保执行记录完整性时出错: {ex.Message}");
            }
        }

        public  async Task RunRegAsync(string privateKey)
        {
            try
            {
                // 初始化服务
                var referralService = new ReferralService();
                Account account = new Account(privateKey);
                var address = account.Address;
                int chainId = 10143;
                string parentReferralCode = "DCUwyu";

               Debug.WriteLine("Starting referral process...");
               Debug.WriteLine($"Address: {address}");
               Debug.WriteLine($"Chain ID: {chainId}");
               Debug.WriteLine($"Parent Referral Code: {parentReferralCode}");
                // 1. 获取 nonce
               Debug.WriteLine("\nStep 1: Getting nonce...");
                var nonceResponse = await referralService.GetNonce(address);
               Debug.WriteLine($"Nonce received: {nonceResponse.Nonce}");

                // 2. 创建会话
               Debug.WriteLine("\nStep 2: Creating session...");
                var sessionResponse = await referralService.CreateSession(address, nonceResponse.Nonce!, privateKey, chainId);
                if (sessionResponse?.Account?.AccountId == null)
                {
                    throw new Exception("Failed to create session: Account ID is missing");
                }
               Debug.WriteLine($"Session created successfully for account: {sessionResponse.Account.AccountId}");

                // 3. 检查推荐注册状态
               Debug.WriteLine("\nStep 3: Checking referral registration status...");
                var referralCheck = await referralService.CheckReferralRegistration();
               Debug.WriteLine($"Referral check - Account ID: {referralCheck.AccountId}, Is Registered: {referralCheck.IsRegistered}");

                if (!referralCheck.IsRegistered)
                {
                    // 4. 注册推荐码
                    Debug.WriteLine("\nStep 4: Registering referral code...");
                    var referralResponse = await referralService.RegisterReferral(parentReferralCode);
                    if (referralResponse?.ParentAccountId == null || referralResponse?.ChildAccountId == null)
                    {
                        throw new Exception("Failed to register referral: Missing account IDs in response");
                    }
                    Debug.WriteLine($"Referral registered successfully");
                    Debug.WriteLine($"Parent Account ID: {referralResponse.ParentAccountId}");
                    Debug.WriteLine($"Child Account ID: {referralResponse.ChildAccountId}");
                }
                else
                {
                    return;
                }
               
            }
            catch (Exception ex)
            {
               Debug.WriteLine($"\nError: {ex.Message}");
                if (ex.InnerException != null)
                {
                   Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }
        public void SaveExecutionRecords()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_executionRecords, Formatting.Indented);
                File.WriteAllText(_executionRecordsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存执行记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public Models.Task GetTaskById(string taskId)
        {
            return _tasks.FirstOrDefault(t => t.Id == taskId);
        }
        
        public List<TaskExecutionRecord> GetExecutionRecords(string taskId)
        {
            if (_executionRecords.ContainsKey(taskId))
            {
                return _executionRecords[taskId];
            }
            return new List<TaskExecutionRecord>();
        }
        
        /// <summary>
        /// 获取所有任务的执行记录
        /// </summary>
        /// <returns>所有执行记录的集合</returns>
        public List<TaskExecutionRecord> GetAllExecutionRecords()
        {
            var allRecords = new List<TaskExecutionRecord>();
            foreach (var task in _tasks)
            {
                if (_executionRecords.ContainsKey(task.Id))
                {
                    // 添加项目名称到记录中
                    foreach (var record in _executionRecords[task.Id])
                    {
                        // 设置任务名称和项目名称
                        record.TaskName = task.Name;
                        record.ProjectName = task.ProjectName;
                        
                        allRecords.Add(record);
                    }
                }
            }
            return allRecords;
        }
        
        public string AddTask(Models.Task task)
        {
            _tasks.Add(task);
            SaveTasks();
            return task.Id;
        }
        
        public void UpdateTask(Models.Task task)
        {
            var existingTask = _tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existingTask != null)
            {
                int index = _tasks.IndexOf(existingTask);
                _tasks[index] = task;
                SaveTasks();
            }
        }
        
        public void DeleteTask(string taskId)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                _tasks.Remove(task);
                SaveTasks();
                
                // 清除相关的执行记录
                if (_executionRecords.ContainsKey(taskId))
                {
                    _executionRecords.Remove(taskId);
                    SaveExecutionRecords();
                }
            }
        }
        
        public void AddExecutionRecord(string taskId, TaskExecutionRecord record)
        {
            if (!_executionRecords.ContainsKey(taskId))
            {
                _executionRecords[taskId] = new List<TaskExecutionRecord>();
            }
            
            _executionRecords[taskId].Add(record);
            
            // 更新任务中的执行记录引用
            var task = GetTaskById(taskId);
            if (task != null)
            {
                task.ExecutionRecords.Add(record);
                UpdateTaskProgress(task);
            }
            
            SaveExecutionRecords();
            
            // 触发执行记录更新事件
            OnExecutionRecordsChanged();
        }
        
        // 触发执行记录更新事件的方法
        protected virtual void OnExecutionRecordsChanged()
        {
            ExecutionRecordsChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void UpdateTaskProgress(Models.Task task)
        {
            if (task.ExecutionRecords.Count > 0)
            {
                var completedRecords = task.ExecutionRecords.Count(r => r.Success.HasValue);
                task.Progress = (int)((double)completedRecords / task.ExecutionRecords.Count * 100);
                UpdateTask(task);
            }
        }
        
        public void StartTask(string taskId)
        {
            var task = GetTaskById(taskId);
            if (task != null)
            {
                // 如果任务已经完成，需要提示用户是否重新启动
                if (task.Status == TaskStatus.Completed)
                {
                    bool restart = false;
                    int countdown = 5;
                    
                    Application.Current.Dispatcher.Invoke(() => {
                        var window = new Window
                        {
                            Title = "提示",
                            Width = 300,
                            Height = 150,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            ResizeMode = ResizeMode.NoResize
                        };

                        var stackPanel = new StackPanel
                        {
                            Margin = new Thickness(10)
                        };

                        var textBlock = new TextBlock
                        {
                            Text = "此任务已完成，是否重新启动此任务？重新启动将删除之前所有进度信息。",
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 0)
                        };

                        var countdownText = new TextBlock
                        {
                            Text = $"将在 {countdown} 秒后自动确认",
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 0, 0, 0)
                        };

                        var buttonPanel = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };

                        var yesButton = new Button
                        {
                            Content = "是",
                            Width = 60,
                            Margin = new Thickness(5)
                        };

                        var noButton = new Button
                        {
                            Content = "否",
                            Width = 60,
                            Margin = new Thickness(5)
                        };

                        buttonPanel.Children.Add(yesButton);
                        buttonPanel.Children.Add(noButton);
                        stackPanel.Children.Add(textBlock);
                        stackPanel.Children.Add(countdownText);
                        stackPanel.Children.Add(buttonPanel);
                        window.Content = stackPanel;

                        var timer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1)
                        };

                        timer.Tick += (s, e) =>
                        {
                            countdown--;
                            countdownText.Text = $"将在 {countdown} 秒后自动确认";
                            if (countdown <= 0)
                            {
                                timer.Stop();
                                restart = true;
                                window.Close();
                            }
                        };

                        yesButton.Click += (s, e) =>
                        {
                            timer.Stop();
                            restart = true;
                            window.Close();
                        };

                        noButton.Click += (s, e) =>
                        {
                            timer.Stop();
                            restart = false;
                            window.Close();
                        };

                        timer.Start();
                        window.ShowDialog();
                    });
                    
                    if (restart)
                    {
                        // 重置任务状态和进度
                        task.Status = TaskStatus.Pending;
                        task.Progress = 0;
                        task.LastProcessedIndex = 0;
                        task.StartTime = DateTime.Now;
                        task.EndTime = null;
                        task.ExecutionRecords.Clear();
                        
                        // 清除任务详细执行记录
                        Data.ExecutionStore.ClearRecords(task.Id);
                        
                        // 更新任务状态为运行中
                        task.Status = TaskStatus.Running;
                        UpdateTask(task);
                        
                        // 实际启动任务的逻辑
                        System.Threading.Tasks.Task.Run(() => ExecuteTask(task));
                    }
                    return;
                }
                
                // 对于非完成状态的任务，正常启动
                if (task.Status != TaskStatus.Running)
                {
                    task.Status = TaskStatus.Running;
                    task.StartTime = DateTime.Now;
                    UpdateTask(task);
                    
                    // 实际启动任务的逻辑
                    System.Threading.Tasks.Task.Run(() => ExecuteTask(task));
                }
            }
        }
        
        public void PauseTask(string taskId)
        {
            var task = GetTaskById(taskId);
            if (task != null && task.Status == TaskStatus.Running)
            {
                task.Status = TaskStatus.Paused;
                
                // 保存当前执行进度到文件
                SaveTaskResultToFile(task);
                
                // 更新任务状态
                UpdateTask(task);
                
                // 保存执行记录详情
                Data.ExecutionStore.SaveRecords();
                
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show($"任务已暂停，当前进度已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }
        
        public void ResumeTask(string taskId)
        {
            var task = GetTaskById(taskId);
            if (task == null || (task.Status != TaskStatus.Paused && task.Status != TaskStatus.Cancelled))
            {
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show("找不到暂停的任务或任务状态不正确", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return;
            }

            // 如果任务状态是已取消，确保从头开始执行
            if (task.Status == TaskStatus.Cancelled)
            {
                task.LastProcessedIndex = 0;
            }
            // 否则保持当前的LastProcessedIndex以便从暂停处继续

            // 更新状态为运行中
            task.Status = TaskStatus.Running;
            
            // 更新任务状态
            UpdateTask(task);
            
            // 重新运行任务
            System.Threading.Tasks.Task.Run(() => ExecuteTask(task));
            
            //Application.Current.Dispatcher.Invoke(() => {
            //    MessageBox.Show($"任务已恢复执行，将从{(task.Status == TaskStatus.Paused ? "暂停处" : "开始处")}继续", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            //});
        }
        
        public void StopTask(string taskId)
        {
            var task = GetTaskById(taskId);
            if (task != null && (task.Status == TaskStatus.Running || task.Status == TaskStatus.Paused))
            {
                task.Status = TaskStatus.Cancelled;
                task.EndTime = DateTime.Now;
                // 重置LastProcessedIndex以确保下次重新开始
                task.LastProcessedIndex = 0;
                // 更新任务状态
                UpdateTask(task);
                
                // 将结果保存到文件
                SaveTaskResultToFile(task);
                
                // 保存执行记录详情
                Data.ExecutionStore.SaveRecords();
                
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show($"任务已终止", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }
        
        public void CompleteTask(string taskId)
        {
            var task = GetTaskById(taskId);
            if (task != null)
            {
                task.Status = TaskStatus.Completed;
                task.EndTime = DateTime.Now;
                task.Progress = 100;
                UpdateTask(task);
                
                // 保存执行记录详情
                Data.ExecutionStore.SaveRecords();
            }
        }
        
        private async void ExecuteTask(Models.Task task)
        {
            try
            {
                // 获取钱包服务和项目服务
                var walletService = new Services.WalletService();
                var projectService = new Services.ProjectService();
                
                // 获取对应的项目
                var project = projectService.LoadProjects().FirstOrDefault(p => p.Name == task.ProjectName);
                if (project == null)
                {
                    throw new Exception($"找不到项目: {task.ProjectName}");
                }
                
                // 解析任务名称获取执行的具体功能
                string[] taskActions = task.Name.Split(',');
                
                // 确保执行项存在于项目的执行项列表中
                List<string> validTaskActions = new List<string>();
                foreach (var action in taskActions)
                {
                    string trimmedAction = action.Trim();
                    if (project.ExecutionItems.Any(item => item.Name == trimmedAction))
                    {
                        validTaskActions.Add(trimmedAction);
                    }
                }
                
                // 如果没有有效的执行项，使用原始的任务名称
                if (validTaskActions.Count == 0)
                {
                    validTaskActions.AddRange(taskActions);
                }
                
                // 从分组中获取钱包
                string groupId = null;
                
                // 尝试解析分组名获取ID
                if (!string.IsNullOrEmpty(task.GroupName))
                {
                    var group = walletService.WalletGroups.FirstOrDefault(g => g.Name == task.GroupName);
                    if (group != null)
                    {
                        groupId = group.Id;
                    }
                }
                
                // 获取分组中的钱包
                List<Models.Wallet> wallets = new List<Models.Wallet>();
                if (!string.IsNullOrEmpty(groupId))
                {
                    wallets = walletService.GetWalletsInGroup(groupId);
                }
                
                // 如果没有找到钱包，使用模拟地址
                if (wallets.Count == 0)
                {
                    MessageBox.Show("分组中钱包数为0");
                    return;
                }
                
                // 对于刚开始的任务（非继续），清空之前的执行记录并初始化所有钱包的执行记录
                if (task.LastProcessedIndex == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 清除特定任务的记录
                        Data.ExecutionStore.ClearRecords(task.Id);
                        
                        // 获取任务的记录集合
                        var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                        
                        // 为每个钱包创建初始记录
                        foreach (var wallet in wallets)
                        {
                            int walletIndex = wallets.IndexOf(wallet);
                            foreach (var action in validTaskActions)
                            {
                                string trimmedAction = action.Trim();
                                var initialRecord = new Data.ExecutionRecord
                                {
                                    TaskId = task.Id,
                                    WalletAddress = wallet.Address,
                                    OperationTime = DateTime.Now,
                                    Status = task.Status == TaskStatus.Paused ? "已暂停" : "准备中",
                                    Success = null,
                                    ErrorMessage = null,
                                    TransactionHash = null,
                                    // 设置新增属性
                                    TaskName = task.Name,
                                    ProjectName = task.ProjectName,
                                    WalletIndex = walletIndex,
                                    ActionName = trimmedAction
                                };
                                
                                taskRecords.Add(initialRecord);
                            }
                        }
                    });
                }
                
                // 确定开始的钱包索引，如果是继续任务则从上次处理的位置开始
                int startWalletIndex = 0;
                if (task.LastProcessedIndex > 0)
                {
                    startWalletIndex = task.LastProcessedIndex;
                    //Application.Current.Dispatcher.Invoke(() =>
                    //{
                    //    MessageBox.Show($"任务从第 {startWalletIndex + 1} 个钱包继续执行", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
                    //});
                }
                
                  Debug.WriteLine($"开始执行任务：{task.Name}");
                  Debug.WriteLine($"钱包数量：{wallets.Count}");
                  Debug.WriteLine($"开始索引：{startWalletIndex}");
                  Debug.WriteLine($"-任务线程：{task.ThreadCount}");
                // 获取线程数，确保至少使用1个线程

                int threadCount = Math.Max(1, task.ThreadCount);
                LogService.AppendLog($"-使用线程：{threadCount}");
                bool isCtmeme = false;
                // 多线程执行任务
                if (threadCount > 1)
                {
                    Debug.WriteLine("————————多线程执行任务");
                    if (task.UseProxy)
                    {
                        LogService.AppendLog($"使用代理： {task.ProxyPoolId}");
                        // 获取代理分组，使用任务中保存的代理组名
                        string proxyGroupName = "默认"; // 默认值
                        
                        // 如果任务中有指定代理组名，则使用指定的分组
                        if (!string.IsNullOrEmpty(task.ProxyPoolId))
                        {
                            proxyGroupName = task.ProxyPoolId;
                            LogService.AppendLog($"使用任务指定的代理分组: {proxyGroupName}");
                        }
                        else
                        {
                            LogService.AppendLog("使用默认代理分组");
                        }

                    // 使用ParallelOptions限制并行度
                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = threadCount
                    };
                    
                    // 任务同步对象
                    object lockObj = new object();
                    int processedWalletCount = 0;
                    
                    // 将要处理的钱包放入队列
                    var walletQueue = new ConcurrentQueue<Models.Wallet>(wallets.Skip(startWalletIndex));
                    
                    // 记录当前活跃的任务数
                    int activeTaskCount = 0;
                    
                    // 用于等待所有任务完成的信号量
                    var allTasksCompleted = new ManualResetEvent(false);
                    
                    // 添加代理池管理 - 用于跟踪已分配的代理
                    var usedProxies = new ConcurrentDictionary<string, bool>();
                    
                    // 获取可用代理的方法 - 确保线程间不重复使用相同代理
                    IWebProxy GetUniqueProxy(int threadId, string groupName)
                    {
                        // 最多尝试10次获取不同的代理
                        for (int attempt = 0; attempt < 10; attempt++)
                        {
                            var proxy = ucontrols.ProxyConfigPanel.GetRandomProxy(groupName);
                            if (proxy == null)
                            {
                                    LogService.AppendLog($"警告: 无法获取有效代理，线程 {threadId} 将使用直连");
                                return null;
                            }
                            
                            var webProxy = proxy as WebProxy;
                            var proxyUri = webProxy.Address;
                            string proxyKey = proxyUri.ToString();
                                LogService.AppendLog($"检查代理{proxyKey}是否可用");
                            // 先检查代理是否可用
                            if (IsProxyAvailable(webProxy).Result)
                            {
                                // 如果代理可用，再尝试将此代理标记为已用
                                if (usedProxies.TryAdd(proxyKey, true))
                                {
                                    // 成功添加，表示此代理之前未被使用
                                    string host = proxyUri.Host;
                                    int port = proxyUri.Port;
                                    
                                    // 获取凭据信息
                                    string username = "";
                                    string password = "";
                                    if (webProxy.Credentials is NetworkCredential credentials)
                                    {
                                        username = credentials.UserName;
                                        password = credentials.Password;
                                    }
                                    
                                    Debug.WriteLine($"线程 {threadId} 使用代理: {host}:{port} 用户名:{username} 密码:{password} 分组 {groupName}");
                                    return proxy;
                                }

                                    // 此代理已被其他线程使用，继续尝试下一个
                                    LogService.AppendLog($"线程 {threadId} 尝试的代理 {proxyUri} 已被其他线程使用，重新获取...");
                            }
                            else
                            {
                                    LogService.AppendLog($"线程 {threadId} 尝试的代理 {proxyUri} 不可用，跳过此代理");
                            }
                        }
                        
                        // 如果尝试多次后仍无法获取唯一代理，记录警告并返回null
                        LogService.AppendLog($"警告: 线程 {threadId} 无法获取唯一代理，将使用直连");
                        return null;
                    }
                      
                        // 创建处理钱包的方法
                        async Task ProcessWallet(int threadId, Models.Wallet wallet)
                    {
                        // 保存当前使用的代理信息，用于完成后释放
                        string currentProxyKey = null;
                        WebProxy _useProxy = null;
                        try
                        {
                            // 获取当前钱包索引
                            int walletIndex;
                            lock (lockObj)
                            {
                                walletIndex = startWalletIndex + processedWalletCount;
                                processedWalletCount++;
                            }
                            
                            // 获取唯一代理
                            var proxy = GetUniqueProxy(threadId, proxyGroupName);
                            if (proxy != null)
                            {
                                var webProxy = proxy as WebProxy;
                                var proxyUri = webProxy.Address;
                                currentProxyKey = proxyUri.ToString();
                                    _useProxy = webProxy;
                            }
                            
                            // 执行实际任务...
                        int actionIndex = 0;
                        
                        // 对每个任务动作执行操作
                        foreach (var action in validTaskActions)
                        {
                                // 再次检查任务状态，即使在处理单个动作过程中也可以响应暂停/终止
                                var currentTask = GetTaskById(task.Id);
                                if (currentTask == null || currentTask.Status == Models.TaskStatus.Cancelled || currentTask.Status == TaskStatus.Paused)
                                {
                                    // 保存进度并直接退出
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        // 更新最后处理位置
                                        if (currentTask != null)
                                        {
                                            currentTask.LastProcessedIndex = walletIndex;
                                            UpdateTask(currentTask);
                                        }

                                        // 更新钱包执行记录状态
                                        var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                        for (int recordIdx = 0; recordIdx < taskRecords.Count; recordIdx++)
                                        {
                                            var record = taskRecords[recordIdx];

                                            // 只更新未完成记录的状态
                                            if (record.Success == null &&
                                                !record.Status.Contains("暂停") &&
                                                !record.Status.Contains("取消") &&
                                                !record.Status.Contains("成功") &&
                                                !record.Status.Contains("失败"))
                                            {
                                                record.Status = currentTask.Status == TaskStatus.Paused ? "已暂停" : "已取消";
                                            }
                                        }

                                        // 保存执行记录
                                        Data.ExecutionStore.SaveRecords();
                                    });
                                    throw new OperationCanceledException("任务已取消或暂停");
                                }

                            actionIndex++;

                            int recordIndex = walletIndex * validTaskActions.Count + (actionIndex - 1);
                            
                            // 使用应用程序的Dispatcher来更新UI
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                if (recordIndex < taskRecords.Count && recordIndex >= 0)
                                {
                                    var record = taskRecords[recordIndex];
                                    
                                    // 更新记录状态为处理中
                                    record.Status = "处理中";
                                    // 触发UI刷新
                                    var temp = taskRecords[recordIndex];
                                    taskRecords[recordIndex] = null;
                                    taskRecords[recordIndex] = temp;
                                }
                            });
                            
                                    ProxyViewModel proxyViewModel = null;

                                    if (_useProxy != null)
                                    {
                                        // 从WebProxy创建ProxyViewModel对象
                                        proxyViewModel = new ProxyViewModel
                                        {
                                            ServerAddress = _useProxy.Address.Host,
                                            Port = _useProxy.Address.Port,
                                            Username = (_useProxy.Credentials as NetworkCredential)?.UserName,
                                            Password = (_useProxy.Credentials as NetworkCredential)?.Password,
                                            GroupName = proxyGroupName, 
                                        }; 
                                    }
                            // 模拟处理时间 - 多线程时减少等待
                            System.Threading.Thread.Sleep(200);
                            
                            // 模拟操作结果
                            bool isSuccess = false; // 模拟部分成功部分失败
                            string resultStatus = "";
                            string errorMsg = null;
                            string txHash = null;
                            
                                switch (task.ProjectName)
                                {
                                    case "Monad":

                                        switch (action)
                                        {
                                          
                                            case "自动质押magma(gMON)":
                                                LogService.AppendLog("自动质押magma(gMON)");
                                                MonadStaking monadStaking = new MonadStaking(wallet.PrivateKey, proxyViewModel);
                                                var result = await monadStaking.StakeMon(task.Amount);
                                                isSuccess = result.Success;
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = result.ErrorMessage;
                                                txHash = result.Hex;
                                                break;
                                            case "自动质押aPriori(aprMON)":
                                                LogService.AppendLog("自动质押aPriori(aprMON)");
                                                aprstaking aprstaking_ = new aprstaking(wallet.PrivateKey, proxyViewModel);
                                                var aprstaking_result = await aprstaking_.DepositAsync(task.Amount, wallet.Address);
                                                isSuccess = aprstaking_result.Success;
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = aprstaking_result.ErrorMessage;
                                                txHash = aprstaking_result.Hex;
                                                break;
                                            case "自动Mintmagiceden(NFT)":
                                                LogService.AppendLog("自动Mintmagiceden(NFT)");
                                                var NftAdd = "0x9f97586c5be23Eb2036CaAe0F582a28a84eA1B13";
                                                MintNFT mintNFT = new MintNFT();
                                                var mintNFTresult = await mintNFT.MintNftAsync(wallet.PrivateKey, NftAdd, proxyViewModel);
                                                isSuccess = mintNFTresult.Success;
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = mintNFTresult.ErrorMessage;
                                                txHash = mintNFTresult.Hex;
                                                break;
                                            case "自动创建域名(nad.domains)":
                                                try
                                                {
                                                    LogService.AppendLog("自动创建域名(nad.domains)");
                                                    string name = wallet.Address;
                                                    string discountKey = "0x0000000000000000000000000000000000000000000000000000000000000000";
                                                    string referrer = "0x0000000000000000000000000000000000000000";
                                                    string discountClaimProof = "0x0000000000000000000000000000000000000000000000000000000000000000";
                                                    NameServer nameServer = new NameServer();
                                                    var aa = await nameServer.GetSignatureData(name, wallet.Address, true, referrer, discountKey, discountClaimProof, "W10", 10143,proxyViewModel);

                                                    if (aa.Success)
                                                    {
                                                        LogService.AppendLog("获取签名数据成功,开始创建域名");
                                                        var domainresult = await nameServer.MintNameServer(wallet.PrivateKey, name, aa.Nonce, aa.Deadline, aa.Signature, proxyViewModel);
                                                        isSuccess = domainresult.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = domainresult.ErrorMessage;
                                                        txHash = domainresult.Hex;
                                                        LogService.AppendLog(resultStatus);
                                                    }
                                                    else
                                                    {
                                                        LogService.AppendLog("获取签名数据失败");
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = "获取签名数据失败";
                                                    }

                                                }
                                                catch (Exception ex)
                                                {
                                                    LogService.AppendLog(ex.Message);
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = ex.Message;
                                                }

                                                break;
                                            case "自动买卖本帐号创建Meme(nad.fun)":

                                                #region 买卖自已发行的代币
                                                try
                                                {
                                                    var client = new NadFunApiClient(proxyViewModel);
                                                    LogService.AppendLog("买卖自已发行的代币");
                                                    await RunRegAsync(wallet.PrivateKey);
                                                    var mytoken = await client.GetAccountCreatedTokensAsync(wallet.Address);
                                                    if (mytoken.total_count == 0)
                                                    {
                                                        LogService.AppendLog("本帐号未发行代币");
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = "本帐号未发行代币";
                                                        break;
                                                    }
                                                    LogService.AppendLog($"本帐号共发行代币{mytoken.total_count}");
                                                    //自己创建的Meme
                                                    foreach (var item in mytoken.tokens)
                                                    {
                                                        Debug.WriteLine($"{item.token.name}-{item.token.token_address}");
                                                        LogService.AppendLog($"正在购买代币{item.token.name}");
                                                        var buyresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, item.token.token_address, "buy", proxyViewModel, task.Amount.ToString());
                                                        isSuccess = buyresult.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = buyresult.ErrorMessage;
                                                        txHash = buyresult.Hex;
                                                        if (isSuccess)
                                                        {
                                                            await Task.Delay(5000);
                                                            var sellresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, item.token.token_address, "sell", proxyViewModel, task.Amount.ToString());
                                                            isSuccess = sellresult.Success;
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = sellresult.ErrorMessage;
                                                            txHash = sellresult.Hex;
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {

                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = ex.Message;
                                                }

                                                #endregion
                                                break;
                                            case "自动买卖最新内盘Meme(nad.fun)":
                                                #region 买卖最新发行的MEME 
                                                try
                                                {
                                                    LogService.AppendLog("自动买卖最新内盘Meme(nad.fun)");
                                                    var client_nodev = new NadFunApiClient(proxyViewModel);
                                                    await RunRegAsync(wallet.PrivateKey);
                                                    var latestTokens = await client_nodev.GetTokensByCreationTimeAsync(1, 5);
                                                    if (latestTokens.order_token != null && latestTokens.order_token.Count > 0)
                                                    {
                                                        var firstToken = latestTokens.order_token[0];
                                                        var newtokenAddress = firstToken.token_info.token_address;
                                                        var marketInfo = await client_nodev.GetTokenMarketInfoAsync(newtokenAddress);
                                                        var nodevbuyresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "buy", proxyViewModel, task.Amount.ToString());
                                                        isSuccess = nodevbuyresult.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = nodevbuyresult.ErrorMessage;
                                                        txHash = nodevbuyresult.Hex;

                                                        if (isSuccess)
                                                        {
                                                            await Task.Delay(5000);
                                                            var nodevsellresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "sell", proxyViewModel, task.Amount.ToString());
                                                            isSuccess = nodevsellresult.Success;
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = nodevsellresult.ErrorMessage;
                                                            txHash = nodevsellresult.Hex;
                                                        }

                            }
                            else
                            {
                                                        Debug.WriteLine("没有找到代币数据");
                                                    }


                                                }
                                                catch (Exception ex)
                                                {
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = ex.Message;

                                                }

                                                #endregion
                                                break;
                                            case "自动买卖市值前[1-10]Meme(nad.fun)":
                                                try
                                                {
                                                    try
                                                    {
                                                  //      await RunRegAsync(wallet.PrivateKey);
                                                    }
                                                    catch (Exception)
                                                    {
                                                    }

                                                    var client_dev = new NadFunApiClient(proxyViewModel);
                                                    #region 买卖已成功发射的代币（市值排名前10的）
                                                    LogService.AppendLog("买卖已成功发射的代币（随机买卖市值排名前10的)");
                                                        try
                                                        {
                                                            var DEVtoken = await client_dev.GetTokensByMarketCapAsync();
                                                            if (DEVtoken.order_token != null && DEVtoken.order_token.Count > 0)
                                                            {
                                                                if (DEVtoken.order_token != null && DEVtoken.order_token.Count > 9)
                                                                {
                                                                    Random random = new Random();
                                                                    int number = random.Next(0, 10);
                                                                    var firstToken = DEVtoken.order_token[number];
                                                                    var newtokenAddress = firstToken.token_info.token_address;
                                                                    if (newtokenAddress.Length < 10)
                                                                    {
                                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                                        errorMsg = "获取失败";
                                                                    }
                                                                    //  var marketInfo = await client_dev.GetTokenMarketInfoAsync(newtokenAddress);
                                                                    var Devbuy_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "buy", proxyViewModel, task.Amount.ToString());
                                                                    isSuccess = Devbuy_result.Success;
                                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                                    errorMsg = Devbuy_result.ErrorMessage;
                                                                    txHash = Devbuy_result.Hex;
                                                                    if (isSuccess)
                                                                    {
                                                                        await Task.Delay(5000);
                                                                        var DevSell_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "sell", proxyViewModel, task.Amount.ToString());
                                                                        isSuccess = DevSell_result.Success;
                                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                                        errorMsg = DevSell_result.ErrorMessage;
                                                                        txHash = DevSell_result.Hex;
                                                                    }

                                                                }
                                                                else
                                                                {
                                                                    var firstToken = DEVtoken.order_token[0];
                                                                    var newtokenAddress = firstToken.token_info.token_address;
                                                                    var marketInfo = await client_dev.GetTokenMarketInfoAsync(newtokenAddress);
                                                                    var Devbuy_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "buy", proxyViewModel, task.Amount.ToString());
                                                                    isSuccess = Devbuy_result.Success;
                                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                                    errorMsg = Devbuy_result.ErrorMessage;
                                                                    txHash = Devbuy_result.Hex;
                                                                    if (isSuccess)
                                                                    {
                                                                        await Task.Delay(5000);
                                                                        var DevSell_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "sell", proxyViewModel, task.Amount.ToString());
                                                                        isSuccess = DevSell_result.Success;
                                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                                        errorMsg = DevSell_result.ErrorMessage;
                                                                        txHash = DevSell_result.Hex;
                                                                    }
                                                                }


                                                            }
                                                            else
                                                            {
                                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                                errorMsg = "没有找到代币数据";
                                                                Debug.WriteLine("没有找到代币数据");
                                                            }
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = ex.Message;
                                                        }
                                                    }
                                                        catch (Exception eex)
                                                        {

                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = eex.Message;
                                                        break;
                                                    } 
                                                #endregion
                                                break;
                                            case "自动卖出所有持仓Meme(nad.fun)":
                                                try
                                                {
                                                    var client_sellall = new NadFunApiClient(proxyViewModel);
                                                    LogService.AppendLog("卖出所有持仓");
                                                    var positions = await client_sellall.GetAccountPositionsAsync(wallet.Address, "open", 1, 10);

                                                    if (positions.positions == null || positions.positions.Count == 0)
                                                    {
                                                        LogService.AppendLog("没有找到仓位数据");
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = "没有找到仓位数据";
                                                    }
                                                    else
                                                    {
                                                        LogService.AppendLog($"找到 {positions.positions.Count} 个仓位");
                                                        foreach (var position in positions.positions)
                                                        {
                                                            try
                                                            {

                                                                LogService.AppendLog($"代币: {position.token.name} ({position.token.symbol})");
                                                                LogService.AppendLog($"数量: {position.position.FormatTokenAmount()}");
                                                                LogService.AppendLog($"价格: {position.market.FormatPrice()}");
                                                                var FormatUnrealizedPnl = Web3.Convert.FromWei(BigInteger.Parse(position.position.FormatUnrealizedPnl()));
                                                                LogService.AppendLog($"未实现盈亏: {FormatUnrealizedPnl} MON");
                                                            }
                                                            catch (Exception)
                                                            {


                                                            }
                                                            var resultmeme = await TradingExample.RunExampleAsync(wallet.PrivateKey, position.token.token_address, "sell", proxyViewModel,position.position.FormatTokenAmount());
                                                            isSuccess = resultmeme.Success;
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = resultmeme.ErrorMessage;
                                                            txHash = resultmeme.Hex;
                                                        }

                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = ex.Message;
                                                }


                                                break;
                                            case "自动创建Meme(nad.fun)":
                                                   isCtmeme = true;
                                                    //try
                                                    //{
                                                    //    await RunRegAsync(wallet.PrivateKey);
                                                    //    NdfMint ndfMint = new NdfMint();
                                                    //    var ndfresult = await ndfMint.NdfMintAsync(wallet.PrivateKey);
                                                    //    isSuccess = ndfresult.Success;
                                                    //    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    //    errorMsg = ndfresult.ErrorMessage;
                                                    //    txHash = ndfresult.Hex;
                                                    //}
                                                    //catch (Exception ex)
                                                    //{
                                                    //    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    //    errorMsg = ex.Message;

                                                    //}
                                                    errorMsg = "交互失败";
                                                    resultStatus = "交互失败";
                                                    
                                                    break;
                                            default:
                                                break;
                                        }
                                        break;
                                    case "PharosNetwork":
                                            switch (action)
                                            {
                                                case "绑定主帐号(PharosNetwork)":
                                                    FaucetRequester cfaucetRequester = new FaucetRequester(proxyViewModel);
                                                    Debug.WriteLine("交互任务绑定主帐号" + task.Info);
                                                    var loginSignResult = await cfaucetRequester.LoginAndSignInAsync(wallet, task.Info);
                                                    Debug.WriteLine(loginSignResult.message);
                                                    isSuccess = loginSignResult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = loginSignResult.message;
                                                    break;
                                                case "每日签到(PharosNetwork)":
                                                    FaucetRequester faucetRequester = new FaucetRequester(proxyViewModel);
                                                    var result = await faucetRequester.LoginAndSignInAsync(wallet, "");
                                                    isSuccess = result.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    // errorMsg = result.message;
                                                    txHash = result.message;
                                                    break;
                                                case "交互任务SWAP(Phrs换wPhrs)(PharosNetwork)":
                                                    var phtowphwapper = new TokenSwapper(wallet.PrivateKey, proxyViewModel);
                                                    await Task.Delay(2000);
                                                    var phtowphbalance = await phtowphwapper.GetWETHBalance(wallet.Address);
                                                    var ethAmount = Web3.Convert.ToWei(task.Amount);
                                                    var phtowphswapresult = await phtowphwapper.ConvertEthToWeth(ethAmount);
                                                    isSuccess = phtowphswapresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = phtowphswapresult.message;
                                                    break; 
                                                case "交互任务SWAP(wPhrs换USDC)(PharosNetwork)":
                                                    var wptousdcswapper = new TokenSwapper(wallet.PrivateKey, proxyViewModel);
                                                    string _weth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                                    string _usdc = "0xad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37";
                                                    var wptousdcisswapresult = await wptousdcswapper.SwapTokenForWETH(_weth, _usdc, task.Amount, 0.00031m);
                                                    isSuccess = wptousdcisswapresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = wptousdcisswapresult.message;
                                                    break;
                                                case "交互任务SWAP(wPhrs换USDT)(PharosNetwork)":
                                                    var wptousdtswapper = new TokenSwapper(wallet.PrivateKey, proxyViewModel);
                                                    string _usweth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                                    string _usdt = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";
                                                    var wptousdtissusdtwapresult = await wptousdtswapper.SwapTokenForWETH(_usweth, _usdt, task.Amount, 0.00031m);
                                                    isSuccess = wptousdtissusdtwapresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = wptousdtissusdtwapresult.message;
                                                    break;
                                                case "交互任务SWAP(wPhrs换Phrs)(PharosNetwork)":
                                                    var wswapper = new TokenSwapper(wallet.PrivateKey, proxyViewModel);
                                                    //await Task.Delay(2000);
                                                    //var wbalance = await wswapper.GetWETHBalance(wallet.Address);
                                                    var _ethAmount = Web3.Convert.ToWei(task.Amount);
                                                    var wisswapresult = await wswapper.WithdrawWETH(_ethAmount);
                                                    isSuccess = wisswapresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = wisswapresult.message;
                                                    break;
                                                case "交互任务SWAP(USDC换wPhrs)(PharosNetwork)":
                                                    var swapper = new TokenSwapper(wallet.PrivateKey, proxyViewModel);
                                                    string weth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                                    string usdc = "0xad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37";
                                                    var isswapresult = await swapper.SwapTokenForWETH(usdc, weth, task.Amount, 0.00031m);
                                                    isSuccess = isswapresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = isswapresult.message;
                                                    break;
                                                case "交互任务SWAP(USDT换wPhrs)(PharosNetwork)":
                                                    var usdtswapper = new TokenSwapper(wallet.PrivateKey, proxyViewModel);
                                                    string usweth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                                    string usdt = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";
                                                    var issusdtwapresult = await usdtswapper.SwapTokenForWETH(usdt, usweth, task.Amount, 0.00031m);
                                                    isSuccess = issusdtwapresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = issusdtwapresult.message;
                                                    break;

                                                case "交互任务Addliquidity(wPhrs/usdc)[添加流动性0.0001wPhrs/usdc](PharosNetwork)":
                                                    var AddliquiditywPhrsusdc = new TokenSwapper (wallet.PrivateKey, proxyViewModel);
                                                   
                                                    var Addliquidityresult = await AddliquiditywPhrsusdc.AddlquiditywPharUsdc();
                                                    isSuccess = Addliquidityresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = Addliquidityresult.message;
                                                    break;
                                                case "交互任务Addliquidity(usdc/usdt)[添加流动性1usdc/usdt](PharosNetwork)":
                                                    var Addliquidityusdc = new TokenSwapper(wallet.PrivateKey, proxyViewModel);

                                                    var Addliquidityusdcresult = await Addliquidityusdc.AddlquiditywUsdcUsdt();
                                                    isSuccess = Addliquidityusdcresult.success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    txHash = Addliquidityusdcresult.message;
                                                    break;
                                                //特殊福利[使用本脚本每日可领0.01Phrs](PharosNetwork)  交互任务Addliquidity(wPhrs/usdc)[添加流动性0.0001wPhrs/usdc](PharosNetwork)
                                                case "特殊福利[使用本脚本每日可领0.01Phrs](PharosNetwork)":
                                                    

                                                    try
                                                    {
                                                       
                                                        //isSuccess = resultfaucet.success;
                                                        //resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        //txHash = resultfaucet.message;
                                                        //LogService.AppendLog($"{resultStatus}" + txHash);
                                                        //await Task.Delay(1000);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        LogService.AppendLog(e.Message);
                                                        break;
                                                    } 
                                                    break;
                                                case "每日领水1000USDC(PharosNetwork)":
                                                    //FaucetRequester faucetRequesterFaucet = new FaucetRequester();
                                                    //var resultfaucet = await faucetRequesterFaucet.RequestFaucetAsync("0xAD902CF99C2dE2f1Ba5ec4D642Fd7E49cae9EE37",wallet.Address);//1.老版本通过网页领取USDC已改版
                                                    //var usdcaddress = "0xAD902CF99C2dE2f1Ba5ec4D642Fd7E49cae9EE37";  
                                                    //var usdtaddress = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";
                                                  
                                                    //    try
                                                    //    {
                                                    //        var caller = new MintCaller(wallet.PrivateKey, proxyViewModel);//2.新新本通过合约领取已被官方禁用
                                                    //        var resultfaucet = await caller.CallMintAsync(usdcaddress);
                                                    //        LogService.AppendLog($"{wallet.Address} Mint tx hash: {txHash}");
                                                    //        isSuccess = resultfaucet.success;
                                                    //        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    //        txHash = resultfaucet.message;
                                                    //        LogService.AppendLog($"{resultStatus}" + txHash);
                                                    //        await Task.Delay(1000);
                                                    //    }
                                                    //    catch (Exception e)
                                                    //{
                                                    //    resultStatus = isSuccess ? "交互成功" : "交互失败";  
                                                    //    LogService.AppendLog(e.Message);
                                                    //        break; 
                                                    //    }
                                                     
                                                    
                                                    // var caller = new MintCaller(wallet.PrivateKey);
                                                    //var resultfaucet = await caller.CallMintAsync(usdtaddress); 
                                                    //Console.WriteLine($"Mint tx hash: {txHash}");
                                                    //isSuccess = resultfaucet.success;
                                                    //resultStatus = isSuccess ? "交互成功" : "交互失败"; 
                                                    //txHash = resultfaucet.message;
                                                    break;
                                                default:
                                                    break;
                                            }
                                            break;
                                        default:
                                            break; 
                            }
                            
                           
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                if (recordIndex < taskRecords.Count && recordIndex >= 0)
                                {
                                    var record = taskRecords[recordIndex];
                                    
                                    
                                    record.Status = resultStatus;
                                    record.Success = isSuccess;
                                    record.ErrorMessage = errorMsg;
                                    record.TransactionHash = txHash;
                                    record.OperationTime = DateTime.Now;
                                    
                                    var temp = taskRecords[recordIndex];
                                    taskRecords[recordIndex] = null;
                                    taskRecords[recordIndex] = temp;
                                     
                                    var taskRecord = new TaskExecutionRecord
                                    {
                                        WalletAddress = wallet.Address,
                                        Status = resultStatus,
                                        OperationTime = DateTime.Now,
                                        Success = isSuccess,
                                        ErrorMessage = errorMsg,
                                            TransactionHash = txHash,
                                            TaskName = task.Name,
                                            ProjectName = task.ProjectName
                                    };
                                    
                                    AddExecutionRecord(task.Id, taskRecord);
                                }
                                
                                
                                Data.ExecutionStore.SaveRecords();
                                
                                  
                                    task.LastProcessedIndex = walletIndex;
                                    UpdateTask(task);

                                  
                                    UpdateTaskProgressByTotalActions(task, wallets.Count, validTaskActions.Count, walletIndex, actionIndex);
                                });
                            }
                        }
                        finally
                        {
                            
                            if (!string.IsNullOrEmpty(currentProxyKey))
                            {
                                bool removed;
                                usedProxies.TryRemove(currentProxyKey, out removed);
                                Debug.WriteLine($"线程 {threadId} 释放代理: {currentProxyKey}");
                            }
                            
                            
                                lock (lockObj)
                                {
                                activeTaskCount--;
                                
                                // 如果队列为空且没有活跃任务，通知所有任务完成
                                if (walletQueue.IsEmpty && activeTaskCount == 0)
                                {
                                    // 清空代理使用池
                                    usedProxies.Clear();
                                    allTasksCompleted.Set();
                                }
                            }
                        }
                    }
                    
                    // 创建一个工作线程，不断从队列获取并处理钱包
                    async Task WorkerThread(int threadId)
                    {
                        while (walletQueue.TryDequeue(out var wallet))
                        {
                            // 检查任务是否已取消
                            if (task.Status == Models.TaskStatus.Cancelled || task.Status == TaskStatus.Paused)
                            {
                                break;
                            }
                            
                            // 增加活跃任务计数
                            lock (lockObj)
                            {
                                activeTaskCount++;
                            }
                            
                            try
                            {
                                await ProcessWallet(threadId, wallet);
                            }
                            catch (OperationCanceledException)
                            {
                                // 任务已取消，跳出循环
                                break;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"处理钱包时出错: {ex.Message}");
                                // 继续处理下一个钱包
                            }
                        }
                    }
                    
                    // 启动工作线程
                    var workerTasks = new List<Task>();
                    for (int i = 0; i < threadCount; i++)
                    {
                        int threadId = i; // 捕获循环变量
                        workerTasks.Add(Task.Run(() => WorkerThread(threadId)));
                    }
                    
                    // 等待所有工作线程完成或任务被取消
                    if (workerTasks.Count > 0)
                    {
                        // 等待所有任务完成的信号，或者最长等待1小时
                        allTasksCompleted.WaitOne(TimeSpan.FromMinutes(3));
                    }
                        if (isCtmeme)
                        {
                            MessageBox.Show("自己创建Meme不支持多线程");
                        }
                }
            
                
                // 任务执行完成后更新状态
                if (task.Status == TaskStatus.Running)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CompleteTask(task.Id);
                        
                        // 确保最终状态也保存到文件
                        Data.ExecutionStore.SaveRecords();
                    });
                    }
                }
                else // 单线程执行（原始方式）
                {
                Debug.WriteLine("————————单线程执行任务");
                    // 为每个钱包执行任务
                    int walletIndex = startWalletIndex;
                    for (int i = startWalletIndex; i < wallets.Count; i++)
                    {
                    // 重要：每次开始新钱包处理前获取最新的任务状态
                    var currentTask = GetTaskById(task.Id);
                    if (currentTask == null || currentTask.Status == Models.TaskStatus.Cancelled || currentTask.Status == TaskStatus.Paused)
                    {
                        // 保存进度并直接退出
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 更新最后处理位置
                            if (currentTask != null)
                            {
                                currentTask.LastProcessedIndex = i;
                                UpdateTask(currentTask);
                            }

                            // 更新钱包执行记录状态
                            var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                            for (int recordIdx = 0; recordIdx < taskRecords.Count; recordIdx++)
                            {
                                var record = taskRecords[recordIdx];

                                // 只更新未完成记录的状态
                                if (record.Success == null &&
                                    !record.Status.Contains("暂停") &&
                                    !record.Status.Contains("取消") &&
                                    !record.Status.Contains("成功") &&
                                    !record.Status.Contains("失败"))
                                {
                                    record.Status = currentTask.Status == TaskStatus.Paused ? "已暂停" : "已取消";
                                }
                            }

                            // 保存执行记录
                            Data.ExecutionStore.SaveRecords();
                        });
                        return; // 直接退出不再继续处理
                    }

                    var wallet = wallets[i];
                        walletIndex = i;
                        int actionIndex = 0;
                        
                        // 对每个任务动作执行操作
                        foreach (var action in validTaskActions)
                        {
                        // 再次检查任务状态，即使在处理单个动作过程中也可以响应暂停/终止
                        currentTask = GetTaskById(task.Id);
                        if (currentTask == null || currentTask.Status == Models.TaskStatus.Cancelled || currentTask.Status == TaskStatus.Paused)
                        {
                            // 保存进度并直接退出
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // 更新最后处理位置
                                if (currentTask != null)
                                {
                                    currentTask.LastProcessedIndex = i;
                                    UpdateTask(currentTask);
                                }

                                // 更新钱包执行记录状态
                                var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                for (int recordIdx = 0; recordIdx < taskRecords.Count; recordIdx++)
                                {
                                    var record = taskRecords[recordIdx];

                                    // 只更新未完成记录的状态
                                    if (record.Success == null &&
                                        !record.Status.Contains("暂停") &&
                                        !record.Status.Contains("取消") &&
                                        !record.Status.Contains("成功") &&
                                        !record.Status.Contains("失败"))
                                    {
                                        record.Status = currentTask.Status == TaskStatus.Paused ? "已暂停" : "已取消";
                                    }
                                }

                                // 保存执行记录
                                Data.ExecutionStore.SaveRecords();
                            });
                            throw new OperationCanceledException("任务已取消或暂停");
                        }

                            actionIndex++;

                            int recordIndex = i * validTaskActions.Count + (actionIndex - 1);
                            
                            // 使用应用程序的Dispatcher来更新UI
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                if (recordIndex < taskRecords.Count && recordIndex >= 0)
                                {
                                    var record = taskRecords[recordIndex];
                                    
                                    // 更新记录状态为处理中
                                    record.Status = "处理中";
                                    // 触发UI刷新
                                    var temp = taskRecords[recordIndex];
                                    taskRecords[recordIndex] = null;
                                    taskRecords[recordIndex] = temp;
                                }
                            });

                        
                        // 模拟操作结果
                      bool isSuccess =false;  
                            string resultStatus = "";
                            string errorMsg = null;
                            string txHash = null;

                            switch (task.ProjectName)
                            {
                                case "Monad":

                                    switch (action)
                                    {
                                        case "自动领水(MON)":
                                            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
                                            resultStatus = isSuccess ? "领水成功" : "领水失败";
                                            errorMsg = isSuccess ? null : $"{action} 执行失败";
                                            txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null; 
                                            break;
                                        case "自动质押magma(gMON)":
                                            LogService.AppendLog("自动质押magma(gMON)");
                                        MonadStaking monadStaking = new MonadStaking(wallet.PrivateKey);
                                            var result = await monadStaking.StakeMon(task.Amount);
                                            isSuccess = result.Success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            errorMsg = result.ErrorMessage;
                                            txHash = result.Hex; 
                                            break;
                                        case "自动质押aPriori(aprMON)":
                                            LogService.AppendLog("自动质押aPriori(aprMON)");
                                        aprstaking aprstaking_ = new aprstaking(wallet.PrivateKey);
                                            var aprstaking_result = await aprstaking_.DepositAsync(task.Amount, wallet.Address);
                                            isSuccess = aprstaking_result.Success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            errorMsg = aprstaking_result.ErrorMessage;
                                            txHash = aprstaking_result.Hex;
                                            break;
                                        case "自动Mintmagiceden(NFT)":
                                            LogService.AppendLog("自动Mintmagiceden(NFT)");
                                            var NftAdd = "0x9f97586c5be23Eb2036CaAe0F582a28a84eA1B13";
                                            MintNFT mintNFT = new MintNFT();
                                            var mintNFTresult = await mintNFT.MintNftAsync(wallet.PrivateKey, NftAdd);
                                            isSuccess = mintNFTresult.Success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            errorMsg = mintNFTresult.ErrorMessage;
                                            txHash = mintNFTresult.Hex; 
                                            break;
                                        case "自动创建域名(nad.domains)":
                                            try
                                            {
                                                LogService.AppendLog("自动创建域名(nad.domains)");
                                                string name = wallet.Address;
                                                string discountKey = "0x0000000000000000000000000000000000000000000000000000000000000000";
                                                string referrer = "0x0000000000000000000000000000000000000000";
                                                string discountClaimProof = "0x0000000000000000000000000000000000000000000000000000000000000000";
                                                NameServer nameServer = new NameServer();
                                                var aa = await nameServer.GetSignatureData(name, wallet.Address, true, referrer, discountKey, discountClaimProof, "W10", 10143);
                                               
                                                if (aa.Success)
                                                {
                                                    LogService.AppendLog("获取签名数据成功,开始创建域名");
                                                    var domainresult = await nameServer.MintNameServer(wallet.PrivateKey, name, aa.Nonce, aa.Deadline, aa.Signature);
                                                    isSuccess = domainresult.Success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = domainresult.ErrorMessage;
                                                    txHash = domainresult.Hex;
                                                    LogService.AppendLog(resultStatus);
                                                }
                                                else
                                                {
                                                    LogService.AppendLog("获取签名数据失败"); 
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = "获取签名数据失败";
                                                }

                                            }
                                            catch (Exception ex)
                                            {
                                                LogService.AppendLog(ex.Message);
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ex.Message;
                                            }
                                          
                                            break; 
                                        case "自动买卖本帐号创建Meme(nad.fun)":
                                           
                                            #region 买卖自已发行的代币
                                            try
                                            {
                                                var client = new NadFunApiClient();
                                                LogService.AppendLog("买卖自已发行的代币");
                                            await RunRegAsync(wallet.PrivateKey);
                                                var mytoken = await client.GetAccountCreatedTokensAsync(wallet.Address);
                                                if (mytoken.total_count == 0)
                                                {
                                                    LogService.AppendLog("本帐号未发行代币");
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = "本帐号未发行代币";
                                                    break;
                                                }
                                                LogService.AppendLog($"本帐号共发行代币{mytoken.total_count}");
                                                //自己创建的Meme
                                                foreach (var item in mytoken.tokens)
                                                {
                                                Debug.WriteLine($"{item.token.name}-{item.token.token_address}");
                                                    LogService.AppendLog($"正在购买代币{item.token.name}");
                                                    var buyresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, item.token.token_address, "buy", task.Amount.ToString());
                                                    isSuccess = buyresult.Success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = buyresult.ErrorMessage;
                                                    txHash = buyresult.Hex;
                                                    if (isSuccess)
                                                    {
                                                        await Task.Delay(5000);
                                                        var sellresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, item.token.token_address, "sell", task.Amount.ToString());
                                                        isSuccess = sellresult.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = sellresult.ErrorMessage;
                                                        txHash = sellresult.Hex;
                                                    } 
                                                }
                                            }
                                            catch (Exception ex)
                                            {

                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ex.Message; 
                                            }
                                          
                                            #endregion
                                            break;
                                        case "自动买卖最新内盘Meme(nad.fun)":
                                            #region 买卖最新发行的MEME 
                                            try
                                            {
                                                LogService.AppendLog("自动买卖最新内盘Meme(nad.fun)");
                                                var client_nodev = new NadFunApiClient();
                                                await RunRegAsync(wallet.PrivateKey);
                                                var latestTokens = await client_nodev.GetTokensByCreationTimeAsync(1, 5);
                                                if (latestTokens.order_token != null && latestTokens.order_token.Count > 0)
                                                {
                                                    var firstToken = latestTokens.order_token[0];
                                                    var newtokenAddress = firstToken.token_info.token_address;
                                                    var marketInfo = await client_nodev.GetTokenMarketInfoAsync(newtokenAddress);
                                                    var nodevbuyresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "buy", task.Amount.ToString());
                                                    isSuccess = nodevbuyresult.Success;
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = nodevbuyresult.ErrorMessage;
                                                    txHash = nodevbuyresult.Hex;

                                                    if (isSuccess)
                                                    {
                                                        await Task.Delay(5000);
                                                        var nodevsellresult = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "sell", task.Amount.ToString());
                                                        isSuccess = nodevsellresult.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = nodevsellresult.ErrorMessage;
                                                        txHash = nodevsellresult.Hex;
                                                    }

                                                }
                                                else
                                                {
                                                    Debug.WriteLine("没有找到代币数据");
                                                }


                                            }
                                            catch (Exception ex)
                                            {
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ex.Message;

                                            }
                                           
                                            #endregion
                                            break;
                                        case "自动买卖市值前[1-10]Meme(nad.fun)":
                                            try
                                            {
                                                try
                                                {
                                            //    await RunRegAsync(wallet.PrivateKey);
                                                }
                                                catch (Exception)
                                                { 
                                                }
                                              
                                                var client_dev = new NadFunApiClient();
                                                #region 买卖已成功发射的代币（市值排名前1的）
                                                LogService.AppendLog("买卖已成功发射的代币（随机买卖市值排名前10的)");
                                                var DEVtoken = await client_dev.GetTokensByMarketCapAsync();
                                                if (DEVtoken.order_token != null && DEVtoken.order_token.Count > 0)
                                                {
                                                if (DEVtoken.order_token != null && DEVtoken.order_token.Count > 9)
                                                    {
                                                        Random random = new Random();
                                                        int number = random.Next(0, 10);
                                                        var firstToken = DEVtoken.order_token[number];
                                                        var newtokenAddress = firstToken.token_info.token_address;
                                                    if (newtokenAddress.Length < 10)
                                                        { 
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = "获取失败";
                                                        }
                                                       //  var marketInfo = await client_dev.GetTokenMarketInfoAsync(newtokenAddress);
                                                        var Devbuy_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "buy", task.Amount.ToString());
                                                        isSuccess = Devbuy_result.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = Devbuy_result.ErrorMessage;
                                                        txHash = Devbuy_result.Hex;
                                                        if (isSuccess)
                                                        {
                                                            await Task.Delay(5000);
                                                            var DevSell_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "sell", task.Amount.ToString());
                                                            isSuccess = DevSell_result.Success;
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = DevSell_result.ErrorMessage;
                                                            txHash = DevSell_result.Hex;
                                                        }

                                                    }
                                                    else
                                                    {
                                                        var firstToken = DEVtoken.order_token[0];
                                                        var newtokenAddress = firstToken.token_info.token_address;
                                                        var marketInfo = await client_dev.GetTokenMarketInfoAsync(newtokenAddress);
                                                        var Devbuy_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "buy", task.Amount.ToString());
                                                        isSuccess = Devbuy_result.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = Devbuy_result.ErrorMessage;
                                                        txHash = Devbuy_result.Hex;
                                                        if (isSuccess)
                                                        {
                                                            await Task.Delay(5000);
                                                            var DevSell_result = await TradingExample.RunExampleAsync(wallet.PrivateKey, newtokenAddress, "sell", task.Amount.ToString());
                                                            isSuccess = DevSell_result.Success;
                                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                            errorMsg = DevSell_result.ErrorMessage;
                                                            txHash = DevSell_result.Hex;
                                                        }
                                                    }
                                                    
                                                  
                                                }
                                                else
                                                {
                                                    resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                    errorMsg = "没有找到代币数据";
                                                    Debug.WriteLine("没有找到代币数据");
                                                }
                                            }
                                            catch (Exception ex)
                                            { 
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ex.Message;
                                            }
                                           
                                            #endregion
                                            break;
                                        case "自动卖出所有持仓Meme(nad.fun)":
                                            try
                                            {
                                                var client_sellall = new NadFunApiClient();
                                                LogService.AppendLog("卖出所有持仓");
                                                var positions = await client_sellall.GetAccountPositionsAsync(wallet.Address, "open", 1, 10);

                                                if (positions.positions == null || positions.positions.Count == 0)
                                                {
                                                    LogService.AppendLog("没有找到仓位数据");
                                                resultStatus = "交互成功";
                                                //  errorMsg = "没有找到仓位数据";
                                                }
                                                else
                                                {
                                                    LogService.AppendLog($"找到 {positions.positions.Count} 个仓位");
                                                    foreach (var position in positions.positions)
                                                    {
                                                        try
                                                        {
                                                           
                                                                  LogService.AppendLog($"代币: {position.token.name} ({position.token.symbol})");
                                                                  LogService.AppendLog($"数量: {position.position.FormatTokenAmount()}");
                                                                  LogService.AppendLog($"价格: {position.market.FormatPrice()}");
                                                            var FormatUnrealizedPnl = Web3.Convert.FromWei(BigInteger.Parse(position.position.FormatUnrealizedPnl()));
                                                            LogService.AppendLog($"未实现盈亏: {FormatUnrealizedPnl} MON");
                                                        }
                                                        catch (Exception)
                                                        {

                                                           
                                                        } 
                                                        var resultmeme = await TradingExample.RunExampleAsync(wallet.PrivateKey, position.token.token_address, "sell", position.position.FormatTokenAmount());
                                                        isSuccess = resultmeme.Success;
                                                        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                        errorMsg = resultmeme.ErrorMessage;
                                                        txHash = resultmeme.Hex;
                                                    }
                                                  
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ex.Message; 
                                            }
                                           
                                     
                                    break;
                                        case "自动创建Meme(nad.fun)":
                                            try
                                            {
                                            LogService.AppendLog("单线程创建Meme");
                                            // await RunRegAsync(wallet.PrivateKey);
                                                NdfMint ndfMint = new NdfMint();
                                                var ndfresult = await ndfMint.NdfMintAsync(wallet.PrivateKey);
                                                isSuccess = ndfresult.Success;
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ndfresult.ErrorMessage;
                                                txHash = ndfresult.Hex;
                                            }
                                            catch (Exception ex)
                                            {
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                errorMsg = ex.Message;

                                            }
                                          
                                            break;
                                        default:
                                            break;
                                    } 
                                    break;
                                case "PharosNetwork":
                                    switch (action)
                                    {
                                        case "每日签到(PharosNetwork)":
                                            FaucetRequester faucetRequester = new FaucetRequester();
                                            var result =  await faucetRequester.LoginAndSignInAsync(wallet, "");
                                            isSuccess = result.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                           // errorMsg = result.message;
                                            txHash = result.message;
                                            break;
                                        case "交互任务SWAP(Phrs换wPhrs)(PharosNetwork)":
                                            var phtowphwapper = new TokenSwapper(wallet.PrivateKey);
                                            await Task.Delay(2000);
                                            var phtowphbalance = await phtowphwapper.GetWETHBalance(wallet.Address);
                                            var ethAmount = Web3.Convert.ToWei(task.Amount);
                                            var phtowphswapresult = await phtowphwapper.ConvertEthToWeth(ethAmount);
                                            isSuccess = phtowphswapresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = phtowphswapresult.message;
                                            break;
                                        case "交互任务SWAP(wPhrs换USDC)(PharosNetwork)":
                                            var wptousdcswapper = new TokenSwapper(wallet.PrivateKey);
                                            string _weth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                            string _usdc = "0xad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37";
                                            var wptousdcisswapresult = await wptousdcswapper.SwapTokenForWETH(_weth, _usdc, task.Amount, 0.00031m);
                                            isSuccess = wptousdcisswapresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = wptousdcisswapresult.message;
                                            break;
                                        case "交互任务SWAP(wPhrs换USDT)(PharosNetwork)":
                                            var wptousdtswapper = new TokenSwapper(wallet.PrivateKey);
                                            string _usweth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                            string _usdt = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";
                                            var wptousdtissusdtwapresult = await wptousdtswapper.SwapTokenForWETH(_usweth, _usdt, task.Amount, 0.00031m);
                                            isSuccess = wptousdtissusdtwapresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = wptousdtissusdtwapresult.message;
                                            break;
                                        case "交互任务SWAP(wPhrs换Phrs)(PharosNetwork)":
                                            var wswapper = new TokenSwapper(wallet.PrivateKey);
                                            //await Task.Delay(2000);
                                            //var wbalance = await wswapper.GetWETHBalance(wallet.Address);
                                            var _ethAmount = Web3.Convert.ToWei(task.Amount);
                                            var wisswapresult = await wswapper.WithdrawWETH(_ethAmount);
                                            isSuccess = wisswapresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = wisswapresult.message;
                                            break;
                                        case "交互任务SWAP(USDC换wPhrs)(PharosNetwork)":
                                            var swapper = new TokenSwapper(wallet.PrivateKey);
                                            string weth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                            string usdc = "0xad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37";
                                            var isswapresult = await swapper.SwapTokenForWETH(usdc, weth, task.Amount, 0.00031m);
                                            isSuccess = isswapresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = isswapresult.message;
                                            break;
                                        case "交互任务SWAP(USDT换wPhrs)(PharosNetwork)":
                                            var usdtswapper = new TokenSwapper(wallet.PrivateKey);
                                            string usweth = "0x76aaada469d23216be5f7c596fa25f282ff9b364";
                                            string usdt = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";
                                            var issusdtwapresult = await usdtswapper.SwapTokenForWETH(usdt, usweth, task.Amount, 0.00031m);
                                            isSuccess = issusdtwapresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = issusdtwapresult.message;
                                            break;

                                        case "交互任务Addliquidity(wPhrs/usdc)[添加流动性0.0001wPhrs/usdc](PharosNetwork)":
                                            var AddliquiditywPhrsusdc = new TokenSwapper(wallet.PrivateKey);

                                            var Addliquidityresult = await AddliquiditywPhrsusdc.AddlquiditywPharUsdc();
                                            isSuccess = Addliquidityresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = Addliquidityresult.message;
                                            break;
                                        case "交互任务Addliquidity(usdc/usdt)[添加流动性1usdc/usdt](PharosNetwork)":
                                            var Addliquidityusdc = new TokenSwapper(wallet.PrivateKey);

                                            var Addliquidityusdcresult = await Addliquidityusdc.AddlquiditywUsdcUsdt();
                                            isSuccess = Addliquidityusdcresult.success;
                                            resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            txHash = Addliquidityusdcresult.message;
                                            break;
                                        //特殊福利[使用本脚本每日可领0.01Phrs](PharosNetwork)  交互任务Addliquidity(wPhrs/usdc)[添加流动性0.0001wPhrs/usdc](PharosNetwork)
                                        case "特殊福利[使用本脚本每日可领0.01Phrs](PharosNetwork)":


                                            try
                                            {

                                                //isSuccess = resultfaucet.success;
                                                //resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                //txHash = resultfaucet.message;
                                                //LogService.AppendLog($"{resultStatus}" + txHash);
                                                //await Task.Delay(1000);
                                            }
                                            catch (Exception e)
                                            {
                                                resultStatus = isSuccess ? "交互成功" : "交互失败";
                                                LogService.AppendLog(e.Message);
                                                break;
                                            }
                                            break;
                                        case "每日领水1000USDC(PharosNetwork)":
                                            //FaucetRequester faucetRequesterFaucet = new FaucetRequester();
                                            //var resultfaucet = await faucetRequesterFaucet.RequestFaucetAsync("0xAD902CF99C2dE2f1Ba5ec4D642Fd7E49cae9EE37",wallet.Address);//1.老版本通过网页领取USDC已改版
                                            //var usdcaddress = "0xAD902CF99C2dE2f1Ba5ec4D642Fd7E49cae9EE37";  
                                            //var usdtaddress = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";

                                            //    try
                                            //    {
                                            //        var caller = new MintCaller(wallet.PrivateKey, proxyViewModel);//2.新新本通过合约领取已被官方禁用
                                            //        var resultfaucet = await caller.CallMintAsync(usdcaddress);
                                            //        LogService.AppendLog($"{wallet.Address} Mint tx hash: {txHash}");
                                            //        isSuccess = resultfaucet.success;
                                            //        resultStatus = isSuccess ? "交互成功" : "交互失败";
                                            //        txHash = resultfaucet.message;
                                            //        LogService.AppendLog($"{resultStatus}" + txHash);
                                            //        await Task.Delay(1000);
                                            //    }
                                            //    catch (Exception e)
                                            //{
                                            //    resultStatus = isSuccess ? "交互成功" : "交互失败";  
                                            //    LogService.AppendLog(e.Message);
                                            //        break; 
                                            //    }


                                            // var caller = new MintCaller(wallet.PrivateKey);
                                            //var resultfaucet = await caller.CallMintAsync(usdtaddress); 
                                            //Console.WriteLine($"Mint tx hash: {txHash}");
                                            //isSuccess = resultfaucet.success;
                                            //resultStatus = isSuccess ? "交互成功" : "交互失败"; 
                                            //txHash = resultfaucet.message;
                                            break;
                                        default:
                                            break;
                                    }
                                    break;
                                default:
                                    break;
                            }
                         

                  

                            //// 根据不同任务动作设置不同的描述
                            //if (action.Contains("质押"))
                            //{
                            //    resultStatus = isSuccess ? "质押成功" : "质押失败";
                            //    errorMsg = isSuccess ? null : $"{action} 执行失败";
                            //    txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null;
                            //}
                            //else if (action.Contains("兑换"))
                            //{
                            //    resultStatus = isSuccess ? "兑换成功" : "兑换失败";
                            //    errorMsg = isSuccess ? null : $"{action} 执行失败";
                            //    txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null;
                            //}
                            //else if (action.Contains("mint") || action.Contains("Mint"))
                            //{
                            //    resultStatus = isSuccess ? "Mint成功" : "Mint失败";
                            //    errorMsg = isSuccess ? null : $"{action} 执行失败";
                            //    txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null;
                            //}
                            //else if (action.Contains("取回"))
                            //{
                            //    resultStatus = isSuccess ? "取回成功" : "取回失败";
                            //    errorMsg = isSuccess ? null : $"{action} 执行失败";
                            //    txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null;
                            //}
                            //else if (action.Contains("领水"))
                            //{
                            //    resultStatus = isSuccess ? "领水成功" : "领水失败";
                            //    errorMsg = isSuccess ? null : $"{action} 执行失败";
                            //    txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null;
                            //}
                            //else
                            //{
                            //    resultStatus = isSuccess ? "执行成功" : "执行失败";
                            //    errorMsg = isSuccess ? null : $"{action} 执行失败";
                            //    txHash = isSuccess ? $"0x{Guid.NewGuid().ToString().Replace("-", "")}" : null;
                            //}

                            // 使用应用程序的Dispatcher来更新UI
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                                if (recordIndex < taskRecords.Count && recordIndex >= 0)
                                {
                                    var record = taskRecords[recordIndex];
                                    
                                    // 更新记录最终状态
                                    record.Status = resultStatus;
                                    record.Success = isSuccess;
                                    record.ErrorMessage = errorMsg;
                                    record.TransactionHash = txHash;
                                    record.OperationTime = DateTime.Now;
                                    
                                    // 触发UI刷新
                                    var temp = taskRecords[recordIndex];
                                    taskRecords[recordIndex] = null;
                                    taskRecords[recordIndex] = temp;
                                    
                                    // 同时添加到任务记录中
                                    var taskRecord = new TaskExecutionRecord
                                    {
                                        WalletAddress = wallet.Address,
                                        Status = resultStatus,
                                        OperationTime = DateTime.Now,
                                        Success = isSuccess,
                                        ErrorMessage = errorMsg,
                                    TransactionHash = txHash,
                                    TaskName = task.Name,
                                    ProjectName = task.ProjectName
                                    };
                                    
                                    AddExecutionRecord(task.Id, taskRecord);
                                }
                                
                                // 每处理一条记录就保存一次执行详情，确保数据不会丢失
                                Data.ExecutionStore.SaveRecords();

                            // 定期保存任务进度到任务文件，确保程序意外关闭时不会丢失进度
                            // 更新任务的最后处理索引
                            task.LastProcessedIndex = i;
                            UpdateTask(task);
                                
                                // 更新进度 - 基于总任务数计算
                            UpdateTaskProgressByTotalActions(task, wallets.Count, validTaskActions.Count, walletIndex, actionIndex);
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 记录错误
                    var errorRecord = new Data.ExecutionRecord
                    {
                        TaskId = task.Id,
                        WalletAddress = "系统",
                        Status = "执行错误",
                        ErrorMessage = ex.Message,
                        OperationTime = DateTime.Now,
                        Success = false
                    };
                    
                    // 添加到ExecutionStore
                    var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
                    taskRecords.Add(errorRecord);
                    
                    // 添加到任务记录
                    var taskErrorRecord = new TaskExecutionRecord
                    {
                        WalletAddress = "系统",
                        Status = "执行错误",
                        ErrorMessage = ex.Message,
                        OperationTime = DateTime.Now,
                        Success = false
                    };
                    
                    AddExecutionRecord(task.Id, taskErrorRecord);
                    
                    // 标记任务为失败
                    task.Status = TaskStatus.Failed;
                    task.EndTime = DateTime.Now;
                    UpdateTask(task);
                    
                    // 保存执行记录详情
                    Data.ExecutionStore.SaveRecords();
                });
            }
        }
        
        // 将任务执行结果保存到文件
        private void SaveTaskResultToFile(Models.Task task)
        {
            try
            {
                // 创建任务执行结果目录
                string resultsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TaskResults");
                if (!Directory.Exists(resultsDirectory))
                {
                    Directory.CreateDirectory(resultsDirectory);
                }

                // 创建任务特定的结果文件
                string resultFilePath = Path.Combine(resultsDirectory, $"Task_{task.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                
                using (var writer = new StreamWriter(resultFilePath))
                {
                    // 写入任务基本信息
                    writer.WriteLine($"任务ID: {task.Id}");
                    writer.WriteLine($"项目名称: {task.ProjectName}");
                    writer.WriteLine($"分组名称: {task.GroupName}");
                    writer.WriteLine($"创建时间: {task.CreateTime}");
                    writer.WriteLine($"开始时间: {task.StartTime}");
                    writer.WriteLine($"结束时间: {task.EndTime ?? DateTime.Now}");
                    writer.WriteLine($"当前状态: {GetStatusText(task.Status)}");
                    writer.WriteLine($"执行进度: {task.Progress}%");
                    writer.WriteLine($"处理索引: {task.LastProcessedIndex}");
                    writer.WriteLine("");
                    
                    // 写入执行记录
                    writer.WriteLine("执行记录:");
                    writer.WriteLine("--------------------------------------------------");
                    writer.WriteLine("钱包地址\t操作时间\t状态\t成功\t错误信息\t交易哈希");
                    writer.WriteLine("--------------------------------------------------");
                    
                    foreach (var record in task.ExecutionRecords)
                    {
                        writer.WriteLine($"{record.WalletAddress}\t{record.OperationTime}\t{record.Status}\t{record.Success}\t{record.ErrorMessage}\t{record.TransactionHash}");
                    }
                    
                    // 写入任务执行统计
                    int successCount = task.ExecutionRecords.Count(r => r.Success == true);
                    int failedCount = task.ExecutionRecords.Count(r => r.Success == false);
                    int pendingCount = task.ExecutionRecords.Count(r => r.Success == null);
                    
                    writer.WriteLine("");
                    writer.WriteLine("执行统计:");
                    writer.WriteLine($"总记录数: {task.ExecutionRecords.Count}");
                    writer.WriteLine($"成功数: {successCount}");
                    writer.WriteLine($"失败数: {failedCount}");
                    writer.WriteLine($"待处理数: {pendingCount}");
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show($"保存任务结果时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        
        private string GetStatusText(TaskStatus status)
        {
            switch (status)
            {
                case TaskStatus.Pending:
                    return "等待执行";
                case TaskStatus.Running:
                    return "正在运行";
                case TaskStatus.Completed:
                    return "已完成";
                case TaskStatus.Failed:
                    return "失败";
                case TaskStatus.Cancelled:
                    return "已取消";
                case TaskStatus.Paused:
                    return "已暂停";
                default:
                    return "未知";
            }
        }
        
        // 根据总任务数更新任务进度
        private void UpdateTaskProgressByTotalActions(Models.Task task, int totalWallets, int actionsPerWallet, int currentWalletIndex, int currentActionIndex)
        {
            // 获取任务执行记录
            var taskRecords = Data.ExecutionStore.GetRecords(task.Id);
            
            // 计算总任务数
            int totalTasks = totalWallets * actionsPerWallet;
            
            // 计算已完成的任务数 (成功或失败)
            int completedTasks = taskRecords.Count(r => r.Success.HasValue);
            
            // 计算并设置进度
            task.Progress = totalTasks > 0 ? (int)((double)completedTasks / totalTasks * 100) : 0;
            
            // 更新任务
            UpdateTask(task);
        }
        
        /// <summary>
        /// 检查是否有任务正在运行
        /// </summary>
        /// <returns>如果有任务正在运行则返回true，否则返回false</returns>
        public bool IsAnyTaskRunning()
        {
            return _tasks.Any(t => t.Status == TaskStatus.Running);
        }
        
        /// <summary>
        /// 获取正在运行的任务数量
        /// </summary>
        /// <returns>正在运行的任务数量</returns>
        public int GetRunningTaskCount()
        {
            return _tasks.Count(t => t.Status == TaskStatus.Running);
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 检查代理是否可用的方法
        private async Task<bool> IsProxyAvailable(WebProxy proxy)
        {
            try
            {
                 ProxyViewModel proxyViewModel = null;
                var _useProxy = proxy as WebProxy;
                                    if (_useProxy != null)
                                    {
                                        // 从WebProxy创建ProxyViewModel对象
                                        proxyViewModel = new ProxyViewModel
                                        {
                                            ServerAddress = _useProxy.Address.Host,
                                            Port = _useProxy.Address.Port,
                                            Username = (_useProxy.Credentials as NetworkCredential)?.UserName,
                                            Password = (_useProxy.Credentials as NetworkCredential)?.Password, 
                                        }; 
                                    }
                var client_dev = new NadFunApiClient(proxyViewModel);
                var res = await client_dev.GetTokensByMarketCapAsync();
                Debug.WriteLine("测试代理连接站点获取排名："+res.total_count);
                return true;
               
                //LogService.AppendLog("买卖已成功发射的代币（随机买卖市值排名前10的)");
                //// 使用HttpClient测试代理连接
                //using (var handler = new HttpClientHandler
                //{
                //    Proxy = proxy,
                //    UseProxy = true
                //})
                //using (var client = new HttpClient(handler))
                //{
                //    client.Timeout = TimeSpan.FromSeconds(10); // 设置超时时间

                //    // 尝试访问一个可靠的网站，如Google或其他你确定能访问的站点
                //    var response = client.GetAsync("https://www.google.com").Result;

                //    // 检查响应是否成功
                //    return response.IsSuccessStatusCode;
                //}
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"代理测试失败: {ex.Message}");
                return false;
            }
        }
    }
} 