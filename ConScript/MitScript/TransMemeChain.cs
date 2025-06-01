using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;
using System;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Nethereum.Signer;
using Nethereum.JsonRpc.Client;
using NadFun.Api;
using Nethereum.RPC.Eth.DTOs;
using Detest.MitScript;
using System.Security.Cryptography.X509Certificates;
using web3script.ContractScript;
using System.Diagnostics;
using web3script.Services;
using web3script.ucontrols;

namespace NadFunTrading
{
    public class ContractAddresses
    {
        public static string CORE => Environment.GetEnvironmentVariable("CORE_CONTRACT_ADDRESS") ??
            "0x822EB1ADD41cf87C3F178100596cf24c9a6442f6";

        public static string BONDING_CURVE_FACTORY => Environment.GetEnvironmentVariable("BONDING_CURVE_FACTORY_ADDRESS") ??
            "0x60216FB3285595F4643f9f7cddAB842E799BD642";

        public static string INTERNAL_UNISWAP_V2_ROUTER => Environment.GetEnvironmentVariable("INTERNAL_UNISWAP_V2_ROUTER_ADDRESS") ??
            "0x619d07287e87C9c643C60882cA80d23C8ed44652";

        public static string INTERNAL_UNISWAP_V2_FACTORY => Environment.GetEnvironmentVariable("INTERNAL_UNISWAP_V2_FACTORY_ADDRESS") ??
            "0x13eD0D5e1567684D964469cCbA8A977CDA580827";

        public static string WRAPPED_MON => Environment.GetEnvironmentVariable("WRAPPED_MON_ADDRESS") ??
            "0x3bb9AFB94c82752E47706A10779EA525Cf95dc27";
    }

    public class TokenMarketInfo
    {
        public string market_type { get; set; }
        public string virtual_native { get; set; }
        public string virtual_token { get; set; }
        public string reserve_token { get; set; }
        public string reserve_native { get; set; }
        public string target_token { get; set; }
        public bool is_listing { get; set; }
    }

    public class TokenInfo
    {
        public bool is_listing { get; set; }
    }

    public class NadFunTradingService
    {
        private readonly Web3 _web3;
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl = "https://testnet-bot-api-server.nad.fun";

        public NadFunTradingService(string privateKey, string rpcUrl)
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
            };
            var account = new Account(privateKey,chainId:10143);
            _web3 = new Web3(account, rpcUrl);
            _httpClient = new HttpClient(handler);

           LogService.AppendLog($"初始化交易服务，账户地址：{account.Address}");
        }
        public NadFunTradingService(string privateKey, string rpcUrl,ProxyViewModel proxyViewModel)
        {
            HttpClientHandler httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
            var rpcClient = sHttpHandler.GetRpcClient(httphandler, rpcUrl);
            var account = new Account(privateKey, chainId: 10143);
            _web3 = new Web3(account, rpcClient);
            _httpClient = new HttpClient(httphandler); 
            LogService.AppendLog($"初始化交易服务，账户地址：{account.Address}");
        }
        #region Bonding Curve Operations

        /// <summary>
        /// 从债券曲线购买代币
        /// </summary>
        /// <param name="tokenAddress">要购买的代币地址</param>
        /// <param name="amount">要花费的MON(原生代币)数量</param>
        /// <returns>交易哈希</returns>
        public async Task<string> BuyFromCore(string tokenAddress, string amount)
        {
          
            var userAddress = _web3.TransactionManager.Account.Address;

            try
            {
               LogService.AppendLog($"准备从债券曲线购买代币：{tokenAddress}");
               LogService.AppendLog($"花费：{amount} MON");

                // 加载Core合约
                var coreContract = _web3.Eth.GetContract(GetCoreABI(), ContractAddresses.CORE);
                var buyFunction = coreContract.GetFunction("buy");

                // 计算金额和手续费
                var amountIn = Web3.Convert.ToWei(amount);
                // 1% fee
                var fee = (amountIn * 10) / 1000;
                var totalValue = amountIn + fee;

                // 计算20分钟后的截止时间
                var deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
                await Task.Delay(1000);
                // 估算交易所需的gas
                var estimatedGas = await buyFunction.EstimateGasAsync(
                    userAddress,
                    null,  // gas价格使用默认值
                    new HexBigInteger(totalValue),  // 发送ETH数量
                    amountIn,       // amountIn
                    fee,            // fee
                    tokenAddress,   // token
                    userAddress,    // to
                    deadline        // deadline
                );
                await Task.Delay(1000);
                // 发送交易
                var txHash = await buyFunction.SendTransactionAsync(
                    from: userAddress,
                    gas: estimatedGas,
                    value: new HexBigInteger(totalValue),  // 发送ETH数量
                    functionInput: new object[] {
                        amountIn,       // amountIn
                        fee,            // fee
                        tokenAddress,   // token
                        userAddress,    // to
                        deadline        // deadline
                    }
                );

                 LogService.AppendLog($"债券曲线购买交易已发送，等待确认哈希：{txHash}"); 
                await Task.Delay(1000); 
                var receiptService = new Nethereum.RPC.TransactionReceipts.TransactionReceiptPollingService(_web3.TransactionManager);

                // 设置超时时间（例如30秒）
                var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                try
                {
                    await Task.Delay(1000);
                    var approveReceipt = await receiptService.PollForReceiptAsync(txHash, cancellationTokenSource.Token);
                   LogService.AppendLog($"债券曲线购买交易已确认，状态：{(approveReceipt.Status.Value == 1 ? "成功" : "失败")}");
                    if (approveReceipt.Status.Value != 1)
                    {
                        throw new Exception("债券曲线购买交易失败");
                    }
                }
                catch (OperationCanceledException)
                {
                   LogService.AppendLog("等待交易确认超时");
                }
                catch (Exception ex)
                {
                   LogService.AppendLog($"等待交易确认时发生错误: {ex.Message}");
                }
                 
               
              
                return txHash;
            }
            catch (Exception ex)
            {
               LogService.AppendLog($"从债券曲线购买代币出错：{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从债券曲线购买精确数量的代币，最后一次购买会触发DEX上线
        /// </summary>
        /// <param name="tokenAddress">要购买的代币地址</param>
        /// <param name="tokensOut">确切要接收的代币数量</param>
        /// <returns>交易哈希</returns>
        public async Task<string> ExactOutBuyFromCore(string tokenAddress, BigInteger tokensOut)
        {
            var userAddress = _web3.TransactionManager.Account.Address;

            try
            {
               LogService.AppendLog($"准备从债券曲线购买精确数量的代币：{tokenAddress}");
               LogService.AppendLog($"精确购买数量：{Web3.Convert.FromWei(tokensOut)} 代币");

                // 获取代币市场信息
                var marketData = await GetTokenMarketInfo(tokenAddress);

                // 提取市场数据中的值
                var virtualNative = BigInteger.Parse(marketData.virtual_native);
                var virtualToken = BigInteger.Parse(marketData.virtual_token);

                var reserveToken = !string.IsNullOrEmpty(marketData.reserve_token) ?
                    BigInteger.Parse(marketData.reserve_token) : BigInteger.Zero;

                var targetToken = !string.IsNullOrEmpty(marketData.target_token) ?
                    BigInteger.Parse(marketData.target_token) : BigInteger.Zero;

                // 计算可用代币数量
                var availableTokens = reserveToken != BigInteger.Zero ? reserveToken - targetToken : BigInteger.Zero;

                // 检查请求的数量是否超过可用供应
                if (tokensOut > availableTokens)
                {
                    throw new Exception($"请求的代币数量 ({tokensOut}) 超过可用供应 ({availableTokens})");
                }

                // 计算所需的输入金额
                var k = virtualNative * virtualToken;
                var effectiveAmount = CalculateRequiredAmountIn(tokensOut, k, virtualNative, virtualToken);

                // 计算手续费 (1%)
                var fee = (effectiveAmount * 10) / 1000;

                // 设置截止时间(简单起见设为10秒后)
                var deadline = DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeSeconds();

                // 加载Core合约
                var coreContract = _web3.Eth.GetContract(GetCoreABI(), ContractAddresses.CORE);
                var exactOutBuyFunction = coreContract.GetFunction("exactOutBuy");

                // 估算交易所需的gas
                var estimatedGas = await exactOutBuyFunction.EstimateGasAsync(
                    userAddress,
                    null,  // gas价格使用默认值
                    new HexBigInteger(effectiveAmount + fee),  // 发送ETH数量
                    effectiveAmount + fee,  // amountInMax
                    tokensOut,           // amountOut
                    tokenAddress,         // token
                    userAddress,          // to
                    deadline              // deadline
                );

                // 为安全起见，增加20%的gas限制
                //var gasLimit = new HexBigInteger(
                //    (BigInteger)(estimatedGas.Value * 1.2)
                //);

                // 发送交易
                var txHash = await exactOutBuyFunction.SendTransactionAsync(
                    from: userAddress,
                    gas: estimatedGas,
                    value: new HexBigInteger(effectiveAmount + fee),  // 发送ETH数量
                    functionInput: new object[] {
                        effectiveAmount + fee,  // amountInMax
                        tokensOut,           // amountOut
                        tokenAddress,         // token
                        userAddress,          // to
                        deadline              // deadline
                    }
                );

               LogService.AppendLog($"精确购买交易已发送，哈希：{txHash}");
                return txHash;
            }
            catch (Exception ex)
            {
               LogService.AppendLog($"精确购买代币出错：{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 向债券曲线出售代币
        /// </summary>
        /// <param name="tokenAddress">要出售的代币地址</param>
        /// <param name="amount">要出售的代币数量</param>
        /// <returns>交易哈希</returns>
        //public async Task<string> SellToCore(string tokenAddress, string amount)
        //{
        //    var userAddress = _web3.TransactionManager.Account.Address;

        //    //try
        //    //{
        //       LogService.AppendLog($"准备向债券曲线出售代币：{tokenAddress}");
        //       LogService.AppendLog($"出售数量：{amount} 代币");

        //        var tokenContract = _web3.Eth.GetContract(GetTokenABI(), tokenAddress);
        //        var coreContract = _web3.Eth.GetContract(GetCoreABI(), ContractAddresses.CORE);

        //        var amountIn = Web3.Convert.ToWei(amount);
        //        var deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();
        //        LogService.AppendLog("检查当前代币授权");
        //        // 检查当前代币授权
        //        var allowanceFunction = tokenContract.GetFunction("allowance");
        //        var currentAllowance = await allowanceFunction.CallAsync<BigInteger>(
        //            userAddress,
        //            ContractAddresses.CORE
        //        );
        //       LogService.AppendLog("当前代币授权"+ currentAllowance);
        //        // 如果需要，授权代币
        //        if (currentAllowance < amountIn)
        //        {
        //            var approveFunction = tokenContract.GetFunction("approve");
        //            var maxApproveAmount = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129635"); // Max uint256

        //           LogService.AppendLog($"授权Core合约操作代币...{maxApproveAmount}");

        //            // 估算授权所需的gas
        //            var approveEstimatedGas = await approveFunction.EstimateGasAsync(
        //                userAddress,
        //                null,
        //                null,
        //                ContractAddresses.CORE,  // spender
        //                maxApproveAmount         // amount
        //            );

        //            // 为安全起见，增加10%的gas限制
        //            //var approveGasLimit = new HexBigInteger(
        //            //    (BigInteger)(approveEstimatedGas.Value * 1.1)
        //            //);

        //            // 发送授权交易
        //            var approveTxHash = await approveFunction.SendTransactionAsync(
        //                from: userAddress,
        //                gas: approveEstimatedGas,
        //                value: new HexBigInteger(0), // 授权不需要发送ETH
        //                functionInput: new object[] {
        //                    ContractAddresses.CORE,  // spender
        //                    maxApproveAmount         // amount
        //                }
        //            );

        //           LogService.AppendLog($"授权交易已发送，等待交易确认  哈希：{approveTxHash}");


        //            // === 等待交易确认 ===
        //            var receiptService = new Nethereum.RPC.TransactionReceipts.TransactionReceiptPollingService(_web3.TransactionManager);
        //            var approveReceipt = await receiptService.PollForReceiptAsync(approveTxHash);
        //           LogService.AppendLog($"授权交易已确认，状态：{(approveReceipt.Status.Value == 1 ? "成功" : "失败")}");

        //            if (approveReceipt.Status.Value != 1)
        //            {
        //                throw new Exception("授权交易失败");
        //            }
        //        }

        //        // 执行出售交易
        //        var sellFunction = coreContract.GetFunction("sell");
        //       LogService.AppendLog("正在估算GAS");
        //        // 估算交易所需的gas
        //        var sellEstimatedGas = await sellFunction.EstimateGasAsync(
        //            userAddress,
        //            null,
        //            new HexBigInteger(0),
        //            amountIn,        // amountIn
        //            tokenAddress,    // token
        //            userAddress,     // to
        //            deadline         // deadline
        //        );

        //        // 为安全起见，增加20%的gas限制
        //        //var sellGasLimit = new HexBigInteger(
        //        //    (BigInteger)(sellEstimatedGas.Value * 1.2)
        //        //);

        //        // 发送交易
        //        var txHash = await sellFunction.SendTransactionAsync(
        //            from: userAddress,
        //            gas: sellEstimatedGas, 
        //            value: new HexBigInteger(0), // 卖出代币不需要发送ETH
        //            functionInput: new object[] {
        //                amountIn,        // amountIn
        //                tokenAddress,    // token
        //                userAddress,     // to
        //                deadline         // deadline
        //            }
        //        );

        //       LogService.AppendLog($"出售交易已发送，哈希：{txHash}");
        //        return txHash;
        //    //}
        //    //catch (Exception ex)
        //    //{
        //    //   LogService.AppendLog($"向债券曲线出售代币出错：{ex.Message}");
        //    //    throw;
        //    //}
        //}
        /// <summary>
        /// 向债券曲线出售代币
        /// </summary>
        /// <param name="tokenAddress">要出售的代币地址</param>
        /// <param name="amount">要出售的代币数量</param>
        /// <returns>交易哈希</returns>
        public async Task<string> SellToCore(string tokenAddress, string amount)
        {
            var userAddress = _web3.TransactionManager.Account.Address;

            
               LogService.AppendLog($"准备向债券曲线出售代币：{tokenAddress}");
               LogService.AppendLog($"出售数量：{amount} 代币");

                // 首先检查代币是否在债券曲线阶段
                try
                {
                    var marketInfo = await GetTokenMarketInfo(tokenAddress);
                    if (marketInfo.market_type != "CURVE")
                    {
                       LogService.AppendLog($"警告：代币 {tokenAddress} 不在债券曲线阶段，市场类型: {marketInfo.market_type}");
                       LogService.AppendLog("尝试使用DEX出售...");
                       return await SellToDex(tokenAddress, amount, 0.5);
                    }
                }
                catch (Exception ex)
                {
                     throw;
                }

                var tokenContract = _web3.Eth.GetContract(GetTokenABI(), tokenAddress);
                var coreContract = _web3.Eth.GetContract(GetCoreABI(), ContractAddresses.CORE);
            await Task.Delay(1000);
            // 检查用户代币余额
            while (true)
            {
                int retryCount = 0;
                try
                {
                    var balanceFunction = tokenContract.GetFunction("balanceOf");
                    var userBalance = await balanceFunction.CallAsync<BigInteger>(userAddress);
                    var amountIn = Web3.Convert.ToWei(amount);

                    LogService.AppendLog($"当前代币余额：{Web3.Convert.FromWei(userBalance)}");

                    if (userBalance < amountIn)
                    {
                        throw new Exception($"余额不足。当前余额: {Web3.Convert.FromWei(userBalance)}, 需要: {amount}");
                    }
                    await Task.Delay(1000);
                    // 先授权代币，再进行gas估算
                    var allowanceFunction = tokenContract.GetFunction("allowance");
                    var currentAllowance = await allowanceFunction.CallAsync<BigInteger>(
                        userAddress,
                        ContractAddresses.CORE
                    );

                    LogService.AppendLog($"当前授权额度: {currentAllowance}");

                    // 如果需要，授权代币
                    if (currentAllowance < amountIn)
                    {
                        var approveFunction = tokenContract.GetFunction("approve");
                        var maxApproveAmount = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"); // Max uint256

                        LogService.AppendLog($"当前授权不足，正在授权Core合约 {ContractAddresses.CORE} 操作代币...");
                        await Task.Delay(1000);
                        try
                        {     //            // 估算授权所需的gas
                            var approveEstimatedGas = await approveFunction.EstimateGasAsync(
                                userAddress,
                                null,
                                null,
                                ContractAddresses.CORE,  // spender
                                maxApproveAmount         // amount
                            );
                            await Task.Delay(1000);
                            // 发送授权交易
                            var approveTxHash = await approveFunction.SendTransactionAsync(
                                    userAddress,
                                    approveEstimatedGas,
                                    new HexBigInteger(0),     // 不发送ETH
                                    ContractAddresses.CORE,   // spender
                                    maxApproveAmount          // amount
                                );

                            LogService.AppendLog($"授权交易已发送，哈希：{approveTxHash}");
                            await Task.Delay(1000);
                            // 等待授权交易被确认
                            var receiptService = new Nethereum.RPC.TransactionReceipts.TransactionReceiptPollingService(_web3.TransactionManager);
                            var approveReceipt = await receiptService.PollForReceiptAsync(approveTxHash);
                            LogService.AppendLog($"授权交易已确认，状态：{(approveReceipt.Status.Value == 1 ? "成功" : "失败")}");

                            if (approveReceipt.Status.Value != 1)
                            {
                                throw new Exception("授权交易失败");
                            }
                            while (true)
                            {
                                // 再次检查授权
                                int retryCountx = 0;
                                try
                                {
                                    if (retryCountx >= 3)
                                    {
                                        break;
                                    }
                                    await Task.Delay(3000);
                                    LogService.AppendLog("循环检查授权额度...");
                                    var allowanceFunctionx = tokenContract.GetFunction("allowance");
                                    var currentAllowancex = await allowanceFunctionx.CallAsync<BigInteger>(
                                        userAddress,
                                        ContractAddresses.CORE);
                                    LogService.AppendLog($"授权后的额度: {currentAllowancex}");
                                    if (currentAllowancex >= amountIn)
                                    {
                                        break;
                                    }
                                    await Task.Delay(3000);
                                }
                                catch (Exception e)
                                {
                                    retryCountx++;
                                    await Task.Delay(3000);
                                    LogService.AppendLog(e.Message);
                                }

                            }

                        }
                        catch (Exception ex)
                        {
                            LogService.AppendLog($"授权过程中出错: {ex.Message}");
                            throw;
                        }
                    }

                    // 设置截止时间
                    var deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();




                    try
                    {
                        await Task.Delay(1000);
                        var sellFunction = coreContract.GetFunction("sell");
                        var esgas = await sellFunction.EstimateGasAsync(
                                 from: userAddress,
                                 gas: null,
                                 value: new HexBigInteger(0), // 卖出代币不需要发送ETH
                                 functionInput: new object[] {
                    amountIn,        // amountIn
                    tokenAddress,    // token
                    userAddress,     // to
                    deadline         // deadline
                                 }
                             );

                        await Task.Delay(1000);
                        // 发送交易
                        var txHash = await sellFunction.SendTransactionAsync(
                                from: userAddress,
                                gas: esgas,
                                value: new HexBigInteger(0), // 卖出代币不需要发送ETH
                                functionInput: new object[] {
                    amountIn,        // amountIn
                    tokenAddress,    // token
                    userAddress,     // to
                    deadline         // deadline
                                }
                            );

                        LogService.AppendLog($"出售交易已发送，哈希：{txHash}");
                        return txHash;
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogService.AppendLog($"发送卖出交易失败: {ex.Message}");
                        throw;
                    }


                }
                catch (Exception)
                {
                    if (retryCount>=3)
                    {
                        throw;
                    }
                    await  Task.Delay(2000);
                    retryCount++;
                }
            }
         
                    
               
        }
        #endregion

        #region DEX Operations

        /// <summary>
        /// 从DEX购买代币（仅在代币上线后可用）
        /// </summary>
        /// <param name="tokenAddress">要购买的代币地址</param>
        /// <param name="amount">要花费的MON(原生代币)数量</param>
        /// <param name="slippage">滑点百分比(默认0.5%)</param>
        /// <returns>交易哈希</returns>
        public async Task<string> BuyFromDex(string tokenAddress, string amount, double slippage = 0.5)
        {
            var userAddress = _web3.TransactionManager.Account.Address;

            try
            {
               LogService.AppendLog($"准备从DEX购买代币：{tokenAddress}");
               LogService.AppendLog($"花费：{amount} MON，滑点：{slippage}%");

                // 检查代币是否已上线
                var tokenInfo = await GetTokenInfo(tokenAddress);

                if (!tokenInfo.is_listing)
                {
                    throw new Exception("代币尚未在DEX上线。请使用债券曲线功能进行购买。");
                }

                
                var routerContract = _web3.Eth.GetContract(GetUniswapRouterABI(), ContractAddresses.INTERNAL_UNISWAP_V2_ROUTER);

                 
                var valueInWei = Web3.Convert.ToWei(amount);

                 
                var deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();

                
                var getAmountsOutFunction = routerContract.GetFunction("getAmountsOut");
                var path = new List<string> { ContractAddresses.WRAPPED_MON, tokenAddress };
                await Task.Delay(1000);
                var amounts = await getAmountsOutFunction.CallAsync<List<BigInteger>>(
                    valueInWei,
                    path
                );

                 
                var expectedTokenAmount = amounts[1];
                var slippageFactor = 1000 - (BigInteger)Math.Floor(slippage * 10);
                var minTokens = (expectedTokenAmount * slippageFactor) / 1000;
                await Task.Delay(1000);
                
                var swapFunction = routerContract.GetFunction("swapExactNativeForTokens");
                await Task.Delay(1000);
                
                var swapEstimatedGas = await swapFunction.EstimateGasAsync(
                    userAddress,
                    null,
                    new HexBigInteger(valueInWei),  
                    minTokens,           // amountOutMin
                    path,                // path
                    userAddress,         // to
                    deadline             // deadline
                );
                await Task.Delay(1000);
                // 为安全起见，增加20%的gas限制
                //var swapGasLimit = new HexBigInteger(
                //    (BigInteger)(swapEstimatedGas.Value * 1.2)
                //);

                // 发送交易
                var txHash = await swapFunction.SendTransactionAsync(
                    from: userAddress,
                    gas: swapEstimatedGas,
                    value: new HexBigInteger(valueInWei), 
                    functionInput: new object[] {
                        minTokens,           // amountOutMin
                        path,                // path
                        userAddress,         // to
                        deadline             // deadline
                    }
                );

               LogService.AppendLog($"DEX购买交易已发送，哈希：{txHash}");
                return txHash;
            }
            catch (Exception ex)
            {
               LogService.AppendLog($"从DEX购买代币出错：{ex.Message}");
                throw;
            }
        }

      
        public async Task<string> SellToDex(string tokenAddress, string amount, double slippage = 0.5)
        {
            var userAddress = _web3.TransactionManager.Account.Address;
            int retryCountx = 0;
            while (true)
            {
                try
                {
                    LogService.AppendLog($"准备向DEX出售代币：{tokenAddress}");
                    var routerContract = _web3.Eth.GetContract(GetUniswapRouterABI(), ContractAddresses.INTERNAL_UNISWAP_V2_ROUTER);
                    var tokenContract = _web3.Eth.GetContract(GetTokenABI(), tokenAddress);

                    var deadline = DateTimeOffset.UtcNow.AddMinutes(20).ToUnixTimeSeconds();

                    var balanceFunction = tokenContract.GetFunction("balanceOf");
                    var userBalance = await balanceFunction.CallAsync<BigInteger>(userAddress);
                    // 使用 BigInteger 处理大数值
                    // LogService.AppendLog($"使用 BigInteger 处理大数值：amountIn = Web3.Convert.ToWei(amount)");
                    var amountIn = Web3.Convert.ToWei(amount);
                    LogService.AppendLog($"当前代币余额：{Web3.Convert.FromWei(userBalance)}->{userBalance} ，需要出售：{amount}-->{amountIn}");
                    if (userBalance < amountIn)
                    { throw new Exception($"余额不足。当前余额: {Web3.Convert.FromWei(userBalance)}, 需要: {amount}"); }

                    LogService.AppendLog($"检查和授权");
                    // ===== 检查和授权 =====
                    var allowanceFunction = tokenContract.GetFunction("allowance");
                    var currentAllowance = await allowanceFunction.CallAsync<BigInteger>(userAddress, ContractAddresses.INTERNAL_UNISWAP_V2_ROUTER);


                    LogService.AppendLog($"授权金额： {currentAllowance} ，需要授权：{amount} ");
                    if (currentAllowance < amountIn)
                    {
                        LogService.AppendLog($"当前授权额度不足（{currentAllowance}），开始授权最大值...");

                        var approveFunction = tokenContract.GetFunction("approve");
                        // 使用 BigInteger 的最大值
                        BigInteger maxApproval = BigInteger.Pow(2, 256) - 1;

                        var approveEstimatedGas = await approveFunction.EstimateGasAsync(
                            userAddress, null, null,
                            ContractAddresses.INTERNAL_UNISWAP_V2_ROUTER,
                            maxApproval
                        );

                        var approveTxHash = await approveFunction.SendTransactionAsync(
                            from: userAddress,
                            gas: approveEstimatedGas,
                            value: new HexBigInteger(0),
                            functionInput: new object[] {
                            ContractAddresses.INTERNAL_UNISWAP_V2_ROUTER,
                            maxApproval
                            }
                        );

                        LogService.AppendLog($"授权交易已发送，哈希：{approveTxHash}");

                        var receiptService = new Nethereum.RPC.TransactionReceipts.TransactionReceiptPollingService(_web3.TransactionManager);
                        var approveReceipt = await receiptService.PollForReceiptAsync(approveTxHash);
                        LogService.AppendLog($"授权交易确认状态：{(approveReceipt.Status.Value == 1 ? "成功" : "失败")}");

                        if (approveReceipt.Status.Value != 1)
                            throw new Exception("授权交易失败");

                        // 再次确认授权是否成功
                        int retryCount = 0;
                        while (retryCount < 3)
                        {
                            try
                            {
                                var newcurrentAllowance = await allowanceFunction.CallAsync<BigInteger>(userAddress, ContractAddresses.INTERNAL_UNISWAP_V2_ROUTER);
                                if (newcurrentAllowance > currentAllowance)
                                {
                                    break;
                                }
                                else
                                {
                                    LogService.AppendLog("授权额度仍不足，重试。");
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine($"多次授权额度检查出错：{e.Message}");
                            }
                            await Task.Delay(3000);
                            retryCount++;
                        }
                    }
                    else
                    {
                        LogService.AppendLog($"已授权足够额度：{currentAllowance}");
                    }

                    // ===== 获取最小期望金额（考虑滑点） =====
                    var getAmountsOutFunction = routerContract.GetFunction("getAmountsOut");
                    var path = new List<string> { tokenAddress, ContractAddresses.WRAPPED_MON };

                    var amounts = await getAmountsOutFunction.CallAsync<List<BigInteger>>(amountIn, path);
                    var expectedAmount = amounts[1];

                    // 使用 BigInteger 计算滑点
                    var slippageFactor = 1000 - (BigInteger)(slippage * 10);
                    var minAmount = (expectedAmount * slippageFactor) / 1000;

                    // ===== 执行Swap =====
                    var swapFunction = routerContract.GetFunction("swapExactTokensForNative");
                    var swapEstimatedGas = await swapFunction.EstimateGasAsync(
                        userAddress, null, null,
                        amountIn,
                        minAmount,
                        path,
                        userAddress,
                        deadline
                    );

                    var txHash = await swapFunction.SendTransactionAsync(
                        from: userAddress,
                        gas: swapEstimatedGas,
                        value: new HexBigInteger(0),
                        functionInput: new object[] {
                        amountIn,
                        minAmount,
                        path,
                        userAddress,
                        deadline
                        }
                    );

                    LogService.AppendLog($"DEX出售交易已发送，哈希：{txHash}");
                    return txHash;
                }
                catch (Exception ex)
                {
                    if (retryCountx>=3)
                    {
                        LogService.AppendLog($"向DEX出售代币出错：{ex.Message}");
                        throw;
                    }
                    retryCountx++;
                }



            }
       
        }
        public static BigInteger SafeToWei(string amount, int decimals = 18)
        {
            var parts = amount.Split('.');
            BigInteger integerPart = BigInteger.Parse(parts[0]) * BigInteger.Pow(10, decimals);

            BigInteger fractionalPart = 0;
            if (parts.Length > 1)
            {
                string fractional = parts[1].PadRight(decimals, '0').Substring(0, decimals); // 控制精度
                fractionalPart = BigInteger.Parse(fractional);
            }

            return integerPart + fractionalPart;
        }
        public static double SafeFromWei(BigInteger value, int decimals = 18)
        {
            // 使用 double，避免 decimal 越界
            return (double)value / Math.Pow(10, decimals);
        }
        /// <summary>
        /// 获取代币余额
        /// </summary>
        /// <param name="tokenAddress">代币地址</param>
        /// <returns>代币余额</returns>
        public async Task<BigInteger> GetTokenBalance(string tokenAddress)
        {
            var userAddress = _web3.TransactionManager.Account.Address;
            int retryCount = 0;
            while (true)
            {
                
                try
                {
                    await Task.Delay(3000);
                    var tokenContract = _web3.Eth.GetContract(GetTokenABI(), tokenAddress);
                    var balanceFunction = tokenContract.GetFunction("balanceOf");

                    var balance = await balanceFunction.CallAsync<BigInteger>(userAddress);
                    return balance;
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    retryCount++;
                    Debug.WriteLine($"获取代币余额出错，重试中...{retryCount}");

                }
            }
           
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// 获取代币市场信息
        /// </summary>
        public async Task<TokenMarketInfo> GetTokenMarketInfo(string tokenAddress)
        {
            int retryCount = 0;
            while (true)
            {
                await Task.Delay(2000);
                try
                {
                    await Task.Delay(3000);
                    var response = await _httpClient.GetAsync($"{_apiBaseUrl}/token/market/{tokenAddress}");
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var marketInfo = JsonSerializer.Deserialize<TokenMarketInfo>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    return marketInfo;
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    retryCount++;
                    Debug.WriteLine($"API请求失败，重试中...{retryCount}");

                }
            } 
        }

        /// <summary>
        /// 获取代币基本信息
        /// </summary>
        private async Task<TokenInfo> GetTokenInfo(string tokenAddress)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/token/{tokenAddress}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var tokenInfo = JsonSerializer.Deserialize<TokenInfo>(
                    content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                return tokenInfo;
            }
            catch (Exception ex)
            {
               LogService.AppendLog($"获取代币信息失败：{ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 计算债券曲线exactOutBuy所需的输入金额
        /// </summary>
        private BigInteger CalculateRequiredAmountIn(
            BigInteger tokensOut,
            BigInteger k,
            BigInteger virtualNative,
            BigInteger virtualToken)
        {
            // 公式: (k / (virtualToken - tokensOut)) - virtualNative
            return k / (virtualToken - tokensOut) - virtualNative;
        }

        #endregion

        #region ABI Methods

        // 返回Core合约的ABI
        private string GetCoreABI()
        {
            return @"[
                {
                    ""type"": ""function"",
                    ""name"": ""buy"",
                    ""inputs"": [
                        {""name"": ""amountIn"", ""type"": ""uint256""},
                        {""name"": ""fee"", ""type"": ""uint256""},
                        {""name"": ""token"", ""type"": ""address""},
                        {""name"": ""to"", ""type"": ""address""},
                        {""name"": ""deadline"", ""type"": ""uint256""}
                    ],
                    ""outputs"": [],
                    ""stateMutability"": ""payable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""exactOutBuy"",
                    ""inputs"": [
                        {""name"": ""amountInMax"", ""type"": ""uint256""},
                        {""name"": ""amountOut"", ""type"": ""uint256""},
                        {""name"": ""token"", ""type"": ""address""},
                        {""name"": ""to"", ""type"": ""address""},
                        {""name"": ""deadline"", ""type"": ""uint256""}
                    ],
                    ""outputs"": [],
                    ""stateMutability"": ""payable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""sell"",
                    ""inputs"": [
                        {""name"": ""amountIn"", ""type"": ""uint256""},
                        {""name"": ""token"", ""type"": ""address""},
                        {""name"": ""to"", ""type"": ""address""},
                        {""name"": ""deadline"", ""type"": ""uint256""}
                    ],
                    ""outputs"": [],
                    ""stateMutability"": ""nonpayable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""getCurveData"",
                    ""inputs"": [
                        {""name"": ""_factory"", ""type"": ""address""},
                        {""name"": ""token"", ""type"": ""address""}
                    ],
                    ""outputs"": [
                        {""name"": ""curve"", ""type"": ""address""},
                        {""name"": ""virtualNative"", ""type"": ""uint256""},
                        {""name"": ""virtualToken"", ""type"": ""uint256""},
                        {""name"": ""k"", ""type"": ""uint256""}
                    ],
                    ""stateMutability"": ""view""
                }
            ]";
        }

        // 返回Token合约的ABI
        private string GetTokenABI()
        {
            return @"[
                {
                    ""type"": ""function"",
                    ""name"": ""approve"",
                    ""inputs"": [
                        {""name"": ""spender"", ""type"": ""address""},
                        {""name"": ""value"", ""type"": ""uint256""}
                    ],
                    ""outputs"": [
                        {""name"": """", ""type"": ""bool""}
                    ],
                    ""stateMutability"": ""nonpayable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""allowance"",
                    ""inputs"": [
                        {""name"": ""owner"", ""type"": ""address""},
                        {""name"": ""spender"", ""type"": ""address""}
                    ],
                    ""outputs"": [
                        {""name"": """", ""type"": ""uint256""}
                    ],
                    ""stateMutability"": ""view""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""balanceOf"",
                    ""inputs"": [
                        {""name"": ""account"", ""type"": ""address""}
                    ],
                    ""outputs"": [
                        {""name"": """", ""type"": ""uint256""}
                    ],
                    ""stateMutability"": ""view""
                }
            ]";
        }

        // 返回Uniswap Router的ABI
        private string GetUniswapRouterABI()
        {
            return @"[
                {
                    ""type"": ""function"",
                    ""name"": ""WNATIVE"",
                    ""inputs"": [],
                    ""outputs"": [
                        {""name"": """", ""type"": ""address""}
                    ],
                    ""stateMutability"": ""view""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""swapExactNativeForTokens"",
                    ""inputs"": [
                        {""name"": ""amountOutMin"", ""type"": ""uint256""},
                        {""name"": ""path"", ""type"": ""address[]""},
                        {""name"": ""to"", ""type"": ""address""},
                        {""name"": ""deadline"", ""type"": ""uint256""}
                    ],
                    ""outputs"": [
                        {""name"": ""amounts"", ""type"": ""uint256[]""}
                    ],
                    ""stateMutability"": ""payable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""swapExactTokensForNative"",
                    ""inputs"": [
                        {""name"": ""amountIn"", ""type"": ""uint256""},
                        {""name"": ""amountOutMin"", ""type"": ""uint256""},
                        {""name"": ""path"", ""type"": ""address[]""},
                        {""name"": ""to"", ""type"": ""address""},
                        {""name"": ""deadline"", ""type"": ""uint256""}
                    ],
                    ""outputs"": [
                        {""name"": ""amounts"", ""type"": ""uint256[]""}
                    ],
                    ""stateMutability"": ""nonpayable""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""getAmountsOut"",
                    ""inputs"": [
                        {""name"": ""amountIn"", ""type"": ""uint256""},
                        {""name"": ""path"", ""type"": ""address[]""}
                    ],
                    ""outputs"": [
                        {""name"": ""amounts"", ""type"": ""uint256[]""}
                    ],
                    ""stateMutability"": ""view""
                }
            ]";
        }

        // 返回BondingCurveFactory的ABI
        private string GetBondingCurveFactoryABI()
        {
            return @"[
                {
                    ""type"": ""function"",
                    ""name"": ""getCurve"",
                    ""inputs"": [
                        {""name"": ""token"", ""type"": ""address""}
                    ],
                    ""outputs"": [
                        {""name"": ""curve"", ""type"": ""address""}
                    ],
                    ""stateMutability"": ""view""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""getConfig"",
                    ""inputs"": [],
                    ""outputs"": [
                        {
                            ""name"": """",
                            ""type"": ""tuple"",
                            ""components"": [
                                {""name"": ""deployFee"", ""type"": ""uint256""},
                                {""name"": ""listingFee"", ""type"": ""uint256""},
                                {""name"": ""tokenTotalSupply"", ""type"": ""uint256""},
                                {""name"": ""virtualNative"", ""type"": ""uint256""},
                                {""name"": ""virtualToken"", ""type"": ""uint256""},
                                {""name"": ""k"", ""type"": ""uint256""},
                                {""name"": ""targetToken"", ""type"": ""uint256""},
                                {""name"": ""feeNumerator"", ""type"": ""uint16""},
                                {""name"": ""feeDenominator"", ""type"": ""uint8""}
                            ]
                        }
                    ],
                    ""stateMutability"": ""view""
                }
            ]";
        }

        // 返回UniswapV2Factory的ABI
        private string GetUniswapFactoryABI()
        {
            return @"[
                {
                    ""type"": ""function"",
                    ""name"": ""getPair"",
                    ""inputs"": [
                        {""name"": ""tokenA"", ""type"": ""address""},
                        {""name"": ""tokenB"", ""type"": ""address""}
                    ],
                    ""outputs"": [
                        {""name"": ""pair"", ""type"": ""address""}
                    ],
                    ""stateMutability"": ""view""
                },
                {
                    ""type"": ""function"",
                    ""name"": ""createPair"",
                    ""inputs"": [
                        {""name"": ""tokenA"", ""type"": ""address""},
                        {""name"": ""tokenB"", ""type"": ""address""}
                    ],
                    ""outputs"": [
                        {""name"": ""pair"", ""type"": ""address""}
                    ],
                    ""stateMutability"": ""nonpayable""
                }
            ]";
        }

        #endregion
    }

    /// <summary>
    /// 交易示例
    /// </summary>
    public class TradingExample
    {
       
        public  static  async Task<ConScriptResult> RunExampleAsync(string privateKey,string tokenAddress,string type, string amount="0.1")
        {
            string rpcUrl = "https://testnet-rpc.monad.xyz";
            try
            { 
                // 初始化交易服务
                var tradingService = new NadFunTradingService(privateKey, rpcUrl);

               LogService.AppendLog($"正在获取代币 {tokenAddress} 的市场信息...");

                
                // 获取代币市场信息
              
                var tokenMarketInfo = await tradingService.GetTokenMarketInfo(tokenAddress); 

                // 判断代币是在债券曲线阶段还是DEX阶段
                if (tokenMarketInfo.market_type == "CURVE")
                {
                   LogService.AppendLog("代币处于债券曲线阶段"); 
                     
                    if (true)
                    { 
                        switch (type)
                        {
                            case"buy":
                                try
                                {
                                    await Task.Delay(1000);
                                    int retryCount = 0;
                                    var tokenoldbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                    LogService.AppendLog($"当前余额{tokenoldbanlance}");
                                    await Task.Delay(1000);
                                    var buyTxHash = await tradingService.BuyFromCore(tokenAddress, amount);
                                    while (true)
                                    {
                                        try
                                        {
                                            if (retryCount > 3)
                                            {
                                                return new ConScriptResult { Success = false, Hex = buyTxHash, ErrorMessage = "购买超时" };
                                            }
                                            await Task.Delay(1000);
                                            var tokennewbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                            if (tokenoldbanlance < tokennewbanlance)
                                            {
                                                LogService.AppendLog($"购买成功，当前余额{tokennewbanlance}");
                                                return new ConScriptResult { Success = true, Hex = buyTxHash };
                                            }
                                            retryCount++;
                                            await Task.Delay(2000);
                                        }
                                        catch (Exception)
                                        {
                                            if (retryCount > 3)
                                            {
                                                return new ConScriptResult { Success = false, Hex = buyTxHash, ErrorMessage = "购买异常" };
                                            }

                                        }
                                       
                                    }
                                }
                                catch (Exception e)
                                {
                                    return new ConScriptResult { Success = false, ErrorMessage =e.Message };
                                    
                                }  
                                break;
                            case"sell":

                                try
                                {
                                    await Task.Delay(1000);
                                    var tokenBalance = await tradingService.GetTokenBalance(tokenAddress);
                                    var bawei = Web3.Convert.FromWei(tokenBalance); 
                                    var sellTxHash =  await tradingService.SellToCore(tokenAddress, bawei.ToString());
                                    return new ConScriptResult { Success = true, Hex = sellTxHash };
                                }
                                catch (Exception e)
                                { 
                                    return new ConScriptResult { Success = false, ErrorMessage = e.Message };
                                }
                              
                                break;
                            default:
                                break;
                        }
                        
                    }
                    else
                    {
                       LogService.AppendLog("债券曲线中没有可用代币。代币可能已在DEX上线。");
                    }
                }
                else if (tokenMarketInfo.market_type == "DEX")
                {
                   LogService.AppendLog("代币已在DEX上线"); 
                    switch (type)
                    {
                        case "buy":

                            try
                            {
                                int retryCount = 0;
                                var tokenoldbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                LogService.AppendLog($"当前余额{tokenoldbanlance}");
                                var buyTxHash = await tradingService.BuyFromDex(tokenAddress, amount, 0.5);
                                while (true)
                                {
                                    if (retryCount > 3)
                                    {
                                        return new ConScriptResult { Success = false, Hex = buyTxHash, ErrorMessage = "购买超时" };
                                        break;
                                    }
                                    var tokennewbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                    if (tokenoldbanlance < tokennewbanlance)
                                    {
                                        LogService.AppendLog($"购买成功，当前余额{Web3.Convert.FromWei(tokennewbanlance)}");
                                        return new ConScriptResult { Success = true,Hex = buyTxHash }; //return new ConScriptResult { Success = true, ErrorMessage = e.Message };
                                        break;
                                    }
                                    await Task.Delay(2000);
                                }
                            }
                            catch (Exception e)
                            { 
                                return new ConScriptResult { Success = false, ErrorMessage = e.Message };
                            } 
                            break;
                        case "sell":
                            try
                            {
                                // var tokenBalance = await tradingService.GetTokenBalance(tokenAddress); 
                                var tokenBalance = await tradingService.GetTokenBalance(tokenAddress);
                                var bawei = Web3.Convert.FromWei(tokenBalance);
                                var sellTxHash = await tradingService.SellToDex(tokenAddress, bawei.ToString(), 0.5);
                                return new ConScriptResult { Success = true, Hex = sellTxHash };
                            }
                            catch (Exception e)
                            {

                                return new ConScriptResult { Success = false, ErrorMessage = e.Message }; ;
                            }
                           
                            break;
                        default:
                            break;
                    }
                    //if (tokenBalance > BigInteger.Zero)
                    //{
                       

                    //    // 出售90%的代币余额，保留一些以备将来使用
                    //    var amountToSell = (tokenBalance * 90) / 100;
                    //    var amountToSellFormatted = Web3.Convert.FromWei(amountToSell).ToString(); 
                    //   LogService.AppendLog($"准备出售 {amountToSellFormatted} 代币 (当前余额的50%)"); 
                    //}
                    //else
                    //{
                    //   LogService.AppendLog("代币余额为零，无法执行出售操作");
                    //}
                    // 向DEX出售代币
                    //if (Console.ReadLine()=="s")
                    //{
                    //   LogService.AppendLog("输入s卖DEX");
                    //    var sellTxHash = await tradingService.SellToDex(tokenAddress, tokenBalance.ToString(), 0.5);
                    //   LogService.AppendLog($"DEX出售交易哈希: {sellTxHash}");
                    //}
                 
                }
            }
            catch (Exception ex)
            {
               LogService.AppendLog($"错误: {ex.Message}");
               LogService.AppendLog(ex.StackTrace);
                return new ConScriptResult { Success = false, ErrorMessage = ex.Message };
            }
            return new ConScriptResult { Success = false, ErrorMessage = "未知错误" };
        }
        public static async Task<ConScriptResult> RunExampleAsync(string privateKey, string tokenAddress, string type, ProxyViewModel proxyViewModel, string amount = "0.1")
        {
            string rpcUrl = "https://testnet-rpc.monad.xyz";
            HttpClientHandler httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
            var rpcClient = sHttpHandler.GetRpcClient(httphandler, rpcUrl);


            try
            {
                // 初始化交易服务
                var tradingService = new NadFunTradingService(privateKey, rpcUrl, proxyViewModel);
                var account = new Account(privateKey, chainId: 10143);
                LogService.AppendLog($"[{account.Address}]正在获取代币 {tokenAddress} 的市场信息..."); 
                // 获取代币市场信息

                var tokenMarketInfo = await tradingService.GetTokenMarketInfo(tokenAddress);
                LogService.AppendLog($"[{account.Address}]获取代币 {tokenAddress} 的市场信息成功");
                // 判断代币是在债券曲线阶段还是DEX阶段
                if (tokenMarketInfo.market_type == "CURVE")
                {
                    LogService.AppendLog("代币处于债券曲线阶段");

                    //// 检查可用代币
                    //var reserveToken = !string.IsNullOrEmpty(tokenMarketInfo.reserve_token) ?
                    //    BigInteger.Parse(tokenMarketInfo.reserve_token) : BigInteger.Zero;

                    //var targetToken = !string.IsNullOrEmpty(tokenMarketInfo.target_token) ?
                    //    BigInteger.Parse(tokenMarketInfo.target_token) : BigInteger.Zero;

                    //var availableTokens = reserveToken - targetToken;

                    //LogService.AppendLog($"可用代币数量: {Web3.Convert.FromWei(availableTokens)}"); 

                    if (true)
                    {
                        switch (type)
                        {
                            case "buy":
                                try
                                {
                                    await Task.Delay(1000);
                                    int retryCount = 0;
                                    var tokenoldbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                    LogService.AppendLog($"[{account.Address}]当前余额{tokenoldbanlance}");
                                    await Task.Delay(1000);
                                    var buyTxHash = await tradingService.BuyFromCore(tokenAddress, amount);
                                    while (true)
                                    {
                                        if (retryCount > 3)
                                        {
                                            return new ConScriptResult { Success = false, Hex = buyTxHash, ErrorMessage = "购买超时" };
                                        }
                                        await Task.Delay(1000);
                                        var tokennewbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                        if (tokenoldbanlance < tokennewbanlance)
                                        {
                                            LogService.AppendLog($"[{account.Address}]购买成功，当前余额{tokennewbanlance}");
                                            return new ConScriptResult { Success = true, Hex = buyTxHash };
                                        }
                                        retryCount++;
                                        await Task.Delay(2000);
                                    }
                                }
                                catch (Exception e)
                                {
                                    LogService.AppendLog($"[{account.Address}]购买失败， 错误信息：{e.Message}");
                                    return new ConScriptResult { Success = false, ErrorMessage = e.Message };

                                }
                                break;
                            case "sell":

                                try
                                {
                                    await Task.Delay(1000);
                                    LogService.AppendLog($"[{account.Address}] 开始出售[{tokenAddress}]");
                                    var tokenBalance = await tradingService.GetTokenBalance(tokenAddress);
                                    var bawei = Web3.Convert.FromWei(tokenBalance);
                                    var sellTxHash = await tradingService.SellToCore(tokenAddress, bawei.ToString());
                                    LogService.AppendLog($"[{account.Address}] 开始出售[{tokenAddress}]成功，交易哈希：{sellTxHash}");
                                    return new ConScriptResult { Success = true, Hex = sellTxHash };
                                }
                                catch (Exception e)
                                {
                                    LogService.AppendLog($"[{account.Address}] 开始出售[{tokenAddress}]失败，错误信息：{e.Message}");
                                    return new ConScriptResult { Success = false, ErrorMessage = e.Message };
                                }

                                break;
                            default:
                                break;
                        }



                        // 选项2: 购买精确数量的代币(用于购买剩余代币)
                        //if (availableTokens <= Web3.Convert.ToWei(1000))
                        //{
                        //    // 如果只剩少量代币，购买全部以触发DEX上线
                        //   LogService.AppendLog("剩余代币较少，尝试购买所有剩余代币以触发DEX上线");
                        //    var exactBuyTxHash = await tradingService.ExactOutBuyFromCore(
                        //        tokenAddress,
                        //        availableTokens
                        //    );
                        //   LogService.AppendLog($"精确购买交易哈希: {exactBuyTxHash}");
                        //   LogService.AppendLog("代币现在应该已在DEX上线!");
                        //}


                    }
                    else
                    {
                        LogService.AppendLog("债券曲线中没有可用代币。代币可能已在DEX上线。");
                    }
                }
                else if (tokenMarketInfo.market_type == "DEX")
                {
                    LogService.AppendLog("代币已在DEX上线");
                    switch (type)
                    {
                        case "buy":

                            try
                            {
                                int retryCount = 0;
                                var tokenoldbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                LogService.AppendLog($"当前余额{tokenoldbanlance}");
                                var buyTxHash = await tradingService.BuyFromDex(tokenAddress, amount, 0.5);
                                while (true)
                                {
                                    if (retryCount > 3)
                                    {
                                        return new ConScriptResult { Success = false, Hex = buyTxHash, ErrorMessage = "购买超时" };
                                        break;
                                    }
                                    var tokennewbanlance = await tradingService.GetTokenBalance(tokenAddress);
                                    if (tokenoldbanlance < tokennewbanlance)
                                    {
                                        LogService.AppendLog($"购买成功，当前余额{Web3.Convert.FromWei(tokennewbanlance)}");
                                        return new ConScriptResult { Success = true, Hex = buyTxHash }; //return new ConScriptResult { Success = true, ErrorMessage = e.Message };
                                        break;
                                    }
                                    await Task.Delay(2000);
                                }
                            }
                            catch (Exception e)
                            {
                                return new ConScriptResult { Success = false, ErrorMessage = e.Message };
                            }
                            break;
                        case "sell":
                            try
                            {
                                // var tokenBalance = await tradingService.GetTokenBalance(tokenAddress); 
                                var tokenBalance = await tradingService.GetTokenBalance(tokenAddress);
                                var bawei = Web3.Convert.FromWei(tokenBalance);
                                var sellTxHash = await tradingService.SellToDex(tokenAddress, bawei.ToString(), 0.5);
                                return new ConScriptResult { Success = true, Hex = sellTxHash };
                            }
                            catch (Exception e)
                            {

                                return new ConScriptResult { Success = false, ErrorMessage = e.Message }; ;
                            }

                            break;
                        default:
                            break;
                    }
                    //if (tokenBalance > BigInteger.Zero)
                    //{


                    //    // 出售90%的代币余额，保留一些以备将来使用
                    //    var amountToSell = (tokenBalance * 90) / 100;
                    //    var amountToSellFormatted = Web3.Convert.FromWei(amountToSell).ToString(); 
                    //   LogService.AppendLog($"准备出售 {amountToSellFormatted} 代币 (当前余额的50%)"); 
                    //}
                    //else
                    //{
                    //   LogService.AppendLog("代币余额为零，无法执行出售操作");
                    //}
                    // 向DEX出售代币
                    //if (Console.ReadLine()=="s")
                    //{
                    //   LogService.AppendLog("输入s卖DEX");
                    //    var sellTxHash = await tradingService.SellToDex(tokenAddress, tokenBalance.ToString(), 0.5);
                    //   LogService.AppendLog($"DEX出售交易哈希: {sellTxHash}");
                    //}

                }
            }
            catch (Exception ex)
            {
                LogService.AppendLog($"错误: {ex.Message}");
                LogService.AppendLog(ex.StackTrace);
                return new ConScriptResult { Success = false, ErrorMessage = ex.Message };
            }
            return new ConScriptResult { Success = false, ErrorMessage = "未知错误" };
        }
    }

    
}
public class FunCoin
{
    public string Address { get; set; }
    public string Balance { get; set; } 
    public string nouunreal { get; set; }
}