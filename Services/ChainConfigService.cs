using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace web3script.Services
{
    public class ChainConfigService
    {
        private static readonly Lazy<JObject> _coinConfig = new Lazy<JObject>(() => 
            JObject.Parse(GetCoinTypesJson()));

        /// <summary>
        /// 获取全局币种配置
        /// </summary>
        public static JObject CoinConfig => _coinConfig.Value;

        /// <summary>
        /// 获取币种配置的JSON
        /// </summary>
        public static string GetCoinTypesJson()
        {
            // JSON配置内容
            return @"{
                ""ETH"": {
                    ""rpcUrls"": [
                        ""https://eth-mainnet.g.alchemy.com/v2/demo"",
                        ""https://mainnet.infura.io/v3/9aa3d95b3bc440fa88ea12eaa4456161"",
                        ""https://rpc.ankr.com/eth""
                    ],
                    ""USDT"": {""contract"": ""0xdAC17F958D2ee523a2206206994597C13D831ec7"", ""decimals"": 6},
                    ""USDC"": {""contract"": ""0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48"", ""decimals"": 6},
                    ""PEPE"": {""contract"": ""0x6982508145454Ce325dDbE47a25d4ec3d2311933"", ""decimals"": 18}
                },
                ""BSC"": {
                    ""rpcUrls"": [
                        ""https://bsc-dataseed.binance.org"",
                        ""https://bsc-dataseed1.defibit.io"",
                        ""https://bsc-dataseed1.ninicoin.io""
                    ],
                    ""USDT"": {""contract"": ""0x55d398326f99059fF775485246999027B3197955"", ""decimals"": 18},
                    ""USDC"": {""contract"": ""0x8AC76a51cc950d9822D68b83fE1Ad97B32Cd580d"", ""decimals"": 18},
                    ""CAKE"": {""contract"": ""0x0E09FaBB73Bd3Ade0a17ECC321fD13a19e81cE82"", ""decimals"": 18}
                },
                ""MonadTest"": {
                    ""rpcUrls"": [
                        ""https://testnet-rpc.monad.xyz""
                    ],
                    ""USDT"": {""contract"": ""0x94654B2d41AD764E6F0a75a2C0FB56b4738f27b5"", ""decimals"": 6}
                },
                ""HumanityTest_1"": {
                    ""rpcUrls"": [
                        ""https://rpc.testnet.humanity.org""
                    ],
                    ""RWT"": {""contract"": ""0x693cB8de384f00A5c2580D544B38013BFB496529"", ""decimals"": 18}
                } 
            }";
        }

        /// <summary>
        /// 获取链的RPC URL，随机选择一个实现负载均衡
        /// </summary>
        public static string GetChainRpcUrl(string chain)
        {
            try
            {
                // 首先检查链是否存在
                if (CoinConfig == null)
                    throw new InvalidOperationException("配置尚未初始化");
                    
                JToken chainToken = CoinConfig[chain];
                if (chainToken == null)
                    throw new ArgumentException($"找不到链 {chain} 的配置");
                    
                JToken rpcUrlsToken = chainToken["rpcUrls"];
                if (rpcUrlsToken == null)
                    throw new ArgumentException($"链 {chain} 的配置中找不到RPC URLs");
                    
                // 获取该链的RPC URLs数组
                var rpcUrls = rpcUrlsToken as JArray;
                if (rpcUrls == null || rpcUrls.Count == 0)
                    throw new ArgumentException($"链 {chain} 的RPC URLs列表为空");
                    
                // 随机选择一个RPC URL，实现负载均衡
                Random random = new Random();
                int index = random.Next(rpcUrls.Count);
                string rpcUrl = rpcUrls[index]?.ToString();
                
                if (string.IsNullOrEmpty(rpcUrl))
                    throw new ArgumentException($"链 {chain} 的RPC URL为空");
                    
                return rpcUrl;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取链 {chain} 的RPC URL失败: {ex.Message}");
                // 返回默认RPC作为应急方案
                if (chain.Equals("ETH", StringComparison.OrdinalIgnoreCase))
                    return "https://rpc.ankr.com/eth";
                if (chain.Equals("BSC", StringComparison.OrdinalIgnoreCase)) 
                    return "https://bsc-dataseed.binance.org";
                if (chain.Equals("MonadTest", StringComparison.OrdinalIgnoreCase))
                    return "https://testnet-rpc.monad.xyz";
                if (chain.Equals("HumanityTest_1", StringComparison.OrdinalIgnoreCase))
                    return "https://rpc.testnet.humanity.org";
                    
                throw new ArgumentException($"找不到链 {chain} 的RPC URL，且无默认值");
            }
        }

        /// <summary>
        /// 获取指定链的所有RPC URLs
        /// </summary>
        public static List<string> GetChainRpcUrls(string chain)
        {
            var result = new List<string>();
            
            try
            {
                // 首先检查链是否存在
                if (CoinConfig == null)
                    return result;
                    
                JToken chainToken = CoinConfig[chain];
                if (chainToken == null)
                    return result;
                    
                JToken rpcUrlsToken = chainToken["rpcUrls"];
                if (rpcUrlsToken == null)
                    return result;
                    
                // 获取该链的RPC URLs数组
                var rpcUrls = rpcUrlsToken as JArray;
                if (rpcUrls == null || rpcUrls.Count == 0)
                    return result;
                    
                foreach (var url in rpcUrls)
                {
                    string urlStr = url?.ToString();
                    if (!string.IsNullOrEmpty(urlStr))
                        result.Add(urlStr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取链 {chain} 全部RPC URLs失败: {ex.Message}");
                
                // 添加默认URL作为应急方案
                if (chain.Equals("ETH", StringComparison.OrdinalIgnoreCase))
                    result.Add("https://rpc.ankr.com/eth");
                if (chain.Equals("BSC", StringComparison.OrdinalIgnoreCase)) 
                    result.Add("https://bsc-dataseed.binance.org");
                if (chain.Equals("MonadTest", StringComparison.OrdinalIgnoreCase))
                    result.Add("https://testnet-rpc.monad.xyz");
                if (chain.Equals("HumanityTest_1", StringComparison.OrdinalIgnoreCase))
                    result.Add("https://rpc.testnet.humanity.org");
            }
            
            return result;
        }

        /// <summary>
        /// 获取币种的元数据
        /// </summary>
        public static JToken GetCoinTypeData(string tokenType)
        {
            try
            {
                // 验证输入
                if (string.IsNullOrEmpty(tokenType))
                    return null;
                    
                string[] parts = tokenType.Split('.');
                if (parts.Length != 2)
                    return null;
                    
                string chain = parts[0];
                string token = parts[1];
                
                // 检查配置是否存在
                if (CoinConfig == null)
                    return null;
                    
                // 检查链是否存在
                JToken chainToken = CoinConfig[chain];
                if (chainToken == null)
                    return null;
                    
                // 检查代币是否存在
                JToken tokenData = chainToken[token];
                return tokenData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取币种 {tokenType} 元数据失败: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 检查链是否支持
        /// </summary>
        public static bool IsChainSupported(string chain)
        {
            try
            {
                if (CoinConfig == null)
                    return false;
                
                // 先检查配置中是否有该链
                if (CoinConfig[chain] != null)
                    return true;
                
                // 检查默认支持的链
                return chain.Equals("ETH", StringComparison.OrdinalIgnoreCase) ||
                       chain.Equals("BSC", StringComparison.OrdinalIgnoreCase) ||
                       chain.Equals("MonadTest", StringComparison.OrdinalIgnoreCase) ||
                       chain.Equals("HumanityTest_1", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取所有支持的链名称
        /// </summary>
        public static List<string> GetSupportedChains()
        {
            var chains = new List<string>();
            foreach (var prop in CoinConfig.Properties())
            {
                chains.Add(prop.Name);
            }
            return chains;
        }
        
        /// <summary>
        /// 获取链上所有支持的代币名称
        /// </summary>
        public static List<string> GetSupportedTokensForChain(string chain)
        {
            var tokens = new List<string>();
            
            if (CoinConfig[chain] != null)
            {
                foreach (var prop in CoinConfig[chain].Children<JProperty>())
                {
                    // 跳过rpcUrls属性
                    if (!prop.Name.Equals("rpcUrls", StringComparison.OrdinalIgnoreCase))
                    {
                        tokens.Add(prop.Name);
                    }
                }
            }
            
            return tokens;
        }
    }
} 
