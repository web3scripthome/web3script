using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net;
using Nethereum.JsonRpc.Client;
using web3script.ucontrols;
using web3script.Services;

namespace web3script.ContractScript
{
    
    
    public class ConScriptResult
    {
        public bool Success { get; set; }
        public decimal Balance { get; set; }
        public string ErrorMessage { get; set; } 
        public string Hex { get; set; }
    }
    public class MonadStaking
    {
        private readonly Web3 _web3;
        private readonly Contract _contract;
        private readonly string rpcUrl = "https://testnet-rpc.monad.xyz";
        private readonly string _contractAddress = "0x2c9c959516e9aaedb2c748224a41249201ca8be7"; //  
        
        public MonadStaking(string privateKey,ProxyViewModel proxyViewModel)
        {

               var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
               var rpcClient = sHttpHandler.GetRpcClient(httphandler,rpcUrl);
               
                var account = new Account(privateKey);
                _web3 = new Web3(account, rpcClient);
                _contract = _web3.Eth.GetContract(GetAbi(), _contractAddress);
            
          
        }
        public MonadStaking(string privateKey)
        {
            var account = new Account(privateKey);
            _web3 = new Web3(account, rpcUrl);
            _contract = _web3.Eth.GetContract(GetAbi(), _contractAddress);
        }
        public MonadStaking()
        {
           
            _web3 = new Web3( rpcUrl);
            _contract = _web3.Eth.GetContract(GetAbi(), _contractAddress);
        }


        private string GetAbi()
        {
            // 从ABI.json文件中读取的实际ABI
            return @"[
                {
                    ""inputs"": [],
                    ""name"": ""calculateTVL"",
                    ""outputs"": [
                        {
                            ""internalType"": ""uint256"",
                            ""name"": """",
                            ""type"": ""uint256""
                        }
                    ],
                    ""stateMutability"": ""view"",
                    ""type"": ""function""
                },
               {
                    ""inputs"": [],
                    ""name"": ""depositMon"",
                    ""outputs"": [],
                    ""stateMutability"": ""payable"",
                    ""type"": ""function""
                 },
                {
                    ""inputs"": [],
                    ""name"": ""totalValueLocked"",
                    ""outputs"": [
                        {
                            ""internalType"": ""uint256"",
                            ""name"": """",
                            ""type"": ""uint256""
                        }
                    ],
                    ""stateMutability"": ""view"",
                    ""type"": ""function""
                },
                {
                       ""inputs"": [
                        {""internalType"": ""uint256"", ""name"": ""amount"", ""type"": ""uint256""}
                    ],
                    ""name"": ""withdrawMon"",
                    ""outputs"": [],
                    ""stateMutability"": ""nonpayable"",
                    ""type"": ""function""
                }
            ]";
        }

        public async Task<decimal> GetTotalValueLocked()
        {
            var tvlFunction = _contract.GetFunction("totalValueLocked");
            var tvl = await tvlFunction.CallAsync<BigInteger>();
            return Web3.Convert.FromWei(tvl);
        }
        //
        public async Task<ConScriptResult> GetBalanceAsync(string Address)
        {
           // Debug.WriteLine($"into MonadStaking Balance. rpcUrl:{rpcUrl}, contractAddress:{_contractAddress} Address:{Address}");
            string tokenAbi = @"[
                    {
                        ""inputs"": [
                            {
                                ""internalType"": ""address"",
                                ""name"": ""account"",
                                ""type"": ""address""
                            }
                        ],
                        ""name"": ""balanceOf"",
                        ""outputs"": [
                            {
                                ""internalType"": ""uint256"",
                                ""name"": """",
                                ""type"": ""uint256""
                            }
                        ],
                        ""stateMutability"": ""view"",
                        ""type"": ""function""
                    },
                    {
                        ""inputs"": [],
                        ""name"": ""decimals"",
                        ""outputs"": [
                            {
                                ""internalType"": ""uint8"",
                                ""name"": """",
                                ""type"": ""uint8""
                            }
                        ],
                        ""stateMutability"": ""view"",
                        ""type"": ""function""
                    },
                    {
                        ""inputs"": [],
                        ""name"": ""name"",
                        ""outputs"": [
                            {
                                ""internalType"": ""string"",
                                ""name"": """",
                                ""type"": ""string""
                            }
                        ],
                        ""stateMutability"": ""view"",
                        ""type"": ""function""
                    },
                    {
                        ""inputs"": [],
                        ""name"": ""symbol"",
                        ""outputs"": [
                            {
                                ""internalType"": ""string"",
                                ""name"": """",
                                ""type"": ""string""
                            }
                        ],
                        ""stateMutability"": ""view"",
                        ""type"": ""function""
                    }
                ]";
            string contractAddress = "";
            var contract = _web3.Eth.GetContract(tokenAbi, contractAddress);
            decimal actualBalance = 0;
            int retryCount = 0;
            while (true)
            {
                try
                {
                    if (retryCount > 4)
                    {
                        return new ConScriptResult { Success = false, ErrorMessage = "Max retry limit reached" };
                    }
                  
                    var balanceOfFunction = contract.GetFunction("balanceOf");
                    string userAddress = Address;
                    var balance = await balanceOfFunction.CallAsync<BigInteger>(userAddress);
                    var decimalsFunction = contract.GetFunction("decimals");
                    int decimals = await decimalsFunction.CallAsync<int>();
                    actualBalance = (decimal)balance / (decimal)Math.Pow(10, decimals);
                    return new ConScriptResult { Success = true, Balance = actualBalance };
                }
                catch (Exception ex)
                {
                    retryCount++;
                    if (retryCount > 4)
                        return new ConScriptResult { Success = false, ErrorMessage = ex.Message };

                    await Task.Delay(500);

                }
            }
             
        }
        public async Task<ConScriptResult> StakeMon(decimal amount)
        {
            string txHash = "";
            try
            {
                Debug.WriteLine($"into StakeMon");
                int maxRetries = 3;
                for (int retry = 1; retry < maxRetries; retry++)
                {
                     
                        var depositFunction = _contract.GetFunction("depositMon");
                        var weiAmount = Web3.Convert.ToWei(amount);
                        Debug.WriteLine($"尝试估算Gas_{amount},{weiAmount},{_web3.TransactionManager.Account.Address}");
                        // 尝试估算Gas
                        var estimatedGas = await depositFunction.EstimateGasAsync(
                            from: _web3.TransactionManager.Account.Address,
                            gas: null,
                            value: new HexBigInteger(weiAmount)
                        );
                        Debug.WriteLine($"into发送交易");
                        // 发送交易
                        txHash = await depositFunction.SendTransactionAsync(
                            from: _web3.TransactionManager.Account.Address,
                            gas: new HexBigInteger(estimatedGas),
                            value: new HexBigInteger(weiAmount)
                        );

                        // 等待交易收据
                        var receipt = await _web3.TransactionManager.TransactionReceiptService
                            .PollForReceiptAsync(txHash, new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);

                        if (receipt.Status.Value == 1)
                        {
                            return new ConScriptResult { Success = true, Hex= txHash };
                        }
                        else
                        {
                            return new ConScriptResult { Success = false, Hex = txHash };
                        } 
                } 
            }
            catch (Exception e)
            {
                 Debug.WriteLine(e.Message);
                new ConScriptResult { Success = false, Hex = txHash, ErrorMessage = e.Message };
            }
            return new ConScriptResult { Success = false, Hex = txHash, ErrorMessage = "Error:未知错误" };
        }

        public async Task<string> StakeMonWithReferral(decimal amount, long referralId)
        {
            var depositFunction = _contract.GetFunction("depositMon");
            var weiAmount = Web3.Convert.ToWei(amount);

            var receipt = await depositFunction.SendTransactionAndWaitForReceiptAsync(
                from: _web3.TransactionManager.Account.Address,
                gas: new HexBigInteger(200000),
                value: new HexBigInteger(weiAmount),
                functionInput: referralId // 带referralId的版本
            );

            return receipt.TransactionHash;
        }

        public string GetPublicAddressFromPrivateKey(string privateKey)
        {
            var key = new Nethereum.Signer.EthECKey(privateKey);
            return key.GetPublicAddress();
        }

        public async Task<bool> UnstakeMon(decimal amount)
        {
            var function = _contract.GetFunction("withdrawMon");
            var weiAmount = Web3.Convert.ToWei(amount);
            var estimatedGas = await function.EstimateGasAsync(_web3.TransactionManager.Account.Address, null, null, weiAmount);


            try
            {
                string txHash = await function.SendTransactionAsync(
                 from: _web3.TransactionManager.Account.Address,
                 gas: estimatedGas,
                 value: new HexBigInteger(0),
                 functionInput: weiAmount
             );
                var receipt = await _web3.TransactionManager.TransactionReceiptService
                    .PollForReceiptAsync(txHash);

                if (receipt.Status.Value == 1) { return true; }
            }
            catch (Exception ex)
            {
                return false;

            }
            return false;

        }
    }
}