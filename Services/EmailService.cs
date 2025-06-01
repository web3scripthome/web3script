using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using web3script.Models;
using Newtonsoft.Json;

namespace web3script.Services
{
    public class EmailService
    {
        private readonly string _emailConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "email_config.json");
        
        public EmailConfig CurrentConfig { get; private set; }
        
        // 当邮件配置变更时触发的事件
        public event EventHandler EmailConfigChanged;

        public EmailService()
        {
            LoadEmailConfig();
        }

        /// <summary>
        /// 加载邮件配置
        /// </summary>
        public void LoadEmailConfig()
        {
            try
            {
                if (File.Exists(_emailConfigFilePath))
                {
                    string json = File.ReadAllText(_emailConfigFilePath);
                    CurrentConfig = JsonConvert.DeserializeObject<EmailConfig>(json) ?? new EmailConfig();
                }
                else
                {
                    CurrentConfig = new EmailConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载邮件配置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                CurrentConfig = new EmailConfig();
            }
        }

        /// <summary>
        /// 保存邮件配置
        /// </summary>
        public void SaveEmailConfig()
        {
            try
            {
                string directory = Path.GetDirectoryName(_emailConfigFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(CurrentConfig, Formatting.Indented);
                File.WriteAllText(_emailConfigFilePath, json);
                
                // 触发配置变更事件
                EmailConfigChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存邮件配置时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 测试邮件配置
        /// </summary>
        /// <returns>测试结果和消息</returns>
        public async Task<(bool Success, string Message)> TestEmailConfigAsync()
        {
            if (CurrentConfig == null)
            {
                return (false, "邮件配置不存在");
            }

            if (string.IsNullOrWhiteSpace(CurrentConfig.DomainName))
            {
                return (false, "邮件服务器转发域名不能为空");
            }

            if (string.IsNullOrWhiteSpace(CurrentConfig.Username))
            {
                return (false, "登录账号不能为空");
            }

            if (string.IsNullOrWhiteSpace(CurrentConfig.Password))
            {
                return (false, "登录密码不能为空");
            }
            
            if (string.IsNullOrWhiteSpace(CurrentConfig.MailServerName))
            {
                return (false, "邮件真实域名不能为空");
            }

            try
            { 
               
                IMEmailReceiver iMEmailReceiver = new IMEmailReceiver();
                var isVa= await iMEmailReceiver.TestEmailServer(CurrentConfig.MailServerName, 993, CurrentConfig.Username, CurrentConfig.Password);
                if (!isVa.isValid)
                {
                    CurrentConfig.LastTestTime = DateTime.Now;
                    CurrentConfig.LastTestResult = false;
                    CurrentConfig.LastTestMessage =isVa.message;
                    return (false, isVa.message);
                }
                else
                {
                    CurrentConfig.LastTestTime = DateTime.Now;
                    CurrentConfig.LastTestResult = true;
                    CurrentConfig.LastTestMessage = "连接测试成功"; 
                    SaveEmailConfig(); 
                    return (true, "连接测试成功");
                }

                
               
            }
            catch (Exception ex)
            {
                // 更新测试结果
                CurrentConfig.LastTestTime = DateTime.Now;
                CurrentConfig.LastTestResult = false;
                CurrentConfig.LastTestMessage = $"测试失败: {ex.Message}";
                
                // 保存测试结果
                SaveEmailConfig();
                
                return (false, $"测试失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取随机邮箱地址
        /// </summary>
        /// <returns>基于配置域名的随机邮箱地址</returns>
        public string GenerateRandomEmail()
        {
            if (CurrentConfig == null || string.IsNullOrWhiteSpace(CurrentConfig.DomainName))
            {
                return "user@example.com";
            }
            
            string randomPrefix = Guid.NewGuid().ToString().Substring(0, 8);
            return $"{randomPrefix}@{CurrentConfig.DomainName}";
        }
    }
} 