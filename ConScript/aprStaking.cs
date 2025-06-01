using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.JsonRpc.Client;
using web3script.ContractScript;
using System.Security.Policy;
using System.Net.Http;
using System.Diagnostics;
using web3script.ucontrols;
using web3script.Services;

namespace Monad_TestNet_Script
{
    // 提款请求事件DTO
    [Event("RedeemRequest")]
    public class RedeemRequestEventDTO : IEventDTO
    {
        [Parameter("address", "controller", 1, true)]
        public string Controller { get; set; }

        [Parameter("address", "owner", 2, true)]
        public string Owner { get; set; }

        [Parameter("uint256", "requestId", 3, true)]
        public BigInteger RequestId { get; set; }

        [Parameter("address", "sender", 4, false)]
        public string Sender { get; set; }

        [Parameter("uint256", "shares", 5, false)]
        public BigInteger Shares { get; set; }

        [Parameter("uint256", "assets", 6, false)]
        public BigInteger Assets { get; set; }
    }

    public class PendingWithdrawalRequest
    {
        [Parameter("uint256", "requestId", 1)]
        public BigInteger RequestId { get; set; }

        [Parameter("address", "controller", 2)]
        public string Controller { get; set; }

        [Parameter("address", "owner", 3)]
        public string Owner { get; set; }

        [Parameter("uint256", "epoch", 4)]
        public BigInteger Epoch { get; set; }

        [Parameter("uint256", "burnableShares", 5)]
        public BigInteger BurnableShares { get; set; }

        [Parameter("uint256", "pendingAmount", 6)]
        public BigInteger PendingAmount { get; set; }

        [Parameter("bool", "isRedeemable", 7)]
        public bool IsRedeemable { get; set; }
    }

    [FunctionOutput]
    public class PendingWithdrawalAmountsOutput : IFunctionOutputDTO
    {
        [Parameter("uint256", "_totalWithdrawalAmount", 1)]
        public BigInteger TotalWithdrawalAmount { get; set; }

        [Parameter("uint256", "_totalBurnableShares", 2)]
        public BigInteger TotalBurnableShares { get; set; }

        [Parameter("uint256", "_nextRequestId", 3)]
        public BigInteger NextRequestId { get; set; }

        [Parameter("uint256", "_pendingDeposit", 4)]
        public BigInteger PendingDeposit { get; set; }

        [Parameter("uint256", "_blockNumber", 5)]
        public BigInteger BlockNumber { get; set; }
    }

    [FunctionOutput]
    public class PendingWithdrawalRequestsOutput : IFunctionOutputDTO
    {
        [Parameter("tuple[]", "", 1)]
        public List<PendingWithdrawalRequest> Requests { get; set; }
    }
    // 提款请求结构
    [FunctionOutput]
    public class RedeemRequest
    {
        [Parameter("uint256", "shares", 1)]
        public BigInteger Shares { get; set; }

        [Parameter("address", "controller", 2)]
        public string Controller { get; set; }

        [Parameter("uint256", "assets", 3)]
        public BigInteger Assets { get; set; }

        [Parameter("bool", "claimed", 4)]
        public bool Claimed { get; set; }

        [Parameter("uint256", "timestamp", 5)]
        public BigInteger Timestamp { get; set; }
    }
    public class aprstaking
    {
        private readonly string rpcUrl = "https://testnet-rpc.monad.xyz";
        private readonly Web3 _web3;
        private readonly Contract _contract;
        private readonly string _contractAddress = "0xb2f82D0f38dc453D596Ad40A37799446Cc89274A"; // 假设地址，请替换为实际合约地址
       
        public aprstaking(string privateKey,ProxyViewModel proxyViewModel)
        {
            var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
            var rpcClient = sHttpHandler.GetRpcClient(httphandler, rpcUrl);

            var account = new Account(privateKey);
            _web3 = new Web3(account, rpcClient);
            _contract = _web3.Eth.GetContract(GetAbi(), _contractAddress);
        }
        public aprstaking(string privateKey)
        {
            
            var account = new Account(privateKey);
            _web3 = new Web3(account,rpcUrl);
            _contract = _web3.Eth.GetContract(GetAbi(), _contractAddress);
        }
        public aprstaking()
        {

          
            _web3 = new Web3(rpcUrl);
            _contract = _web3.Eth.GetContract(GetAbi(), _contractAddress);
        }
        private string GetAbi()
        {
            // 从ABI.json文件中读取的实际ABI
            return @"[
        {""type"":""function"",""name"":""deposit"",""inputs"":[{""name"":""assets"",""type"":""uint256"",""internalType"":""uint256""},{""name"":""receiver"",""type"":""address"",""internalType"":""address""}],""outputs"":[{""name"":""shares"",""type"":""uint256"",""internalType"":""uint256""}],""stateMutability"":""payable""},
        {""type"":""function"",""name"":""balanceOf"",""inputs"":[{""name"":""account"",""type"":""address"",""internalType"":""address""}],""outputs"":[{""name"":"""",""type"":""uint256"",""internalType"":""uint256""}],""stateMutability"":""view""},
        {""type"":""function"",""name"":""requestRedeem"",""inputs"":[{""name"":""shares"",""type"":""uint256"",""internalType"":""uint256""},{""name"":""controller"",""type"":""address"",""internalType"":""address""},{""name"":""owner"",""type"":""address"",""internalType"":""address""}],""outputs"":[{""name"":""requestId"",""type"":""uint256"",""internalType"":""uint256""}],""stateMutability"":""nonpayable""},
        {""type"":""function"",""name"":""withdrawalWaitTime"",""inputs"":[],""outputs"":[{""name"":"""",""type"":""uint256"",""internalType"":""uint256""}],""stateMutability"":""view""},
        {""type"":""function"",""name"":""redeem"",""inputs"":[{""name"":""requestIDs"",""type"":""uint256[]"",""internalType"":""uint256[]""},{""name"":""receiver"",""type"":""address"",""internalType"":""address""}],""outputs"":[],""stateMutability"":""nonpayable""},
        {""type"": ""function"",""name"": ""getPendingWithdrawalAmounts"",""inputs"": [],""outputs"": [{""name"": ""_totalWithdrawalAmount"",""type"": ""uint256"",""internalType"": ""uint256""},{""name"": ""_totalBurnableShares"",""type"": ""uint256"",""internalType"": ""uint256""},{""name"": ""_nextRequestId"",""type"": ""uint256"",""internalType"": ""uint256""},{""name"": ""_pendingDeposit"",""type"": ""uint256"",""internalType"": ""uint256""},{""name"": ""_blockNumber"",""type"": ""uint256"",""internalType"": ""uint256""}],""stateMutability"": ""view""}
        ]"; 
        }
 
        //
        public async Task<ConScriptResult> DepositAsync(decimal amountInEth, string receiver)
        {
            try
            {
                var depositFunc = _contract.GetFunction("deposit");

                var weiAmount = Web3.Convert.ToWei(amountInEth);

                // 自动估算 Gas
                var estimatedGas = await depositFunc.EstimateGasAsync(
                    from: _web3.TransactionManager.Account.Address,
                    gas: null,
                    value: new HexBigInteger(weiAmount),
                    functionInput: new object[] { weiAmount, receiver }
                );

                var tx = await depositFunc.SendTransactionAndWaitForReceiptAsync(
                    from: _web3.TransactionManager.Account.Address,
                    gas: estimatedGas,
                    value: new HexBigInteger(weiAmount),
                    functionInput: new object[] { weiAmount, receiver });
                var actualBalance = (decimal)weiAmount / (decimal)Math.Pow(10, 18);

                return new ConScriptResult { Success = true, Hex = tx.TransactionHash };
            }
            catch (Exception ex)
            {
                return new ConScriptResult { Success = false, Hex = "", ErrorMessage = ex.Message };

            }
          
            
        }
        public async Task<BigInteger> GetBalanceOfSharesAsync( string address)
        { 
            var balanceOfFunc =_contract.GetFunction("balanceOf");
            return await balanceOfFunc.CallAsync<BigInteger>(address);
        } 
 
        public async Task<BigInteger> GetWaitTimeAsync()
        {
           
            var func = _contract.GetFunction("withdrawalWaitTime");
            return await func.CallAsync<BigInteger>();
        }
        // 请求提款并获取requestId
        public async Task<BigInteger> RequestRedeemAsync(BigInteger shares, string controller, string owner)
        {
            var requestRedeemFunction = _contract.GetFunction("requestRedeem");
            // var gas = await requestRedeemFunction.EstimateGasAsync(shares, controller, owner);
            var egas = await requestRedeemFunction.EstimateGasAsync(
            from: _web3.TransactionManager.Account.Address,
            gas: null,
            value: null,
            functionInput: new object[] { shares, controller, owner }
        );
            // 发送交易并等待收据
            var receipt = await requestRedeemFunction.SendTransactionAndWaitForReceiptAsync(
                from: _web3.TransactionManager.Account.Address,
                gas: egas,
                gasPrice: null,
                value: null,
                functionInput: new object[] { shares, controller, owner }
            );

            // 解析事件以获取requestId
            var redeemRequestEvents = receipt.DecodeAllEvents<RedeemRequestEventDTO>();
            if (redeemRequestEvents.Count > 0)
            {
                return redeemRequestEvents[0].Event.RequestId;
            }

            throw new Exception("未能从交易收据中获取requestId");
        }

        public async Task<List<BigInteger>> GetRedeemableRequestIdsAsync( string account)
        {
            // 获取函数对象
            var func = _contract.GetFunction("getPendingWithdrawalAmounts");
          
            var result = await func.CallAsync<List<BigInteger>>();

            var nextRequestId = result[2]; // 第三个返回值是 _nextRequestId
            Debug.WriteLine(nextRequestId.ToString());

            var output = await func.CallDeserializingToObjectAsync<PendingWithdrawalRequestsOutput>();

            return output.Requests
                .Where(r => r.IsRedeemable)
                .Select(r => r.RequestId)
                .ToList();
        }

        public async Task RedeemAllAsync( string account)
        {
            var redeemableIds = await GetRedeemableRequestIdsAsync(account);
            if (redeemableIds.Count == 0)
            {
                Debug.WriteLine("无可赎回请求");
                return;
            }
            else
            {
                Debug.WriteLine($"有 {redeemableIds.Count} 个可赎回请求");
            } 
            var redeemFunc = _contract.GetFunction("redeem");

            var gas = await redeemFunc.EstimateGasAsync(account, null, null, redeemableIds, account);

            var receipt = await redeemFunc.SendTransactionAndWaitForReceiptAsync(account, gas, null, null, redeemableIds, account);
            Debug.WriteLine($"redeem 成功, tx: {receipt.TransactionHash}");
        }




        public async Task TestAsync()
        {

            // 2. 合约地址和 ABI
            string contractAddress = _contractAddress;

            // 3. 获取合约函数
            var contract = _web3.Eth.GetContract(@"[
                      {
                        ""type"":""function"",
                        ""name"":""redeem"",
                        ""inputs"":[
                          {""name"":""requestIDs"",""type"":""uint256[]""},
                          {""name"":""receiver"",""type"":""address""}
                        ],
                        ""outputs"":[],
                        ""stateMutability"":""nonpayable""
                      }
                    ]", contractAddress);

            var redeemFunction = contract.GetFunction("redeem");

            // 4. 参数准备
            BigInteger[] requestIds = new BigInteger[] { 3563750 };  // 替换为你要赎回的 ID
            string receiver = _web3.Eth.TransactionManager.Account.Address; // 或任何地址

            // 5. 估算 Gas
            HexBigInteger gas = null;
            try
            {
                gas = await redeemFunction.EstimateGasAsync(receiver, null, null, requestIds, receiver);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Gas 估算失败: {ex.Message}");
                return;
            }
                Debug.WriteLine(gas.ToString());
            return;
             

        }
        // 执行提款
        public async Task RedeemAsync(BigInteger requestId, string receiver)
        {
            var redeemFunction = _contract.GetFunction("redeem");

            // 将 requestId 包装为数组（因为合约参数是 uint256[]）
            var requestIds = new BigInteger[] { requestId };

            try
            {
                // 预估 Gas
                var esgas = await redeemFunction.EstimateGasAsync(
                    from: _web3.TransactionManager.Account.Address,
                    gas: null,
                    value: null,
                    functionInput: new object[] { requestIds, receiver }
                );

                // 发送交易并等待收据
                var receipt = await redeemFunction.SendTransactionAndWaitForReceiptAsync(
                    from: _web3.TransactionManager.Account.Address,
                    gas: esgas,
                    gasPrice: null,
                    value: null,
                    functionInput: new object[] { requestIds, receiver }
                );

                Debug.WriteLine($"{receiver}提款交易已完成，交易哈希: {receipt.TransactionHash}");
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                Debug.WriteLine($"合约返回错误: {ex.Message ?? "Reverted without message"}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"发生异常: {ex.Message}");
            }
        }
        //// 执行提款
        //public async Task RedeemAsync(BigInteger requestId, string receiver)
        //{
        //    var redeemFunction = _contract.GetFunction("redeem");

        //    var esgas = await redeemFunction.EstimateGasAsync(
        //      from: _web3.TransactionManager.Account.Address,
        //      gas: null,
        //      value: null,
        //      functionInput: new object[] { requestId, receiver }
        //  );
        //    // 发送交易并等待收据
        //    var receipt = await redeemFunction.SendTransactionAndWaitForReceiptAsync(
        //        from: _web3.TransactionManager.Account.Address,
        //        gas: esgas,
        //        gasPrice: null,
        //        value: null,
        //        functionInput: new object[] { requestId, receiver }
        //    );

        //    Debug.WriteLine($"提款交易已完成，交易哈希: {receipt.TransactionHash}");
        //}

        public async Task<bool> StakeMon(decimal amount)
        {


            try
            {
                var depositFunction = _contract.GetFunction("depositMon");
                var weiAmount = Web3.Convert.ToWei(amount);
                // var function = _contract.GetFunction("depositMon");
                // 自动估算 Gas
                var estimatedGas = await depositFunction.EstimateGasAsync(
                    from: _web3.TransactionManager.Account.Address,
                    gas: null,
                    value: new HexBigInteger(weiAmount)
                );
                var txHash = await depositFunction.SendTransactionAsync(
                    from: _web3.TransactionManager.Account.Address,
                    gas: new HexBigInteger(estimatedGas),
                    value: new HexBigInteger(weiAmount)
                );
                var receipt = await _web3.TransactionManager.TransactionReceiptService
                      .PollForReceiptAsync(txHash);

                if (receipt.Status.Value == 1) { Debug.WriteLine(txHash); return true;  }
            }
            catch (Exception e ) 
            {
                Debug.WriteLine(e.Message);
                return false;
            }
          return false;
           
        }
 

        public string GetPublicAddressFromPrivateKey(string privateKey)
        {
            var key = new Nethereum.Signer.EthECKey(privateKey);
            return key.GetPublicAddress();
        }

       
    }
}