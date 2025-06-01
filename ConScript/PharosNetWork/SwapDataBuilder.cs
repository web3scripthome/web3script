using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using System.Globalization;

namespace web3script
{
    public class SwapDataBuilder
    {
        public static string BuildSwapData(
            string usdcAddress,
            string wethAddress,
            string recipientAddress,
            decimal usdcAmount,
            decimal wethAmount)
        { 
            usdcAddress = usdcAddress.EnsureHexPrefix();
            wethAddress = wethAddress.EnsureHexPrefix();
            recipientAddress = recipientAddress.EnsureHexPrefix(); 
            var usdcAmountHexConvert = ConvertAmountToHex(usdcAmount.ToString(), 18);

            string usdcAmountHex = usdcAmountHexConvert;  
           
            var wethAmountHexConvert = ConvertAmountToHex(wethAmount.ToString(), 16);
            string wethAmountHex = "0x000111819ccd5dc0"; 
            var deadline = DateTimeOffset.UtcNow.AddMinutes(40).ToUnixTimeSeconds();
            string deadlineHex = deadline.ToString("X").PadLeft(64, '0'); 
            string swapCallData = "0x04e45aaf" +  
                usdcAddress.Substring(2).PadLeft(64, '0') + // USDC地址
                wethAddress.Substring(2).PadLeft(64, '0') + // WETH地址
                "0000000000000000000000000000000000000000000000000000000000001f4" + // 手续费 (500 = 0.05%)
                recipientAddress.Substring(2).PadLeft(64, '0') + // 接收地址
                usdcAmountHex.Substring(2).PadLeft(64, '0') + // USDC金额
                wethAmountHex.Substring(2).PadLeft(64, '0') + // WETH金额
                "000000000000000000000000000000000000000000000000000000000000000"; // sqrtPriceLimitX96

            
            var parameters = new[]
            {
                new Parameter("bytes32", "selector"),
                new Parameter("bytes[]", "calls")
            };

          
            var values = new object[]
            {
                deadlineHex.HexToByteArray(),  
                new byte[][] { swapCallData.HexToByteArray() }
            };

         
            var encoder = new FunctionCallEncoder();
            var encoded = encoder.EncodeParameters(parameters, values);

            // multicall函数选择器
            string multicallSelector = "0x5ae401dc";
            return multicallSelector + encoded.ToHex();
        }


        static string ConvertAmountToHex(string amountStr, int decimals)
        {
            if (!decimal.TryParse(amountStr, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal amountDecimal))
            {
                throw new ArgumentException("金额格式不正确");
            }

            // 使用 BigInteger.Pow 来避免浮点误差
            BigInteger multiplier = BigInteger.Pow(10, decimals);

             
            BigInteger amountInt = new BigInteger(amountDecimal * (decimal)multiplier);

           
            string hex = amountInt.ToString("x");

            
            if (hex.Length % 2 != 0)
                hex = "0" + hex;

            return "0x" + hex;
        }



        public static string BuildAddLiquidityCall(string onwer)
        {
            onwer = RemoveHexPrefix(onwer);

            var result = new List<byte>();

            // 1. 函数选择器
            result.AddRange("0x88316456".HexToByteArray());

            // 2. token address (32字节，左补齐12字节)
            result.AddRange(PadAddress("76aaada469d23216be5f7c596fa25f282ff9b364"));

            // 3. usdc address (32字节，左补齐12字节)
            result.AddRange(PadAddress("ad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37"));

            // 4. uint256(500)
            result.AddRange(EncodeUint256(500));

            // 5. int256(57050)（ABI补码）
            result.AddRange(EncodeInt256(57050));

            // 6. uint256(130180)
            result.AddRange(EncodeUint256(130180));
            //0000000000000000000000000000000000000000000000000000
            //0000000000000000000000000000000000000000000000000000
            // 7. token amount: 0x5af3107a4000（右对齐）
            result.AddRange(EncodeUint256Hex("5afa107a4000"));

            // 8. usdc amount: 0x07e4145ac65fba020（右对齐）
            result.AddRange(EncodeUint256Hex("05a0e2595fe9c7ec"));

            // 9. uint256(0x45d37cc100a1)（右对齐）
            result.AddRange(EncodeUint256Hex("486aa7b707a8"));

            // 10. uint256(0x594673105e8583c)（右对齐）
            result.AddRange(EncodeUint256Hex("3aa29feae2ad0f1"));


            // 11. contract address (32字节，左补齐12字节)
            result.AddRange(PadAddress(onwer));

            // 12. deadline (当前时间+20分钟)
            var deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
            result.AddRange(EncodeUint256(deadline));

            return "0x" + result.ToArray().ToHex();
        }
        public static string RemoveHexPrefix(string ethAddress)
        {
            if (string.IsNullOrEmpty(ethAddress))
                return ethAddress;

            return ethAddress.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ethAddress.Substring(2)
                : ethAddress;
        }

        /// <summary>
        /// 地址左补零到32字节
        /// </summary>
        private static byte[] PadAddress(string hex)
        {
            if (hex.StartsWith("0x")) hex = hex.Substring(2);
            var addr = hex.HexToByteArray();
            var padded = new byte[32];
            Array.Copy(addr, 0, padded, 12, 20);
            return padded;
        }

        /// <summary>
        /// uint256编码，右对齐，低位补零
        /// </summary>
        private static byte[] EncodeUint256(long value)
        {
            return EncodeUint256(new BigInteger(value));
        }
        private static byte[] EncodeUint256(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length > 32)
                bytes = bytes.Skip(bytes.Length - 32).ToArray();
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }
        /// <summary>
        /// int256编码，右对齐，低位补零
        /// </summary>
        private static byte[] EncodeInt256(long value)
        {
            var bytes = new BigInteger(value).ToByteArray(isUnsigned: false, isBigEndian: true);
            if (bytes.Length > 32)
                bytes = bytes.Skip(bytes.Length - 32).ToArray();
            var padded = new byte[32];
            Array.Copy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            return padded;
        }
        /// <summary>
        /// 直接用hex字符串右对齐编码为uint256
        /// </summary>
        private static byte[] EncodeUint256Hex(string hexValue)
        {
            if (hexValue.StartsWith("0x")) hexValue = hexValue.Substring(2);
            if (hexValue.Length > 64)
                hexValue = hexValue.Substring(hexValue.Length - 64, 64);
            var bytes = new byte[32];
            var hexBytes = hexValue.PadLeft(64, '0').HexToByteArray();
            Array.Copy(hexBytes, 0, bytes, 32 - hexBytes.Length, hexBytes.Length);
            return bytes;
        }

    }
}
