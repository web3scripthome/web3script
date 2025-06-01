using web3script.Models;
using web3script.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace web3script
{
    public partial class AddTaskDialog : Window
    {
        private ObservableCollection<ExecutionItem> _taskItems = new ObservableCollection<ExecutionItem>();
        private List<ucontrols.ProxyViewModel> _proxyPools = new List<ucontrols.ProxyViewModel>();
        
        // 当前项目
        private Models.Project _currentProject;
        
        // 钱包服务实例
        private WalletService _walletService;
        
        // 返回创建的任务
        public Models.Task CreatedTask { get; private set; }
        
        public AddTaskDialog(Models.Project currentProject, WalletService walletService = null)
        {
            InitializeComponent();
            
            // 保存当前项目
            _currentProject = currentProject;
            
            // 保存钱包服务实例
            _walletService = walletService ?? new WalletService();
            
            // 初始化时间选择下拉框
            InitTimeComboBox();
            
            // 加载分组
            LoadGroups();
            
            // 加载代理池
            LoadProxyPools();
            
            // 初始化日期选择器为当前日期
            dpScheduledDate.SelectedDate = DateTime.Today;
            
            // 设置默认值
            cbRecurrenceType.SelectedIndex = 1; // 默认每天
            
            // 显示当前项目执行内容
            LoadTaskItems();
        }

        private void InitTimeComboBox()
        {
            // 每小时的时间点
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 15)
                {
                    string timeText = $"{hour:00}:{minute:00}";
                    cbScheduledTime.Items.Add(timeText);
                }
            }
            
            // 设置默认为当前时间的下一个15分钟整点
            DateTime now = DateTime.Now;
            int currentMinute = now.Minute;
            int targetMinute = ((currentMinute / 15) + 1) * 15;
            int addHours = 0;
            
            if (targetMinute >= 60)
            {
                targetMinute = 0;
                addHours = 1;
            }
            
            string defaultTime = $"{(now.Hour + addHours) % 24:00}:{targetMinute:00}";
            
            foreach (string item in cbScheduledTime.Items)
            {
                if (item.ToString() == defaultTime)
                {
                    cbScheduledTime.SelectedItem = item;
                    break;
                }
            }
            
            if (cbScheduledTime.SelectedIndex == -1 && cbScheduledTime.Items.Count > 0)
            {
                cbScheduledTime.SelectedIndex = 0;
            }
        }

        private void LoadTaskItems()
        {
            try
            {
                _taskItems.Clear();
                
                // 使用当前项目的执行项目列表
                if (_currentProject != null && _currentProject.ExecutionItems != null)
                {
                    foreach (var item in _currentProject.ExecutionItems)
                    {
                        _taskItems.Add(new Models.ExecutionItem
                        {
                            Name = item.Name,
                            IsSelected = false
                        });
                    }
                }
                
                // 如果当前项目没有执行项，尝试从ProjectService获取
                if (_taskItems.Count == 0)
                {
                    // 获取ProjectService
                    var projectService = new Services.ProjectService();
                    
                    // 加载所有项目
                    var projects = projectService.LoadProjects();
                    
                    // 查找与当前项目名称匹配的项目
                    var matchedProject = projects.FirstOrDefault(p => p.Name == _currentProject?.Name);
                    
                    if (matchedProject != null && matchedProject.ExecutionItems != null && matchedProject.ExecutionItems.Count > 0)
                    {
                        foreach (var item in matchedProject.ExecutionItems)
                        {
                            _taskItems.Add(new Models.ExecutionItem
                            {
                                Name = item.Name,
                                IsSelected = false
                            });
                        }
                    }
                }
                
                lbTasks.ItemsSource = _taskItems;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载执行项目失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadGroups()
        {
            try
            {
                // 加载钱包分组
                List<string> groups = new List<string>();
                
                // 如果没有分组，添加一个默认分组
                if (_walletService.WalletGroups.Count == 0)
                {
                    groups.Add("默认分组");
                }
                else
                {
                    // 从钱包服务获取分组名称
                    foreach (var group in _walletService.WalletGroups)
                    {
                        groups.Add(group.Name);
                    }
                }
                
                cbGroups.ItemsSource = groups;
                
                if (groups.Count > 0)
                {
                    cbGroups.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProxyPools()
        {
            try
            {
                _proxyPools.Clear();
                
                // 加载代理池配置
                if (File.Exists("proxy_config.json"))
                {
                    string json = File.ReadAllText("proxy_config.json");
                    var proxies = JsonConvert.DeserializeObject<List<ucontrols.ProxyViewModel>>(json);
                    
                    if (proxies != null)
                    {
                        _proxyPools.AddRange(proxies);
                    }
                }
                
                // 清空当前列表
                cbProxyPool.Items.Clear();
                
               // // 添加一个默认选项
               //// cbProxyPool.Items.Add("默认代理池");
                
               // // 添加已连接的代理池
               // var connectedProxies = _proxyPools.Where(p => p.Status == ucontrols.ProxyStatus.Connected).ToList();
               // if (connectedProxies.Count > 0)
               // {
               //     cbProxyPool.Items.Add("已连接代理池");
               // }
                
                // 加载代理分组
                if (File.Exists("proxy_groups.json"))
                {
                    string json = File.ReadAllText("proxy_groups.json");
                    var groups = JsonConvert.DeserializeObject<List<ucontrols.ProxyGroup>>(json);
                    
                    if (groups != null && groups.Count > 0)
                    {
                        // 添加每个代理组
                        foreach (var group in groups)
                        {
                            cbProxyPool.Items.Add($"代理组: {group.Name}");
                        }
                    }
                }
                
                if (cbProxyPool.Items.Count > 0)
                {
                    cbProxyPool.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载代理池失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cbGroups.SelectedItem == null)
                {
                    MessageBox.Show("请选择分组", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                var selectedTasks = _taskItems.Where(t => t.IsSelected).ToList();
                if (selectedTasks.Count == 0)
                {
                    MessageBox.Show("请至少选择一个任务", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("请输入有效的金额", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (chkScheduled.IsChecked == true && dpScheduledDate.SelectedDate == null)
                {
                    MessageBox.Show("请选择定时执行日期", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (chkScheduled.IsChecked == true && cbScheduledTime.SelectedItem == null)
                {
                    MessageBox.Show("请选择定时执行时间", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (chkRecurring.IsChecked == true && 
                    (!int.TryParse(txtRecurrenceInterval.Text, out int interval) || interval <= 0))
                {
                    MessageBox.Show("请输入有效的循环间隔", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 检查线程数
                int threadCount = 1; // 默认值
                if (chkUseProxy.IsChecked == true)
                {
                    if (!int.TryParse(txtThreadCount.Text, out threadCount) || threadCount <= 0)
                    {
                        MessageBox.Show("请输入有效的线程数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                
                // 创建任务对象
                var task = new Models.Task
                {
                    Name = string.Join(",", selectedTasks.Select(t => t.Name)),
                    ProjectName = _currentProject.Name,
                    GroupName = cbGroups.SelectedItem.ToString(),
                    Amount = amount,
                    UseProxy = chkUseProxy.IsChecked == true,
                    ThreadCount = threadCount 

                };
                
                // 设置代理池信息
                if (chkUseProxy.IsChecked == true && cbProxyPool.SelectedItem != null)
                {
                    string proxyPoolText = cbProxyPool.SelectedItem.ToString();
                    
                    // 从"代理组: 组名"格式中提取组名
                    if (proxyPoolText.StartsWith("代理组: "))
                    {
                        string groupName = proxyPoolText.Substring("代理组: ".Length);
                        task.ProxyPoolId = groupName; // 保存代理组名，不包含前缀
                    }
                    else
                    {
                        task.ProxyPoolId = proxyPoolText; // 保存原始文本
                    }
                }
                else
                {
                    task.ProxyPoolId = null;
                }
                
                // 设置定时任务
                if (chkScheduled.IsChecked == true)
                {
                    DateTime scheduleDate = dpScheduledDate.SelectedDate.Value;
                    string[] timeParts = cbScheduledTime.SelectedItem.ToString().Split(':');
                    int hour = int.Parse(timeParts[0]);
                    int minute = int.Parse(timeParts[1]);
                    
                    DateTime scheduledDateTime = new DateTime(
                        scheduleDate.Year, scheduleDate.Month, scheduleDate.Day, 
                        hour, minute, 0);
                    
                    task.ScheduleSettings = new ScheduleSettings
                    {
                        IsScheduled = true,
                        ScheduledTime = scheduledDateTime
                    };
                    
                    // 设置循环
                    if (chkRecurring.IsChecked == true)
                    {
                        int recurrenceInterval = int.Parse(txtRecurrenceInterval.Text);
                        RecurrenceType recurrenceType;
                        
                        switch (cbRecurrenceType.SelectedIndex)
                        {
                            case 0:
                                recurrenceType = RecurrenceType.Hourly;
                                break;
                            case 1:
                                recurrenceType = RecurrenceType.Daily;
                                break;
                            case 2:
                                recurrenceType = RecurrenceType.Weekly;
                                break;
                            case 3:
                                recurrenceType = RecurrenceType.Monthly;
                                break;
                            default:
                                recurrenceType = RecurrenceType.Daily;
                                break;
                        }
                        
                        task.ScheduleSettings.IsRecurring = true;
                        task.ScheduleSettings.RecurrenceType = recurrenceType;
                        task.ScheduleSettings.RecurrenceInterval = recurrenceInterval;
                    }
                }
                task.Info = info.Text.Trim();
                // 添加任务
                string taskId = TaskService.Instance.AddTask(task);
                CreatedTask = TaskService.Instance.GetTaskById(taskId);
                
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 