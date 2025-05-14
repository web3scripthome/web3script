using web3script.ContractScript;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Utilities.Encoders;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace web3script.ConScript.MitScript
{
    public class NdfMint
    {
        public  string GenerateRandomLowerCaseLetters(int length = 5)
        {
            Random random = new Random();
            char[] result = new char[length];

            for (int i = 0; i < length; i++)
            {
                // 生成小写字母 (a-z: 97-122)
                result[i] = (char)('a' + random.Next(0, 26));
            }

            return new string(result);
        }
        public async Task<ConScriptResult> NdfMintAsync(string privateKey)
        {
            Debug.WriteLine("MEME代币创建工具启动...");
            try
            {  // 获取当前目录
                string currentDirectory = Directory.GetCurrentDirectory(); 
                // 生成图片路径
                string outputPath = Path.Combine(currentDirectory, "meme.jpg"); 
                var imagePath = outputPath;
                var tokenName = GenerateRandomLowerCaseLetters(4);
                var symbol = tokenName;
                var description = $"this is {tokenName} Coin";


                // 创            // 创建上传器实例
                var uploader = new TransMeme();



                // 执行完整流程
                var txHash = await uploader.CreateMemeTokenWithContract(
                    imagePath,
                    tokenName,
                    symbol,
                    description,
                    privateKey
                );

                Debug.WriteLine("===========================================");
                Debug.WriteLine($"交易已提交，哈希: {txHash}");
                Debug.WriteLine("您可以在区块链浏览器中查看交易状态");
                Debug.WriteLine("===========================================");
                return new ConScriptResult { Success = true, Hex = txHash  };

            }
            catch (Exception ex)
            {
                return new ConScriptResult { Success = false, Hex = "", ErrorMessage = ex.Message };
                
            }
          
           
        }

    }
}

