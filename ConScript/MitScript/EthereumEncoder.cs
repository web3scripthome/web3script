using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Nethereum.ABI;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;

namespace web3script.ConScript.MitScript
{
    /// <summary>
    /// 以太坊ABI编码工具类
    /// </summary>
    public class EthereumEncoder
    {
        // 函数选择器
        private const string METHOD_ID = "0x02a57915";
        // 默认地址
        private const string DEFAULT_ADDRESS = "0x0000000000000000000000000000000000000000";
        // 默认数值
        private const string DEFAULT_VALUE1 = "100000000000000000"; // 0.1MON
        private const string DEFAULT_VALUE2 = "1000000000000000";   // 0.001 MON

        /// <summary>
        /// 编码以太坊函数调用数据
        /// </summary>
        /// <param name="tokenName">令牌名称</param>
        /// <param name="symbol">令牌符号</param>
        /// <param name="metadataUrl">元数据URL</param>
        /// <param name="address">可选地址参数，默认使用固定地址</param>
        /// <param name="value1">可选数值1参数，默认使用0.1 ETH</param>
        /// <param name="value2">可选数值2参数，默认使用0.001 ETH</param>
        /// <returns>编码后的十六进制字符串</returns>
        public static string Encode(
            string tokenName,
            string symbol,
            string metadataUrl,
            string address = null,
            string value1 = null,
            string value2 = null)
        {
            // 使用默认值或传入的参数
            address = string.IsNullOrEmpty(address) ? DEFAULT_ADDRESS : address;
            BigInteger bigInt1 = BigInteger.Parse(string.IsNullOrEmpty(value1) ? DEFAULT_VALUE1 : value1);
            BigInteger bigInt2 = BigInteger.Parse(string.IsNullOrEmpty(value2) ? DEFAULT_VALUE2 : value2);

            // 移除地址可能的0x前缀
            if (address.StartsWith("0x"))
            {
                address = address.Substring(2);
            }

            try
            {
                // 1. 定义函数ABI
                var functionAbi = new FunctionABI("testFunction", false);
                functionAbi.InputParameters = new Parameter[]
                {
                    new Parameter("address", "addr"),
                    new Parameter("string", "tokenName"),
                    new Parameter("string", "symbol"),
                    new Parameter("bytes", "metadataUrl"),
                    new Parameter("uint256", "value1"),
                    new Parameter("uint256", "value2")
                };

                // 2. 准备地址参数
                // 地址需要填充到64个字符，前面补0
                string paddedAddress = address.PadLeft(64, '0');
                // 确保开头是00000000000000000000000
                if (!paddedAddress.StartsWith("000000000000000000000000"))
                {
                    paddedAddress = "000000000000000000000000" + address;
                    paddedAddress = paddedAddress.Substring(paddedAddress.Length - 64);
                }

                // 3. 使用ABIEncode编码参数
                byte[] urlBytes = Encoding.UTF8.GetBytes(metadataUrl);

                // 不使用abiEncode.GetABIEncoded，而是直接构建编码
                // 使用已知格式的编码方式

                // 4. 创建完整编码
                // 注意：直接构建与ABI格式相同的数据结构
                string finalEncoded = METHOD_ID; // 函数选择器
                finalEncoded += paddedAddress;   // 地址部分 (填充到64个字符)

                // 偏移量部分 - 这些值是固定的
                finalEncoded += "00000000000000000000000000000000000000000000000000000000000000c0";
                finalEncoded += "0000000000000000000000000000000000000000000000000000000000000100";
                finalEncoded += "0000000000000000000000000000000000000000000000000000000000000140";

                // 数值部分
                finalEncoded += bigInt1.ToString("x").PadLeft(64, '0');
                finalEncoded += bigInt2.ToString("x").PadLeft(64, '0');

                // 字符串部分 - 从原始数据中提取格式
                // 下面会构造字符串1的长度和内容
                string strLen1 = tokenName.Length.ToString("x").PadLeft(64, '0');
                string strContent1 = "";
                byte[] nameBytes = Encoding.UTF8.GetBytes(tokenName);
                foreach (byte b in nameBytes)
                {
                    strContent1 += b.ToString("x2");
                }
                // 填充到32字节
                strContent1 = strContent1.PadRight(64, '0');

                // 字符串2的长度和内容
                string strLen2 = symbol.Length.ToString("x").PadLeft(64, '0');
                string strContent2 = "";
                byte[] symbolBytes = Encoding.UTF8.GetBytes(symbol);
                foreach (byte b in symbolBytes)
                {
                    strContent2 += b.ToString("x2");
                }
                // 填充到32字节
                strContent2 = strContent2.PadRight(64, '0');

                // URL字节数组的长度和内容
                string urlLen = urlBytes.Length.ToString("x").PadLeft(64, '0');
                string urlContent = "";
                foreach (byte b in urlBytes)
                {
                    urlContent += b.ToString("x2");
                }
                // 填充到最接近的32字节倍数
                int padding = urlContent.Length % 64 == 0 ? 0 : 64 - urlContent.Length % 64;
                urlContent = urlContent.PadRight(urlContent.Length + padding, '0');

                // 添加字符串数据到编码
                finalEncoded += strLen1;
                finalEncoded += strContent1;
                finalEncoded += strLen2;
                finalEncoded += strContent2;
                finalEncoded += urlLen;
                finalEncoded += urlContent;

                return finalEncoded;
            }
            catch (Exception ex)
            {
                throw new Exception($"编码过程中出错: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 验证编码是否正确
        /// </summary>
        /// <param name="original">原始编码</param>
        /// <param name="encoded">生成的编码</param>
        /// <returns>是否匹配</returns>
        public static bool Verify(string original, string encoded)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(encoded))
            {
                return false;
            }

            // 确保两个字符串都以0x开头
            if (!original.StartsWith("0x"))
            {
                original = "0x" + original;
            }

            if (!encoded.StartsWith("0x"))
            {
                encoded = "0x" + encoded;
            }

            // 比较去掉0x前缀后的字符串
            bool isMatch = original.Substring(2).Equals(encoded.Substring(2), StringComparison.OrdinalIgnoreCase);

            // 输出调试信息 (只有在测试时才启用)
            if (!isMatch && original.Length < 1000) // 避免输出过长的字符串
            {
                Debug.WriteLine("\n【调试】不匹配的编码:");

                // 找出第一个不匹配的索引
                int minLength = Math.Min(original.Length, encoded.Length);
                int firstDiffIndex = -1;

                for (int i = 2; i < minLength; i++)
                {
                    if (char.ToLowerInvariant(original[i]) != char.ToLowerInvariant(encoded[i]))
                    {
                        firstDiffIndex = i;
                        break;
                    }
                }

                if (firstDiffIndex != -1)
                {
                    int contextStart = Math.Max(2, firstDiffIndex - 10);
                    int contextEnd = Math.Min(minLength - 1, firstDiffIndex + 10);

                    Debug.WriteLine($"第一个不匹配的字符位置: {firstDiffIndex}");
                    Debug.WriteLine($"原始: ...{original.Substring(contextStart, contextEnd - contextStart + 1)}...");
                    Debug.WriteLine($"生成: ...{encoded.Substring(contextStart, contextEnd - contextStart + 1)}...");
                }
                else if (original.Length != encoded.Length)
                {
                    Debug.WriteLine($"长度不匹配: 原始={original.Length}, 生成={encoded.Length}");
                }
            }

            return isMatch;
        }
    }
}