using System;
using System.ComponentModel;

namespace web3script.Models
{
    /// <summary>
    /// 邮件配置模型类
    /// </summary>
    public class EmailConfig : INotifyPropertyChanged
    {
        private string _id;
        private string _domainName;
        private string _mailServerName;
        private string _username;
        private string _password;
        private bool _isEnabled;
        private DateTime _lastTestTime;
        private bool _lastTestResult;
        private string _lastTestMessage;

        /// <summary>
        /// 配置ID
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        /// <summary>
        /// 邮件服务器域名
        /// </summary>
    
        /// <summary>
        /// 邮件服务器域名
        /// </summary>
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
        /// <summary>
        /// 登录账号
        /// </summary>
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

        /// <summary>
        /// 登录密码
        /// </summary>
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

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        /// <summary>
        /// 最后测试时间
        /// </summary>
        public DateTime LastTestTime
        {
            get => _lastTestTime;
            set
            {
                if (_lastTestTime != value)
                {
                    _lastTestTime = value;
                    OnPropertyChanged(nameof(LastTestTime));
                }
            }
        }

        /// <summary>
        /// 最后测试结果
        /// </summary>
        public bool LastTestResult
        {
            get => _lastTestResult;
            set
            {
                if (_lastTestResult != value)
                {
                    _lastTestResult = value;
                    OnPropertyChanged(nameof(LastTestResult));
                }
            }
        }

        /// <summary>
        /// 最后测试消息
        /// </summary>
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

        /// <summary>
        /// 邮件接收地址模式
        /// </summary>
        public string ReceiveEmailPattern => !string.IsNullOrEmpty(DomainName) ? $"*@{DomainName}" : "*@your-domain.com";

        public EmailConfig()
        {
            Id = Guid.NewGuid().ToString();
            LastTestTime = DateTime.MinValue;
            IsEnabled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 
