using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using web3script.Mode;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;

namespace web3script.Handler
{
    /// <summary>
    /// Core进程处理类
    /// </summary>
    public class CoreHandler
    {
        private static string _coreCConfigRes = Global.coreConfigFileName;
        private Dictionary<string, Process> _processes = new Dictionary<string, Process>();
        private Dictionary<string, List<int>> _additionalProcessIds = new Dictionary<string, List<int>>();
        private Action<bool, string>? _updateFunc;
        private readonly string _binPath;
        private readonly string _logPath;
        private Dictionary<string, string> _currentCoreExes = new Dictionary<string, string>();
        private Dictionary<string, ProfileItem> _currentNodes = new Dictionary<string, ProfileItem>();
        private Dictionary<string, string> _configStrings = new Dictionary<string, string>();

       
        public event EventHandler<LogEventArgs> OutputDataReceived;

       
        private static Dictionary<int, bool> _portCheckingStatus = new Dictionary<int, bool>();
        private static readonly object _portCheckLock = new object();

        public CoreHandler(Action<bool, string> update)
        {
            _updateFunc = update;
            _binPath = Utils.GetBinPath();
            _logPath = Utils.GetLogPath();

            Environment.SetEnvironmentVariable("v2ray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("xray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        }

        
        public CoreHandler()
        {
            _binPath = Utils.GetBinPath();
            _logPath = Utils.GetLogPath();

            Environment.SetEnvironmentVariable("v2ray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("xray.location.asset", Utils.GetBinPath(""), EnvironmentVariableTarget.Process);
        }

       
        private void RaiseOutputDataReceived(string content)
        {
            
            OutputDataReceived?.Invoke(this, new LogEventArgs { content = content });
        }

        public void LoadCore(Config config)
        {
            try
            {
                if (config.profileItems == null || config.profileItems.Count == 0)
                {
                    if (_updateFunc != null)
                        _updateFunc(false, "没有可用的服务器配置");
                    LogHandler.AddLog("配置文件中没有服务器配置");
                    return;
                }

                var guid = string.IsNullOrEmpty(config.indexId) ? string.Empty : config.indexId;
                LogHandler.AddLog($"尝试加载服务器配置，indexId: {guid}, 配置中有 {config.profileItems.Count} 个服务器");

                
                if (string.IsNullOrEmpty(guid) && config.profileItems.Count > 0)
                {
                    guid = config.profileItems[0].indexId;
                    LogHandler.AddLog($"没有指定服务器ID，使用第一个服务器: {guid}");
                }

               
                var node = config.profileItems.FirstOrDefault(t => t.indexId == guid);
                if (node == null)
                {
                    if (_updateFunc != null)
                        _updateFunc(false, "未找到对应的服务器配置");
                    LogHandler.AddLog("未找到对应的服务器配置，无法启动");
                    return;
                }

               
                if (_processes.ContainsKey(guid))
                {
                    StopServer(guid);
                }

              
                switch (node.configType)
                {
                    case "custom":
                    case "converted_yaml":
                        LogHandler.AddLog($"启动配置: {node.remarks}, 核心类型: {node.coreType}, 配置类型: {node.configType}");
                        StartWithCustomConfig(node);
                        break;

                    default:
                        if (_updateFunc != null)
                            _updateFunc(false, $"不支持的配置类型：{node.configType}");
                        LogHandler.AddLog($"不支持的配置类型：{node.configType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_updateFunc != null)
                    _updateFunc(false, $"启动时出错：{ex.Message}");
                LogHandler.AddLog($"启动异常：{ex}");
            }
        }

        private void StartWithCustomConfig(ProfileItem node)
        {
            try
            {
                if (string.IsNullOrEmpty(node.address))
                {
                    LogHandler.AddLog("配置文件路径为空");
                    return;
                }

               
                if (!File.Exists(node.address))
                {
                    LogHandler.AddLog($"配置文件不存在：{node.address}");
                    return;
                }

               
                string coreExe = DetermineCoreExe(node.coreType.ToLower());
                if (string.IsNullOrEmpty(coreExe))
                {
                    LogHandler.AddLog($"找不到核心文件：{node.coreType}");
                    return;
                }

                LogHandler.AddLog($"使用核心文件: {coreExe}");

              
                if (node.coreType.ToLower().Contains("clash"))
                {
                    var arguments = $"-f \"{node.address}\"";
                    LogHandler.AddLog($"启动Clash配置: {node.remarks}, 文件: {node.address}");
                    CoreStartViaString(coreExe, arguments, node);
                    return;
                }
                else
                {
                    
                    if (node.preSocksPort <= 0)
                    {
                        node.preSocksPort = 1080;
                    }

                    if (!IsPortAvailable(node.preSocksPort))
                    {
                        LogHandler.AddLog($"端口 {node.preSocksPort} 已被占用");
                        return;
                    }

                    string tmpConfig = Path.Combine(
                        Path.GetTempPath(),
                        "SimpleV2ray",
                        $"v2ray_config_{Path.GetFileNameWithoutExtension(node.address)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.json"
                    );

                    Directory.CreateDirectory(Path.GetDirectoryName(tmpConfig));
                    var socksConfig = GenerateSocksConfig(node, node.address);
                    File.WriteAllText(tmpConfig, socksConfig);

                    CoreStartViaConfig(coreExe, tmpConfig, node);
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"启动配置时出错: {ex.Message}");
            }
        }

        private void StartWithClashConfig(ProfileItem node, string configPath, string coreExe)
        {
            try
            {
                LogHandler.AddLog($"开始启动Clash配置: {node.remarks}, 文件路径: {configPath}");

                if (node.preSocksPort <= 0)
                {
                    node.preSocksPort = 45000;  
                    LogHandler.AddLog($"未指定Socks端口，使用默认端口: {node.preSocksPort}");
                }

                
                if (!IsPortAvailable(node.preSocksPort))
                {
                    if (_updateFunc != null)
                        _updateFunc(false, $"端口 {node.preSocksPort} 已被占用，请尝试其他端口");
                    LogHandler.AddLog($"端口 {node.preSocksPort} 已被占用，请尝试其他端口");
                    return;
                }

                string tmpConfig = Path.Combine(
                    Path.GetTempPath(),
                    "SimpleV2ray",
                    $"clash_config_{Path.GetFileNameWithoutExtension(configPath)}_{DateTime.Now.ToString("yyyyMMddHHmmss")}.yaml"
                );

                // 确保临时目录存在
                Directory.CreateDirectory(Path.GetDirectoryName(tmpConfig));
                LogHandler.AddLog($"创建临时配置文件: {tmpConfig}");

                // 修改Clash配置
                ModifyClashConfig(configPath, tmpConfig, node);
                LogHandler.AddLog($"已修改配置并保存至临时文件: {tmpConfig}");

                // 设置Clash专用启动参数
                var arguments = $"-f \"{tmpConfig}\"";
                LogHandler.AddLog($"Clash启动参数: {arguments}");

                // 将所有代理端口预先标为未激活
                LogHandler.AddLog($"代理端口数量: {node.clashProxyPorts.Count}");
                foreach (var port in node.clashProxyPorts)
                {
                    port.isActive = false;
                    LogHandler.AddLog($"预设端口 {port.port} 为未激活状态");
                }

               
                LogHandler.AddLog($"正在启动Clash代理 ({node.coreType})，端口: {node.preSocksPort}");
                CoreStartViaString(coreExe, arguments, node);
            }
            catch (Exception ex)
            {
                if (_updateFunc != null)
                    _updateFunc(false, $"启动Clash配置时出错：{ex.Message}");
                LogHandler.AddLog($"启动Clash配置异常：{ex}");
            }
        }

        private void ModifyClashConfig(string source, string dest, ProfileItem node)
        {
            try
            {
                // 清空现有端口配置
                node.clashProxyPorts.Clear();

                string fileExt = Path.GetExtension(source).ToLower();
                if (fileExt == ".json")
                {
                    string originalJson = File.ReadAllText(source);
                    var config = JObject.Parse(originalJson);

                    // 提取listeners信息
                    if (config["listeners"] is JArray listeners && listeners.Count > 0)
                    {
                        LogHandler.AddLog($"从JSON配置文件中发现 {listeners.Count} 个监听器");

                        foreach (JObject listener in listeners)
                        {
                            try
                            {
                                string name = listener["name"]?.ToString() ?? "未命名";
                                string type = listener["type"]?.ToString() ?? "未知";
                                int port = listener["port"]?.Value<int>() ?? 0;
                                string proxy = listener["proxy"]?.ToString() ?? "全局";

                                if (port > 0)
                                {
                                    var proxyPort = new ClashProxyPort
                                    {
                                        name = name,
                                        type = type,
                                        port = port,
                                        proxy = proxy,
                                        isActive = false
                                    };
                                    node.clashProxyPorts.Add(proxyPort);
                                    LogHandler.AddLog($"添加监听器: {name}, 类型: {type}, 端口: {port}, 代理: {proxy}");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"解析监听器信息失败: {ex.Message}");
                            }
                        }
                    }

                    // 设置日志级别
                    config["log-level"] = "info";

                    // 保存修改后的配置
                    string modifiedJson = config.ToString();
                    File.WriteAllText(dest, modifiedJson);
                }
                else if (fileExt == ".yaml" || fileExt == ".yml")
                {
                    // 读取YAML文件
                    string yamlContent = File.ReadAllText(source);

                    // 使用正则表达式提取listeners信息
                    var listenerMatches = Regex.Matches(yamlContent, @"listeners:(?:\s*-\s*name:\s*([^\n]*)\s*type:\s*([^\n]*)\s*port:\s*(\d+)(?:\s*proxy:\s*([^\n]*))?)+", RegexOptions.Singleline);

                    if (listenerMatches.Count > 0)
                    {
                        // 更详细的匹配单个listener
                        var singleListenerMatches = Regex.Matches(yamlContent, @"-\s*name:\s*([^\n]*)\s*type:\s*([^\n]*)\s*port:\s*(\d+)(?:\s*proxy:\s*([^\n]*))?", RegexOptions.Multiline);

                        LogHandler.AddLog($"从YAML配置文件中发现 {singleListenerMatches.Count} 个监听器");

                        foreach (Match match in singleListenerMatches)
                        {
                            try
                            {
                                if (match.Groups.Count >= 4)
                                {
                                    string name = match.Groups[1].Value.Trim();
                                    string type = match.Groups[2].Value.Trim();
                                    int port = int.Parse(match.Groups[3].Value.Trim());
                                    string proxy = match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value)
                                        ? match.Groups[4].Value.Trim()
                                        : "全局";

                                    if (port > 0)
                                    {
                                        var proxyPort = new ClashProxyPort
                                        {
                                            name = name,
                                            type = type,
                                            port = port,
                                            proxy = proxy,
                                            isActive = false
                                        };
                                        node.clashProxyPorts.Add(proxyPort);
                                        LogHandler.AddLog($"添加监听器: {name}, 类型: {type}, 端口: {port}, 代理: {proxy}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"解析监听器信息失败: {ex.Message}");
                            }
                        }
                    }

                    // 如果没有找到监听器配置，尝试检查传统的端口配置
                    if (node.clashProxyPorts.Count == 0)
                    {
                        // 尝试匹配port, socks-port, mixed-port
                        var portMatch = Regex.Match(yamlContent, @"port:\s*(\d+)", RegexOptions.Multiline);
                        var socksPortMatch = Regex.Match(yamlContent, @"socks-port:\s*(\d+)", RegexOptions.Multiline);
                        var mixedPortMatch = Regex.Match(yamlContent, @"mixed-port:\s*(\d+)", RegexOptions.Multiline);

                        if (portMatch.Success && portMatch.Groups.Count > 1)
                        {
                            int port = int.Parse(portMatch.Groups[1].Value);
                            node.clashProxyPorts.Add(new ClashProxyPort
                            {
                                name = "HTTP代理",
                                type = "http",
                                port = port,
                                proxy = "全局",
                                isActive = false
                            });
                            LogHandler.AddLog($"添加HTTP代理端口: {port}");
                        }

                        if (socksPortMatch.Success && socksPortMatch.Groups.Count > 1)
                        {
                            int port = int.Parse(socksPortMatch.Groups[1].Value);
                            node.clashProxyPorts.Add(new ClashProxyPort
                            {
                                name = "SOCKS代理",
                                type = "socks",
                                port = port,
                                proxy = "全局",
                                isActive = false
                            });
                            LogHandler.AddLog($"添加SOCKS代理端口: {port}");
                        }

                        if (mixedPortMatch.Success && mixedPortMatch.Groups.Count > 1)
                        {
                            int port = int.Parse(mixedPortMatch.Groups[1].Value);
                            node.clashProxyPorts.Add(new ClashProxyPort
                            {
                                name = "混合代理",
                                type = "mixed",
                                port = port,
                                proxy = "全局",
                                isActive = false
                            });
                            LogHandler.AddLog($"添加混合代理端口: {port}");
                        }
                    }

                    // 设置preSocksPort为第一个端口（如果存在）
                    if (node.clashProxyPorts.Count > 0)
                    {
                        node.preSocksPort = node.clashProxyPorts[0].port;
                    }

                    // 直接复制原文件，保留所有配置
                    File.Copy(source, dest, true);
                }

                // 显示配置的端口信息
                if (node.clashProxyPorts.Count > 0)
                {
                    LogHandler.AddLog($"配置了 {node.clashProxyPorts.Count} 个监听端口");
                }
                else
                {
                    LogHandler.AddLog("警告：未检测到监听端口配置");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"修改Clash配置文件失败: {ex.Message}");
                // 出错时复制原文件
                File.Copy(source, dest, true);
            }
        }

        public void CoreStartViaConfig(string coreExe, string configPath, ProfileItem node)
        {
            try
            {
                if (!File.Exists(coreExe))
                {
                    LogHandler.AddLog($"未找到核心文件: {coreExe}");
                    if (_updateFunc != null)
                        _updateFunc(false, $"未找到核心文件: {coreExe}");
                    return;
                }

                LogHandler.AddLog($"开始启动服务: {node.remarks}");
                LogHandler.AddLog($"使用核心: {coreExe}");
                LogHandler.AddLog($"配置文件: {configPath}");

                // 保存当前服务器的信息
                _currentCoreExes[node.indexId] = Path.GetFileName(coreExe);
                _currentNodes[node.indexId] = node;
                try
                {
                    _configStrings[node.indexId] = File.ReadAllText(configPath);
                }
                catch
                {
                    _configStrings[node.indexId] = string.Empty;
                }

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = coreExe,
                        Arguments = $"-c \"{configPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(coreExe),
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.OutputDataReceived += (s, e) => Process_OutputDataReceived(s, e, node.indexId);
                process.ErrorDataReceived += (s, e) => Process_ErrorDataReceived(s, e, node.indexId);

                _processes[node.indexId] = process;
                _additionalProcessIds[node.indexId] = new List<int>();

                bool started = process.Start();

                if (started)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    LogHandler.AddLog($"服务已启动，进程ID: {process.Id}");

                    if (_updateFunc != null)
                        _updateFunc(true, $"服务启动成功: {node.remarks}");
                    else
                        RaiseOutputDataReceived($"服务启动成功: {node.remarks}");

                    WaitForPortListening(node.preSocksPort);
                }
                else
                {
                    LogHandler.AddLog("服务启动失败");
                    if (_updateFunc != null)
                        _updateFunc(false, "服务启动失败");
                    else
                        RaiseOutputDataReceived("服务启动失败");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"启动服务异常: {ex}");
                if (_updateFunc != null)
                    _updateFunc(false, $"启动服务异常: {ex.Message}");
                else
                    RaiseOutputDataReceived($"启动服务异常: {ex.Message}");
            }
        }

        public void CoreStartViaString(string coreExe, string arguments, ProfileItem node)
        {
            try
            {
                // 检查核心文件是否存在
                if (!File.Exists(coreExe))
                {
                    LogHandler.AddLog($"未找到核心文件: {coreExe}");
                    if (_updateFunc != null)
                        _updateFunc(false, $"未找到核心文件: {coreExe}");
                    return;
                }

                LogHandler.AddLog($"开始启动服务: {node.remarks}");
                LogHandler.AddLog($"使用核心: {coreExe}");
                LogHandler.AddLog($"参数: {arguments}");

                // 保存当前使用的核心可执行文件名
                _currentCoreExes[node.indexId] = Path.GetFileName(coreExe);
                _currentNodes[node.indexId] = node;
                _configStrings[node.indexId] = arguments;

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = coreExe,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(coreExe),
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.OutputDataReceived += (s, e) => Process_ClashOutputDataReceived(s, e, node.indexId);
                process.ErrorDataReceived += (s, e) => Process_ErrorDataReceived(s, e, node.indexId);

                _processes[node.indexId] = process;
                _additionalProcessIds[node.indexId] = new List<int>();

                bool started = process.Start();

                if (started)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    LogHandler.AddLog($"服务已启动，进程ID: {process.Id}");

                    if (_updateFunc != null)
                        _updateFunc(true, $"服务启动成功: {node.remarks}");
                    else
                        RaiseOutputDataReceived($"服务启动成功: {node.remarks}");

                    // 异步检查各端口监听
                    foreach (var port in node.clashProxyPorts)
                    {
                        WaitForPortListening(port.port);
                    }
                }
                else
                {
                    LogHandler.AddLog("服务启动失败");
                    if (_updateFunc != null)
                        _updateFunc(false, "服务启动失败");
                    else
                        RaiseOutputDataReceived("服务启动失败");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"启动服务异常: {ex}");
                if (_updateFunc != null)
                    _updateFunc(false, $"启动服务异常: {ex.Message}");
                else
                    RaiseOutputDataReceived($"启动服务异常: {ex.Message}");
            }
        }

        private void Process_ClashOutputDataReceived(object sender, DataReceivedEventArgs e, string serverId)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (_currentCoreExes.TryGetValue(serverId, out string coreExe))
                {
                    string log = $"[{Path.GetFileNameWithoutExtension(coreExe)}] {e.Data}";
                    LogHandler.AddLog(log);
                    RaiseOutputDataReceived(log);

                    // 检查端口是否在监听
                    if (e.Data.Contains("listening"))
                    {
                        if (_currentNodes.TryGetValue(serverId, out ProfileItem node))
                        {
                            // 尝试提取端口号
                            try
                            {
                                // 匹配类似 "proxy listening at: [::]:42012" 的消息
                                var match = Regex.Match(e.Data, @"listening.*:(\d+)");
                                if (match.Success && match.Groups.Count > 1)
                                {
                                    int port = int.Parse(match.Groups[1].Value);
                                    node.port = port;
                                    node.isRunning = true;
                                    LogHandler.AddLog($"端口 {port} 已激活");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"提取端口信息时出错: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog(ex.ToString());
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e, string serverId)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (_currentCoreExes.TryGetValue(serverId, out string coreExe))
                {
                    string log = $"[{Path.GetFileNameWithoutExtension(coreExe)}] {e.Data}";
                    LogHandler.AddLog(log);
                    RaiseOutputDataReceived(log);
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog(ex.ToString());
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e, string serverId)
        {
            try
            {
                if (string.IsNullOrEmpty(e.Data)) return;

                if (_currentCoreExes.TryGetValue(serverId, out string coreExe))
                {
                    string log = $"[{Path.GetFileNameWithoutExtension(coreExe)}] [Error] {e.Data}";
                    LogHandler.AddLog(log);
                    RaiseOutputDataReceived(log);
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog(ex.ToString());
            }
        }

        private void WaitForPortListening(int port)
        {
           
            lock (_portCheckLock)
            {
                if (_portCheckingStatus.ContainsKey(port) && _portCheckingStatus[port])
                {
                    LogHandler.AddLog($"端口 {port} 已在监控中，跳过重复监控");
                    return;
                }
                _portCheckingStatus[port] = true;
            }

            try
            {
               
                Task.Run(async () =>
                {
                    try
                    {
                        LogHandler.AddLog($"正在监控端口 {port} 启动状态...");

                       
                        int maxRetries = 10;
                        bool isActive = false;

                        for (int i = 0; i < maxRetries; i++)
                        {
                            try
                            {
                                using (var client = new TcpClient())
                                {
                                    
                                    var connectTask = client.ConnectAsync("127.0.0.1", port);
                                   
                                    if (await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask)
                                    {
                                        isActive = client.Connected;

                                        if (isActive)
                                        {
                                            LogHandler.AddLog($"端口 {port} 已激活");
                                            break;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // 连接失败，端口可能还未就绪
                            }

                            // 等待1秒再尝试
                            await Task.Delay(1000);
                        }

                        if (isActive)
                        {
                           
                            Config config = new Config();
                            ConfigHandler.LoadConfig(ref config);

                            if (config.profileItems != null && !string.IsNullOrEmpty(config.indexId))
                            {
                                var server = config.profileItems.FirstOrDefault(p => p.indexId == config.indexId);
                                if (server != null)
                                {
                                    // 找到对应的端口并标记为活跃
                                    var portItem = server.clashProxyPorts.FirstOrDefault(p => p.port == port);
                                    if (portItem != null)
                                    {
                                        portItem.isActive = true;
                                        LogHandler.AddLog($"已将端口 {port} 标记为活跃状态");
                                    }
                                    // 标记服务器为运行状态
                                    server.isRunning = true;
                                    ConfigHandler.SaveConfig(ref config);

                                    // 主动更新UI状态
                                    _updateFunc?.Invoke(true, $"服务已成功启动，端口 {port} 已激活");
                                }
                            }
                        }
                        else
                        {
                            LogHandler.AddLog($"端口 {port} 启动超时，可能存在问题");
                        }
                    }
                    finally
                    {
                        // 完成检测后，无论成功失败，都重置状态标志
                        lock (_portCheckLock)
                        {
                            _portCheckingStatus[port] = false;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"监控端口 {port} 时出错: {ex.Message}");
                // 出错时也要重置状态标志
                lock (_portCheckLock)
                {
                    _portCheckingStatus[port] = false;
                }
            }
        }

        public void CoreStop()
        {
            try
            {
                LogHandler.AddLog("正在停止所有服务...");

                // 清空端口检测状态
                lock (_portCheckLock)
                {
                    _portCheckingStatus.Clear();
                }

                // 停止所有服务器进程
                var serverIds = _processes.Keys.ToList();
                foreach (var serverId in serverIds)
                {
                    StopServer(serverId);
                }

                // 更新配置
                try
                {
                    var config = new Config();
                    ConfigHandler.LoadConfig(ref config);

                    if (config.profileItems != null)
                    {
                        bool updated = false;
                        foreach (var item in config.profileItems)
                        {
                            if (item.isRunning)
                            {
                                item.isRunning = false;
                                updated = true;
                            }

                            foreach (var port in item.clashProxyPorts)
                            {
                                port.isActive = false;
                            }
                        }

                        if (updated)
                        {
                            ConfigHandler.SaveConfig(ref config);
                            LogHandler.AddLog("已更新所有服务器的运行状态");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHandler.AddLog($"更新服务器状态时出错: {ex.Message}");
                }

                if (_updateFunc != null)
                    _updateFunc(true, "所有服务已停止");
                else
                    RaiseOutputDataReceived("所有服务已停止");
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"停止服务时出错：{ex.Message}");
                if (_updateFunc != null)
                    _updateFunc(false, $"停止服务时出错：{ex.Message}");
                else
                    RaiseOutputDataReceived($"停止服务时出错：{ex.Message}");
            }
        }

        // 添加新方法用于停止特定服务器
        public void StopServer(string serverId)
        {
            try
            {
                if (_processes.TryGetValue(serverId, out Process process))
                {
                    LogHandler.AddLog($"正在停止服务器 {serverId}...");

                    // 停止主进程
                    if (!process.HasExited)
                    {
                        try
                        {
                            LogHandler.AddLog($"正在停止进程: PID={process.Id}");
                            process.Kill();

                            if (!process.WaitForExit(3000))
                            {
                                LogHandler.AddLog("进程未能在3秒内退出，将强制终止");
                            }

                            process.Close();
                            process.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LogHandler.AddLog($"停止进程时出错: {ex.Message}");
                        }
                    }

                    // 停止附加进程
                    if (_additionalProcessIds.TryGetValue(serverId, out List<int> additionalPids))
                    {
                        foreach (int pid in additionalPids)
                        {
                            try
                            {
                                Process p = Process.GetProcessById(pid);
                                p.Kill();
                                p.WaitForExit(1000);
                            }
                            catch (Exception ex)
                            {
                                LogHandler.AddLog($"停止附加进程失败(PID: {pid}): {ex.Message}");
                            }
                        }
                        _additionalProcessIds.Remove(serverId);
                    }

                    // 清理相关资源
                    _processes.Remove(serverId);
                    _currentCoreExes.Remove(serverId);
                    _currentNodes.Remove(serverId);
                    _configStrings.Remove(serverId);

                    LogHandler.AddLog($"服务器 {serverId} 已停止");
                }
            }
            catch (Exception ex)
            {
                LogHandler.AddLog($"停止服务器 {serverId} 时出错: {ex.Message}");
            }
        }

        private bool IsPortAvailable(int port)
        {
            try
            {
                var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
                var tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

                if (tcpConnInfoArray.Any(endpoint => endpoint.Port == port))
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GenerateSocksConfig(ProfileItem node, string configPath)
        {
            var inbounds = new[] {
                new {
                    tag = "socks-in",
                    port = node.preSocksPort,
                    protocol = "socks",
                    settings = new {
                        udp = true,
                        auth = "noauth",
                        userLevel = 8
                    },
                    sniffing = new {
                        enabled = true,
                        destOverride = new[] { "http", "tls" }
                    },
                    listen = "127.0.0.1"
                }
            };

            var outbounds = new[] {
                new {
                    tag = "proxy",
                    protocol = "freedom",
                    settings = new {},
                    streamSettings = new {
                        sockopt = new {
                            mark = 255
                        }
                    },
                    proxySettings = new {
                        tag = "direct"
                    }
                }
            };

            var routing = new
            {
                domainStrategy = "AsIs",
                rules = new[] {
                    new {
                        type = "field",
                        inboundTag = new[] { "socks-in" },
                        outboundTag = "proxy"
                    }
                }
            };

            // 构建配置并将其序列化为JSON字符串
            var config = new
            {
                log = new
                {
                    access = Path.Combine(_logPath, "access.log"),
                    error = Path.Combine(_logPath, "error.log"),
                    loglevel = "warning"
                },
                inbounds,
                outbounds,
                routing,
                others = new
                {
                    v2raya_extras = new
                    {
                        original_config = configPath
                    }
                }
            };

            return JsonConvert.SerializeObject(config, Formatting.Indented);
        }

        private string? DetermineCoreExe(string coreType)
        {
            if (string.IsNullOrEmpty(coreType))
            {
                LogHandler.AddLog("未指定核心类型，默认使用xray");
                coreType = "xray";
            }

            // 根据coreType确定可执行文件
            string exeName = coreType.ToLower();

            // 获取可能的可执行文件名称列表
            List<string> possibleExeNames = new List<string>();

            switch (exeName)
            {
                case "v2fly":
                    possibleExeNames.Add("v2ray.exe");
                    possibleExeNames.Add("wv2ray.exe");
                    break;

                case "xray":
                    possibleExeNames.Add("xray.exe");
                    possibleExeNames.Add("wxray.exe");
                    break;

                case "clash_meta":
                    // 按照v2rayN的匹配顺序
                    possibleExeNames.Add("Clash.Meta-windows-amd64-compatible.exe");
                    possibleExeNames.Add("Clash.Meta-windows-amd64.exe");
                    possibleExeNames.Add("Clash.Meta-windows-386.exe");
                    possibleExeNames.Add("Clash.Meta.exe");
                    possibleExeNames.Add("clash-meta.exe");  // 我们自己添加的名称
                    possibleExeNames.Add("clash.exe");  // 兼容方案，最后尝试
                    break;

                case "clash":
                    possibleExeNames.Add("clash-windows-amd64-v3.exe");
                    possibleExeNames.Add("clash-windows-amd64.exe");
                    possibleExeNames.Add("clash-windows-386.exe");
                    possibleExeNames.Add("clash.exe");
                    break;

                default:
                    // 默认只添加与coreType同名的exe
                    possibleExeNames.Add($"{exeName}.exe");
                    break;
            }

            // 尝试查找所有可能的可执行文件
            foreach (string exeFileName in possibleExeNames)
            {
                string exePath = Utils.GetBinPath(exeFileName);
                if (File.Exists(exePath))
                {
                    LogHandler.AddLog($"找到核心文件: {exePath}");
                    return exePath;
                }
            }

            // 没有找到任何匹配的可执行文件
            LogHandler.AddLog($"找不到核心可执行文件: {string.Join(", ", possibleExeNames)}");
            LogHandler.AddLog($"请下载并放置合适的核心文件到: {Utils.GetBinPath()}");
            return null;
        }

        private void ShowMsg(bool updateToTrayTooltip, string msg)
        {
            // 先触发UI事件
            RaiseOutputDataReceived(msg);

            // 兼容旧的回调方式
            _updateFunc?.Invoke(updateToTrayTooltip, msg);

            // 写入日志文件，但不触发事件
            try
            {
                string formattedLog = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")} - {msg}";
                string logFile = Path.Combine(Utils.GetLogPath(), $"SimpleV2ray_{DateTime.Now.ToString("yyyyMMdd")}.log");
                File.AppendAllText(logFile, formattedLog + Environment.NewLine);
            }
            catch
            {
                // 忽略日志写入错误
            }
        }
    }
}