using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using web3script.Services;
using Nethereum.Web3;
using Nethereum.JsonRpc.Client;
using Nethereum.Hex.HexTypes;
using Nethereum.Contracts;
using web3script.ucontrols;
using System.Numerics;
using System.Text;
using System.Diagnostics;
using System.Net;

namespace web3script.Services
{
    public class ChainBalanceQuery
    {
        // 获取原生代币余额
        public async Task<decimal> GetNativeBalance(string chain, string address, ProxyViewModel proxy = null)
        { 
            return await GetNativeBalanceWithProxy(chain, address, proxy);
        }

        // 使用代理获取原生代币余额
        public async Task<decimal> GetNativeBalanceWithProxy(string chain, string address, ProxyViewModel proxy = null, int retry = 3)
        {
            if (!ChainConfigService.IsChainSupported(chain))
                throw new ArgumentException($"不支持的链: {chain}");
                
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("钱包地址不能为空");
            
            // 使用ChainConfigService获取RPC URL
            string rpcUrl;
            try {
                rpcUrl = ChainConfigService.GetChainRpcUrl(chain);
            }
            catch (Exception ex) {
                Debug.WriteLine($"获取链 {chain} 的RPC URL失败: {ex.Message}");
                throw new Exception($"获取链 {chain} 的RPC URL失败: {ex.Message}");
            }
            
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    Debug.WriteLine($"尝试查询 {chain} 上的原生代币余额，钱包: {address}, 重试次数: {i+1}/{retry}");
                    
                    // 创建Web3实例，根据是否有代理选择不同的构造方法
                    Web3 web3;
                    if (proxy == null)
                    {
                        // 不使用代理
                        web3 = new Web3(rpcUrl);
                    }
                    else
                    {
                        // 使用代理
                        var httpHandler = sHttpHandler.GetHttpClientHandler(proxy);
                        var client = sHttpHandler.GetRpcClient(httpHandler, rpcUrl);
                        web3 = new Web3(client);
                    }
                    
                    // 查询余额
                    var balanceWei = await web3.Eth.GetBalance.SendRequestAsync(address);
                    var result = Web3.Convert.FromWei(balanceWei);
                    
                    Debug.WriteLine($"成功查询到 {chain} 上的原生代币余额: {result}, 钱包: {address}");
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"查询 {chain} 上的原生代币余额失败 (重试 {i+1}/{retry}): {ex.Message}");
                    
                    if (i == retry - 1) 
                        throw new Exception($"查询原生代币余额失败，已重试{retry}次: {ex.Message}");
                        
                    await Task.Delay(1000);
                }
            }
            
            throw new Exception("查询失败");
        }

        // 获取ERC20代币余额
        public async Task<decimal> GetErc20Balance(string chain, string address, string contract, int decimals, ProxyViewModel proxy = null)
        {
            string rpcUrl = ChainConfigService.GetChainRpcUrl(chain);
            return await GetErc20BalanceWithProxy(chain, address, contract, decimals, proxy);
        }

        // 使用代理获取ERC20代币余额
        public async Task<decimal> GetErc20BalanceWithProxy(string chain, string address, string contract, int decimals, ProxyViewModel proxy = null, int retry = 3)
        {
            if (!ChainConfigService.IsChainSupported(chain))
                throw new ArgumentException($"不支持的链: {chain}");
                
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("钱包地址不能为空");
                
            if (string.IsNullOrEmpty(contract))
                throw new ArgumentException("合约地址不能为空");
            
            // 使用ChainConfigService获取RPC URL
            string rpcUrl;
            try {
                rpcUrl = ChainConfigService.GetChainRpcUrl(chain);
            }
            catch (Exception ex) {
                Debug.WriteLine($"获取链 {chain} 的RPC URL失败: {ex.Message}");
                throw new Exception($"获取链 {chain} 的RPC URL失败: {ex.Message}");
            }
            
            for (int i = 0; i < retry; i++)
            {
                try
                {
                    Debug.WriteLine($"尝试查询 {chain} 上的代币 {contract} 余额，钱包: {address}, 重试次数: {i+1}/{retry}");
                    
                    // 创建Web3实例，根据是否有代理选择不同的构造方法
                    Web3 web3;
                    if (proxy == null)
                    {
                        // 不使用代理
                        web3 = new Web3(rpcUrl);
                    }
                    else
                    {
                        // 使用代理
                        var httpHandler = sHttpHandler.GetHttpClientHandler(proxy);
                        var client = sHttpHandler.GetRpcClient(httpHandler, rpcUrl);
                        web3 = new Web3(client);
                    }
                    
                    // 创建合约服务并查询余额
                    var erc20 = web3.Eth.ERC20.GetContractService(contract);
                    var balance = await erc20.BalanceOfQueryAsync(address);
                    var result = Web3.Convert.FromWei(balance, decimals);
                    
                    Debug.WriteLine($"成功查询到 {chain} 上的代币 {contract} 余额: {result}, 钱包: {address}");
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"查询 {chain} 上的代币 {contract} 余额失败 (重试 {i+1}/{retry}): {ex.Message}");
                    
                    if (i == retry - 1) 
                        throw new Exception($"查询ERC20余额失败，已重试{retry}次: {ex.Message}");
                        
                    await Task.Delay(1000);
                }
            }
            
            throw new Exception("查询ERC20余额失败");
        }

        // 获取多钱包的ERC20代币余额（批量查询）
        public async Task<Dictionary<string, decimal>> GetErc20BalancesMultiple(string chain, List<string> addresses, string tokenContract, int decimals = 18)
        {
            if (!ChainConfigService.IsChainSupported(chain))
                throw new ArgumentException($"不支持的链: {chain}");
                
            // 使用ChainConfigService获取RPC URL
            string rpcUrl = ChainConfigService.GetChainRpcUrl(chain);
            
            Dictionary<string, decimal> results = new Dictionary<string, decimal>();
            
            try
            {
                // 尝试批量RPC调用，如果不支持则回退到单个查询
                List<Task<decimal>> tasks = new List<Task<decimal>>();
                foreach (var address in addresses)
                {
                    tasks.Add(GetErc20Balance(chain, address, tokenContract, decimals));
                }
                
                var balances = await Task.WhenAll(tasks);
                
                for (int i = 0; i < addresses.Count; i++)
                {
                    results[addresses[i]] = balances[i];
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"批量查询代币余额失败: {ex.Message}");
            }
            
            return results;
        }
        
        // 尝试获取余额，如果失败则自动重试并切换RPC
        public async Task<decimal> TryGetBalanceWithRetry(string chain, string address, string tokenContract = null, int decimals = 18, int retryCount = 3)
        {
            //Exception lastException = null;
            
            // 获取该链的所有RPC URLs
            var rpcUrls = ChainConfigService.GetChainRpcUrls(chain);
            if (rpcUrls.Count == 0)
                throw new ArgumentException($"找不到链 {chain} 的RPC URLs");


            // 根据是否提供tokenContract判断是原生代币还是ERC20代币
            if (string.IsNullOrEmpty(tokenContract))
            {
                return await GetNativeBalance(chain, address);
            }
            else
            {
                return await GetErc20Balance(chain, address, tokenContract, decimals);
            }
            //// 逐个尝试RPC URL
            //foreach (var rpcUrl in rpcUrls)
            //{
            //    for (int i = 0; i < retryCount; i++)
            //    {
            //        try
            //        {

            //        }
            //        catch (Exception ex)
            //        {
            //            lastException = ex;
            //            await Task.Delay(1000); // 失败后延迟1秒再重试
            //        }
            //    }
            //}

            // 所有RPC都尝试失败
        // throw new Exception($"查询余额失败，已尝试所有RPC: {lastException?.Message}");
        }

       
       
        
        // 获取适当的HttpClient，根据是否使用代理
        private HttpClient GetHttpClient(ProxyViewModel proxy)
        {
            if (proxy == null)
                return new HttpClient();
                
            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"{proxy.ServerAddress}:{proxy.Port}", false)
                {
                    UseDefaultCredentials = false
                },
                UseProxy = true
            };
            
            // 如果代理需要身份验证
            if (!string.IsNullOrEmpty(proxy.Username) && !string.IsNullOrEmpty(proxy.Password))
            {
                handler.Proxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
            }
            
            return new HttpClient(handler);
        }
    }
    
    // 用于反序列化JSON RPC响应的类
    public class BalanceResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get; set; }
        
        [JsonProperty("result")]
        public string Result { get; set; }
        
        [JsonProperty("error")]
        public RpcError Error { get; set; }
    }
    
    public class RpcError
    {
        [JsonProperty("code")]
        public int Code { get; set; }
        
        [JsonProperty("message")]
        public string Message { get; set; }
    }
} 