using web3script.Services;
using web3script.ucontrols;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Net;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;
using web3script.Models;
using Task = System.Threading.Tasks.Task;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;

namespace web3script.Services
{
    public class BalanceQueryService
    {
        private readonly ChainBalanceQuery _chainQuery = new ChainBalanceQuery();
        WalletService walletService = new WalletService();
        
        // 余额缓存文件路径
        private const string BALANCE_CACHE_FILE = "balance_cache.json";
        
        // 余额缓存数据结构
        private class BalanceCache
        {
            public Dictionary<string, Dictionary<string, string>> WalletBalances { get; set; } = new Dictionary<string, Dictionary<string, string>>();
            public DateTime LastUpdated { get; set; } = DateTime.Now;
        }
        
        // 余额缓存实例
        private BalanceCache _balanceCache;
        
        public BalanceQueryService()
        {
            // 初始化时加载缓存
            LoadBalanceCache();
        }
        
        /// <summary>
        /// 加载余额缓存
        /// </summary>
        private void LoadBalanceCache()
        {
            try
            {
                if (File.Exists(BALANCE_CACHE_FILE))
                {
                    string json = File.ReadAllText(BALANCE_CACHE_FILE);
                    _balanceCache = JsonConvert.DeserializeObject<BalanceCache>(json) ?? new BalanceCache();
                    Debug.WriteLine($"已加载余额缓存，上次更新时间: {_balanceCache.LastUpdated}，包含 {_balanceCache.WalletBalances.Count} 个钱包数据");
                }
                else
                {
                    _balanceCache = new BalanceCache();
                    Debug.WriteLine("未找到余额缓存，将创建新的缓存");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载余额缓存失败: {ex.Message}");
                _balanceCache = new BalanceCache();
            }
        }
        
        /// <summary>
        /// 保存余额缓存
        /// </summary>
        private void SaveBalanceCache()
        {
            try
            {
                _balanceCache.LastUpdated = DateTime.Now;
                string json = JsonConvert.SerializeObject(_balanceCache, Formatting.Indented);
                File.WriteAllText(BALANCE_CACHE_FILE, json);
                Debug.WriteLine($"已保存余额缓存，更新时间: {_balanceCache.LastUpdated}，包含 {_balanceCache.WalletBalances.Count} 个钱包数据");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存余额缓存失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 缓存指定钱包和代币的余额
        /// </summary>
        private void CacheBalance(string walletAddress, string tokenType, string balance)
        {
            if (_balanceCache == null)
                _balanceCache = new BalanceCache();
                
            // 确保钱包地址存在于缓存中
            if (!_balanceCache.WalletBalances.ContainsKey(walletAddress))
                _balanceCache.WalletBalances[walletAddress] = new Dictionary<string, string>();
                
            // 更新指定代币的余额
            _balanceCache.WalletBalances[walletAddress][tokenType] = balance;
        }
        
        /// <summary>
        /// 获取指定钱包和代币的缓存余额
        /// </summary>
        /// <returns>如果缓存存在则返回缓存余额，否则返回null</returns>
        private string GetCachedBalance(string walletAddress, string tokenType)
        {
            if (_balanceCache == null)
                return null;
                
            if (_balanceCache.WalletBalances.TryGetValue(walletAddress, out var tokens))
            {
                if (tokens.TryGetValue(tokenType, out var balance))
                    return balance;
            }
            
            return null;
        }
        
        /// <summary>
        /// 加载缓存的余额数据到结果集合中
        /// </summary>
        public void LoadCachedBalances(string projectName, string groupName, string balanceType, ObservableCollection<WalletBalanceInfo> results)
        {
            results.Clear();
            
            string groupId = null;
            if (!string.IsNullOrEmpty(groupName))
            {
                var group = walletService.WalletGroups.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                    groupId = group.Id;
            }
            
            List<web3script.Models.Wallet> wallets = new List<web3script.Models.Wallet>();
            if (!string.IsNullOrEmpty(groupId))
                wallets = walletService.GetWalletsInGroup(groupId);
            
            if (wallets.Count == 0)
                return;
                
            foreach (var wallet in wallets)
            {
                string balance = GetCachedBalance(wallet.Address, balanceType);
                
                // 如果没有缓存余额，显示为等待刷新
                if (balance == null)
                    balance = "等待刷新";
                    
                results.Add(new WalletBalanceInfo
                {
                    WalletAddress = wallet.Address,
                    Balance = balance,
                    TokenType = balanceType,
                    PrivateKey = wallet.PrivateKey,
                    Mnemonic = wallet.Mnemonic
                });
            }
            
            Debug.WriteLine($"已从缓存加载 {results.Count} 个钱包的 {balanceType} 余额");
        }
        
        /// <summary>
        /// 检查代理是否可用
        /// </summary>
        /// <param name="proxy">要检查的代理</param>
        /// <returns>代理是否可用</returns>
        private async Task<bool> IsProxyAvailable(ProxyViewModel proxy)
        {
            try
            {
                // 创建HttpClientHandler并配置代理
                var handler = sHttpHandler.GetHttpClientHandler(proxy);
                using (var client = new HttpClient(handler))
                {
                    // 设置10秒超时，避免长时间等待不可用的代理
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    // 尝试访问一个可靠的网站
                    var response = await client.GetAsync("https://www.google.com");
            //        Debug.WriteLine($"代理 {proxy.ServerAddress}:{proxy.Port} 测试结果: {response.StatusCode}");
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
             //   Debug.WriteLine($"代理 {proxy.ServerAddress}:{proxy.Port} 测试失败: {ex.Message}");
                return false;
            }
        }
        
        private List<ProxyViewModel> GetAllAvailableProxies()
        {
            var proxyFile = "proxy_config.json";
            if (!System.IO.File.Exists(proxyFile)) return new List<ProxyViewModel>();
            var json = System.IO.File.ReadAllText(proxyFile);
            var proxies = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ProxyViewModel>>(json) ?? new List<ProxyViewModel>();
            
            // 返回所有代理，不再过滤状态
         //   Debug.WriteLine($"找到 {proxies.Count} 个代理");
            return proxies;
        }
        
        // 根据项目、分组、币种查询余额
        public async Task QueryBalancesAsync(string projectName, string groupName, string balanceType, ObservableCollection<WalletBalanceInfo> results)
        {
            var availableProxies = GetAllAvailableProxies();
            
            string groupId = null;
            if (!string.IsNullOrEmpty(groupName))
            {
                var group = walletService.WalletGroups.FirstOrDefault(g => g.Name == groupName);
                if (group != null)
                    groupId = group.Id;
            }
            
            List<web3script.Models.Wallet> wallets = new List<web3script.Models.Wallet>();
            if (!string.IsNullOrEmpty(groupId))
                wallets = walletService.GetWalletsInGroup(groupId);
            
            if (wallets.Count == 0)
                return;

            // 清空当前结果集
            results.Clear();

            if (availableProxies.Count == 0) 
            {
             //   Debug.WriteLine("没有可用的代理，将使用直连方式查询余额");
                await QueryBalancesDirectly(wallets, balanceType, results);
            }
            else
            {
             //   Debug.WriteLine($"使用 {availableProxies.Count} 个代理进行余额查询");
                await QueryBalancesWithProxies(wallets, balanceType, availableProxies, results);
            }
            
            // 查询完成后保存缓存
            SaveBalanceCache();
        }
        
        // 不使用代理直接查询余额 - 边查询边返回结果
        private async Task QueryBalancesDirectly(List<web3script.Models.Wallet> wallets, string balanceType, ObservableCollection<WalletBalanceInfo> results)
        {
            
          
            foreach (var wallet in wallets)
            {
                try
                {
                    decimal balance = 0;
                    
                    // 检查是否为ERC20代币格式 (chain.token)
                    if (balanceType.Contains('.'))
                    {
                        string[] parts = balanceType.Split('.');
                        string chain = parts[0];
                        string token = parts[1];
                        
                        // 获取代币合约和精度
                        var coinData = ChainConfigService.GetCoinTypeData(balanceType);
                        string contract = coinData?["contract"]?.ToString();
                        int decimals = coinData?["decimals"]?.Value<int>() ?? 18;
                        
                        if (!string.IsNullOrEmpty(contract))
                        {
                            balance = await _chainQuery.GetErc20Balance(chain, wallet.Address, contract, decimals);
                        }
                    }
                    else if (ChainConfigService.IsChainSupported(balanceType))
                    {
                        // 查询原生代币余额
                        balance = await _chainQuery.GetNativeBalance(balanceType, wallet.Address);
                    }
                    else
                    {
                        Debug.WriteLine($"不支持的链或格式: {balanceType}");
                        balance = 0;
                    }
                    
                    // 格式化余额并缓存
                    string formattedBalance = FormatBalance(balance);
                    CacheBalance(wallet.Address, balanceType, formattedBalance);
                    
                    // 在UI线程上更新结果集合
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        results.Add(new WalletBalanceInfo
                        {
                            WalletAddress = wallet.Address,
                            Balance = formattedBalance,
                            TokenType = balanceType,
                            PrivateKey = wallet.PrivateKey,
                            Mnemonic = wallet.Mnemonic
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"查询钱包 {wallet.Address} 余额失败: {ex.Message}");
                    
                    // 在UI线程上添加错误信息
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        results.Add(new WalletBalanceInfo
                        {
                            WalletAddress = wallet.Address,
                            Balance = "查询失败",
                            TokenType = balanceType,
                            PrivateKey = wallet.PrivateKey,
                            Mnemonic = wallet.Mnemonic
                        });
                    });
                    
                    // 缓存错误状态
                    CacheBalance(wallet.Address, balanceType, "查询失败");
                }
            }
             
           
        }

        // 使用多线程和代理查询余额 - 边查询边返回结果
        //private async Task QueryBalancesWithProxies(List<web3script.Models.Wallet> wallets, string balanceType, List<ProxyViewModel> proxies, ObservableCollection<WalletBalanceInfo> results)
        //{
        //    int proxyCount = proxies.Count;

        //    // 使用信号量限制并发请求数量
        //    int maxConcurrency = Math.Min(proxyCount, 50); // 最多同时使用2个代理
        //    Debug.WriteLine($"-----------------------------------------------------------------使用 {maxConcurrency} 个代理进行并发查询----------------------------------------------------------------");
        //    var semaphore = new SemaphoreSlim(maxConcurrency);

        //    // 记录已测试过的代理状态，避免重复测试
        //    var testedProxies = new ConcurrentDictionary<string, bool>();

        //    // 跟踪正在使用的代理
        //    var inUseProxies = new ConcurrentDictionary<string, byte>();

        //    // 用于线程安全地获取下一个可用代理的锁对象
        //    var proxyLock = new object();

        //    // 对每个钱包创建一个单独的任务
        //    foreach (var wallet in wallets)
        //    {
        //        // 等待获取信号量，限制并发数量
        //        await semaphore.WaitAsync();

        //        ProxyViewModel selectedProxy = null;
        //        string proxyKey = "";

        //        try
        //        {
        //            // 尝试获取一个未被其他线程使用的代理
        //            for (int attempt = 0; attempt < 3; attempt++)
        //            {
        //                lock (proxyLock)
        //                {
        //                    // 找到一个没有被使用的代理
        //                    for (int i = 0; i < proxyCount; i++)
        //                    {
        //                        var proxy = proxies[i];
        //                        proxyKey = $"{proxy.ServerAddress}:{proxy.Port}";

        //                        // 如果代理未被使用，标记为使用中并返回
        //                        if (!inUseProxies.ContainsKey(proxyKey))
        //                        {
        //                            inUseProxies[proxyKey] = 1;
        //                            selectedProxy = proxy;
        //                            Debug.WriteLine($"钱包 {wallet.Address} 分配到代理: {proxyKey}");
        //                            break;
        //                        }
        //                    }

        //                    // 如果所有代理都在使用中，等待一小段时间后重试
        //                    if (selectedProxy == null && attempt < 2)
        //                    {
        //                        Debug.WriteLine($"钱包 {wallet.Address} 没有可用的未被使用的代理，等待后重试");
        //                    }
        //                }

        //                if (selectedProxy != null)
        //                    break;

        //                // 如果所有代理都在使用中，等待一小段时间后重试
        //                await Task.Delay(500); // 等待500毫秒
        //            }

        //            // 如果无法获取到未使用的代理，随机选择一个
        //            if (selectedProxy == null)
        //            {
        //                lock (proxyLock)
        //                {
        //                    int randomIndex = new Random().Next(proxyCount);
        //                    selectedProxy = proxies[randomIndex];
        //                    proxyKey = $"{selectedProxy.ServerAddress}:{selectedProxy.Port}";
        //                    inUseProxies[proxyKey] = 1;
        //                    Debug.WriteLine($"钱包 {wallet.Address} 没有可用的未使用代理，随机选择代理: {proxyKey}");
        //                }
        //            }

        //            // 测试选中的代理
        //            bool isAvailable = false;
        //            if (!testedProxies.TryGetValue(proxyKey, out isAvailable))
        //            {
        //                isAvailable = await IsProxyAvailable(selectedProxy);
        //                testedProxies.TryAdd(proxyKey, isAvailable);
        //            }

        //            decimal balance = 0;

        //            if (isAvailable)
        //            {
        //                // 使用可用的代理查询余额
        //                balance = await QueryBalanceWithProxy(wallet.Address, balanceType, selectedProxy);
        //            }
        //            else
        //            {
        //                // 如果代理不可用，尝试直连
        //                Debug.WriteLine($"钱包 {wallet.Address} 的代理 {proxyKey} 不可用，使用直连");
        //                balance = await QueryBalanceWithProxy(wallet.Address, balanceType, null);
        //            }

        //            // 在UI线程上更新结果
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                results.Add(new WalletBalanceInfo
        //                {
        //                    WalletAddress = wallet.Address,
        //                    Balance = FormatBalance(balance),
        //                    TokenType = balanceType,
        //                    PrivateKey = wallet.PrivateKey,
        //                    Mnemonic = wallet.Mnemonic
        //                });
        //            });
        //        }
        //        catch (Exception ex)
        //        {
        //            Debug.WriteLine($"查询钱包 {wallet.Address} 余额失败: {ex.Message}");

        //            // 在UI线程上添加错误信息
        //            Application.Current.Dispatcher.Invoke(() =>
        //            {
        //                results.Add(new WalletBalanceInfo
        //                {
        //                    WalletAddress = wallet.Address,
        //                    Balance = "查询失败",
        //                    TokenType = balanceType,
        //                    PrivateKey = wallet.PrivateKey,
        //                    Mnemonic = wallet.Mnemonic
        //                });
        //            });
        //        }
        //        finally
        //        {
        //            // 释放代理使用标记
        //            if (!string.IsNullOrEmpty(proxyKey))
        //            {
        //                byte unused;
        //                inUseProxies.TryRemove(proxyKey, out unused);
        //                Debug.WriteLine($"钱包 {wallet.Address} 释放代理: {proxyKey}");
        //            }

        //            // 释放信号量
        //            semaphore.Release();
        //        }

        //    }

        //    // 等待所有钱包处理完成
        //    // 这样等待的方式会使主线程等待，但各个子任务已经开始独立运行并更新UI
        //    await Task.Delay(500); // 给任务启动一些时间

        //    while (semaphore.CurrentCount < maxConcurrency)
        //    {
        //        // 每500毫秒检查一次是否所有任务都完成了
        //        await Task.Delay(500);
        //    }

        //    // 确保所有任务都有充分的时间完成
        //    await Task.Delay(1000);
        //}
        private async Task QueryBalancesWithProxies(List<web3script.Models.Wallet> wallets, string balanceType, List<ProxyViewModel> proxies, ObservableCollection<WalletBalanceInfo> results)
        {
            int maxConcurrency = Math.Min(proxies.Count, 50);
            var testedProxies = new ConcurrentDictionary<string, bool>();
            var rand = new Random();

            for (int i = 0; i < wallets.Count; i += maxConcurrency)
            {
                var batch = wallets.Skip(i).Take(maxConcurrency).ToList();
                var batchResults = new ConcurrentBag<WalletBalanceInfo>();
                var tasks = new List<Task>();

                var inUseProxies = new ConcurrentDictionary<string, byte>();
                var proxyLock = new object();

                foreach (var wallet in batch)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        ProxyViewModel selectedProxy = null;
                        string proxyKey = "";

                        try
                        {
                            for (int attempt = 0; attempt < 3; attempt++)
                            {
                                lock (proxyLock)
                                {
                                    for (int j = 0; j < proxies.Count; j++)
                                    {
                                        var proxy = proxies[j];
                                        proxyKey = $"{proxy.ServerAddress}:{proxy.Port}";

                                        if (!inUseProxies.ContainsKey(proxyKey))
                                        {
                                            inUseProxies[proxyKey] = 1;
                                            selectedProxy = proxy;
                                            break;
                                        }
                                    }
                                }

                                if (selectedProxy != null) break;
                                await Task.Delay(300);
                            }

                            if (selectedProxy == null)
                            {
                                lock (proxyLock)
                                {
                                    selectedProxy = proxies[rand.Next(proxies.Count)];
                                    proxyKey = $"{selectedProxy.ServerAddress}:{selectedProxy.Port}";
                                    inUseProxies[proxyKey] = 1;
                                }
                            }

                            // 测试选中的代理
                            bool isAvailable = false;
                            if (!testedProxies.TryGetValue(proxyKey, out isAvailable))
                            {
                                isAvailable = await IsProxyAvailable(selectedProxy);
                                testedProxies.TryAdd(proxyKey, isAvailable);
                            }

                            decimal balance = 0;

                            if (isAvailable)
                            {
                                // 使用可用的代理查询余额
                                balance = await QueryBalanceWithProxy(wallet.Address, balanceType, selectedProxy);
                            }
                            else 
                            { 
                                balance = await QueryBalanceWithProxy(wallet.Address, balanceType, null); 
                            }
                              
                            // 格式化余额并缓存
                            string formattedBalance = FormatBalance(balance);
                            CacheBalance(wallet.Address, balanceType, formattedBalance);

                            batchResults.Add(new WalletBalanceInfo
                            {
                                WalletAddress = wallet.Address,
                                Balance = formattedBalance,
                                TokenType = balanceType,
                                PrivateKey = wallet.PrivateKey,
                                Mnemonic = wallet.Mnemonic
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"钱包 {wallet.Address} 查询失败: {ex.Message}");
                            
                            // 缓存错误状态
                            CacheBalance(wallet.Address, balanceType, "查询失败");
                            
                            batchResults.Add(new WalletBalanceInfo
                            {
                                WalletAddress = wallet.Address,
                                Balance = "查询失败",
                                TokenType = balanceType,
                                PrivateKey = wallet.PrivateKey,
                                Mnemonic = wallet.Mnemonic
                            });
                        }
                        finally
                        {
                            if (!string.IsNullOrEmpty(proxyKey))
                                inUseProxies.TryRemove(proxyKey, out _);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // ✅ 当前批次查询完成后一次性更新 UI
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var result in batchResults)
                        results.Add(result);
                });

                Debug.WriteLine($"--- 批次 {i / maxConcurrency + 1} 查询完成，已更新 {batchResults.Count} 个钱包余额 ---");
            }
        }


        // 使用指定代理（或直连）查询余额
        private async Task<decimal> QueryBalanceWithProxy(string walletAddress, string balanceType, ProxyViewModel proxy)
        {
            try
            {
                // 检查是否为ERC20代币格式 (chain.token)
                if (balanceType.Contains('.'))
                {
                    string[] parts = balanceType.Split('.');
                    string chain = parts[0];
                    string token = parts[1];
                    
                    // 获取代币合约和精度
                    var coinData = ChainConfigService.GetCoinTypeData(balanceType);
                    string contract = coinData?["contract"]?.ToString();
                    int decimals = coinData?["decimals"]?.Value<int>() ?? 18;
                    
                    if (!string.IsNullOrEmpty(contract))
                    {
                        // 使用代理（如果有）查询ERC20余额
                        return await _chainQuery.GetErc20Balance(chain, walletAddress, contract, decimals, proxy);
                    }
                }
                else if (ChainConfigService.IsChainSupported(balanceType))
                {
                    // 查询原生代币余额，传入代理参数（如果有）
                    return await _chainQuery.GetNativeBalance(balanceType, walletAddress, proxy);
                }
                
                Debug.WriteLine($"不支持的链或格式: {balanceType}");
                return 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"查询余额时出现异常: {ex.Message}");
                throw; // 向上层抛出异常，由调用者处理
            }
        }
        
        // 批量查询ERC20代币余额
        public async Task QueryErc20BalancesAsync(string chain, string contractAddress, int decimals, List<string> addresses, Dictionary<string, decimal> results)
        {
            try
            {
                var balances = await _chainQuery.GetErc20BalancesMultiple(chain, addresses, contractAddress, decimals);
                foreach (var pair in balances)
                {
                    results[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"批量查询ERC20代币余额失败: {ex.Message}");
                throw;
            }
        }
        
        // 格式化余额显示
        private string FormatBalance(decimal balance)
        {
            // 如果余额为0，简单显示0
            if (balance == 0)
                return "0";
                
            // 对于非常小的数字（小于0.00001），使用科学计数法
            if (balance > 0 && balance < 0.00001m)
                return balance.ToString("E4");
                
            // 对于正常范围的数字，最多显示8位小数
            return balance.ToString("0.########");
        }
    }
}

//public class WalletBalanceInfo
//{
//    public string WalletAddress { get; set; }
//    public string Balance { get; set; }
//    public string TokenType { get; set; }
//}
