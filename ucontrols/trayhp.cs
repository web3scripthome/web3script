using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Resources;
using Application = System.Windows.Application;

namespace web3script
{
    public class TrayHelper : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Window _mainWindow;

        public TrayHelper(Window mainWindow, string tooltip = "正在运行")
        {
            _mainWindow = mainWindow; 
            // 获取资源流
            var iconUri = new Uri("pack://application:,,,/tryResources/bitbug_favicon.ico", UriKind.RelativeOrAbsolute);
            StreamResourceInfo sri = Application.GetResourceStream(iconUri);

            if (sri == null)
                throw new FileNotFoundException($"找不到图标资源");

            using var iconStream = sri.Stream;
            _notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon(iconStream),
                Visible = true,
                Text = tooltip
            };

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("显示窗口", null, ShowWindow);
            contextMenu.Items.Add("退出程序", null, ExitApp);
            _notifyIcon.ContextMenuStrip = contextMenu;

            _notifyIcon.DoubleClick += ShowWindow;

            _mainWindow.StateChanged += MainWindow_StateChanged;
            _mainWindow.Closed += MainWindow_Closed;
        }

        private void ShowWindow(object? sender, EventArgs e)
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        private void ExitApp(object? sender, EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            Application.Current.Shutdown();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.Hide();
            }
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
