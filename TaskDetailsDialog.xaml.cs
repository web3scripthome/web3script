using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using web3script.Services;
using web3script.ViewModels;

namespace web3script
{
    public partial class TaskDetailsDialog : Window
    {
        private const int GWL_STYLE = -16;
        private const int WS_MINIMIZEBOX = 0x00020000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        public TaskDetailsDialog()
        { 
            InitializeComponent();
            
            // 创建ViewModel并绑定
            DataContext = new TaskDetailsViewModel();
        }
        public void InitProjectInfo(string taskId, string projectName, string groupName, int threadCount)
        {
            if (DataContext is TaskDetailsViewModel viewModel)
            {
                // 获取任务对象以确定实际状态
                var task = Services.TaskService.Instance.GetTaskById(taskId);
                string statusText = "准备中"; // 默认状态
                
                if (task != null)
                {
                    switch (task.Status)
                    {
                        case Models.TaskStatus.Running:
                            statusText = "正在执行";
                            break;
                        case Models.TaskStatus.Completed:
                            statusText = "已完成";
                            break;
                        case Models.TaskStatus.Failed:
                            statusText = "执行失败";
                            break;
                        case Models.TaskStatus.Cancelled:
                            statusText = "已取消";
                            break;
                        case Models.TaskStatus.Paused:
                            statusText = "已暂停";
                            break;
                        case Models.TaskStatus.Pending:
                            statusText = "准备中";
                            break;
                    }
                }
                
                viewModel.TaskId = taskId;
                viewModel.ProjectName = projectName;
                viewModel.GroupName = groupName;
                viewModel.CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                viewModel.ThreadCount = threadCount;
                viewModel.Status = statusText;
                
                // 确保将项目名称传递给每个执行记录
                viewModel.SetProjectName(projectName);
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (DataContext is TaskDetailsViewModel viewModel)
            {
                viewModel.StartSync();
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            
            // 停止同步
            if (DataContext is TaskDetailsViewModel viewModel)
            {
                viewModel.StopSync();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~WS_MINIMIZEBOX; // 移除最小化按钮
            SetWindowLong(hwnd, GWL_STYLE, style);
        }

        private void BT_Click(object sender, RoutedEventArgs e)
        {
            LogService.ShowLogWindow();
        }
    }
}

