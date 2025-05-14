using web3script.Models;
using web3script.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace web3script.ucontrols
{
    /// <summary>
    /// EmailConfigPanel.xaml 的交互逻辑
    /// </summary>
    public partial class EmailConfigPanel : UserControl, INotifyPropertyChanged
    {
        // 邮件服务
        private EmailService emailService;
        
        // 私有字段，用于UI绑定
        private string _domainName;
        private string _username;
        private string _password;
        private string _mailServerName;
        private string _lastTestStatusText = "未测试";
        private string _lastTestTimeText = "无";
        private string _lastTestMessage = "尚未进行测试";
        
        // 公共属性，用于数据绑定
        public string DomainName
        {
            get => _domainName;
            set
            {
                if (_domainName != value)
                {
                    _domainName = value;
                    OnPropertyChanged(nameof(DomainName));
                    OnPropertyChanged(nameof(ReceiveEmailPattern));
                }
            }
        }
        
        public string MailServerName
        {
            get => _mailServerName;
            set
            {
                if (_mailServerName != value)
                {
                    _mailServerName = value;
                    OnPropertyChanged(nameof(MailServerName));
                }
            }
        }
        
        public string Username
        {
            get => _username;
            set
            {
                if (_username != value)
                {
                    _username = value;
                    OnPropertyChanged(nameof(Username));
                }
            }
        }
        
        public string Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password = value;
                    OnPropertyChanged(nameof(Password));
                }
            }
        }
        
        public string ReceiveEmailPattern => !string.IsNullOrEmpty(DomainName) ? $"*@{DomainName}" : "*@your-domain.com";
        
        public string LastTestStatusText
        {
            get => _lastTestStatusText;
            set
            {
                if (_lastTestStatusText != value)
                {
                    _lastTestStatusText = value;
                    OnPropertyChanged(nameof(LastTestStatusText));
                }
            }
        }
        
        public string LastTestTimeText
        {
            get => _lastTestTimeText;
            set
            {
                if (_lastTestTimeText != value)
                {
                    _lastTestTimeText = value;
                    OnPropertyChanged(nameof(LastTestTimeText));
                }
            }
        }
        
        public string LastTestMessage
        {
            get => _lastTestMessage;
            set
            {
                if (_lastTestMessage != value)
                {
                    _lastTestMessage = value;
                    OnPropertyChanged(nameof(LastTestMessage));
                }
            }
        }
        
        // 邮件配置引用，用于绑定LastTestResult等属性
        public EmailConfig EmailConfig => emailService?.CurrentConfig;
        
        public EmailConfigPanel()
        {
            InitializeComponent();
            
            // 设置数据上下文
            this.DataContext = this;
        }
        
        /// <summary>
        /// 设置邮件服务实例（从MainWindow传入）
        /// </summary>
        public void SetEmailService(EmailService service)
        {
            // 解除旧事件订阅
            if (emailService != null)
            {
                emailService.EmailConfigChanged -= EmailService_EmailConfigChanged;
            }
            
            emailService = service;
            
            // 订阅配置变更事件
            emailService.EmailConfigChanged += EmailService_EmailConfigChanged;
            
            // 加载配置数据
            LoadEmailConfig();
        }
        
        /// <summary>
        /// 当邮件配置变更时触发的事件处理
        /// </summary>
        private void EmailService_EmailConfigChanged(object sender, EventArgs e)
        {
            // 在UI线程上更新界面
            this.Dispatcher.Invoke(() =>
            {
                LoadEmailConfig();
            });
        }
        
        /// <summary>
        /// 加载邮件配置到UI
        /// </summary>
        private void LoadEmailConfig()
        {
            if (emailService != null && emailService.CurrentConfig != null)
            {
                var config = emailService.CurrentConfig;
                
                // 更新UI绑定属性
                DomainName = config.DomainName;
                Username = config.Username;
                Password = config.Password;
                MailServerName = config.MailServerName;
                
                // 如果密码不为空，更新PasswordBox
                if (!string.IsNullOrEmpty(config.Password))
                {
                    passwordBox.Password = config.Password;
                }
                
                // 更新测试状态
                UpdateTestStatusDisplay();
                
                // 通知绑定更新
                OnPropertyChanged(nameof(EmailConfig));
            }
        }
        
        /// <summary>
        /// 更新测试状态显示
        /// </summary>
        private void UpdateTestStatusDisplay()
        {
            if (emailService != null && emailService.CurrentConfig != null)
            {
                var config = emailService.CurrentConfig;
                
                // 如果从未测试过
                if (config.LastTestTime == DateTime.MinValue)
                {
                    LastTestStatusText = "未测试";
                    LastTestTimeText = "无";
                    LastTestMessage = "尚未进行测试";
                }
                else
                {
                    LastTestStatusText = config.LastTestResult ? "测试成功" : "测试失败";
                    LastTestTimeText = config.LastTestTime.ToString("yyyy-MM-dd HH:mm:ss");
                    LastTestMessage = config.LastTestMessage;
                }
            }
        }
        
        /// <summary>
        /// 域名文本变更事件
        /// </summary>
        private void Domain_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 已通过绑定处理，不需要额外操作
        }
        
        /// <summary>
        /// 密码变更事件
        /// </summary>
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // 由于安全原因，PasswordBox不能直接绑定，需要手动获取值
            Password = passwordBox.Password;
        }
        
        /// <summary>
        /// 测试连接按钮点击事件
        /// </summary>
        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            if (emailService == null)
            {
                MessageBox.Show("邮件服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                // 先更新服务中的配置值
                UpdateServiceConfig();
                
                // 禁用测试按钮，防止重复点击
                var testButton = sender as Button;
                if (testButton != null)
                {
                    testButton.IsEnabled = false;
                    testButton.Content = "测试中...";
                }
                
                // 执行测试
                var result = await emailService.TestEmailConfigAsync();
                
                // 更新测试状态显示
                UpdateTestStatusDisplay();
                
                // 显示测试结果
                MessageBox.Show(
                    result.Message,
                    result.Success ? "测试成功" : "测试失败",
                    MessageBoxButton.OK,
                    result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 恢复按钮状态
                var testButton = sender as Button;
                if (testButton != null)
                {
                    testButton.IsEnabled = true;
                    testButton.Content = "测试连接";
                }
            }
        }
        
        /// <summary>
        /// 保存配置按钮点击事件
        /// </summary>
        private void SaveConfig_Click(object sender, RoutedEventArgs e)
        {
            if (emailService == null)
            {
                MessageBox.Show("邮件服务未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            try
            {
                // 验证输入
                if (string.IsNullOrWhiteSpace(DomainName))
                {
                    MessageBox.Show("请输入邮件服务器转发域名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(Username))
                {
                    MessageBox.Show("请输入登录账号", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(Password))
                {
                    MessageBox.Show("请输入登录密码", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(MailServerName))
                {
                    MessageBox.Show("请输入邮件真实域名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 更新服务中的配置值
                UpdateServiceConfig();
                
                // 保存配置
                emailService.SaveEmailConfig();
                
                MessageBox.Show("邮件配置保存成功", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存配置时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 更新服务中的配置
        /// </summary>
        private void UpdateServiceConfig()
        {
            if (emailService != null && emailService.CurrentConfig != null)
            {
                emailService.CurrentConfig.DomainName = DomainName;
                emailService.CurrentConfig.Username = Username;
                emailService.CurrentConfig.Password = Password;
                emailService.CurrentConfig.MailServerName = MailServerName;
            }
        }
        
        // INotifyPropertyChanged 实现
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 
