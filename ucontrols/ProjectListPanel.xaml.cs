using web3script.Models;
using web3script.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.IO;
using Newtonsoft.Json;

namespace web3script.ucontrols
{
    public partial class ProjectListPanel : UserControl, INotifyPropertyChanged
    {
        private ProjectService projectService;
        private WalletService walletService;
        private List<Project> projects;
        private Project selectedProject;
        
        public event Action<string, string> CreateTaskRequested;
        
        public ProjectListPanel()
        {
            InitializeComponent();
            
            // 初始化服务
            projectService = new ProjectService();
            walletService = new WalletService();
            
            // 订阅钱包分组变更事件
            walletService.WalletGroupsChanged += WalletService_WalletGroupsChanged;
            
            // 加载项目数据
            LoadProjects();
            
            // 初始化时间选择下拉框
            InitTimeComboBox();
            
            // 订阅Unloaded事件，用于清理资源
            this.Unloaded += ProjectListPanel_Unloaded;
        }
        
        // 控件卸载时取消事件订阅
        private void ProjectListPanel_Unloaded(object sender, RoutedEventArgs e)
        {
            // 取消订阅钱包分组变更事件，防止内存泄漏
            if (walletService != null)
            {
                walletService.WalletGroupsChanged -= WalletService_WalletGroupsChanged;
            }
        }
        
        // 当钱包分组变更时触发的事件处理
        private void WalletService_WalletGroupsChanged(object sender, EventArgs e)
        {
            // 在UI线程上更新分组下拉框
            this.Dispatcher.Invoke(() =>
            {
                // 只在项目详情页面可见时更新分组下拉框
                if (projectDetailGrid.Visibility == Visibility.Visible && selectedProject != null)
                {
                    UpdateProjectExecuteGroupComboBox();
                }
            });
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
        
        public void LoadProjects()
        {
            try
            {
                // 从ProjectService加载项目
                projects = projectService.LoadProjects();

                //// 设置项目列表数据源
                projectListView.ItemsSource = projects;

                //// 默认选中第一个项目
                //if (projects.Count > 0)
                //{
                //    projectListView.SelectedIndex = 0;
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载项目失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string selectedType = ((ComboBoxItem)cbProjectType.SelectedItem).Content.ToString();
                string selectedStatus = ((ComboBoxItem)cbProjectStatus.SelectedItem).Content.ToString();
                
                var filteredProjects = projects;
                
                // 根据类型筛选
                if (selectedType != "全部类型")
                {
                    filteredProjects = filteredProjects.Where(p => p.ProjectType == selectedType).ToList();
                }
                
                // 根据状态筛选（如果状态信息存在）
                if (selectedStatus != "全部状态")
                {
                    filteredProjects = filteredProjects.Where(p => p.Status == selectedStatus).ToList();
                }
                
                projectListView.ItemsSource = filteredProjects;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"筛选项目失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ProjectListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var currentSelection = projectListView.SelectedItem as Project;
            
            // 如果没有选中项，直接返回
            if (currentSelection == null)
                return;
        
            // 检查是否点击的是已经选中的项目
            if (selectedProject == currentSelection && projectDetailGrid.Visibility == Visibility.Visible)
            {
                // 如果是同一个项目，并且详情页面已经显示，先重置一下当前选择
                projectListView.SelectedItem = null;
                // 然后重新选择同一个项目，这样会重新触发SelectionChanged事件
                projectListView.SelectedItem = currentSelection;
                return;
            }
            
            // 更新当前选中的项目
            selectedProject = currentSelection;
            UpdateProjectDetails();
            projectDetailGrid.Visibility = Visibility.Visible;
            
            // 每次显示项目详情时，确保钱包分组下拉框是最新的
            UpdateProjectExecuteGroupComboBox();
        }
        
        private void UpdateProjectDetails()
        {
            if (selectedProject == null) return;
            
            // 设置基本信息
            lblProjectName.Text = selectedProject.Name;
            lblProjectType.Text = $"{selectedProject.ProjectType} | {selectedProject.Ecosystem}";
            txtProjectDesc.Text = selectedProject.Description;
            txtProjectLink.Text = selectedProject.Website;
            
            // 设置Logo
            if (!string.IsNullOrEmpty(selectedProject.LogoBg))
            {
                projectLogoBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(selectedProject.LogoBg));
            }
            else
            {
                projectLogoBorder.Background = new SolidColorBrush(Colors.Gray);
            }
            
            projectLogoText.Text = !string.IsNullOrEmpty(selectedProject.LogoText) 
                ? selectedProject.LogoText 
                : selectedProject.Name.Substring(0, 1).ToUpper();
            
            // 设置线程数
            txtThreadCount.Text = selectedProject.ThreadCount?.ToString() ?? "5";
            
            // 设置交互金额
            if (selectedProject.Amount <= 0)
            {
                txtAmount.Text = "0.01";
                selectedProject.Amount = 0.01m;
            }
            else
            {
                txtAmount.Text = selectedProject.Amount.ToString();
            }
            
            // 更新执行分组下拉框
            UpdateProjectExecuteGroupComboBox();
            
            // 加载代理池选项
            LoadProxyPools();
            
            // 设置已保存的代理设置
            chkUseProxy.IsChecked = selectedProject.UseProxy;
            if (selectedProject.UseProxy && !string.IsNullOrEmpty(selectedProject.ProxyPool))
            {
                for (int i = 0; i < cbProxyPool.Items.Count; i++)
                {
                    if (cbProxyPool.Items[i].ToString() == selectedProject.ProxyPool)
                    {
                        cbProxyPool.SelectedIndex = i;
                        break;
                    }
                }
            }
            
            // 设置定时任务和循环选项
            if (selectedProject.ScheduleSettings != null)
            {
                chkScheduleTask.IsChecked = selectedProject.ScheduleSettings.IsScheduled;
                
                if (selectedProject.ScheduleSettings.IsScheduled && selectedProject.ScheduleSettings.ScheduledTime.HasValue)
                {
                    dpScheduledDate.SelectedDate = selectedProject.ScheduleSettings.ScheduledTime.Value.Date;
                    string timeStr = $"{selectedProject.ScheduleSettings.ScheduledTime.Value.Hour:00}:{selectedProject.ScheduleSettings.ScheduledTime.Value.Minute:00}";
                    
                    for (int i = 0; i < cbScheduledTime.Items.Count; i++)
                    {
                        if (cbScheduledTime.Items[i].ToString() == timeStr)
                        {
                            cbScheduledTime.SelectedIndex = i;
                            break;
                        }
                    }
                    
                    // 设置循环执行选项
                    if (selectedProject.ScheduleSettings.IsRecurring)
                    {
                        chkRecurring.IsChecked = true;
                        txtRecurrenceInterval.Text = selectedProject.ScheduleSettings.RecurrenceInterval.ToString();
                        
                        switch (selectedProject.ScheduleSettings.RecurrenceType)
                        {
                            case RecurrenceType.Hourly:
                                cbRecurrenceType.SelectedIndex = 0;
                                break;
                            case RecurrenceType.Daily:
                                cbRecurrenceType.SelectedIndex = 1;
                                break;
                            case RecurrenceType.Weekly:
                                cbRecurrenceType.SelectedIndex = 2;
                                break;
                            case RecurrenceType.Monthly:
                                cbRecurrenceType.SelectedIndex = 3;
                                break;
                        }
                    }
                    else
                    {
                        chkRecurring.IsChecked = false;
                        cbRecurrenceType.SelectedIndex = 1; // 默认选择每天
                        txtRecurrenceInterval.Text = "1";
                    }
                }
            }
            else
            {
                chkScheduleTask.IsChecked = false;
                chkRecurring.IsChecked = false;
                cbRecurrenceType.SelectedIndex = 1; // 默认选择每天
                txtRecurrenceInterval.Text = "1";
            }
            
            // 清除执行内容面板
            executionItemsPanel.Children.Clear();
            
            // 获取项目的执行项
            var executionItems = selectedProject.ExecutionItems;
            if (executionItems == null || executionItems.Count == 0)
            {
                // 如果项目没有执行项，尝试从ProjectService获取
                executionItems = projectService.LoadProjects().FirstOrDefault(p => p.Name == selectedProject.Name)?.ExecutionItems;
                
                // 如果仍然没有执行项，创建默认执行项
                if (executionItems == null || executionItems.Count == 0)
                {
                    
                    selectedProject.ExecutionItems = executionItems;
                }
            }
            
            // 如果仍然没有执行项，显示提示信息
            if (executionItems == null || executionItems.Count == 0)
            {
                TextBlock noItemsText = new TextBlock
                {
                    Text = "此项目暂无执行内容",
                    Margin = new Thickness(0, 5, 0, 5),
                    Foreground = new SolidColorBrush(Colors.Gray)
                };
                executionItemsPanel.Children.Add(noItemsText);
                return;
            }
            
            // Monad项目特殊处理（显示成对的选项）
            if (selectedProject.Name == "Monzad")
            {
                // 为Monad项目创建成对的CheckBox
                CreatePairCheckBoxes("自动质押mon (Aprion)", "自动取回mon (Aprion)");
                
                // 特殊处理Magma质押
                var magmaCheckBox = new CheckBox
                {
                    Content = "自动质押mon (magma)",
                    IsChecked = GetIsSelectedForItem("自动质押mon (magma)"),
                    Margin = new Thickness(0, 5, 0, 5),
                    Tag = "自动质押mon (magma)"
                };
                magmaCheckBox.Checked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), true);
                magmaCheckBox.Unchecked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), false);
                executionItemsPanel.Children.Add(magmaCheckBox);
            }
            else
            {
                // 普通项目处理（单列显示选项）
                foreach (var item in executionItems)
                {
                    CheckBox checkBox = new CheckBox
                    {
                        Content = item.Name,
                        IsChecked = item.IsSelected,
                        Margin = new Thickness(0, 5, 0, 5),
                        Tag = item.Name
                    };
                    
                    checkBox.Checked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), true);
                    checkBox.Unchecked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), false);
                    
                    executionItemsPanel.Children.Add(checkBox);
                }
            }
        }
        
        // 为Monad项目创建成对的CheckBox
        private void CreatePairCheckBoxes(string leftItemName, string rightItemName)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            
            // 左侧CheckBox
            CheckBox leftCheckBox = new CheckBox
            {
                Content = leftItemName,
                IsChecked = GetIsSelectedForItem(leftItemName),
                Margin = new Thickness(5),
                Tag = leftItemName
            };
            leftCheckBox.Checked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), true);
            leftCheckBox.Unchecked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), false);
            Grid.SetColumn(leftCheckBox, 0);
            
            // 右侧CheckBox
            CheckBox rightCheckBox = new CheckBox
            {
                Content = rightItemName,
                IsChecked = GetIsSelectedForItem(rightItemName),
                Margin = new Thickness(5),
                Tag = rightItemName
            };
            rightCheckBox.Checked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), true);
            rightCheckBox.Unchecked += (s, e) => UpdateExecutionItemSelection((s as CheckBox).Tag.ToString(), false);
            Grid.SetColumn(rightCheckBox, 1);
            
            grid.Children.Add(leftCheckBox);
            grid.Children.Add(rightCheckBox);
            
            executionItemsPanel.Children.Add(grid);
        }
        
        // 获取执行项是否被选中
        private bool GetIsSelectedForItem(string itemName)
        {
            if (selectedProject?.ExecutionItems == null) return false;
            
            var item = selectedProject.ExecutionItems.FirstOrDefault(i => i.Name == itemName);
            return item?.IsSelected ?? false;
        }
        
        // 更新成对CheckBox的选中状态
        private void UpdatePairCheckBoxesSelection(int gridIndex, string leftItemName, string rightItemName)
        {
            if (executionItemsPanel.Children.Count > gridIndex && executionItemsPanel.Children[gridIndex] is Grid grid)
            {
                if (grid.Children.Count > 0 && grid.Children[0] is CheckBox leftCheckBox)
                {
                    UpdateExecutionItemSelection(leftItemName, leftCheckBox.IsChecked ?? false);
                }
                
                if (grid.Children.Count > 1 && grid.Children[1] is CheckBox rightCheckBox)
                {
                    UpdateExecutionItemSelection(rightItemName, rightCheckBox.IsChecked ?? false);
                }
            }
        }
        
        private void UpdateProjectExecuteGroupComboBox()
        {
            try
            {
                cbExecuteGroup.Items.Clear();
                
                // 确保使用最新的钱包分组数据
                walletService.LoadWalletGroups();
                var walletGroups = walletService.WalletGroups;
                
                if (walletGroups.Count > 0)
                {
                    foreach (var group in walletGroups)
                    {
                        int walletCount = walletService.GetWalletsInGroup(group.Id).Count;
                        string groupText = $"{group.Name} ({walletCount})";
                        ComboBoxItem item = new ComboBoxItem { Content = groupText, Tag = group.Id };
                        cbExecuteGroup.Items.Add(item);
                    }
                    
                    // 如果有保存的分组ID，选中它
                    if (!string.IsNullOrEmpty(selectedProject.ExecuteGroupId))
                    {
                        foreach (ComboBoxItem item in cbExecuteGroup.Items)
                        {
                            if (item.Tag.ToString() == selectedProject.ExecuteGroupId)
                            {
                                cbExecuteGroup.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    
                    if (cbExecuteGroup.SelectedIndex == -1 && cbExecuteGroup.Items.Count > 0)
                    {
                        cbExecuteGroup.SelectedIndex = 0;
                    }
                }
                else
                {
                    cbExecuteGroup.Items.Add(new ComboBoxItem { Content = "默认分组", Tag = "default" });
                    cbExecuteGroup.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载钱包分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveProject_Click(object sender, RoutedEventArgs e)
        {
            SaveProjectChanges();
            MessageBox.Show("项目设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // 询问用户是否要创建任务
            if (MessageBox.Show("是否要创建执行任务?", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // 检查条件
                if (selectedProject == null)
                {
                    MessageBox.Show("未选择项目，无法创建任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 检查是否选择了钱包分组
                if (string.IsNullOrEmpty(selectedProject.ExecuteGroupId))
                {
                    MessageBox.Show("未选择钱包分组，无法创建任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 检查是否选择了执行项
                if (selectedProject.ExecutionItems == null || !selectedProject.ExecutionItems.Any(item => item.IsSelected))
                {
                    MessageBox.Show("未选择任何执行内容，无法创建任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 直接调用方法创建任务
                CreateTask(selectedProject);
                
                // 返回项目列表
                projectDetailGrid.Visibility = Visibility.Collapsed;
            }
        }
        
        // 创建任务的方法
        private void CreateTask(Project project)
        {
            try
            {
                // 验证交互金额
                if (project.Amount <= 0)
                {
                    MessageBox.Show("交互金额必须大于0，请输入有效的金额", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 获取当前线程数
                int threadCount = 1; // 默认值
                if (int.TryParse(txtThreadCount.Text, out int parsedThreadCount) && parsedThreadCount > 0)
                {
                    threadCount = parsedThreadCount;
                }

                // 创建新任务
                var newTask = new Models.Task
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = string.Join(",", project.ExecutionItems.Where(i => i.IsSelected).Select(i => i.Name)),
                    ProjectName = project.Name,
                    GroupName = project.ExecuteGroupName,
                    Status = Models.TaskStatus.Pending,
                    Amount = project.Amount,
                    CreateTime = DateTime.Now,
                    Progress = 0,
                    UseProxy = project.UseProxy,
                    ProxyPoolId = project.ProxyPool,
                    ThreadCount = threadCount, // 添加线程数
                    Info = info.Text.Trim()
                    
                };
                
                // 如果有定时设置，复制到任务
                if (project.ScheduleSettings != null && project.ScheduleSettings.IsScheduled)
                {
                    newTask.ScheduleSettings = new ScheduleSettings
                    {
                        IsScheduled = project.ScheduleSettings.IsScheduled,
                        ScheduledTime = project.ScheduleSettings.ScheduledTime,
                        IsRecurring = project.ScheduleSettings.IsRecurring,
                        RecurrenceType = project.ScheduleSettings.RecurrenceType,
                        RecurrenceInterval = project.ScheduleSettings.RecurrenceInterval
                    };
                }
                
                // 添加任务
                string taskId = TaskService.Instance.AddTask(newTask);
                
                if (!string.IsNullOrEmpty(taskId))
                {
                    MessageBox.Show($"已成功创建任务：{project.Name}\n线程数：{threadCount}\n使用代理：{(project.UseProxy ? "是" : "否")}\n代理池：{project.ProxyPool ?? "无"}\n交互金额：{project.Amount}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void CreateTask(string groupId, string groupName)
        {
            if (selectedProject == null) return;
            
            try
            {
                // 首先保存所有设置
                SaveProjectChanges();
                
                // 然后触发任务创建事件
                if (CreateTaskRequested != null)
                {
                    CreateTaskRequested(groupId, groupName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建任务失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void SaveProjectChanges()
        {
            if (selectedProject == null) return;
            
            try
            {
                // 保存线程数
                if (int.TryParse(txtThreadCount.Text, out int threadCount))
                {
                    selectedProject.ThreadCount = threadCount.ToString();
                }
                
                // 保存执行分组
                if (cbExecuteGroup.SelectedItem != null)
                {
                    string groupId = (cbExecuteGroup.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                    string groupName = (cbExecuteGroup.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    
                    if (!string.IsNullOrEmpty(groupId))
                    {
                        selectedProject.ExecuteGroupId = groupId;
                        selectedProject.ExecuteGroupName = ExtractGroupName(groupName);
                    }
                }
                
                // 保存金额
                if (decimal.TryParse(txtAmount.Text, out decimal amount))
                {
                    selectedProject.Amount = amount;
                }
                else
                {
                    // 如果解析失败，设置默认值0.01
                    selectedProject.Amount = 0.01m;
                    txtAmount.Text = "0.01";
                }
                
                // 保存代理设置
                selectedProject.UseProxy = chkUseProxy.IsChecked == true;
                if (selectedProject.UseProxy && cbProxyPool.SelectedItem != null)
                {
                    selectedProject.ProxyPool = cbProxyPool.SelectedItem.ToString();
                }
                
                // 保存定时任务设置
                if (chkScheduleTask.IsChecked == true && dpScheduledDate.SelectedDate.HasValue && cbScheduledTime.SelectedItem != null)
                {
                    // 初始化ScheduleSettings如果不存在
                    if (selectedProject.ScheduleSettings == null)
                    {
                        selectedProject.ScheduleSettings = new ScheduleSettings();
                    }
                    
                    // 设置定时任务属性
                    selectedProject.ScheduleSettings.IsScheduled = true;
                    
                    // 设置定时时间
                    DateTime scheduleDate = dpScheduledDate.SelectedDate.Value;
                    string[] timeParts = cbScheduledTime.SelectedItem.ToString().Split(':');
                    if (timeParts.Length == 2 && int.TryParse(timeParts[0], out int hour) && int.TryParse(timeParts[1], out int minute))
                    {
                        selectedProject.ScheduleSettings.ScheduledTime = new DateTime(
                            scheduleDate.Year, scheduleDate.Month, scheduleDate.Day, hour, minute, 0);
                    }
                    
                    // 保存循环设置
                    selectedProject.ScheduleSettings.IsRecurring = chkRecurring.IsChecked == true;
                    if (selectedProject.ScheduleSettings.IsRecurring && int.TryParse(txtRecurrenceInterval.Text, out int interval))
                    {
                        selectedProject.ScheduleSettings.RecurrenceInterval = interval;
                        
                        // 保存循环类型
                        switch (cbRecurrenceType.SelectedIndex)
                        {
                            case 0:
                                selectedProject.ScheduleSettings.RecurrenceType = RecurrenceType.Hourly;
                                break;
                            case 1:
                                selectedProject.ScheduleSettings.RecurrenceType = RecurrenceType.Daily;
                                break;
                            case 2:
                                selectedProject.ScheduleSettings.RecurrenceType = RecurrenceType.Weekly;
                                break;
                            case 3:
                                selectedProject.ScheduleSettings.RecurrenceType = RecurrenceType.Monthly;
                                break;
                            default:
                                selectedProject.ScheduleSettings.RecurrenceType = RecurrenceType.Daily;
                                break;
                        }
                    }
                }
                else
                {
                    // 如果没有选择定时任务，清除定时设置
                    if (selectedProject.ScheduleSettings != null)
                    {
                        selectedProject.ScheduleSettings.IsScheduled = false;
                        selectedProject.ScheduleSettings.IsRecurring = false;
                    }
                }
                
                // 保存执行项选择状态
                if (selectedProject.ExecutionItems != null)
                {
                    foreach (var child in executionItemsPanel.Children)
                    {
                        if (child is CheckBox checkBox)
                        {
                            string itemName = checkBox.Tag.ToString();
                            bool isSelected = checkBox.IsChecked == true;
                            
                            var execItem = selectedProject.ExecutionItems.FirstOrDefault(item => item.Name == itemName);
                            if (execItem != null)
                            {
                                execItem.IsSelected = isSelected;
                            }
                        }
                        else if (child is Grid grid)
                        {
                            foreach (var gridChild in grid.Children)
                            {
                                if (gridChild is CheckBox gridCheckBox)
                                {
                                    string itemName = gridCheckBox.Tag.ToString();
                                    bool isSelected = gridCheckBox.IsChecked == true;
                                    
                                    var execItem = selectedProject.ExecutionItems.FirstOrDefault(item => item.Name == itemName);
                                    if (execItem != null)
                                    {
                                        execItem.IsSelected = isSelected;
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 保存项目
                projectService.SaveProjects(projects);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存项目设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private string ExtractGroupName(string groupText)
        {
            if (string.IsNullOrEmpty(groupText))
                return string.Empty;
                
            int bracketIndex = groupText.IndexOf(" (");
            if (bracketIndex > 0)
            {
                return groupText.Substring(0, bracketIndex);
            }
            
            return groupText;
        }
        
        private void UpdateExecutionItemSelection(string itemName, bool isSelected)
        {
            if (selectedProject?.ExecutionItems == null) return;
            
            var item = selectedProject.ExecutionItems.FirstOrDefault(i => i.Name == itemName);
            if (item != null)
            {
                item.IsSelected = isSelected;
            }
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            projectDetailGrid.Visibility = Visibility.Collapsed;
            // 清除当前选择的项目，解决点击返回后再次点击同一项目无法进入的问题
            selectedProject = null;
            projectListView.SelectedItem = null;
        }
        
        private void WebsiteLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(selectedProject?.Website))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = selectedProject.Website,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开网站失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        // 加载代理池选项
        private void LoadProxyPools()
        {
            try
            {
                cbProxyPool.Items.Clear();
                
                // 加载代理分组
                if (File.Exists("proxy_groups.json"))
                {
                    string json = File.ReadAllText("proxy_groups.json");
                    var groups = JsonConvert.DeserializeObject<List<ProxyGroup>>(json);
                    
                    if (groups != null && groups.Count > 0)
                    {
                        // 添加每个代理组
                        foreach (var group in groups)
                        {
                            cbProxyPool.Items.Add(group.Name);
                        }
                    }
                }
                
                // 默认选中第一项
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
        
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
} 