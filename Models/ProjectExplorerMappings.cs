using System;
using System.Collections.Generic;

namespace web3script.Models
{
    /// <summary>
    /// 提供项目名称到区块浏览器URL的映射
    /// </summary>
    public static class ProjectExplorerMappings
    {
        // 项目名称到区块浏览器基础URL的映射字典
        private static readonly Dictionary<string, string> _explorerUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // 常用项目映射
            { "Monad", "https://monad-testnet.socialscan.io/address/" },
            { "Monzad", "https://monad-testnet.socialscan.io/address/" },
            { "Scroll", "https://scrollscan.com/address/" },
            { "ZkSync", "https://explorer.zksync.io/address/" },
            { "Zora", "https://explorer.zora.energy/address/" },
            { "Base", "https://basescan.org/address/" },
            { "Arbitrum", "https://arbiscan.io/address/" },
            { "Optimism", "https://optimistic.etherscan.io/address/" },
            { "Polygon", "https://polygonscan.com/address/" },
            { "Avalanche", "https://snowtrace.io/address/" },
            { "Ethereum", "https://etherscan.io/address/" },
            { "Linea", "https://lineascan.build/address/" },
            { "Manta", "https://pacific-explorer.manta.network/address/" },
            { "Viction", "https://vicscan.xyz/address/" }
        };

        /// <summary>
        /// 获取指定项目名称对应的区块浏览器地址URL
        /// </summary>
        /// <param name="projectName">项目名称</param>
        /// <param name="walletAddress">钱包地址</param>
        /// <returns>完整的区块浏览器URL，如果项目未找到则返回null</returns>
        public static string GetExplorerUrl(string projectName, string walletAddress)
        {
            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(walletAddress))
                return null;

            // 尝试从映射字典中获取对应的区块浏览器URL
            if (_explorerUrls.TryGetValue(projectName, out string baseUrl))
            {
                return baseUrl + walletAddress;
            }

            // 如果没有找到明确的映射，可以尝试使用通用的Etherscan作为默认值
            return "https://etherscan.io/address/" + walletAddress;
        }

        /// <summary>
        /// 检查是否支持指定项目的区块浏览器链接
        /// </summary>
        /// <param name="projectName">项目名称</param>
        /// <returns>如果支持则返回true，否则返回false</returns>
        public static bool IsExplorerSupported(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
                return false;

            return _explorerUrls.ContainsKey(projectName);
        }
    }
} 