using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Nethereum.Hex.HexTypes;
using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.JsonRpc.Client;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Diagnostics;
using Nethereum.RPC.TransactionReceipts;
public class PharosContractService
{
    private Web3 _web3;
    private readonly string _monadDistributorABI;
    private readonly string _relayContractABI;
    private readonly string _monadDistributorByteCode;
    private readonly string _relayContractByteCode;
    private string _useraddress;
    public PharosContractService(string rpcUrl, string privateKey)
    {
        var account = new Account(privateKey, chainId: 688688);
        _web3 = new Web3(account, rpcUrl);
        _useraddress = account.Address; 
        _monadDistributorABI = @"[{
            ""anonymous"": false,
            ""inputs"": [
                {
                    ""indexed"": false,
                    ""internalType"": ""bool"",
                    ""name"": ""success"",
                    ""type"": ""bool""
                }
            ],
            ""name"": ""Forwarded"",
            ""type"": ""event""
        },
        {
            ""anonymous"": false,
            ""inputs"": [
                {
                    ""indexed"": false,
                    ""internalType"": ""address"",
                    ""name"": ""relay"",
                    ""type"": ""address""
                }
            ],
            ""name"": ""RelaySet"",
            ""type"": ""event""
        },
        {
            ""anonymous"": false,
            ""inputs"": [
                {
                    ""indexed"": false,
                    ""internalType"": ""bool"",
                    ""name"": ""success"",
                    ""type"": ""bool""
                }
            ],
            ""name"": ""SentToRelay"",
            ""type"": ""event""
        },
        {
            ""inputs"": [
                {
                    ""internalType"": ""address"",
                    ""name"": ""recipient"",
                    ""type"": ""address""
                }
            ],
            ""name"": ""distribute"",
            ""outputs"": [],
            ""stateMutability"": ""payable"",
            ""type"": ""function""
        },
        {
            ""inputs"": [],
            ""name"": ""relayContract"",
            ""outputs"": [
                {
                    ""internalType"": ""address payable"",
                    ""name"": """",
                    ""type"": ""address""
                }
            ],
            ""stateMutability"": ""view"",
            ""type"": ""function""
        },
        {
            ""inputs"": [
                {
                    ""internalType"": ""address payable"",
                    ""name"": ""_relay"",
                    ""type"": ""address""
                }
            ],
            ""name"": ""setRelayContract"",
            ""outputs"": [],
            ""stateMutability"": ""nonpayable"",
            ""type"": ""function""
        },
        {
            ""stateMutability"": ""payable"",
            ""type"": ""receive""
        }]";

        _relayContractABI = @"[{
            ""inputs"": [
                {
                    ""internalType"": ""address"",
                    ""name"": ""to"",
                    ""type"": ""address""
                }
            ],
            ""name"": ""forwardFunds"",
            ""outputs"": [],
            ""stateMutability"": ""nonpayable"",
            ""type"": ""function""
        },
        {
            ""stateMutability"": ""payable"",
            ""type"": ""receive""
        }]";

        _monadDistributorByteCode = @"0x6080604052348015600e575f5ffd5b506109528061001c5f395ff3fe608060405260043610610037575f3560e01c80635c550ac21461004257806363453ae11461006c578063fdfa2bfd146100885761003e565b3661003e57005b5f5ffd5b34801561004d575f5ffd5b506100566100b0565b604051610063919061051c565b60405180910390f35b61008660048036038101906100819190610574565b6100d4565b005b348015610093575f5ffd5b506100ae60048036038101906100a991906105c9565b6103d6565b005b5f5f9054906101000a900473ffffffffffffffffffffffffffffffffffffffff1681565b5f3411610116576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161010d9061064e565b60405180910390fd5b5f73ffffffffffffffffffffffffffffffffffffffff165f5f9054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16036101a4576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161019b906106b6565b60405180910390fd5b5f5f5f9054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff16346040516101e990610701565b5f6040518083038185875af1925050503d805f8114610223576040519150601f19603f3d011682016040523d82523d5f602084013e610228565b606091505b505090507f4d1dfc60efb11ee9226635e98d3299db3bf2213dacdd432b1cb651feb3c73a0f8160405161025b919061072f565b60405180910390a1806102a3576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161029a90610792565b60405180910390fd5b5f5f9054906101000a900473ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1663d1aea543836040518263ffffffff1660e01b81526004016102fc91906107bf565b5f604051808303815f87803b158015610313575f5ffd5b505af1925050508015610324575060015b61039a577f5b5026b36749b2662a1a1558e252b7ed21745abd64b6530540d29637f8c737825f604051610357919061072f565b60405180910390a16040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161039190610822565b60405180910390fd5b7f5b5026b36749b2662a1a1558e252b7ed21745abd64b6530540d29637f8c7378260016040516103ca919061072f565b60405180910390a15050565b5f73ffffffffffffffffffffffffffffffffffffffff168173ffffffffffffffffffffffffffffffffffffffff1603610444576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161043b9061088a565b60405180910390fd5b805f5f6101000a81548173ffffffffffffffffffffffffffffffffffffffff021916908373ffffffffffffffffffffffffffffffffffffffff1602179055507fb44971547033304571a4b3a4fac23330a8c71fcd085d1634be35979c5c29ec315f5f9054906101000a900473ffffffffffffffffffffffffffffffffffffffff166040516104d29190610903565b60405180910390a150565b5f73ffffffffffffffffffffffffffffffffffffffff82169050919050565b5f610506826104dd565b9050919050565b610516816104fc565b82525050565b5f60208201905061052f5f83018461050d565b92915050565b5f5ffd5b5f610543826104dd565b9050919050565b61055381610539565b811461055d575f5ffd5b50565b5f8135905061056e8161054a565b92915050565b5f6020828403121561058957610588610535565b5b5f61059684828501610560565b91505092915050565b6105a8816104fc565b81146105b2575f5ffd5b50565b5f813590506105c38161059f565b92915050565b5f602082840312156105de576105dd610535565b5b5f6105eb848285016105b5565b91505092915050565b5f82825260208201905092915050565b7f4d7573742073656e6420455448000000000000000000000000000000000000005f82015250565b5f610638600d836105f4565b915061064382610604565b602082019050919050565b5f6020820190508181035f8301526106658161062c565b9050919050565b7f52656c6179206e6f7420736574000000000000000000000000000000000000005f82015250565b5f6106a0600d836105f4565b91506106ab8261066c565b602082019050919050565b5f6020820190508181035f8301526106cd81610694565b9050919050565b5f81905092915050565b50565b5f6106ec5f836106d4565b91506106f7826106de565b5f82019050919050565b5f61070b826106e1565b9150819050919050565b5f8115159050919050565b61072981610715565b82525050565b5f6020820190506107425f830184610720565b92915050565b7f53656e6420746f2072656c6179206661696c65640000000000000000000000005f82015250565b5f61077c6014836105f4565b915061078782610748565b602082019050919050565b5f6020820190508181035f8301526107a981610770565b9050919050565b6107b981610539565b82525050565b5f6020820190506107d25f8301846107b0565b92915050565b7f466f7277617264206661696c65640000000000000000000000000000000000005f82015250565b5f61080c600e836105f4565b9150610817826107d8565b602082019050919050565b5f6020820190508181035f83015261083981610800565b9050919050565b7f496e76616c6964206164647265737300000000000000000000000000000000005f82015250565b5f610874600f836105f4565b915061087f82610840565b602082019050919050565b5f6020820190508181035f8301526108a181610868565b9050919050565b5f819050919050565b5f6108cb6108c66108c1846104dd565b6108a8565b6104dd565b9050919050565b5f6108dc826108b1565b9050919050565b5f6108ed826108d2565b9050919050565b6108fd816108e3565b82525050565b5f6020820190506109165f8301846108f4565b9291505056fea26469706673582212202363d2b70153571feed309d76624791617c775a3553c729ce0f4b4cf7475467f64736f6c634300081e0033";

        _relayContractByteCode = @"0x6080604052348015600e575f5ffd5b506103278061001c5f395ff3fe608060405260043610610021575f3560e01c8063d1aea5431461002c57610028565b3661002857005b5f5ffd5b348015610037575f5ffd5b50610052600480360381019061004d91906101a5565b610054565b005b5f4790505f811161009a576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004016100919061022a565b60405180910390fd5b5f8273ffffffffffffffffffffffffffffffffffffffff16826040516100bf90610275565b5f6040518083038185875af1925050503d805f81146100f9576040519150601f19603f3d011682016040523d82523d5f602084013e6100fe565b606091505b5050905080610142576040517f08c379a0000000000000000000000000000000000000000000000000000000008152600401610139906102d3565b60405180910390fd5b505050565b5f5ffd5b5f73ffffffffffffffffffffffffffffffffffffffff82169050919050565b5f6101748261014b565b9050919050565b6101848161016a565b811461018e575f5ffd5b50565b5f8135905061019f8161017b565b92915050565b5f602082840312156101ba576101b9610147565b5b5f6101c784828501610191565b91505092915050565b5f82825260208201905092915050565b7f4e6f2066756e647320746f20666f7277617264000000000000000000000000005f82015250565b5f6102146013836101d0565b915061021f826101e0565b602082019050919050565b5f6020820190508181035f83015261024181610208565b9050919050565b5f81905092915050565b50565b5f6102605f83610248565b915061026b82610252565b5f82019050919050565b5f61027f82610255565b9150819050919050565b7f5472616e73666572206661696c656400000000000000000000000000000000005f82015250565b5f6102bd600f836101d0565b91506102c882610289565b602082019050919050565b5f6020820190508181035f8301526102ea816102b1565b905091905056fea26469706673582212203d76845c114bff58c7094f105fcee88fb199c79e4bd9c949a2891e8b806a32ab64736f6c634300081e0033";
    }
    // 部署主合约

    public async Task<string> DeployMonadDistributor()
    {
        int retryCount = 0;

        while (retryCount < 20)
        {
            try
            {
                Debug.WriteLine("主合约部署中...");

                // 发送部署交易
                string txnHash = await _web3.Eth.DeployContract.SendRequestAsync(
                    _monadDistributorABI,
                    _monadDistributorByteCode,
                    _useraddress,
                    new HexBigInteger(1000000) // 可根据需要调整 gas limit
                );

                Debug.WriteLine($"📨 部署交易已发送，Hash: {txnHash}");

                // 手动轮询获取交易回执
                var start = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(3);

                while (DateTime.UtcNow - start < timeout)
                {
                    var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);
                    if (receipt != null)
                    {
                        if (receipt.Status.Value == 1)
                        {
                            Debug.WriteLine($"✅ 合约已部署成功，地址: {receipt.ContractAddress}");
                            await Task.Delay(4000);
                            return receipt.ContractAddress;
                        }
                        else
                        {
                            Debug.WriteLine("❌ 部署交易执行失败，将重试...");
                            break; // 进入下次 while retry 循环
                        }
                    }

                    await Task.Delay(3000); // 每 3 秒轮询一次
                }

                Debug.WriteLine("⚠️ 超时未确认部署交易，将重试...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 异常: {ex.Message}");
            }

            retryCount++;
            Debug.WriteLine($"⏳ 部署失败，重试次数：{retryCount}");
            await Task.Delay(3000);
        }

        throw new Exception("合约部署失败，已达到最大重试次数");
    }

    //public async Task<string> DeployMonadDistributor()
    //{
    //    int retryCount = 0;
    //    while (true)
    //    {
    //        try
    //        {
    //            Debug.WriteLine("主合约部署...");
    //            //var gas = await _web3.Eth.DeployContract.EstimateGasAsync(
    //            //                   _monadDistributorABI,
    //            //                   _monadDistributorByteCode,
    //            //                   _useraddress,
    //            //                   null
    //            //               );
    //            //Debug.WriteLine($"Gas: {gas}");
    //            //var gasWithBuffer = new HexBigInteger(BigInteger.Multiply(gas.Value, 120) / 100);
    //            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(500)); // 设置超时时间为 30 秒
    //            var receipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    //                               _monadDistributorABI,
    //                               _monadDistributorByteCode,
    //                               _useraddress,
    //                               new HexBigInteger(1000000),
    //                               cts.Token
    //                           );

    //            var receiptService = new Nethereum.RPC.TransactionReceipts.TransactionReceiptPollingService(_web3.TransactionManager);
    //            Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
    //            if (receipt.Status.Value == 1)
    //            {
    //                Debug.WriteLine($"✅ 合约已部署，地址: {receipt.ContractAddress}");
    //                await Task.Delay(4000);
    //                return receipt.ContractAddress;
    //            }
    //            else
    //            {

    //                if (retryCount >= 20)
    //                {
    //                    throw new Exception("合约部署失败");
    //                }
    //                retryCount++;
    //            }

    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.WriteLine(ex.Message);
    //            if (retryCount >= 20)
    //            {
    //                throw new Exception("合约部署失败");
    //            }
    //            retryCount++;
    //            Debug.WriteLine($"部署主合约失败: {ex.Message}重试次数：{retryCount}");
    //        }
    //    }

    //}

    // 部署中继合约

    public async Task<string> DeployRelayContract()
    {
        int retryCount = 0;
        while (retryCount < 20)
        {
            try
            {
                Debug.WriteLine("中继合约部署（发送交易）...");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300)); // 发送 tx 的超时

                // 第一步：发送部署请求（不等确认）
                var txHash = await _web3.Eth.DeployContract.SendRequestAsync(
                    _relayContractABI,
                    _relayContractByteCode,
                    _useraddress,
                    new HexBigInteger(1000000)
                );

                Debug.WriteLine($"🚀 交易已发送，等待部署确认，Hash: {txHash}");

                // 第二步：轮询获取 receipt
                var receiptService = new TransactionReceiptPollingService(_web3.TransactionManager);
                var receipt = await receiptService.PollForReceiptAsync(txHash, cancellationToken: cts.Token);

                if (receipt.Status.Value == 1)
                {
                    Debug.WriteLine($"✅ 中继合约部署成功，地址: {receipt.ContractAddress}");
                    return receipt.ContractAddress;
                }
                else
                {
                    Debug.WriteLine("⚠️ 部署失败，但已产生交易，状态为失败。");
                    retryCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 部署失败：{ex.Message}，重试 {retryCount}");
                retryCount++;
            }
        }

        throw new Exception("中继合约部署失败：重试次数超过上限");
    }

    //public async Task<string> DeployRelayContract()
    //{
    //    int retryCount = 0;
    //    while (true)
    //    {
    //        try
    //        {

    //            //var gas = await _web3.Eth.DeployContract.EstimateGasAsync(
    //            //                  _relayContractABI,
    //            //                  _relayContractByteCode,
    //            //                  _useraddress,
    //            //                  null
    //            //              );
    //            //Debug.WriteLine($"Gas: {gas}");
    //            //var gasWithBuffer = new HexBigInteger(BigInteger.Multiply(gas.Value, 120) / 100);
    //            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(500)); 
    //            Debug.WriteLine("中继合约部署...");
    //            var receipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
    //                               _relayContractABI,
    //                               _relayContractByteCode,
    //                               _useraddress,
    //                               new HexBigInteger(1000000),
    //                               cts.Token
    //                           ); 
    //            var _relayContractAddress = receipt.ContractAddress;
    //            Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
    //            if (receipt.Status.Value == 1)
    //            {
    //                Debug.WriteLine($"✅ 中继合约已部署，地址: {receipt.ContractAddress}");
    //                await Task.Delay(4000);
    //                return receipt.ContractAddress;
    //            }
    //            else
    //            {
    //                if (retryCount >= 20)
    //                {
    //                    throw new Exception("中继合约部署失败");
    //                }
    //                retryCount++;
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.WriteLine($"中继合约部署失败{ex.Message} 重试{retryCount}");
    //            if (retryCount >= 20)
    //            {
    //                throw new Exception("中继合约部署失败" + ex.Message);
    //            }
    //            retryCount++;

    //        }
    //    }

    //}

    // 设置中继合约地址
    public async Task<string> SetRelayContract(string relayAddress, string _monadDistributorAddress)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                Debug.WriteLine("设置中继合约地址..");
                var contract = _web3.Eth.GetContract(_monadDistributorABI, _monadDistributorAddress);
                var setRelayFunction = contract.GetFunction("setRelayContract");
                var gas = new HexBigInteger(500000);
                
               // var gasWithBuffer = new HexBigInteger(BigInteger.Multiply(gas.Value, 120) / 100);
                Debug.WriteLine($"Gas: {gas}");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(500)); // 设置超时时间为 120 秒
                var receipt = await setRelayFunction.SendTransactionAndWaitForReceiptAsync(
                    from: _useraddress,
                    gas: new HexBigInteger(gas),
                    value: new HexBigInteger(0),
                    receiptRequestCancellationToken: cts.Token, 
                    relayAddress

                );
                await Task.Delay(2000); 
                Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
                if (receipt.Status.Value == 1)
                {
                    Debug.WriteLine($"✅ 中继合约已设置，Hash: {receipt.TransactionHash}");
                    return receipt.TransactionHash;
                }
                else
                {

                    if (retryCount >= 20)
                    {
                        throw new Exception("中继合约设置失败");
                    }
                    retryCount++;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"中继合约设置失败{ex.Message} 重试{retryCount}");
                if (retryCount >= 20)
                {
                    throw new Exception("中继合约设置失败");
                }
                retryCount++;
            }


        }

    }
    public async Task<System.Numerics.BigInteger> GetNativeBalance(string accountAddress)
    {

        var balance = await _web3.Eth.GetBalance.SendRequestAsync(accountAddress);
        return balance.Value;
    }
    // 执行 distribute 方法
    //public async Task<string> ExecuteDistribute(string monadDistributorAddress, string recipientAddress, decimal amountInEth)
    //{
    //    int retryCount = 0;

    //    while (true)
    //    {
    //        try
    //        {
    //            Debug.WriteLine("执行 distribute 方法...");

    //            // 获取当前账户余额
    //            var balance = await GetNativeBalance(_useraddress);
    //            Debug.WriteLine($"用户余额：{Web3.Convert.FromWei(balance)} ETH");

    //            var contract = _web3.Eth.GetContract(_monadDistributorABI, monadDistributorAddress);
    //            var distributeFunction = contract.GetFunction("distribute");

    //            // 要分发的目标金额（单位 ETH → WEI）
    //            var targetAmountInWei = Web3.Convert.ToWei(amountInEth);

    //            // 先用目标金额估算 Gas
    //            var estimatedGas = await distributeFunction.EstimateGasAsync(
    //                from: _useraddress,
    //                gas: null,
    //                value: new HexBigInteger(targetAmountInWei),
    //                functionInput: recipientAddress
    //            );

    //            // 加 20% 的 buffer
    //            var gasWithBuffer = new HexBigInteger(BigInteger.Multiply(estimatedGas.Value, 120) / 100);

    //            // 获取当前 gas price
    //            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
    //            var gasCost = gasWithBuffer.Value * gasPrice.Value;

    //            // 如果总支出 > 余额，提示错误（用户可能没有足够余额）
    //            if (gasCost >= balance)
    //            {
    //                throw new Exception("用户余额不足以支付 gas 费用");
    //            }

    //            // 计算最终发送金额（余额 - gas）
    //            var sendAmount = balance - gasCost;

    //            if (sendAmount <= 0)
    //            {
    //                throw new Exception("剩余余额不足以发送任何金额");
    //            }

    //            Debug.WriteLine($"估算 Gas: {estimatedGas.Value}");
    //            Debug.WriteLine($"加 Buffer 后的 Gas: {gasWithBuffer.Value}");
    //            Debug.WriteLine($"Gas Price: {Web3.Convert.FromWei(gasPrice.Value)} ETH");
    //            Debug.WriteLine($"预计 Gas 费用: {Web3.Convert.FromWei(gasCost)} ETH");
    //            Debug.WriteLine($"最终发送金额（减去 Gas 后）: {Web3.Convert.FromWei(sendAmount)} ETH");

    //            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));

    //            var receipt = await distributeFunction.SendTransactionAndWaitForReceiptAsync(
    //                from: _useraddress,
    //                gas: gasWithBuffer,
    //                gasPrice: gasPrice,
    //                value: new HexBigInteger(sendAmount),
    //                receiptRequestCancellationToken: cts.Token,
    //                functionInput: recipientAddress
    //            );

    //            Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");

    //            if (receipt.Status.Value == 1)
    //            {
    //                Debug.WriteLine($"✅ distribute 成功！Hash: {receipt.TransactionHash}");
    //                return receipt.TransactionHash;
    //            }
    //            else
    //            {
    //                Debug.WriteLine("❌ 交易失败，尝试重试...");
    //                if (++retryCount >= 20)
    //                    throw new Exception("通过中继合约发送代币失败，已达到最大重试次数");
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            Debug.WriteLine($"❌ 异常：{ex.Message}，重试次数：{retryCount}");

    //            if (++retryCount >= 20)
    //                throw new Exception("通过中继合约发送代币失败，错误：" + ex.Message);
    //        }
    //    }
    //}

    public async Task<string> ExecuteDistribute(string monadDistributorAddress, string recipientAddress, decimal amountInEth)
    {
        int retryCount = 0;

        while (retryCount < 20)
        {
            try
            {
                Debug.WriteLine("执行 distribute 方法...");

                var balance = await GetNativeBalance(_useraddress);
                Debug.WriteLine($"用户余额：{Web3.Convert.FromWei(balance)} ");

                var contract = _web3.Eth.GetContract(_monadDistributorABI, monadDistributorAddress);
                var distributeFunction = contract.GetFunction("distribute");

                var value = Web3.Convert.ToWei(amountInEth);
                var estimatedGas = new HexBigInteger(500000);
                try
                {
                      estimatedGas = await distributeFunction.EstimateGasAsync(
                   from: _useraddress,
                   gas: null,
                   value: new HexBigInteger(value),
                   functionInput: recipientAddress
                    );
                }
                catch (Exception)
                {

                    
                }
               

                var gasWithBuffer = new HexBigInteger(BigInteger.Multiply(estimatedGas.Value, 120) / 100);
                var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                var gasCost = gasWithBuffer.Value * gasPrice.Value;

                if (gasCost >= balance)
                    throw new Exception("用户余额不足以支付 gas 费用");

                var sendAmount = balance - gasCost;
                if (sendAmount <= 0)
                    throw new Exception("剩余余额不足以发送任何金额");

                Debug.WriteLine($"估算 Gas: {estimatedGas.Value}");
                Debug.WriteLine($"加 Buffer 后的 Gas: {gasWithBuffer.Value}");
                Debug.WriteLine($"Gas Price: {Web3.Convert.FromWei(gasPrice.Value)} ETH");
                Debug.WriteLine($"预计 Gas 费用: {Web3.Convert.FromWei(gasCost)} ETH");
                Debug.WriteLine($"最终发送金额（减去 Gas 后）: {Web3.Convert.FromWei(sendAmount)} ETH");

                // 发送交易（不等待确认）
                var txnHash = await distributeFunction.SendTransactionAsync(
                    from: _useraddress,
                    gas: gasWithBuffer,
                    gasPrice: gasPrice,
                    value: new HexBigInteger(sendAmount),
                    functionInput: recipientAddress
                );

                Debug.WriteLine($"⏳ distribute 交易已发送，Hash: {txnHash}");

                // 开始轮询确认状态（最多 2 分钟）
                var start = DateTime.UtcNow;
                var timeout = TimeSpan.FromMinutes(3);

                while (DateTime.UtcNow - start < timeout)
                {
                    var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(txnHash);
                    if (receipt != null)
                    {
                        if (receipt.Status.Value == 1)
                        {
                            Debug.WriteLine($"✅ distribute 成功！Hash: {txnHash}");
                            return txnHash;
                        }
                        else
                        {
                            Debug.WriteLine("❌ 交易上链但执行失败，准备重试...");
                            break; // exit while, retry outer loop
                        }
                    }

                    await Task.Delay(3000); // 3 秒轮询一次
                }

                Debug.WriteLine("⚠️ 交易未在预期时间内确认，将重试...");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 异常：{ex.Message}，重试次数：{retryCount}");
            }

            retryCount++;
            await Task.Delay(3000); // 避免重试太快
        }

        throw new Exception("通过中继合约发送代币失败，已达到最大重试次数");
    }



    public async Task<HexBigInteger> _GetBalance(string address)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                var balance = await _web3.Eth.GetBalance.SendRequestAsync(address);
                return balance;
            }
            catch (Exception)
            {
                await Task.Delay(2000);
                if (retryCount >= 3)
                {
                    throw;
                }
                retryCount++;
            }
        }


    }
    public async Task<string> DistributeWithRelay(string monadDistributorAddress, string recipientAddress, decimal amountInEth)
    {
        Debug.WriteLine($"执行 distributeWithRelay 方法.. {monadDistributorAddress} 接收地址{recipientAddress} 金额：{amountInEth}");
        int retryCount = 0;
        while (true)
        {
            try
            {
                var contract = _web3.Eth.GetContract(_monadDistributorABI, monadDistributorAddress);
                var distributeWithRelayFunction = contract.GetFunction("distribute");
                var senderAddress = _useraddress;
                var balance = await _GetBalance(senderAddress);
                var gasPrice = await _GetGasPrice();

             

                var estimatedGas = await distributeWithRelayFunction.EstimateGasAsync(
                            from: _useraddress,
                            gas: null,
                            value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)), 
                            recipientAddress
            );
                Debug.WriteLine($"Balance: {Web3.Convert.FromWei(balance.Value)}");
                Debug.WriteLine($"Gas: {estimatedGas.Value}");
                Debug.WriteLine($"GasPrice: {gasPrice.Value}");

                // 初始gas倍数，最多尝试到2.0倍
                decimal[] gasMultipliers = new decimal[] { 1.0m, 1.1m, 1.2m, 1.4m, 1.6m, 1.8m, 2.0m };

                foreach (var multiplier in gasMultipliers)
                {
                    var gasLimitDecimal = (decimal)estimatedGas.Value * multiplier;
                    var gasLimit = new BigInteger(gasLimitDecimal);
                    var gasCost = gasLimit * gasPrice.Value;

                    BigInteger amountToSend = balance.Value - gasCost;
                    if (amountToSend < 0)
                        continue; // gas费用都不够，再试更高倍数

                    Debug.WriteLine($"尝试用 gas 倍数: {multiplier}，发送金额: {Web3.Convert.FromWei(amountToSend)} Phar");

                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(500)); // 设置超时时间为 120 秒
                        var receipt = await distributeWithRelayFunction.SendTransactionAndWaitForReceiptAsync(
                            from: _useraddress,
                            gas: new HexBigInteger(gasLimit),
                            value: new HexBigInteger(amountToSend),
                            receiptRequestCancellationToken: cts.Token,
                            recipientAddress
                        );

                        Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");

                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"尝试失败: {ex.Message}");
                        // 继续尝试更高倍数
                    }
                }

                // 所有尝试都失败，最后强制试一下转0 wei，只为完成方法调用
                try
                {
                    var gas = await distributeWithRelayFunction.EstimateGasAsync(
                      from: _useraddress,
                            gas: null,
                            value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
                            recipientAddress
                    );
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(500)); // 设置超时时间为 120 秒
                    var receipt = await distributeWithRelayFunction.SendTransactionAndWaitForReceiptAsync(
                            from: _useraddress,
                            gas: gas,
                            value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
                            receiptRequestCancellationToken: cts.Token,
                            recipientAddress
                       );
                    return receipt.TransactionHash;
                }
                catch (Exception finalEx)
                {
                    Debug.WriteLine($"最终强制尝试也失败: {finalEx.Message}");
                }

                throw new Exception("即使调整 gas buffer，也无法完成转账，可能余额不足以支付最低 gas 费用。");

            }
            catch (Exception)
            {
                Debug.WriteLine("中继转换失败，尝试重试");
                if (retryCount >= 10)
                {
                    throw;
                }
                await Task.Delay(3000);
                retryCount++;

            }



        }

    }

    private async Task<HexBigInteger> _GetGasPrice()
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                return await _web3.Eth.GasPrice.SendRequestAsync();
            }
            catch (Exception)
            {
                await Task.Delay(2000);
                if (retryCount >= 3)
                {
                    throw;
                }
                retryCount++;
            }

        }


    }

}