using System.Windows;
using web3script.Views;
using System.Collections.Generic;
using System.Windows.Documents;

namespace web3script.Services
{
    public static class LogService
    {
        private static LogWindow _logWindow;
        private static readonly List<string> _logHistory = new List<string>();

        public static void ShowLogWindow()
        {
            if (_logWindow == null)
            {
                _logWindow = new LogWindow();
                _logWindow.Closing += (s, e) =>
                {
                    e.Cancel = true;  // 取消关闭
                    _logWindow.Hide(); // 改为隐藏
                };
            }
            _logWindow.Show();
        }

        public static void AppendLog(string message)
        {
            // 保存到历史记录
            _logHistory.Add($"[{DateTime.Now:HH:mm:ss} {message}\n"); // 格式化时间message");

            if (_logWindow == null)
            {
                _logWindow = new LogWindow();
                _logWindow.Closing += (s, e) =>
                {
                    e.Cancel = true;
                    _logWindow.Hide();
                };
            }
            _logWindow.Dispatcher.Invoke(() =>
            {
                _logWindow.AppendLog($"[{DateTime.Now:HH:mm:ss}] {message}");
            });
        }
        public static void hideLogWindow()
        {
            _logWindow.Hide();
        }
        public static void ClearLogs()
        {
            _logHistory.Clear();
            if (_logWindow != null)
            {
                _logWindow.Dispatcher.Invoke(() =>
                {
                    _logWindow.ClearLogs();
                });
            }
        }
    }
} 