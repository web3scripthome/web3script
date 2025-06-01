using web3script.ContractScript;
using web3script.Services;
using web3script.ucontrols;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static Detest.MitScript.NameServer;

namespace Detest.MitScript
{
    internal class NameServer
    {
        public async Task<ConScriptResult> MintNameServer(string privatekey, string name, string nonce, string deadline, string signature)
        {
            try
            {


                string contractAddress = "0x72D68A3F0Ccc62F9Ea98467E011d17c56b2160DE";
                string rpcUrl = "https://testnet-rpc.monad.xyz";  
                var account = new Account(privatekey);
                var web3 = new Web3(account, rpcUrl);
                var contract = web3.Eth.GetContract(abi, contractAddress);
                var value = new HexBigInteger(Web3.Convert.ToWei(0.2m)); 
                var registerFunction = contract.GetFunction("registerWithSignature");
                // 准备参数数据
                var registerData = new RegisterData
                {
                    name = name,
                    nameOwner = account.Address,
                    setAsPrimaryName = true,
                    referrer = "0x0000000000000000000000000000000000000000",
                    discountKey = HexStringToByteArray("0x0000000000000000000000000000000000000000000000000000000000000000"),
                    discountClaimProof = HexStringToByteArray("0x0000000000000000000000000000000000000000000000000000000000000000"),
                    nonce = BigInteger.Parse(nonce),
                    deadline = BigInteger.Parse(deadline),
                    attributes = new List<Attribute>()  // 空数组，对应 W10
                };
                var signatureBytes = HexStringToByteArray(signature);


                // 估计 Gas
                var egas = await registerFunction.EstimateGasAsync(
                    account.Address,
                    null,  // gas
                    value,  // value
                    new object[] { registerData, signatureBytes }   
                );
               Debug.WriteLine($"Gas: {egas}");

                var transactionReceipt = await registerFunction.SendTransactionAndWaitForReceiptAsync(
                    account.Address,
                    egas,
                    value,
                    null,
                    registerData,
                    signatureBytes
                    );
                Console.WriteLine("Transaction Hash: " + transactionReceipt.TransactionHash);
                return new ConScriptResult { Success = true, Hex = transactionReceipt.TransactionHash };
            }
            catch (Exception e)
            {

                return new ConScriptResult { Success = false, ErrorMessage = e.Message };
            }

        }
        public async Task<ConScriptResult> MintNameServer(string privatekey, string name, string nonce, string deadline, string signature,ProxyViewModel proxyViewModel)
        {
            try
            {
                string rpcUrl = "https://testnet-rpc.monad.xyz"; // Monad测试网RPC 
                var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
                var rpcClient = sHttpHandler.GetRpcClient(httphandler, rpcUrl);

                string contractAddress = "0x72D68A3F0Ccc62F9Ea98467E011d17c56b2160DE";
               
                var account = new Account(privatekey);
                var web3 = new Web3(account, rpcClient);
                var contract = web3.Eth.GetContract(abi, contractAddress);
                var value = new HexBigInteger(Web3.Convert.ToWei(0.2m)); 
                var registerFunction = contract.GetFunction("registerWithSignature");
                // 准备参数数据
                var registerData = new RegisterData
                {
                    name = name,
                    nameOwner = account.Address,
                    setAsPrimaryName = true,
                    referrer = "0x0000000000000000000000000000000000000000",
                    discountKey = HexStringToByteArray("0x0000000000000000000000000000000000000000000000000000000000000000"),
                    discountClaimProof = HexStringToByteArray("0x0000000000000000000000000000000000000000000000000000000000000000"),
                    nonce = BigInteger.Parse(nonce),
                    deadline = BigInteger.Parse(deadline),
                    attributes = new List<Attribute>()  // 空数组，对应 W10
                };
                var signatureBytes = HexStringToByteArray(signature);


                // 估计 Gas
                var egas = await registerFunction.EstimateGasAsync(
                    account.Address,
                    null,  // gas
                    value,  // value
                    new object[] { registerData, signatureBytes }  // functionInput
                );
                Debug.WriteLine($"Gas: {egas}");

                var transactionReceipt = await registerFunction.SendTransactionAndWaitForReceiptAsync(
                    account.Address,
                    egas,
                    value,
                    null,
                    registerData,
                    signatureBytes
                    );
                Console.WriteLine("Transaction Hash: " + transactionReceipt.TransactionHash);
                return new ConScriptResult { Success = true, Hex = transactionReceipt.TransactionHash };
            }
            catch (Exception e)
            {

                return new ConScriptResult { Success = false, ErrorMessage = e.Message };
            }

        }
        public static bool ValidateServerCertificate(
          object sender,
          X509Certificate certificate,
          X509Chain chain,
          SslPolicyErrors sslPolicyErrors)
        {
            return true; // 忽略证书错误
        }
        public  async Task<SignatureResponse> GetSignatureData(string name, string nameOwner, bool setAsPrimaryName, string referrer, string discountKey, string discountClaimProof, string attributes, int chainId)
        {
            using (var client = new HttpClient())
            {
              
                // 设置请求头
                client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
                client.DefaultRequestHeaders.Add("Origin", "https://app.nad.domains");
                client.DefaultRequestHeaders.Add("Referer", "https://app.nad.domains/");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");

                // 构建请求 URL
                var url = $"https://api.nad.domains/v2/register/signature?" +
                    $"name={Uri.EscapeDataString(name)}&" +
                    $"nameOwner={Uri.EscapeDataString(nameOwner)}&" +
                    $"setAsPrimaryName={setAsPrimaryName.ToString().ToLower()}&" +
                    $"referrer={Uri.EscapeDataString(referrer)}&" +
                    $"discountKey={Uri.EscapeDataString(discountKey)}&" +
                    $"discountClaimProof={Uri.EscapeDataString(discountClaimProof)}&" +
                    $"attributes={Uri.EscapeDataString(attributes)}&" +
                    $"chainId={chainId}";

                Console.WriteLine("Request URL: " + url); 
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode(); 
                var responseBody = await response.Content.ReadAsStringAsync();  
                var signatureResponse = JsonConvert.DeserializeObject<SignatureResponse>(responseBody);
                return signatureResponse;
            }
        }

        public async Task<SignatureResponse> GetSignatureData(string name, string nameOwner, bool setAsPrimaryName, string referrer, string discountKey, string discountClaimProof, string attributes, int chainId,ProxyViewModel proxyViewModel)
        {

            var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
           



            using (var client = new HttpClient(httphandler))
            {

                // 设置请求头
                client.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
                client.DefaultRequestHeaders.Add("Origin", "https://app.nad.domains");
                client.DefaultRequestHeaders.Add("Referer", "https://app.nad.domains/");
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36");

                // 构建请求 URL
                var url = $"https://api.nad.domains/v2/register/signature?" +
                    $"name={Uri.EscapeDataString(name)}&" +
                    $"nameOwner={Uri.EscapeDataString(nameOwner)}&" +
                    $"setAsPrimaryName={setAsPrimaryName.ToString().ToLower()}&" +
                    $"referrer={Uri.EscapeDataString(referrer)}&" +
                    $"discountKey={Uri.EscapeDataString(discountKey)}&" +
                    $"discountClaimProof={Uri.EscapeDataString(discountClaimProof)}&" +
                    $"attributes={Uri.EscapeDataString(attributes)}&" +
                    $"chainId={chainId}";

                Console.WriteLine("Request URL: " + url);
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var signatureResponse = JsonConvert.DeserializeObject<SignatureResponse>(responseBody);
                return signatureResponse;
            }
        }


        // 辅助：Hex字符串转byte数组
        public static byte[] HexStringToByteArray(string hex)
        {
            if (hex.StartsWith("0x")) hex = hex.Substring(2);
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        // 定义 RegisterData 的结构
        [FunctionOutput]
        public class RegisterData
        {
            [Parameter("string", "name", 1)]
            public string name { get; set; }

            [Parameter("address", "nameOwner", 2)]
            public string nameOwner { get; set; }

            [Parameter("bool", "setAsPrimaryName", 3)]
            public bool setAsPrimaryName { get; set; }

            [Parameter("address", "referrer", 4)]
            public string referrer { get; set; }

            [Parameter("bytes32", "discountKey", 5)]
            public byte[] discountKey { get; set; }

            [Parameter("bytes", "discountClaimProof", 6)]
            public byte[] discountClaimProof { get; set; }

            [Parameter("uint256", "nonce", 7)]
            public BigInteger nonce { get; set; }

            [Parameter("uint256", "deadline", 8)]
            public BigInteger deadline { get; set; }

            [Parameter("tuple[]", "attributes", 9)]
            public List<Attribute> attributes { get; set; }
        }

        [FunctionOutput]
        public class Attribute
        {
            [Parameter("string", "key", 1)]
            public string key { get; set; }

            [Parameter("string", "value", 2)]
            public string value { get; set; }
        }

        public class SignatureResponse
        {
            [JsonProperty("code")]
            public int Code { get; set; }

            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }

            [JsonProperty("signature")]
            public string Signature { get; set; }

            [JsonProperty("nonce")]
            public string Nonce { get; set; }

            [JsonProperty("deadline")]
            public string Deadline { get; set; }
        }
        private static readonly string abi = @"[
        {
            ""inputs"": [
                {
                    ""components"": [
                        {""internalType"": ""string"", ""name"": ""name"", ""type"": ""string""},
                        {""internalType"": ""address"", ""name"": ""nameOwner"", ""type"": ""address""},
                        {""internalType"": ""bool"", ""name"": ""setAsPrimaryName"", ""type"": ""bool""},
                        {""internalType"": ""address"", ""name"": ""referrer"", ""type"": ""address""},
                        {""internalType"": ""bytes32"", ""name"": ""discountKey"", ""type"": ""bytes32""},
                        {""internalType"": ""bytes"", ""name"": ""discountClaimProof"", ""type"": ""bytes""},
                        {""internalType"": ""uint256"", ""name"": ""nonce"", ""type"": ""uint256""},
                        {""internalType"": ""uint256"", ""name"": ""deadline"", ""type"": ""uint256""},
                        {
                            ""components"": [
                                {""internalType"": ""string"", ""name"": ""key"", ""type"": ""string""},
                                {""internalType"": ""string"", ""name"": ""value"", ""type"": ""string""}
                            ],
                            ""internalType"": ""struct IAttributeStorage.Attribute[]"",
                            ""name"": ""attributes"",
                            ""type"": ""tuple[]""
                        }
                    ],
                    ""internalType"": ""struct NNSRegistrarController.RegisterData"",
                    ""name"": ""params"",
                    ""type"": ""tuple""
                },
                {""internalType"": ""bytes"", ""name"": ""signature"", ""type"": ""bytes""}
            ],
            ""name"": ""registerWithSignature"",
            ""outputs"": [],
            ""stateMutability"": ""payable"",
            ""type"": ""function""
        }
    ]";
    }
}
