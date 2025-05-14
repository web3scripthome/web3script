using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace web3script.ConScript.MitScript
{
    public class TransMeme
    {

      
        private static readonly HttpClient client = new HttpClient();

        
        private const string BASE_URL = "https://r2-access-worker.jeeterlabs.workers.dev";

       
        private const string STORAGE_URL = "https://storaccge.nadapp.net";

      
        private const string USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36";
        private const string REFERER = "https://testnet.nad.fun/";
        private const string ORIGIN = "https://testnet.nad.fun";

       
        private const string CONTRACT_ADDRESS = "0x822EB1ADD41cf87C3F178100596cf24c9a6442f6";
        private const string RPC_URL1 = "https://monad-testnet.g.alchemy.com/v2/bnccqdpDiwQSnb3Sc_qup8RI7sYgAkBCb";

      
        private const string RPC_URL = "https://testnet-rpc.monad.xyz";




        public async Task<string> CreateMemeTokenWithContract(
        string imagePath,
        string tokenName,
        string symbol,
        string description,
        string privateKey
        )
        {
            try
            {
                Debug.WriteLine("开始完整MEME代币创建流程...");

                // 1. 上传图片并生成元数据
                var metadataUrl = await UploadMemeWithWebRequest(imagePath, tokenName, symbol, description);
                Debug.WriteLine($"元数据已上传，URL: {metadataUrl}");

                // 验证元数据URL是否可访问
                if (await VerifyMetadataUrl(metadataUrl))
                {
                    Debug.WriteLine("元数据URL验证成功！");
                }
                else
                {
                    Debug.WriteLine("警告：元数据URL可能尚未完全处理，继续执行合约调用...");
                }

                // 2. 调用合约创建代币
                // 尝试使用Nethereum的合约功能
                try
                {
                    Account account = new Account(privateKey, 10143);
                    var data = EthereumEncoder.Encode(tokenName, symbol, metadataUrl, account.Address);
                    Debug.WriteLine("解码数据:" + data);
                    Debug.WriteLine("交易数据");
                    return await SendTransactionAsync(privateKey, data);

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Nethereum调用失败: {ex.Message}");
                    // 尝试使用精确交易数据
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"创建MEME代币过程中出错: {ex.Message}");
                throw;
            }
        }
        public async Task<string> UploadMemeWithWebRequest(string imagePath, string tokenName, string symbol, string description)
        {
            Debug.WriteLine("开始上传MEME图片并生成元数据(使用WebRequest)...");

            // 1. 读取本地图片文件信息
            var fileInfo = new FileInfo(imagePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("图片文件不存在", imagePath);
            }

            // 读取图片文件的二进制数据
            var fileBytes = File.ReadAllBytes(imagePath);

            // 生成唯一的文件名 (使用UUID/GUID避免文件名冲突)
            var fileId = Guid.NewGuid().ToString();
            var fileName = $"coin/{fileId}";

            // 设置文件类型和大小信息
            var fileType = "image/jpeg"; // 根据实际文件类型调整
            var fileSize = fileInfo.Length;

            Debug.WriteLine($"准备上传图片: {Path.GetFileName(imagePath)}, 大小: {fileSize} 字节");

            // 2. 获取图片上传URL
            Debug.WriteLine("正在获取图片上传URL...");
            var uploadUrlResponse = await GetUploadUrl(fileName, fileType, fileSize);

            Debug.WriteLine($"获取上传URL成功: {uploadUrlResponse.Url}");
            Debug.WriteLine($"图片将存储于: {uploadUrlResponse.FileUrl}");

            // 3. 上传图片文件
            Debug.WriteLine("正在上传图片...");
            var uploadResponse = await UploadImageWithWebRequest(uploadUrlResponse.Url, fileBytes, fileName, Path.GetFileName(imagePath));
            Debug.WriteLine("图片上传完成");

            // 4. 准备元数据信息
            // 生成唯一的元数据文件名
            var metadataId = Guid.NewGuid().ToString();
            var metadataFileName = $"metadata-{metadataId}.json";

            Debug.WriteLine($"正在准备元数据: {metadataFileName}");

            // 创建元数据对象，包含代币信息
            var metadata = new
            {
                name = tokenName,           // 代币名称
                symbol,            // 代币符号
                image_uri = uploadUrlResponse.FileUrl,  // 上传的图片URL
                description,  // 代币描述
                home_page = "",             // 主页URL(可选)
                twitter = "",               // Twitter账号(可选)
                telegram = ""               // Telegram群组(可选)
            };

            // 5. 获取元数据上传URL并上传元数据
            Debug.WriteLine("正在获取元数据上传URL...");
            await GetMetadataUploadUrl(metadataFileName, metadata);

            Debug.WriteLine("正在上传元数据...");
            await UploadMetadataWithWebRequest(metadataFileName, metadata);

            // 构建最终的元数据URL，供后续访问
            var metadataUrl = $"{STORAGE_URL}/{metadataFileName}";
            Debug.WriteLine($"元数据上传完成，URL: {metadataUrl}");

            return metadataUrl;
        }

        private async Task<UploadResponse> UploadImageWithWebRequest(string uploadUrl, byte[] fileBytes, string fileName, string originalFileName)
        {
            // 创建请求
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uploadUrl);
            request.Method = "POST";

            // 设置请求头
            string boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString().Substring(0, 16);
            request.ContentType = $"multipart/form-data; boundary={boundary}";
            request.UserAgent = USER_AGENT;
            request.Headers.Add("Origin", ORIGIN);
            request.Referer = REFERER;

            Debug.WriteLine($"使用boundary: {boundary}");

            // 准备请求内容
            using (var requestStream = await request.GetRequestStreamAsync())
            {
                // 添加文件部分
                var fileHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{originalFileName}\"\r\nContent-Type: image/jpeg\r\n\r\n";
                var fileHeaderBytes = Encoding.UTF8.GetBytes(fileHeader);
                await requestStream.WriteAsync(fileHeaderBytes, 0, fileHeaderBytes.Length);

                // 写入文件内容
                await requestStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                await requestStream.WriteAsync(Encoding.UTF8.GetBytes("\r\n"), 0, 2);

                // 添加fileName字段
                var fieldHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"fileName\"\r\n\r\n{fileName}\r\n";
                var fieldHeaderBytes = Encoding.UTF8.GetBytes(fieldHeader);
                await requestStream.WriteAsync(fieldHeaderBytes, 0, fieldHeaderBytes.Length);

                // 添加结束边界
                var endBoundary = $"--{boundary}--\r\n";
                var endBoundaryBytes = Encoding.UTF8.GetBytes(endBoundary);
                await requestStream.WriteAsync(endBoundaryBytes, 0, endBoundaryBytes.Length);
            }

            // 获取响应
            try
            {
                using (var response = (System.Net.HttpWebResponse)await request.GetResponseAsync())
                {
                    using (var responseStream = response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream))
                    {
                        var responseBody = await reader.ReadToEndAsync();
                        Debug.WriteLine($"上传图片响应: {responseBody}");
                        return JsonConvert.DeserializeObject<UploadResponse>(responseBody);
                    }
                }
            }
            catch (System.Net.WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var errorResponse = (System.Net.HttpWebResponse)ex.Response)
                    {
                        using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                        {
                            string errorText = await reader.ReadToEndAsync();
                            Debug.WriteLine($"上传图片错误 ({(int)errorResponse.StatusCode}): {errorText}");
                        }
                    }
                }
                throw;
            }
        }
        /// <summary>
        /// 从服务器获取图片上传URL
        /// </summary>
        private async Task<UploadUrlResponse> GetUploadUrl(string fileName, string fileType, long fileSize)
        {
            // 创建请求内容，包含文件信息
            var request = new
            {
                fileName,   // 文件名（带有coin/前缀的GUID）
                fileType,   // 文件MIME类型
                fileSize    // 文件大小
            };

            // 将请求对象序列化为JSON
            var jsonRequest = JsonConvert.SerializeObject(request);

            // 创建HTTP请求
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}/get-upload-url");
            httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            // 发送请求
            var response = await client.SendAsync(httpRequest);

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 读取响应内容
            var responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"获取上传URL响应: {responseBody}");

            // 将JSON响应反序列化为对象
            return JsonConvert.DeserializeObject<UploadUrlResponse>(responseBody);
        }
        /// <summary>
        /// 获取元数据上传URL
        /// </summary>
        private async Task GetMetadataUploadUrl(string metadataFileName, object metadata)
        {
            // 创建请求对象
            var request = new
            {
                fileName = metadataFileName,
                metadata
            };

            // 序列化请求对象为JSON
            var jsonRequest = JsonConvert.SerializeObject(request);

            // 创建HTTP请求
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BASE_URL}/get-metadata-upload-url");
            httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            // 发送请求
            var response = await client.SendAsync(httpRequest);

            // 确保请求成功
            response.EnsureSuccessStatusCode();

            // 这个API没有返回内容，但我们需要确保它成功完成
            Debug.WriteLine("获取元数据上传URL成功");
        }

        /// <summary>
        /// 使用WebRequest上传元数据
        /// </summary>
        private async Task UploadMetadataWithWebRequest(string metadataFileName, object metadata)
        {
            // 元数据上传URL
            string uploadUrl = $"{BASE_URL}/upload-metadata/{metadataFileName}";

            // 创建请求
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uploadUrl);
            request.Method = "POST";

            // 设置请求头
            string boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString().Substring(0, 16);
            request.ContentType = $"multipart/form-data; boundary={boundary}";
            request.UserAgent = USER_AGENT;
            request.Headers.Add("Origin", ORIGIN);
            request.Referer = REFERER;

            // 序列化元数据对象为JSON
            var metadataJson = JsonConvert.SerializeObject(metadata);

            // 准备请求内容
            using (var requestStream = await request.GetRequestStreamAsync())
            {
                // 添加文件部分
                var fileHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"{metadataFileName}\"\r\nContent-Type: application/json\r\n\r\n{metadataJson}\r\n";
                var fileHeaderBytes = Encoding.UTF8.GetBytes(fileHeader);
                await requestStream.WriteAsync(fileHeaderBytes, 0, fileHeaderBytes.Length);

                // 添加fileName字段
                var fieldHeader = $"--{boundary}\r\nContent-Disposition: form-data; name=\"fileName\"\r\n\r\n{metadataFileName}\r\n";
                var fieldHeaderBytes = Encoding.UTF8.GetBytes(fieldHeader);
                await requestStream.WriteAsync(fieldHeaderBytes, 0, fieldHeaderBytes.Length);

                // 添加结束边界
                var endBoundary = $"--{boundary}--\r\n";
                var endBoundaryBytes = Encoding.UTF8.GetBytes(endBoundary);
                await requestStream.WriteAsync(endBoundaryBytes, 0, endBoundaryBytes.Length);
            }

            // 获取响应
            try
            {
                using (var response = (System.Net.HttpWebResponse)await request.GetResponseAsync())
                {
                    using (var responseStream = response.GetResponseStream())
                    using (var reader = new StreamReader(responseStream))
                    {
                        var responseBody = await reader.ReadToEndAsync();
                        Debug.WriteLine($"上传元数据响应: {responseBody}");
                    }
                }
            }
            catch (System.Net.WebException ex)
            {
                if (ex.Response != null)
                {
                    using (var errorResponse = (System.Net.HttpWebResponse)ex.Response)
                    {
                        using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                        {
                            string errorText = await reader.ReadToEndAsync();
                            Debug.WriteLine($"元数据上传错误 ({(int)errorResponse.StatusCode}): {errorText}");
                        }
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// 验证元数据URL是否可访问
        /// </summary>
        public async Task<bool> VerifyMetadataUrl(string metadataUrl)
        {
            try
            {
                // 发送GET请求检查URL是否可访问
                var response = await client.GetAsync(metadataUrl);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"验证元数据URL时出错: {ex.Message}");
                return false;
            }
        }
        public static async Task<string> SendTransactionAsync(string privateKey, string data)
        {
            try
            {
                var chainId = 10143;
                var account = new Account(privateKey, chainId);
                var web3 = new Web3(account, "https://testnet-rpc.monad.xyz");
                string toAddress = "0x822EB1ADD41cf87C3F178100596cf24c9a6442f6";
                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
                var gasLimit = new BigInteger(3000000);

                var txnInput = new TransactionInput
                {
                    From = account.Address,
                    To = toAddress,
                    Gas = new HexBigInteger(gasLimit),
                    GasPrice = gasPrice,
                    Value = new HexBigInteger(BigInteger.Parse("1ade0dbe1d28000", NumberStyles.HexNumber)),
                    Data = data
                };
                var receipt = await web3.TransactionManager.SendTransactionAndWaitForReceiptAsync(txnInput);
                return receipt.TransactionHash;
            }
            catch (Exception)
            {

                throw;
            }

        }

        /// <summary>
        /// 上传URL响应类
        /// </summary>
        private class UploadUrlResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("fileUrl")]
            public string FileUrl { get; set; }
        }

        /// <summary>
        /// 上传响应类
        /// </summary>
        private class UploadResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("fileName")]
            public string FileName { get; set; }

            [JsonProperty("fileUrl")]
            public string FileUrl { get; set; }
        }
    }
}

