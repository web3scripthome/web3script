using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

namespace web3script
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // 定义互斥体变量
        private static Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "MyUniqueAppMutex_OnlyOneInstance";
            bool createdNew;

            // 创建互斥体，确保只有一个实例运行
            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // 如果程序已运行，激活现有窗口
                MessageBox.Show("程序已运行,请在右下角激活", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                ActivateExistingInstance();
                Shutdown(); // 退出当前实例
               
                return;
            }

            // 如果是第一次运行，启动主窗口
            base.OnStartup(e);
        }

        // 激活已有实例的窗口
        private void ActivateExistingInstance()
        {
            // 找到已打开的窗口，并激活它
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Activate();
            }
        }
    }
    }

