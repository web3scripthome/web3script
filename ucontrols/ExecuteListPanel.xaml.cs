using web3script.Data;
using web3script.Models;
using web3script.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using TaskStatus = web3script.Models.TaskStatus;

namespace web3script.ucontrols
{
    public partial class ExecuteListPanel : UserControl
    {
        // 当前页码和分页相关属性
        private int _currentPage = 1;
        private int _pageSize = 100;
        private int _totalPages = 1;
        
        // 过滤条件
        private Models.TaskStatus? _filterStatus = null;
        private bool? _filterSuccess = null;
        
        // 视图模型列表
        private ObservableCollection<ExecutionTaskViewModel> _taskViewModels = new ObservableCollection<ExecutionTaskViewModel>();
        private ICollectionView _tasksView;
        
        // 钱包服务
        private WalletService _walletService;
        
        // 公开事件
        public event EventHandler<string> BreadcrumbChanged;
        
        // 添加定时器用于自动刷新
        private DispatcherTimer _refreshTimer;
        
        // 哈希值用于保存上次任务列表的状态
        private string _lastTasksHash = string.Empty;
        
        public ExecuteListPanel()
        {
            InitializeComponent();
            
            // 初始化任务服务并绑定数据
            TaskService.Instance.PropertyChanged += TaskService_PropertyChanged;
            
            // 创建绑定视图
            _tasksView = CollectionViewSource.GetDefaultView(_taskViewModels);
            executeListView.ItemsSource = _tasksView;
            
            // 设置自动刷新
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _refreshTimer.Tick += (s, e) => RefreshData();
            _refreshTimer.Start();
            
            RefreshData();
        }
        
        // 设置钱包服务实例（从MainWindow传入）
        public void SetWalletService(WalletService service)
        {
            _walletService = service;
        }
        
        /// <summary>
        /// 创建新任务
        /// </summary>
        /// <param name="groupId">钱包分组ID</param>
        /// <param name="groupName">钱包分组名称</param>
        public void CreateNewTask(string groupId, string groupName)
        {
            try
            {
                // 获取项目服务
                var projectService = new ProjectService();
                
                // 获取选中的项目
                var project = projectService.LoadProjects().FirstOrDefault(p => p.ExecuteGroupId == groupId);
                
                if (project == null)
                {
                    MessageBox.Show($"未找到绑定到分组 {groupName} 的项目", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 检查项目是否有选中的执行项
                if (project.ExecutionItems == null || !project.ExecutionItems.Any(item => item.IsSelected))
                {
                    MessageBox.Show($"项目 {project.Name} 未选择任何执行项目", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 打开添加任务对话框，使用共享的钱包服务实例
                var addTaskDialog = new AddTaskDialog(project, _walletService);
                addTaskDialog.Owner = Window.GetWindow(this);
                
                if (addTaskDialog.ShowDialog() == true)
                {
                    // 刷新数据
                    RefreshData();
                    
                    MessageBox.Show($"已成功创建任务：{project.Name}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Initialize()
        {
            try
            {
                // 初始化分页大小下拉框
                switch (_pageSize)
                {
                    case 20:
                        pageSizeComboBox.SelectedIndex = 0;
                        break;
                    case 50:
                        pageSizeComboBox.SelectedIndex = 1;
                        break;
                    case 100:
                        pageSizeComboBox.SelectedIndex = 2;
                        break;
                    case 200:
                        pageSizeComboBox.SelectedIndex = 3;
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化执行列表面板时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TaskService_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TaskService.Tasks))
            {
                RefreshData();
            }
        }
        
        private void RefreshData()
        {
            try
            {
                // 使用Dispatcher确保在UI线程上更新集合
                this.Dispatcher.Invoke(() =>
                {
                    // 从TaskService获取数据
                    var tasks = TaskService.Instance.Tasks;
                    
                    // 计算当前任务列表的哈希值
                    string currentTasksHash = GetTasksHash(tasks);
                    
                    // 如果任务列表没有变化，则不刷新
                    if (currentTasksHash == _lastTasksHash && !string.IsNullOrEmpty(_lastTasksHash))
                    {
                        return;
                    }
                    
                    // 保存当前哈希值
                    _lastTasksHash = currentTasksHash;
                    
                    // 保存当前选中状态
                    var selectedTaskIds = _taskViewModels
                        .Where(vm => vm.IsSelected)
                        .Select(vm => vm.Id)
                        .ToList();
                    
                    // 保存当前视图位置
                    var selectedItem = executeListView.SelectedItem;
                    string selectedId = (selectedItem as ExecutionTaskViewModel)?.Id;
                    
                    // 清空现有数据
                    _taskViewModels.Clear();
                    
                    // 转换为视图模型
                    int index = 1;
                    foreach (var task in tasks)
                    {
                        var viewModel = new ExecutionTaskViewModel
                        {
                            Id = task.Id,
                            RowIndex = index++,
                            ProjectName = task.ProjectName,
                            Status = task.Status,
                            StatusText = GetStatusText(task.Status),
                            Progress = task.Progress,
                            CreateTime = task.CreateTime,
                            IsSelected = selectedTaskIds.Contains(task.Id), // 恢复选中状态
                            LastProcessedIndex = task.LastProcessedIndex,
                            Remark = task.Name // 设置备注为任务名称，即交互内容
                        };
                        
                        _taskViewModels.Add(viewModel);
                    }
                    
                    // 恢复选中项
                    if (selectedId != null)
                    {
                        var newSelectedItem = _taskViewModels.FirstOrDefault(vm => vm.Id == selectedId);
                        if (newSelectedItem != null)
                        {
                            executeListView.SelectedItem = newSelectedItem;
                        }
                    }
                    
                    // 更新总记录数
                    totalRecordsText.Text = _taskViewModels.Count.ToString();
                    
                    // 应用过滤器
                    ApplyFilter();
                    
                    // 更新分页控件
                    UpdatePagination();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新数据时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 获取任务列表的哈希值
        private string GetTasksHash(IEnumerable<Models.Task> tasks)
        {
            if (tasks == null || !tasks.Any())
                return string.Empty;
            
            var taskInfo = tasks.Select(t => new
            {
                t.Id,
                t.Status,
                t.Progress,
                t.LastProcessedIndex
            });
            
            return JsonSerializer.Serialize(taskInfo);
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
        
        private void ApplyFilter()
        {
            _tasksView.Filter = item =>
            {
                if (!(item is ExecutionTaskViewModel task))
                {
                    return false;
                }
                
                // 状态过滤
                if (_filterStatus.HasValue && task.Status != _filterStatus.Value)
                {
                    return false;
                }
                
                // 成功/失败过滤
                if (_filterSuccess.HasValue)
                {
                    bool isSuccess = task.Status == TaskStatus.Completed;
                    if (isSuccess != _filterSuccess.Value)
                    {
                        return false;
                    }
                }
                
                return true;
            };
        }
        
        private void UpdatePagination()
        {
            // 确保_tasksView不为null
            if (_tasksView == null)
            {
                _totalPages = 1;
                _currentPage = 1;
                return;
            }

            int filteredCount = _tasksView.Cast<object>().Count();
            _totalPages = (int)Math.Ceiling(filteredCount / (double)_pageSize);
            
            if (_currentPage > _totalPages && _totalPages > 0)
            {
                _currentPage = _totalPages;
            }
            else if (_totalPages == 0)
            {
                _currentPage = 1;
            }
            
            UpdatePaginationControls();
        }
        
        private void UpdatePaginationControls()
        {
            // 生成页码按钮
            List<PageButtonModel> pageButtons = new List<PageButtonModel>();
            
            // 最多显示5个页码按钮
            int maxVisibleButtons = 5;
            int startPage = Math.Max(1, _currentPage - maxVisibleButtons / 2);
            int endPage = Math.Min(_totalPages, startPage + maxVisibleButtons - 1);
            
            // 调整开始页，确保显示足够数量的按钮
            if (endPage - startPage + 1 < maxVisibleButtons && startPage > 1)
            {
                startPage = Math.Max(1, endPage - maxVisibleButtons + 1);
            }
            
            // 创建页码按钮模型
            for (int i = startPage; i <= endPage; i++)
            {
                pageButtons.Add(new PageButtonModel
                {
                    PageNumber = i,
                    IsCurrentPage = i == _currentPage
                });
            }
            
            // 设置数据源
            pageButtonsPanel.ItemsSource = pageButtons;
            
            // 更新文本框中的页码
            pageNumberTextBox.Text = _currentPage.ToString();
        }
        
        // 查询按钮点击事件
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            // 获取执行状态过滤条件
            if (cbStatus.SelectedIndex > 0)
            {
                _filterStatus = (TaskStatus)(cbStatus.SelectedIndex - 1);
            }
            else
            {
                _filterStatus = null;
            }
            
            // 获取所有状态过滤条件
            if (cbAllStatus.SelectedIndex > 0)
            {
                _filterSuccess = cbAllStatus.SelectedIndex == 1;
            }
            else
            {
                _filterSuccess = null;
            }
            
            // 重置为第一页
            _currentPage = 1;
            
            // 重新应用过滤器
            ApplyFilter();
            
            // 更新分页控件
            UpdatePagination();
        }
        
        // 清空按钮点击事件
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            // 重置过滤条件
            cbStatus.SelectedIndex = 0;
            cbAllStatus.SelectedIndex = 0;
            _filterStatus = null;
            _filterSuccess = null;
            
            // 重置为第一页
            _currentPage = 1;
            
            // 重新应用过滤器
            ApplyFilter();
            
            // 更新分页控件
            UpdatePagination();
        }
        
        // 任务列表双击事件
        private void ExecuteListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (executeListView.SelectedItem is ExecutionTaskViewModel selectedTask)
            {
                ViewTaskDetails(selectedTask.Id);
            }
        }
        
        // 查看任务按钮点击事件
        private void ViewTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ExecutionTaskViewModel task)
            {
                ViewTaskDetails(task.Id);
            }
        }
        
        // 查看任务详情
        private void ViewTaskDetails(string taskId)
        {
            var task = TaskService.Instance.GetTaskById(taskId);
            if (task != null)
            {
                var taskDetailsDialog = new TaskDetailsDialog();
                
                // 设置任务详情视图模型
                var viewModel = taskDetailsDialog.DataContext as ViewModels.TaskDetailsViewModel;
                if (viewModel != null)
                {
                    // 使用InitProjectInfo方法初始化视图模型
                    taskDetailsDialog.InitProjectInfo(
                        taskId,
                        task.ProjectName,
                        task.GroupName,
                        task.ThreadCount);
                    
                    // 设置任务状态和进度
                    viewModel.Status = GetStatusText(task.Status);
                    viewModel.Progress = task.Progress;
                    
                    // 清除并设置执行项目列表（交互内容）
                    viewModel.ExecutionItems.Clear();
                    if (!string.IsNullOrEmpty(task.Name))
                    {
                        // 分割交互内容并添加到执行项目列表
                        string[] items = task.Name.Split(',');
                        foreach (var item in items)
                        {
                            viewModel.ExecutionItems.Add(item.Trim());
                        }
                    }
                    
                    // 开始同步执行记录
                    viewModel.StartSync();
                }
                
                // 显示对话框
                taskDetailsDialog.Owner = Window.GetWindow(this);
                taskDetailsDialog.ShowDialog();
                //taskDetailsDialog.WindowStyle = WindowStyle.ToolWindow;
                //taskDetailsDialog.Topmost = false;
                //taskDetailsDialog.Show();
                // 停止同步
                if (viewModel != null)
                {
                    viewModel.StopSync();
                }
            }
        }
        
        // 删除任务按钮点击事件
        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ExecutionTaskViewModel task)
            {
                if (MessageBox.Show($"确定要删除任务 \"{task.ProjectName}\" 吗?", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    TaskService.Instance.DeleteTask(task.Id);
                }
            }
        }
        
        // 停止任务按钮点击事件
        private void StopTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ExecutionTaskViewModel task)
            {
                if (MessageBox.Show($"确定要停止任务 \"{task.ProjectName}\" 吗?", "确认停止", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    TaskService.Instance.StopTask(task.Id);
                }
            }
        }
        
        // 暂停任务按钮点击事件
        private void PauseTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ExecutionTaskViewModel task)
            {
                if (MessageBox.Show($"确定要暂停任务 \"{task.ProjectName}\" 吗?", "确认暂停", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    TaskService.Instance.PauseTask(task.Id);
                    
                    // 刷新视图以更新按钮状态
                    RefreshData();
                }
            }
        }
        
        // 任务复选框点击事件
        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null && checkBox.DataContext is ExecutionTaskViewModel task)
            {
                task.IsSelected = checkBox.IsChecked ?? false;
            }
        }
        
        // 批量启动按钮点击事件
        private void StartBatch_Click(object sender, RoutedEventArgs e)
        {
            var selectedTasks = _taskViewModels.Where(t => t.IsSelected).ToList();
            if (selectedTasks.Count == 0)
            {
                MessageBox.Show("请先选择要启动的任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 检查是否有已完成的任务
            var completedTasks = selectedTasks.Where(t => t.Status == TaskStatus.Completed).ToList();
            if (completedTasks.Count > 0)
            {
                bool restart = MessageBox.Show($"选中的任务中有 {completedTasks.Count} 个已完成的任务，是否重新启动这些任务？\n重新启动将删除之前所有进度信息。", 
                    "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                
                if (!restart)
                {
                    // 如果用户选择不重启已完成任务，则从选择列表中移除它们
                    selectedTasks = selectedTasks.Where(t => t.Status != TaskStatus.Completed).ToList();
                    
                    // 如果没有剩余任务，直接返回
                    if (selectedTasks.Count == 0)
                    {
                        return;
                    }
                }
            }
            
            if (MessageBox.Show($"确定要启动选中的 {selectedTasks.Count} 个任务吗?", "确认启动", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var task in selectedTasks)
                {
                    TaskService.Instance.StartTask(task.Id);
                }
                
                // 刷新数据显示最新状态
                RefreshData();
            }
        }
        
        // 批量删除按钮点击事件
        private void DeleteBatch_Click(object sender, RoutedEventArgs e)
        {
            var selectedTasks = _taskViewModels.Where(t => t.IsSelected).ToList();
            if (selectedTasks.Count == 0)
            {
                MessageBox.Show("请先选择要删除的任务", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (MessageBox.Show($"确定要删除选中的 {selectedTasks.Count} 个任务吗?", "确认删除", 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var task in selectedTasks)
                {
                    TaskService.Instance.DeleteTask(task.Id);
                }
            }
        }
        
        // 页码按钮点击事件
        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PageButtonModel model)
            {
                _currentPage = model.PageNumber;
                UpdatePaginationControls();
            }
        }
        
        // 首页按钮点击事件
        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage = 1;
                UpdatePaginationControls();
            }
        }
        
        // 上一页按钮点击事件
        private void PreviousPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePaginationControls();
            }
        }
        
        // 下一页按钮点击事件
        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdatePaginationControls();
            }
        }
        
        // 末页按钮点击事件
        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage = _totalPages;
                UpdatePaginationControls();
            }
        }
        
        // 跳转到页按钮点击事件
        private void JumpToPage_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(pageNumberTextBox.Text, out int pageNumber) && pageNumber > 0 && pageNumber <= _totalPages)
            {
                _currentPage = pageNumber;
                UpdatePaginationControls();
            }
            else
            {
                pageNumberTextBox.Text = _currentPage.ToString();
            }
        }
        
        // 分页大小改变事件
        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (pageSizeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                if (int.TryParse(selectedItem.Content.ToString(), out int pageSize))
                {
                    _pageSize = pageSize;
                    _currentPage = 1;
                    UpdatePagination();
                }
            }
        }
        
        // 数字输入验证
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c))
                {
                    e.Handled = true;
                    return;
                }
            }
        }
        
        // 启动任务按钮点击事件
        private void StartTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ExecutionTaskViewModel taskViewModel)
            {
                // 保存任务ID，以便后续检查
                string taskId = taskViewModel.Id;
                
                // 尝试启动任务
                TaskService.Instance.StartTask(taskId);
                
                // 刷新数据以获取最新状态
                RefreshData();
                
                // 获取任务的最新状态
                var task = TaskService.Instance.GetTaskById(taskId);
                
                // 仅当任务确实是在运行状态时才自动弹出任务详情对话框
                if (task != null && task.Status == Models.TaskStatus.Running)
                {
                    // 自动弹出任务详情对话框
                    ViewTaskDetails(taskId);
                }
            }
        }
        
        // 添加执行按钮点击事件
        private void AddExecute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用ProjectService实例
                var projectService = new web3script.Services.ProjectService();
                
                // 加载所有项目
                var projects = projectService.LoadProjects();
                
                // 如果没有项目，提示用户先创建项目
                if (projects.Count == 0)
                {
                    MessageBox.Show("没有可用的项目，请先在项目清单中创建项目", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                // 显示项目选择对话框
                var projectSelectDialog = new ProjectSelectDialog(projects);
                projectSelectDialog.Owner = Window.GetWindow(this);
                
                if (projectSelectDialog.ShowDialog() == true)
                {
                    var selectedProject = projectSelectDialog.SelectedProject;
                    if (selectedProject != null)
                    {
                        // 打开添加任务对话框，传递钱包服务实例
                        var addTaskDialog = new AddTaskDialog(selectedProject, _walletService);
                        addTaskDialog.Owner = Window.GetWindow(this);
                        
                        if (addTaskDialog.ShowDialog() == true)
                        {
                            // 任务已添加到TaskService，刷新列表
                            RefreshData();
                            
                            MessageBox.Show("任务添加成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加执行任务时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 继续任务按钮点击事件
        private void ResumeTask_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is ExecutionTaskViewModel task)
            {
                TaskService.Instance.ResumeTask(task.Id);
                
                // 自动弹出任务详情对话框
                ViewTaskDetails(task.Id);
            }
        }
        
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 停止自动刷新定时器
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
            }
        }
    }
    
    public class PageButtonModel : INotifyPropertyChanged
    {
        private int _pageNumber;
        private bool _isCurrentPage;
        
        public int PageNumber 
        { 
            get => _pageNumber; 
            set
            {
                if (_pageNumber != value)
                {
                    _pageNumber = value;
                    OnPropertyChanged(nameof(PageNumber));
                }
            }
        }
        
        public bool IsCurrentPage 
        { 
            get => _isCurrentPage; 
            set
            {
                if (_isCurrentPage != value)
                {
                    _isCurrentPage = value;
                    OnPropertyChanged(nameof(IsCurrentPage));
                }
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public class ExecutionTaskViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; }
        public string ProjectName { get; set; }
        public string Remark { get; set; } // 备注，显示交互内容
        public Models.TaskStatus Status 
        { 
            get => _status; 
            set 
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(IsRunning));
                    OnPropertyChanged(nameof(CanStart));
                    OnPropertyChanged(nameof(CanDelete));
                    OnPropertyChanged(nameof(CanResume));
                }
            }
        }
        public string StatusText { get; set; }
        public int Progress { get; set; }
        public DateTime CreateTime { get; set; }
        public bool IsSelected { get; set; }
        public bool IsRunning => Status == TaskStatus.Running;
        public int RowIndex { get; set; }
        public int LastProcessedIndex { get; set; } // 记录上次处理到的索引

        // 按钮状态计算属性
        public bool CanStart => Status == TaskStatus.Pending || Status == TaskStatus.Failed || Status == TaskStatus.Completed;
        public bool CanDelete => Status != TaskStatus.Running;
        public bool CanResume => Status == TaskStatus.Paused || Status == TaskStatus.Cancelled;

        private Models.TaskStatus _status;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 
