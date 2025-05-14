using MailKit.Search;
using MailKit.Security;
using MailKit;
using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System; 
using MailKit.Net.Imap;  
using System.Threading.Tasks; 
using System.Collections.Generic;
using System.Threading;
using System.Net.Http.Headers;
using Nethereum.Contracts.QueryHandlers.MultiCall;
using System.Diagnostics;
namespace web3script.Services
{
    public class TestMailReceiver
    {
        public bool isValid { get; set; }
        public string message { get; set; }

    }
    internal class IMEmailReceiver
    {
        private string _imapServer = " "; // IMAP 邮件服务器地址
        private int _port = 993; // IMAP 端口（SSL 加密，使用 993 端口）
        private string _emailAddress = " "; // 邮箱地址
        private string _password = " "; // 邮箱密码
                                        // private string _password = "Abcd1234"; // 邮箱密码
        public async Task<string> StartReceivingAsync(string account)
        {
            try
            {
                // 禁用证书验证（因为使用的是非加密连接，不需要证书）
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                using (var imapClient = new ImapClient())
                {
                    imapClient.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                    // 使用加密连接
                    await imapClient.ConnectAsync(_imapServer, _port, SecureSocketOptions.SslOnConnect);  // 如果需要非加密连接，使用 SecureSocketOptions.None
                    Debug.WriteLine("成功连接到 IMAP 服务器。");

                    // 使用用户名和密码进行身份验证
                    try
                    {
                        await imapClient.AuthenticateAsync(_emailAddress, _password);
                        Debug.WriteLine("身份验证成功！");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"身份验证失败: {ex.Message}");
                        return null; // 如果身份验证失败，返回 null
                    }



                    var inbox = imapClient.Inbox;
                    await inbox.OpenAsync(MailKit.FolderAccess.ReadWrite); // 打开收件箱，允许读取和删除邮件
                    int ct = inbox.Count; // 初始邮件数

                    while (true)
                    {
                        try
                        {
                            Debug.WriteLine($"{account} 正在检查匹配的邮件.. 当前邮件数: {ct}");

                            // 获取所有新邮件的 UID
                            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);
                            if (uids.Count > 0)
                            {
                                Debug.WriteLine($"检测到新邮件，共 {uids.Count} 封未读邮件");

                                foreach (var uid in uids)
                                {
                                    var message = await inbox.GetMessageAsync(uid);
                                    string code = await CheckNewEmails(inbox, account); // 检查新邮件
                                    if (!string.IsNullOrEmpty(code))
                                    {
                                        return code; // 返回匹配的 code
                                    }
                                }
                            }
                            else
                            {
                                Debug.WriteLine("没有检测到新邮件");
                            }

                            // 更新邮件计数
                            ct = inbox.Count;
                        }
                        catch (Exception ex)
                        {


                            Debug.WriteLine($"检查邮件时出错: {ex.Message}");
                            return "";
                        }

                        await Task.Delay(TimeSpan.FromSeconds(5)); // 每隔 5 秒检查一次
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"接收邮件时发生错误: {ex.Message}");
                return null;  // 如果发生错误，返回 null
            }
        }

        public async Task<TestMailReceiver> TestEmailServer(string imapServer, int port, string emailAddress, string password)
        {
            try
            {
                Debug.WriteLine($"imapServer: {imapServer}, port: {port}, emailAddress: {emailAddress}, password: {password}");
                // 禁用证书验证（因为使用的是非加密连接，不需要证书）
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                using (var imapClient = new ImapClient())
                {
                    imapClient.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                    // 使用加密连接
                    await imapClient.ConnectAsync(imapServer, port, SecureSocketOptions.SslOnConnect);  // 如果需要非加密连接，使用 SecureSocketOptions.None
                     
                    try
                    {
                        await imapClient.AuthenticateAsync(emailAddress, password);
                        var result = new TestMailReceiver() { isValid = true, message = "邮件服务器连接成功" };
                        return result;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"身份验证失败: {ex.Message}");
                        var result = new TestMailReceiver() { isValid = false, message = "身份验证失败" };
                        return result;
                    }

                      

                }
            }
            catch (Exception ex)
            {

                return   new TestMailReceiver() { isValid = false, message = "连接服务器发生错误"+ex.Message };
            }
        }

        public async Task StartReceivingAsyncx(string account)
        {
            try
            {
                // 禁用证书验证（因为使用的是非加密连接，不需要证书）
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                using (var imapClient = new ImapClient())
                {
                    imapClient.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                    // 使用加密连接
                    await imapClient.ConnectAsync(_imapServer, _port, SecureSocketOptions.SslOnConnect);  // 如果需要非加密连接，使用 SecureSocketOptions.None
                    Debug.WriteLine("成功连接到 IMAP 服务器。");

                    // 使用用户名和密码进行身份验证
                    try
                    {
                        await imapClient.AuthenticateAsync(_emailAddress, _password);
                        Debug.WriteLine("身份验证成功！");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"身份验证失败: {ex.Message}");

                    }



                    var inbox = imapClient.Inbox;
                    await inbox.OpenAsync(MailKit.FolderAccess.ReadWrite); // 打开收件箱，允许读取和删除邮件


                    while (true)
                    {
                        try
                        {
                            Debug.WriteLine($"{account} 正在检查匹配的邮件.. 当前邮件数: {ct}");

                            // 获取所有新邮件的 UID
                            var uids = await inbox.SearchAsync(SearchQuery.NotSeen);
                            if (uids.Count > 0)
                            {
                                Debug.WriteLine($"检测到新邮件，共 {uids.Count} 封未读邮件");

                                foreach (var uid in uids)
                                {
                                    var message = await inbox.GetMessageAsync(uid);
                                    await deNewEmails(inbox, account); // 检查新邮件

                                }
                            }
                            else
                            {
                                Debug.WriteLine("没有检测到新邮件");
                            }

                            // 更新邮件计数
                            ct = inbox.Count;
                        }
                        catch (Exception ex)
                        {


                            Debug.WriteLine($"检查邮件时出错: {ex.Message}");

                        }

                        await Task.Delay(TimeSpan.FromSeconds(16)); // 每隔 5 秒检查一次
                    }

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"接收邮件时发生错误: {ex.Message}");

            }
        }

        public async Task deNewEmails(IMailFolder inbox, string account)
        {
            try
            {
                int messageCount = inbox.Count;

                // 获取所有邮件并按时间排序
                var items = await inbox.FetchAsync(0, messageCount - 1, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate);
                var sortedItems = items.OrderByDescending(item => item.InternalDate).ToList();

                UniqueId? lastMatchedUniqueId = null;
                MimeMessage? lastMatchedMessage = null;

                foreach (var item in sortedItems)
                {
                    var uniqueId = item.UniqueId; // 获取邮件的 UniqueId
                    var message = await inbox.GetMessageAsync(item.Index); // 获取邮件内容



                    if (message.TextBody != null && message.TextBody.Contains(account))
                    {
                        if (lastMatchedMessage == null)
                        {
                            await inbox.AddFlagsAsync(uniqueId, MessageFlags.Deleted, silent: false);
                            await inbox.ExpungeAsync();
                            Debug.WriteLine("删除邮件成功");
                        }

                    }
                }


            }
            catch (Exception ex)
            {


            }
        }

        public int ct = 0; 
        public async Task<string> CheckNewEmails(IMailFolder inbox, string account)
        {
            try
            {
                int messageCount = inbox.Count;
                if (messageCount > ct)
                {
                    Debug.WriteLine("共收到邮件" + messageCount);
                    ct = messageCount;
                }

                // 获取所有邮件并按时间排序
                var items = await inbox.FetchAsync(0, messageCount - 1, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate);
                var sortedItems = items.OrderByDescending(item => item.InternalDate).ToList();

                UniqueId? lastMatchedUniqueId = null;
                MimeMessage? lastMatchedMessage = null;

                foreach (var item in sortedItems)
                {
                    var uniqueId = item.UniqueId; // 获取邮件的 UniqueId
                    var message = await inbox.GetMessageAsync(item.Index); // 获取邮件内容

                    if (message.TextBody != null && message.TextBody.Contains(account))
                    {
                        if (lastMatchedMessage == null)
                        {
                            // 如果还没有设置最后匹配的邮件，则设置为当前邮件
                            lastMatchedMessage = message;
                            lastMatchedUniqueId = uniqueId;
                            await inbox.AddFlagsAsync(uniqueId, MessageFlags.Deleted, silent: false);

                        }
                        else
                        {
                            // 删除其他匹配的邮件
                            await inbox.AddFlagsAsync(uniqueId, MessageFlags.Deleted, silent: false);
                        }
                    }
                    string text = "It's time to build!";
                    if (message.TextBody != null && message.TextBody.Contains(text))
                    {
                        if (lastMatchedMessage == null)
                        {
                         
                            await inbox.AddFlagsAsync(uniqueId, MessageFlags.Deleted, silent: false);

                        }

                    }
                }

                if (lastMatchedMessage != null && lastMatchedUniqueId.HasValue)
                {

                    await inbox.ExpungeAsync();
                    Debug.WriteLine("已清除其他匹配的邮件");

                    // 返回最后匹配邮件的 code
                    var numbers = ExtractNumbers(lastMatchedMessage.TextBody);
                    if (numbers.Count > 0)
                    {
                        return numbers[0]; // 假设提取到的第一个数字是需要的 code
                    }
                }

                return ""; // 如果没有找到匹配的 code，返回空字符串
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理邮件时出错: {ex.Message}");
                return "";
            }
        }

        public string PrintEmailBody(MimeMessage message, string account)
        {
            // 获取邮件的纯文本正文
            string textPart = message.TextBody;
            if (textPart != null && textPart.Contains(account))
            {
                var numbers = ExtractNumbers(textPart);
                if (numbers.Count > 0)
                {
                    return numbers[0];  // 假设提取到的第一个数字是需要的 code
                }
            }
            else
            {

                //  Debug.WriteLine($"不匹配 1:{textPart}---2{account}");

            }

            return "";
        }

        public async Task DeAEmails()
        {
            try
            {
                using (var imapClient = new ImapClient())
                {
                    imapClient.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

                    await imapClient.ConnectAsync(_imapServer, _port, SecureSocketOptions.SslOnConnect);
                    Debug.WriteLine("成功连接到 IMAP 服务器。");

                    await imapClient.AuthenticateAsync(_emailAddress, _password);
                    Debug.WriteLine("身份验证成功！");

                    var inbox = imapClient.Inbox;
                    await inbox.OpenAsync(MailKit.FolderAccess.ReadWrite);

                    Debug.WriteLine($"删除所有 {inbox.Count} 封邮件...");
                    foreach (var summary in await inbox.FetchAsync(0, -1, MessageSummaryItems.UniqueId))
                    {
                        await inbox.AddFlagsAsync(summary.UniqueId, MessageFlags.Deleted, silent: false);
                    }

                    await inbox.ExpungeAsync(); // 清空所有已删除的邮件
                    Debug.WriteLine("所有邮件已成功删除！");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"删除邮件时发生错误: {ex.Message}");
            }
        } 
        private List<string> ExtractNumbers(string text)
        {
            var numbers = new List<string>();
            var regex = new Regex(@"\d+");  // 匹配数字
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                numbers.Add(match.Value);  // 将匹配到的数字加入列表
            }

            return numbers;
        }
    }
}

