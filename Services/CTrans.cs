using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.ContractHandlers;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System.Text.Encodings.Web;
using System.Numerics;
using Nethereum.RLP;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;

namespace Ctrans
{
    public class ContractService
    {
        private readonly Web3 _web3;
        private readonly string _privateKey;
        private readonly string _accountAddress;
        private const string abi = @"[{'inputs':[{'internalType':'address','name':'recipient','type':'address'}],'name':'distribute','outputs':[],'stateMutability':'payable','type':'function'},{'inputs':[{'internalType':'address payable','name':'relayAddress','type':'address'},{'internalType':'address','name':'recipient','type':'address'}],'name':'distributeWithRelay','outputs':[],'stateMutability':'payable','type':'function'}]";


        public ContractService(string rpcUrl, string privateKey)
        {
            _privateKey = privateKey;
            var account = new Account(privateKey);
            _accountAddress = account.Address;
            _web3 = new Web3(account, rpcUrl);
        }
        // 部署 MonadDistributor 合约
        public async Task<string> DeployMonadDistributor()
        {
            // 从 Remix 获取编译后的合约字节码
            // 注意：这个字节码已经包含了 RelayContract 的代码
            var contractByteCode = @"6080604052348015600e575f80fd5b50610af28061001c5f395ff3fe608060405260043610610037575f3560e01c806363453ae1146100425780637167db1b1461005e578063c474a22c1461007a5761003e565b3661003e57005b5f80fd5b61005c600480360381019061005791906104f1565b6100b6565b005b61007860048036038101906100739190610557565b61028d565b005b348015610085575f80fd5b506100a0600480360381019061009b91906104f1565b61046a565b6040516100ad91906105af565b60405180910390f35b5f34116100f8576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004016100ef90610622565b60405180910390fd5b5f60405161010590610486565b604051809103905ff08015801561011e573d5f803e3d5ffd5b5090505f81905060015f808373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f205f6101000a81548160ff0219169083151502179055505f8173ffffffffffffffffffffffffffffffffffffffff163460405161019e9061066d565b5f6040518083038185875af1925050503d805f81146101d8576040519150601f19603f3d011682016040523d82523d5f602084013e6101dd565b606091505b5050905080610221576040517f08c379a0000000000000000000000000000000000000000000000000000000008152600401610218906106cb565b60405180910390fd5b8273ffffffffffffffffffffffffffffffffffffffff1663d1aea543856040518263ffffffff1660e01b815260040161025a91906106f8565b5f604051808303815f87803b158015610271575f80fd5b505af1158015610283573d5f803e3d5ffd5b5050505050505050565b5f34116102cf576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004016102c690610622565b60405180910390fd5b5f808373ffffffffffffffffffffffffffffffffffffffff1673ffffffffffffffffffffffffffffffffffffffff1681526020019081526020015f205f9054906101000a900460ff16610357576040517f08c379a000000000000000000000000000000000000000000000000000000000815260040161034e9061075b565b60405180910390fd5b5f8273ffffffffffffffffffffffffffffffffffffffff163460405161037c9061066d565b5f6040518083038185875af1925050503d805f81146103b6576040519150601f19603f3d011682016040523d82523d5f602084013e6103bb565b606091505b50509050806103ff576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004016103f6906106cb565b60405180910390fd5b8273ffffffffffffffffffffffffffffffffffffffff1663d1aea543836040518263ffffffff1660e01b815260040161043891906106f8565b5f604051808303815f87803b15801561044f575f80fd5b505af1158015610461573d5f803e3d5ffd5b50505050505050565b5f602052805f5260405f205f915054906101000a900460ff1681565b6103438061077a83390190565b5f80fd5b5f73ffffffffffffffffffffffffffffffffffffffff82169050919050565b5f6104c082610497565b9050919050565b6104d0816104b6565b81146104da575f80fd5b50565b5f813590506104eb816104c7565b92915050565b5f6020828403121561050657610505610493565b5b5f610513848285016104dd565b91505092915050565b5f61052682610497565b9050919050565b6105368161051c565b8114610540575f80fd5b50565b5f813590506105518161052d565b92915050565b5f806040838503121561056d5761056c610493565b5b5f61057a85828601610543565b925050602061058b858286016104dd565b9150509250929050565b5f8115159050919050565b6105a981610595565b82525050565b5f6020820190506105c25f8301846105a0565b92915050565b5f82825260208201905092915050565b7f4d7573742073656e6420736f6d652065746865720000000000000000000000005f82015250565b5f61060c6014836105c8565b9150610617826105d8565b602082019050919050565b5f6020820190508181035f83015261063981610600565b9050919050565b5f81905092915050565b50565b5f6106585f83610640565b91506106638261064a565b5f82019050919050565b5f6106778261064d565b9150819050919050565b7f5472616e7366657220746f2072656c6179206661696c656400000000000000005f82015250565b5f6106b56018836105c8565b91506106c082610681565b602082019050919050565b5f6020820190508181035f8301526106e2816106a9565b9050919050565b6106f2816104b6565b82525050565b5f60208201905061070b5f8301846106e9565b92915050565b7f496e76616c69642072656c617920636f6e7472616374000000000000000000005f82015250565b5f6107456016836105c8565b915061075082610711565b602082019050919050565b5f6020820190508181035f83015261077281610739565b905091905056fe6080604052348015600e575f80fd5b506103278061001c5f395ff3fe608060405260043610610021575f3560e01c8063d1aea5431461002c57610028565b3661002857005b5f80fd5b348015610037575f80fd5b50610052600480360381019061004d91906101a5565b610054565b005b5f4790505f811161009a576040517f08c379a00000000000000000000000000000000000000000000000000000000081526004016100919061022a565b60405180910390fd5b5f8273ffffffffffffffffffffffffffffffffffffffff16826040516100bf90610275565b5f6040518083038185875af1925050503d805f81146100f9576040519150601f19603f3d011682016040523d82523d5f602084013e6100fe565b606091505b5050905080610142576040517f08c379a0000000000000000000000000000000000000000000000000000000008152600401610139906102d3565b60405180910390fd5b505050565b5f80fd5b5f73ffffffffffffffffffffffffffffffffffffffff82169050919050565b5f6101748261014b565b9050919050565b6101848161016a565b811461018e575f80fd5b50565b5f8135905061019f8161017b565b92915050565b5f602082840312156101ba576101b9610147565b5b5f6101c784828501610191565b91505092915050565b5f82825260208201905092915050565b7f4e6f2066756e647320746f20666f7277617264000000000000000000000000005f82015250565b5f6102146013836101d0565b915061021f826101e0565b602082019050919050565b5f6020820190508181035f83015261024181610208565b9050919050565b5f81905092915050565b50565b5f6102605f83610248565b915061026b82610252565b5f82019050919050565b5f61027f82610255565b9150819050919050565b7f5472616e73666572206661696c656400000000000000000000000000000000005f82015250565b5f6102bd600f836101d0565b91506102c882610289565b602082019050919050565b5f6020820190508181035f8301526102ea816102b1565b905091905056fea264697066735822122009f39e7e4d0542c3ca43d00928858910d715d0561b7946c72576f492f37d5a0a64736f6c634300081a0033a2646970667358221220c00aee0db56df68b2a9cac52dcf575afb374b4bc60447545a4b6a0fd3fe84e1e64736f6c634300081a0033"; // 替换为编译后的字节码

            // 估算部署所需的 gas
            var gas = await _web3.Eth.DeployContract.EstimateGasAsync(
                                abi,
                                contractByteCode,
                                _accountAddress,
                                null
                            );
            Debug.WriteLine($"Gas: {gas}");

            var receipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                               abi,
                               contractByteCode,
                               _accountAddress,
                               new HexBigInteger(gas)
                           );

            Console.WriteLine($"✅ 合约已部署，地址: {receipt.ContractAddress}");
            return receipt.ContractAddress;
        }
        // 创建新的中继合约并转账
        public async Task<relayResult> Distribute(string distributorAddress, string targetAddress, decimal amountInEth)
        {
            var contract = _web3.Eth.GetContract(
                abi,
                distributorAddress
            );

            var distributeFunction = contract.GetFunction("distribute");

            // 估算交易所需的 gas
            var gas = await distributeFunction.EstimateGasAsync(
               from: _web3.TransactionManager.Account.Address,
               gas: null,
               value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
               functionInput: new object[] { targetAddress }
           );

            var receipt = await distributeFunction.SendTransactionAndWaitForReceiptAsync(
                from: _web3.TransactionManager.Account.Address,
                gas: gas,
                value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
                functionInput: new object[] { targetAddress }
            );
            relayResult relayResult = new relayResult();
            relayResult.hex = receipt.TransactionHash;
            relayResult.relayAddress = await CalculateContractAddressAsync(distributorAddress);
            return relayResult;
        }


        public async Task<string> CalculateContractAddressAsync(string creatorAddress)
        {

            // var nonce = new BigInteger(1);
            var noncex = await _web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(creatorAddress);
            var nonce = noncex.Value - 1;

            // 1. 转换地址为字节数组
            var addressBytes = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.HexToByteArray(
                creatorAddress.StartsWith("0x") ? creatorAddress.Substring(2) : creatorAddress
            );


            // 2. 转换 nonce 为字节数组
            var nonceBytes = nonce.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (nonceBytes.Length == 0)
            {
                nonceBytes = new byte[] { 0 };
            }


            // 3. RLP 编码
            var encodedAddress = RLP.EncodeElement(addressBytes);
            var encodedNonce = RLP.EncodeElement(nonceBytes);
            var encoded = RLP.EncodeList(new[] { encodedAddress, encodedNonce });


            // 4. 计算 Keccak-256 哈希
            var hash = new Sha3Keccack().CalculateHash(encoded);


            // 5. 取最后 20 字节作为地址
            var hashHex = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(hash);
            var address = "0x" + hashHex.Substring(24);


            return address;
        }



        public class relayResult
        {
            public string relayAddress { get; set; }
            public bool success { get; set; }
            public string hex { get; set; }
        }
        // 定义事件类
        [Event("RelayCreated")]
        public class RelayCreatedEvent
        {
            [Parameter("address", "relayAddress", 1, true)]
            public string RelayAddress { get; set; }
        }
        // 使用指定的中继合约转账
        //public async Task<string> DistributeWithRelay(string distributorAddress, string relayAddress, string targetAddress, decimal amountInEth)
        //{
        //    var contract = _web3.Eth.GetContract(
        //        abi,
        //        distributorAddress
        //    );

        //    var distributeWithRelayFunction = contract.GetFunction("distributeWithRelay");


        //    var gas = await distributeWithRelayFunction.EstimateGasAsync(
        //      from: _web3.TransactionManager.Account.Address,
        //      gas: null,
        //      value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
        //      functionInput: new object[] { relayAddress, targetAddress }
        //    );

        //    var receipt = await distributeWithRelayFunction.SendTransactionAndWaitForReceiptAsync(
        //           from: _web3.TransactionManager.Account.Address,
        //           gas: gas,
        //           value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
        //           functionInput: new object[] { relayAddress, targetAddress }
        //       );
        //    return receipt.TransactionHash;
        //}

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
                    if (retryCount>=3)
                    {
                        throw;
                    }
                   retryCount++;
                }
            }
          

        }
        public async Task<string> DistributeWithRelay(string distributorAddress, string relayAddress, string targetAddress, decimal amountInEth)
        {

           int retryCount = 0;
            while (true)
            {
                try
                {
                    var contract = _web3.Eth.GetContract(abi, distributorAddress);
                    var distributeWithRelayFunction = contract.GetFunction("distributeWithRelay");
                    var senderAddress = _web3.TransactionManager.Account.Address;
                    var balance = await _GetBalance(senderAddress);
                    var gasPrice = await _GetGasPrice();

                    var estimatedGas = await distributeWithRelayFunction.EstimateGasAsync(
                       from: senderAddress,
                       gas: null,
                       value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
                       functionInput: new object[] { relayAddress, targetAddress }
                   );

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

                        Debug.WriteLine($"尝试用 gas 倍数: {multiplier}，发送金额: {Web3.Convert.FromWei(amountToSend)} MON");

                        try
                        {
                            var receipt = await distributeWithRelayFunction.SendTransactionAndWaitForReceiptAsync(
                                from: senderAddress,
                                gas: new HexBigInteger(gasLimit),
                                gasPrice: gasPrice,
                                value: new HexBigInteger(amountToSend),
                                functionInput: new object[] { relayAddress, targetAddress }
                            );
                            Debug.WriteLine("转账成功！");
                            return receipt.TransactionHash;
                            
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
                          from: _web3.TransactionManager.Account.Address,
                          gas: null,
                          value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
                          functionInput: new object[] { relayAddress, targetAddress }
                        );

                        var receipt = await distributeWithRelayFunction.SendTransactionAndWaitForReceiptAsync(
                               from: _web3.TransactionManager.Account.Address,
                               gas: gas,
                               value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
                               functionInput: new object[] { relayAddress, targetAddress }
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
                    if (retryCount >= 3)
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

        //public async Task<string> DistributeWithRelay1(string distributorAddress, string relayAddress, string targetAddress, decimal amountInEth)
        //{
        //    try
        //    {
        //        var contract = _web3.Eth.GetContract(
        //            abi,
        //            distributorAddress
        //        );

        //        var distributeWithRelayFunction = contract.GetFunction("distributeWithRelay");

        //        // 获取账户当前余额
        //           var balanceInEth = await _GetBalance(); 
        //           // 估算gas费用
        //           var gas = await distributeWithRelayFunction.EstimateGasAsync(
        //            from: _web3.TransactionManager.Account.Address,
        //            gas: null,
        //            value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
        //            functionInput: new object[] { relayAddress, targetAddress }
        //        );

        //        // 计算gas费用
        //        var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
        //        var gasCost = gas.Value * gasPrice.Value;
        //        var gasCostInEth = Web3.Convert.FromWei(gasCost);
        //        var gas20 = gasCostInEth * 1.2m;
        //        // 计算实际可转账金额（余额减去gas费用）
        //        var actualAmountInEth = balanceInEth - gas20; 
        //        var receipt = await distributeWithRelayFunction.SendTransactionAndWaitForReceiptAsync(
        //            from: _web3.TransactionManager.Account.Address,
        //            gas: gas,
        //            value: new HexBigInteger(Web3.Convert.ToWei(actualAmountInEth)),
        //            functionInput: new object[] { relayAddress, targetAddress }
        //        );

        //        return receipt.TransactionHash;
        //    }
        //    catch (Exception ex)
        //    { 
        //        throw;
        //    }
        //}
    }


    // 调用 distribute 方法
    //public async Task<string> Distribute(string recipientAddress, decimal amountInEth,string _monadDistributorAddress)
    //{
    //    // 获取合约实例
    //    var contract = _web3.Eth.GetContract(
    //        abi, // 替换为 MonadDistributor 的 ABI
    //        _monadDistributorAddress
    //    );

    //    var distributeFunction = contract.GetFunction("distribute");

    //    // 估算交易所需的 gas
    //    var gas = await distributeFunction.EstimateGasAsync(
    //       from: _web3.TransactionManager.Account.Address,
    //       gas: null,
    //       value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
    //       functionInput: new object[] { recipientAddress }
    //   );

    //    // 发送交易
    //    // 注意：这个调用会自动创建新的 RelayContract 实例
    //    var receipt = await distributeFunction.SendTransactionAndWaitForReceiptAsync(
    //        from: _web3.TransactionManager.Account.Address,
    //        gas: gas,
    //        value: new HexBigInteger(Web3.Convert.ToWei(amountInEth)),
    //        functionInput: new object[] { recipientAddress }
    //    ); 
    //    return receipt.TransactionHash;
    //}
}
