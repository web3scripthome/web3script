using Microsoft.Win32;
using web3script.Handler;
using   web3script.Mode;
using System.IO;
using System.Windows;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System;
using System.Linq;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog; 

namespace web3script.Views
{
    public partial class AddServerWindow : Window
    {
        private ProfileItem _profileItem;
        private Config _config = new Config();

        public AddServerWindow(ProfileItem profileItem)
        {
            InitializeComponent();
            this.Owner = Application.Current.MainWindow;

            _profileItem = new ProfileItem();  // 总是创建新的配置项

            // 加载Core类型 
            cmbCoreType.Items.Add("clash_meta");

            // 设置默认值
            chkDisplayLog.IsChecked = true;
            txtPreSocksPort.Text = "1080";  // 设置默认端口为1080
            cmbCoreType.SelectedItem = "clash_meta";  // 设置默认核心类型

            ConfigHandler.LoadConfig(ref _config);
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtRemarks.Text))
                {
                    MessageBox.Show("请填写别名");
                    return;
                }

                if (string.IsNullOrEmpty(txtAddress.Text))
                {
                    MessageBox.Show("请选择配置文件地址");
                    return;
                }

                // 确认文件存在
                if (!File.Exists(txtAddress.Text))
                {
                    MessageBox.Show($"文件不存在: {txtAddress.Text}");
                    return;
                }

                // 处理Socks端口
                if (!int.TryParse(txtPreSocksPort.Text, out int socksPort) || socksPort < 0 || socksPort > 65535)
                {
                    MessageBox.Show("请输入有效的端口号(0-65535)");
                    return;
                }

                // 更新ProfileItem的基本信息
                _profileItem.remarks = txtRemarks.Text;
                _profileItem.coreType = cmbCoreType.SelectedItem?.ToString() ?? "clash_meta";
                _profileItem.displayLog = chkDisplayLog.IsChecked ?? false;
                _profileItem.preSocksPort = socksPort;
                _profileItem.port = socksPort;
                _profileItem.indexId = Guid.NewGuid().ToString();  // 生成新的ID

                // 如果是YAML文件，需要转换
                string extension = Path.GetExtension(txtAddress.Text).ToLowerInvariant();
                if (extension == ".yml" || extension == ".yaml")
                {
                    try
                    {
                        // 使用Utils.GetConfigPath()获取配置目录
                        string configDir = Utils.GetConfigPath();
                        if (!Directory.Exists(configDir))
                        {
                            Directory.CreateDirectory(configDir);
                            LogHandler.AddLog($"创建配置目录: {configDir}");
                        }

                        // 生成转换后的文件名，使用时间戳确保唯一性
                        string fileName = $"{DateTime.Now.ToString("yyyyMMddHHmmss")}_converted_{Guid.NewGuid().ToString("N").Substring(0, 8)}.yaml";
                        string convertedFilePath = Path.Combine(configDir, fileName);
                        LogHandler.AddLog($"准备转换配置文件到: {convertedFilePath}");

                        // 执行转换
                        if (YamlConfigConverter.ConvertClashConfig(txtAddress.Text, convertedFilePath, socksPort))
                        {
                            // 更新配置文件路径和类型
                            _profileItem.address = convertedFilePath;
                            _profileItem.configType = "converted_yaml";
                            LogHandler.AddLog($"配置文件已转换并保存到: {convertedFilePath}, 类型: converted_yaml");

                            // 从转换后的文件中读取端口信息
                            string convertedContent = File.ReadAllText(convertedFilePath);
                            _profileItem.clashProxyPorts.Clear();

                            // 读取listeners部分的端口
                            var listenerMatches = Regex.Matches(convertedContent, @"- name: (.+?)\s+type: (.+?)\s+port: (\d+)", RegexOptions.Multiline);
                            foreach (Match match in listenerMatches)
                            {
                                if (match.Groups.Count >= 4)
                                {
                                    var proxyPort = new ClashProxyPort
                                    {
                                        name = match.Groups[1].Value.Trim(),
                                        type = match.Groups[2].Value.Trim(),
                                        port = int.Parse(match.Groups[3].Value.Trim()),
                                        proxy = "全局",
                                        isActive = false
                                    };
                                    _profileItem.clashProxyPorts.Add(proxyPort);
                                    LogHandler.AddLog($"添加端口: {proxyPort.name}, {proxyPort.type}, {proxyPort.port}");
                                }
                            }

                            // 添加服务器
                            LogHandler.AddLog($"添加服务器，ID: {_profileItem.indexId}, 类型: {_profileItem.configType}");
                            int result = ConfigHandler.AddCustomServer(ref _config, _profileItem, false);
                            if (result != 0)
                            {
                                LogHandler.AddLog($"添加服务器失败，错误码: {result}");
                                MessageBox.Show("添加服务器失败");
                                return;
                            }
                            LogHandler.AddLog($"添加服务器成功: {_profileItem.remarks}");

                            this.DialogResult = true;
                            return;
                        }
                        else
                        {
                            MessageBox.Show("配置文件转换失败");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHandler.AddLog($"转换配置文件时出错: {ex.Message}");
                        MessageBox.Show($"转换配置文件时出错: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    // 非YAML文件，直接使用原始文件
                    _profileItem.address = txtAddress.Text;
                    _profileItem.configType = "custom";
                    LogHandler.AddLog($"使用原始配置文件: {_profileItem.address}, 类型: {_profileItem.configType}");

                    // 添加服务器
                    LogHandler.AddLog($"添加服务器，ID: {_profileItem.indexId}, 类型: {_profileItem.configType}");
                    int result = ConfigHandler.AddCustomServer(ref _config, _profileItem, false);
                    if (result != 0)
                    {
                        LogHandler.AddLog($"添加服务器失败，错误码: {result}");
                        MessageBox.Show("添加服务器失败");
                        return;
                    }
                    LogHandler.AddLog($"添加服务器成功: {_profileItem.remarks}");

                    this.DialogResult = true;
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"保存服务器时出错: {ex.Message}");
                MessageBox.Show($"保存服务器时出错: {ex.Message}");
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog fileDialog = new OpenFileDialog
                {
                    Multiselect = false,
                    Filter = "Config|*.yaml;*.yml|All|*.*"
                };

                if (fileDialog.ShowDialog() == true)
                {
                    // 获取完整的文件路径
                    string fullPath = fileDialog.FileName;
                    LogHandler.AddLog($"选择的文件: {fullPath}");

                    // 检查文件是否存在
                    if (!File.Exists(fullPath))
                    {
                        LogHandler.AddLog($"警告: 选择的文件不存在: {fullPath}");
                        MessageBox.Show($"选择的文件不存在: {fullPath}", "文件错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 设置地址文本框
                    txtAddress.Text = fullPath;

                    // 如果别名为空，自动使用文件名作为别名
                    if (string.IsNullOrEmpty(txtRemarks.Text))
                    {
                        txtRemarks.Text = Path.GetFileNameWithoutExtension(fullPath);
                        LogHandler.AddLog($"自动设置别名: {txtRemarks.Text}");
                    }

                    // 检测文件类型，自动设置核心类型
                    string extension = Path.GetExtension(fullPath).ToLower();
                    LogHandler.AddLog($"文件扩展名: {extension}");

                    // 读取文件内容
                    string fileContent = Utils.LoadFileContent(fullPath);
                    if (string.IsNullOrEmpty(fileContent))
                    {
                        LogHandler.AddLog("警告: 文件内容为空");
                        MessageBox.Show("文件内容为空或读取失败", "文件错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    LogHandler.AddLog($"成功读取文件内容，长度: {fileContent.Length} 字符");

                    // 清空现有的代理端口列表
                    _profileItem.clashProxyPorts.Clear();
                    LogHandler.AddLog("已清空现有代理端口列表");

                    if (!string.IsNullOrEmpty(fileContent))
                    {
                        if (extension == ".yaml" || extension == ".yml")
                        {
                            // 检测YAML是否为Clash配置
                            if (fileContent.Contains("proxies:") || fileContent.Contains("listeners:"))
                            {
                                bool isClashMeta = fileContent.Contains("premium-providers:")
                                    || fileContent.Contains("geosite:")
                                    || fileContent.Contains("geodata-mode:")
                                    || fileContent.Contains("rule-providers:");

                                cmbCoreType.SelectedItem = isClashMeta ? "clash_meta" : "clash";
                                LogHandler.AddLog($"检测到Clash{(isClashMeta ? ".Meta" : "")}配置文件");

                                // 检测端口配置
                                List<int> detectedPorts = new List<int>();

                                // 首先检查listeners部分（优先级最高）
                                var listenerMatches = Regex.Matches(fileContent, @"- name: (.+?)\s+type: (.+?)\s+port: (\d+)", RegexOptions.Multiline);
                                if (listenerMatches.Count > 0)
                                {
                                    LogHandler.AddLog($"检测到 {listenerMatches.Count} 个监听器配置");
                                    foreach (Match match in listenerMatches)
                                    {
                                        if (match.Groups.Count >= 4)
                                        {
                                            string name = match.Groups[1].Value.Trim();
                                            string type = match.Groups[2].Value.Trim();
                                            int port = int.Parse(match.Groups[3].Value.Trim());

                                            if (!detectedPorts.Contains(port))
                                            {
                                                detectedPorts.Add(port);
                                                LogHandler.AddLog($"检测到监听器: {name}, 类型: {type}, 端口: {port}");

                                                // 创建并添加代理端口信息
                                                var proxyPort = new ClashProxyPort
                                                {
                                                    name = name,
                                                    type = type,
                                                    port = port,
                                                    proxy = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "全局",
                                                    isActive = false
                                                };
                                                _profileItem.clashProxyPorts.Add(proxyPort);
                                            }
                                        }
                                    }
                                }

                                // 提取mixed-port（统一端口）
                                Match mixedPortMatch = Regex.Match(fileContent, @"mixed-port:\s*(\d+)");
                                if (mixedPortMatch.Success)
                                {
                                    int port = int.Parse(mixedPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到mixed-port: {port}");

                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "混合代理",
                                            type = "mixed",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }

                                // 提取socks-port
                                Match socksPortMatch = Regex.Match(fileContent, @"socks-port:\s*(\d+)");
                                if (socksPortMatch.Success)
                                {
                                    int port = int.Parse(socksPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到socks-port: {port}");

                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "SOCKS代理",
                                            type = "socks",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }

                                // 提取port（HTTP端口）
                                Match portMatch = Regex.Match(fileContent, @"^port:\s*(\d+)", RegexOptions.Multiline);
                                if (portMatch.Success)
                                {
                                    int port = int.Parse(portMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到HTTP port: {port}");

                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "HTTP代理",
                                            type = "http",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }

                                // 设置默认端口为第一个检测到的端口，或者使用默认值
                                if (detectedPorts.Count > 0)
                                {
                                    txtPreSocksPort.Text = detectedPorts[0].ToString();

                                    // 如果是编辑模式，更新ProfileItem的端口设置
                                    _profileItem.preSocksPort = detectedPorts[0];
                                }
                                else
                                {
                                    txtPreSocksPort.Text = "7890";  // Clash默认端口
                                    _profileItem.preSocksPort = 7890;
                                }

                                LogHandler.AddLog($"已设置主端口: {_profileItem.preSocksPort}, 共检测到 {_profileItem.clashProxyPorts.Count} 个端口");
                            }
                        }
                        else if (extension == ".json")
                        {
                            // 检测JSON是否为V2Ray配置
                            if (fileContent.Contains("\"inbounds\"") && fileContent.Contains("\"outbounds\""))
                            {
                                cmbCoreType.SelectedItem = "v2fly";
                                LogHandler.AddLog("检测到V2Ray配置文件");

                                // 尝试从配置中提取预设端口
                                Match inboundMatch = Regex.Match(fileContent, @"""port"":\s*(\d+)");
                                if (inboundMatch.Success)
                                {
                                    int port = int.Parse(inboundMatch.Groups[1].Value);
                                    txtPreSocksPort.Text = port.ToString();
                                    _profileItem.preSocksPort = port;
                                    LogHandler.AddLog($"检测到端口: {port}");
                                }
                            }
                            // 检测是否为Clash JSON配置
                            else if (fileContent.Contains("\"proxies\"") || fileContent.Contains("\"Proxy\"") || fileContent.Contains("\"listeners\""))
                            {
                                cmbCoreType.SelectedItem = "clash";
                                LogHandler.AddLog("检测到Clash JSON配置文件");

                                // 检测端口配置
                                List<int> detectedPorts = new List<int>();

                                // 首先检查listeners部分
                                try
                                {
                                    var listenerRegex = Regex.Match(fileContent, @"""listeners""\s*:\s*\[(.*?)\]", RegexOptions.Singleline);
                                    if (listenerRegex.Success && listenerRegex.Groups.Count > 1)
                                    {
                                        string listenersSection = listenerRegex.Groups[1].Value;
                                        var listenerMatches = Regex.Matches(listenersSection, @"""name""\s*:\s*""(.*?)""\s*,.*?""type""\s*:\s*""(.*?)""\s*,.*?""port""\s*:\s*(\d+)", RegexOptions.Singleline);

                                        LogHandler.AddLog($"检测到 {listenerMatches.Count} 个监听器配置");

                                        foreach (Match match in listenerMatches)
                                        {
                                            if (match.Groups.Count >= 4)
                                            {
                                                string name = match.Groups[1].Value;
                                                string type = match.Groups[2].Value;
                                                int port = int.Parse(match.Groups[3].Value);

                                                if (!detectedPorts.Contains(port))
                                                {
                                                    detectedPorts.Add(port);
                                                    LogHandler.AddLog($"检测到监听器: {name}, 类型: {type}, 端口: {port}");

                                                    // 提取proxy值（如果有）
                                                    string proxy = "全局";
                                                    var proxyMatch = Regex.Match(match.Value, @"""proxy""\s*:\s*""(.*?)""");
                                                    if (proxyMatch.Success)
                                                    {
                                                        proxy = proxyMatch.Groups[1].Value;
                                                    }

                                                    // 创建并添加代理端口信息
                                                    var proxyPort = new ClashProxyPort
                                                    {
                                                        name = name,
                                                        type = type,
                                                        port = port,
                                                        proxy = proxy,
                                                        isActive = false
                                                    };
                                                    _profileItem.clashProxyPorts.Add(proxyPort);
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogHandler.AddLog($"解析JSON监听器配置失败: {ex.Message}");
                                }

                                // 尝试从配置中提取mixed-port
                                Match mixedPortMatch = Regex.Match(fileContent, @"""mixed-port"":\s*(\d+)");
                                if (mixedPortMatch.Success)
                                {
                                    int port = int.Parse(mixedPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到混合端口: {port}");

                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "混合代理",
                                            type = "mixed",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }

                                // 尝试提取socks-port
                                Match socksPortMatch = Regex.Match(fileContent, @"""socks-port"":\s*(\d+)");
                                if (socksPortMatch.Success)
                                {
                                    int port = int.Parse(socksPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到Socks端口: {port}");

                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "SOCKS代理",
                                            type = "socks",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }

                                // 尝试提取http端口
                                Match httpPortMatch = Regex.Match(fileContent, @"""port"":\s*(\d+)");
                                if (httpPortMatch.Success)
                                {
                                    int port = int.Parse(httpPortMatch.Groups[1].Value);
                                    if (!detectedPorts.Contains(port))
                                    {
                                        detectedPorts.Add(port);
                                        LogHandler.AddLog($"检测到HTTP端口: {port}");

                                        // 添加到代理端口列表
                                        _profileItem.clashProxyPorts.Add(new ClashProxyPort
                                        {
                                            name = "HTTP代理",
                                            type = "http",
                                            port = port,
                                            proxy = "全局",
                                            isActive = false
                                        });
                                    }
                                }

                                // 设置默认端口为第一个检测到的端口，或者使用默认值
                                if (detectedPorts.Count > 0)
                                {
                                    txtPreSocksPort.Text = detectedPorts[0].ToString();
                                    _profileItem.preSocksPort = detectedPorts[0];
                                }
                                else
                                {
                                    txtPreSocksPort.Text = "7890";  // Clash默认端口
                                    _profileItem.preSocksPort = 7890;
                                }

                                LogHandler.AddLog($"已设置主端口: {_profileItem.preSocksPort}, 共检测到 {_profileItem.clashProxyPorts.Count} 个端口");
                            }
                        }
                    }
                    LogHandler.AddLog($"文件处理完成: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"选择文件时出错: {ex.Message}, 堆栈: {ex.StackTrace}");
                MessageBox.Show($"选择文件时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtAddress.Text))
            {
                MessageBox.Show("请先选择配置文件");
                return;
            }

            if (File.Exists(txtAddress.Text))
            {
                try
                {
                    System.Diagnostics.Process.Start("notepad.exe", txtAddress.Text);
                    LogHandler.AddLog($"正在编辑配置文件: {txtAddress.Text}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开配置文件失败: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("配置文件不存在");
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        private void btnImportClash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtAddress.Text) || !File.Exists(txtAddress.Text))
                {
                    MessageBox.Show("请先选择一个原始的Clash配置文件", "配置缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string sourceFile = txtAddress.Text;
                string fileExt = Path.GetExtension(sourceFile).ToLowerInvariant();

                if (fileExt != ".yml" && fileExt != ".yaml")
                {
                    MessageBox.Show("选择的文件不是YAML格式", "格式错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 获取要保存的目标文件名称
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "YAML|*.yaml;*.yml",
                    Title = "保存转换后的Clash配置文件",
                    FileName = Path.GetFileNameWithoutExtension(sourceFile) + "_converted.yaml"
                };

                if (saveDialog.ShowDialog() != true)
                {
                    return;
                }

                // 解析Socks端口
                int socksPort = 1080; // 默认值
                if (int.TryParse(txtPreSocksPort.Text, out int port))
                {
                    socksPort = port;
                }

                // 转换配置文件
                bool result = YamlConfigConverter.ConvertClashConfig(sourceFile, saveDialog.FileName, socksPort);

                if (result)
                {
                    // 更新UI
                    txtAddress.Text = saveDialog.FileName;
                    cmbCoreType.SelectedItem = "clash"; // 默认选择clash

                    // 如果别名为空，则设置别名
                    if (string.IsNullOrEmpty(txtRemarks.Text))
                    {
                        txtRemarks.Text = Path.GetFileNameWithoutExtension(saveDialog.FileName);
                    }

                    MessageBox.Show("Clash配置文件已成功转换并保存", "转换成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("无法转换Clash配置文件，请查看日志了解详情", "转换失败", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"导入Clash配置时发生异常: {ex.Message}");
                MessageBox.Show($"导入Clash配置时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}