using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.StandardTokenEIP20;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System.Diagnostics;
using System.Numerics;
using System.Windows.Forms.VisualStyles;
using web3script.Services;
using web3script.ucontrols;
using static FaucetRequester;

public class TokenSwapper
{
    private readonly Web3 _web3;
    private readonly string _routerAddress;
    private readonly string _liquidityAddress;
    private readonly string _walletAddress;
    private readonly string _wethAddress;
    private readonly string _usdtAddress;
    private readonly string _usdcAddress;
    public TokenSwapper(string privateKey, ProxyViewModel proxyViewModel=null)
    {
        var rpcUrl = "https://testnet.dplabs-internal.com";
        int chainId = 688688;
        _liquidityAddress = "0xf8a1d4ff0f9b9af7ce58e1fc1833688f3bfd6115";
        _routerAddress = "0x1a4de519154ae51200b0ad7c90f7fac75547888a"; 
        _wethAddress = "0x76aaada469d23216be5f7c596fa25f282ff9b364"; 
        _usdtAddress = "0xEd59De2D7ad9C043442e381231eE3646FC3C2939";
        _usdcAddress = "0xad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37";
        var account = new Account(privateKey, chainId);
        _walletAddress = account.Address;
        _web3 = new Web3(account, rpcUrl);
       
    }

    /// <summary>
    /// 获取报价（TokenIn → TokenOut）
    /// </summary>
    public async Task<BigInteger[]> GetQuoteAsync(string[] path, BigInteger amountIn)
    {
        var abi = @"[{
          ""name"": ""getAmountsOut"",
          ""type"": ""function"",
          ""inputs"": [
            {""name"": ""amountIn"", ""type"": ""uint256""},
            {""name"": ""path"", ""type"": ""address[]""}
          ],
          ""outputs"": [{""name"": ""amounts"", ""type"": ""uint256[]""}],
          ""stateMutability"": ""view""
        }]";

        var contract = _web3.Eth.GetContract(abi, _routerAddress);
        var func = contract.GetFunction("getAmountsOut");
        var result = await func.CallAsync<BigInteger[]>(amountIn, path);
        return result;
    }

    /// <summary>
    /// 代币 → 原生币（使用ABI直接交换）
    /// </summary>
    public async Task<string> SwapTokenForNativeDirectAsync(string tokenIn, string[] path, decimal amountInDecimal, decimal minOutDecimal)
    {
        try
        {
            // 1. 检查路径
            if (path.Length != 2 || path[1] != _wethAddress)
            {
                throw new Exception($"无效的交易路径。路径必须是 [tokenIn, WETH]。当前路径: [{string.Join(", ", path)}]");
            }

            BigInteger amountIn = Web3.Convert.ToWei(amountInDecimal);
            BigInteger amountOutMin = Web3.Convert.ToWei(minOutDecimal);
            BigInteger deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;

            // 2. 检查代币余额
            var tokenService = new StandardTokenService(_web3, tokenIn);
            var balance = await tokenService.BalanceOfQueryAsync(_walletAddress);
            LogService.AppendLog($"代币余额: {Web3.Convert.FromWei(balance)}");

            if (balance < amountIn)
            {
                throw new Exception($"代币余额不足。需要: {amountInDecimal}, 可用: {Web3.Convert.FromWei(balance)}");
            }

            // 3. 检查授权
            var allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
            LogService.AppendLog($"当前授权额度: {Web3.Convert.FromWei(allowance)}");

            var requiredAllowance = BigInteger.Multiply(amountIn, 150) / 100; // 增加50%缓冲
            if (allowance < requiredAllowance)
            {
                LogService.AppendLog($"正在授权代币... (当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)})");
                var receipt = await tokenService.ApproveRequestAndWaitForReceiptAsync(_routerAddress, requiredAllowance);
                if (receipt.Status.Value == 0)
                {
                    throw new Exception("代币授权失败");
                }
                LogService.AppendLog("代币授权成功");

                // 等待区块确认
                await Task.Delay(10000); // 增加等待时间到10秒

                // 再次检查授权
                allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
                if (allowance < requiredAllowance)
                {
                    throw new Exception($"授权后额度仍然不足。当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)}");
                }
            }
            else
            {
                LogService.AppendLog("授权额度充足,无需重新授权。");
            }

            // 4. 构建交易
            var swapAbi = @"[{
                ""name"":""swapExactTokensForETH"",
                ""type"":""function"",
                ""stateMutability"":""nonpayable"",
                ""inputs"":[
                    {""name"":""amountIn"",""type"":""uint256""},
                    {""name"":""amountOutMin"",""type"":""uint256""},
                    {""name"":""path"",""type"":""address[]""},
                    {""name"":""to"",""type"":""address""},
                    {""name"":""deadline"",""type"":""uint256""}
                ],
                ""outputs"":[{""name"":""amounts"",""type"":""uint256[]""}]
            }]";

            var contract = _web3.Eth.GetContract(swapAbi, _routerAddress);
            var func = contract.GetFunction("swapExactTokensForETH");

            // 5. 获取当前gas price
            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
            LogService.AppendLog($"Gas Price: {gasPrice}");

            // 6. 使用固定的gas limit
            var gasLimit = new HexBigInteger(500000); // 使用较大的固定gas limit
            LogService.AppendLog($"使用固定Gas Limit: {gasLimit}");

            // 7. 创建交易输入
            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = _walletAddress,
                To = _routerAddress,
                Gas = gasLimit,
                GasPrice = gasPrice,
                Value = new HexBigInteger(0),
                Data = func.GetData(amountIn, amountOutMin, path, _walletAddress, deadline)
            };

            // 8. 检查账户余额
            var ethBalance = await _web3.Eth.GetBalance.SendRequestAsync(_walletAddress);
            var requiredBalance = gasLimit.Value * gasPrice;

            if (ethBalance < requiredBalance)
            {
                throw new Exception($"ETH余额不足。需要: {Web3.Convert.FromWei(requiredBalance)} ETH, 可用: {Web3.Convert.FromWei(ethBalance)} ETH");
            }

            // 9. 发送交易
            LogService.AppendLog("正在发送交易...");
            LogService.AppendLog($"交易参数:");
            LogService.AppendLog($"- 输入金额: {amountInDecimal}");
            LogService.AppendLog($"- 最小输出: {minOutDecimal}");
            LogService.AppendLog($"- 路径: {string.Join(" -> ", path)}");
            LogService.AppendLog($"- 截止时间: {DateTimeOffset.FromUnixTimeSeconds((long)deadline).LocalDateTime}");
            LogService.AppendLog($"- 交易数据: {txInput.Data}");

            // 用 TransactionManager 自动签名并广播
            string txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);
            LogService.AppendLog($"交易已发送。交易哈希: {txHash}");

            return txHash;
        }
        catch (Exception ex)
        {
            LogService.AppendLog($"SwapTokenForNativeDirectAsync错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogService.AppendLog($"内部错误: {ex.InnerException.Message}");
                if (ex.InnerException.InnerException != null)
                {
                    LogService.AppendLog($"更深层错误: {ex.InnerException.InnerException.Message}");
                }
            }
            throw;
        }
    }

    /// <summary>
    /// 代币 → 原生币（swapExactTokensForETH）
    /// </summary>
    public async Task<string> SwapTokenForNativeAsync(string tokenIn, string[] path, decimal amountInDecimal, decimal minOutDecimal)
    {
        try
        {
            // 1. 检查路径
            if (path.Length != 2 || path[1] != _wethAddress)
            {
                throw new Exception($"无效的交易路径。路径必须是 [tokenIn, WETH]。当前路径: [{string.Join(", ", path)}]");
            }

            BigInteger amountIn = Web3.Convert.ToWei(amountInDecimal);
            BigInteger amountOutMin = Web3.Convert.ToWei(minOutDecimal);
            BigInteger deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;

            // 2. 检查代币余额
            var tokenService = new StandardTokenService(_web3, tokenIn);
            var balance = await tokenService.BalanceOfQueryAsync(_walletAddress);
            LogService.AppendLog($"代币余额: {Web3.Convert.FromWei(balance)}");

            if (balance < amountIn)
            {
                throw new Exception($"代币余额不足。需要: {amountInDecimal}, 可用: {Web3.Convert.FromWei(balance)}");
            }

            // 3. 检查授权 - 增加50%的缓冲
            var allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
            LogService.AppendLog($"当前授权额度: {Web3.Convert.FromWei(allowance)}");

            var requiredAllowance = BigInteger.Multiply(amountIn, 150) / 100; // 增加50%缓冲
            if (allowance < requiredAllowance)
            {
                LogService.AppendLog($"正在授权代币... (当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)})");
                var receipt = await tokenService.ApproveRequestAndWaitForReceiptAsync(_routerAddress, requiredAllowance);
                if (receipt.Status.Value == 0)
                {
                    throw new Exception("代币授权失败");
                }
                LogService.AppendLog("代币授权成功");

                // 等待区块确认
                await Task.Delay(10000); // 增加等待时间到10秒

                // 再次检查授权
                allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
                if (allowance < requiredAllowance)
                {
                    throw new Exception($"授权后额度仍然不足。当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)}");
                }
            }
            else
            {
                LogService.AppendLog("授权额度充足,无需重新授权。");
            }

            // 4. 构建交易
            var swapAbi = @"[{
                ""name"":""swapExactTokensForTokens"",
            ""type"":""function"",
            ""stateMutability"":""payable"",
            ""inputs"":[
              {""name"":""amountIn"",""type"":""uint256""},
              {""name"":""amountOutMin"",""type"":""uint256""},
              {""name"":""path"",""type"":""address[]""},
                    {""name"":""to"",""type"":""address""}
            ],
                ""outputs"":[]
        }]";

            var contract = _web3.Eth.GetContract(swapAbi, _routerAddress);
            var func = contract.GetFunction("swapExactTokensForTokens");
            var encoded = func.GetData(amountIn, amountOutMin, path, _walletAddress);

            // 5. 发送multicall交易
            return await SendMulticallAsync(deadline, new[] { encoded });
        }
        catch (Exception ex)
        {
            LogService.AppendLog($"SwapTokenForNativeAsync错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogService.AppendLog($"内部错误: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// 内部方法：发送 multicall 交易
    /// </summary>
    private async Task<string> SendMulticallAsync(BigInteger deadline, string[] encodedCallsHex, BigInteger value = default)
    {
        try
        {
            // 从encodedCallsHex中解析参数
            var firstCall = encodedCallsHex[0];
            var amountIn = BigInteger.Parse(firstCall.Substring(10, 64), System.Globalization.NumberStyles.HexNumber);
            var amountOutMin = BigInteger.Parse(firstCall.Substring(74, 64), System.Globalization.NumberStyles.HexNumber);
            var tokenIn = firstCall.Substring(138, 40);
            var tokenOut = firstCall.Substring(178, 40);

            // 构建multicall bytecode
            var multicallSelector = "0x5ae401dc"; // multicall函数选择器
            var deadlineHex = deadline.ToString("x64").PadLeft(64, '0');
            var dataLengthHex = "0000000000000000000000000000000000000000000000000000000000000040";
            var arrayLengthHex = "0000000000000000000000000000000000000000000000000000000000000002"; // 固定为2个调用

            // 构建第一个调用 (swapExactTokensForTokens)
            var swapSelector = "0x04e45aaf";
            var tokenInHex = tokenIn.ToLower().Replace("0x", "").PadLeft(64, '0');
            var wethHex = _wethAddress.ToLower().Replace("0x", "").PadLeft(64, '0');
            var amountInHex = "0000000000000000000000000000000000000000000000000000000000002710"; // 固定为10000
            var pathLengthHex = "0000000000000000000000000000000000000000000000000000000000000002";
            var amountOutMinHexRaw = amountOutMin.ToString("x");
            LogService.AppendLog($"amountOutMinValue.Value.ToString(\"x\"): {amountOutMinHexRaw}");
            var amountOutMinHex = amountOutMinHexRaw.PadLeft(64, '0');
            var toAddress = _walletAddress.ToLower().Replace("0x", "").PadLeft(64, '0');

            // 4. 构建第一个调用的数据
            string firstCallData = swapSelector
                + tokenInHex
                + wethHex
                + amountInHex
                + pathLengthHex
                + amountOutMinHex
                + toAddress;
            // 填充到448字符（224字节）
            while (firstCallData.Length < 448) firstCallData += "0";
            if (firstCallData.Length > 448) firstCallData = firstCallData.Substring(0, 448);

            // 5. 构建第二个调用 (unwrapWETH9)
            var unwrapSelector = "49404b7c";
            var amount2Hex = amountOutMin.ToString("x").PadLeft(64, '0');
            string recipientHex = _walletAddress.Substring(2).PadLeft(40, '0').PadLeft(64, '0');
            string routerHex = "45a469ae07c09dfbedbc335bf15503e99416c873".PadLeft(40, '0') + new string('0', 24); // 20字节router+12字节0
            string unwrapCallData = unwrapSelector + amount2Hex + recipientHex + routerHex;
            unwrapCallData = unwrapCallData.PadRight(136, '0');

            // 添加头部填充
            string headPadding = new string('0', 238); // 119 bytes = 238 characters
            // 拼接完整数据
            string fullData = "0x" + multicallSelector + deadlineHex + dataLengthHex + arrayLengthHex
                + headPadding + firstCallData + unwrapCallData;

            LogService.AppendLog($"完整交易数据: {fullData}");

            // 估算gas
            HexBigInteger gasLimit;
            try
            {
                var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput
                {
                    From = _walletAddress,
                    To = _routerAddress,
                    Data = fullData,
                    Value = new HexBigInteger(value)
                });
                LogService.AppendLog($"Gas估算: {gasEstimate}");
                gasLimit = new HexBigInteger(BigInteger.Multiply(gasEstimate.Value, 150) / 100); // 增加50%缓冲
            }
            catch (Exception ex)
            {
                LogService.AppendLog($"警告: Gas估算失败: {ex.Message}");
                gasLimit = new HexBigInteger(400000); // 增加默认gas limit
            }

            // 获取当前gas price
            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
            LogService.AppendLog($"Gas Price: {gasPrice}");

            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = _walletAddress,
                To = _routerAddress,
                Data = fullData,
                Gas = gasLimit,
                GasPrice = gasPrice,
                Value = new HexBigInteger(value)
            };

            // 检查账户余额
            var balance = await _web3.Eth.GetBalance.SendRequestAsync(_walletAddress);
            var requiredBalance = value + (gasLimit.Value * gasPrice);

            if (balance < requiredBalance)
            {
                throw new Exception($"余额不足。需要: {Web3.Convert.FromWei(requiredBalance)} ETH, 可用: {Web3.Convert.FromWei(balance)} ETH");
            }

            LogService.AppendLog("正在发送交易...");
            string txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);
            LogService.AppendLog($"交易已发送。交易哈希: {txHash}");

            return txHash;
        }
        catch (Exception ex)
        {
            LogService.AppendLog($"SendMulticallAsync错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogService.AppendLog($"内部错误: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// 原生币 → 代币（swapExactETHForTokens）
    /// </summary>
    public async Task<string> SwapNativeForTokenAsync(string[] path, decimal ethAmountInDecimal, decimal minOutDecimal)
    {
        BigInteger amountIn = Web3.Convert.ToWei(ethAmountInDecimal);
        BigInteger amountOutMin = Web3.Convert.ToWei(minOutDecimal);
        BigInteger deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;

        var swapAbi = @"[{
            ""name"":""swapExactETHForTokens"",
            ""type"":""function"",
            ""stateMutability"":""payable"",
            ""inputs"":[
              {""name"":""amountOutMin"",""type"":""uint256""},
              {""name"":""path"",""type"":""address[]""},
              {""name"":""to"",""type"":""address""},
              {""name"":""deadline"",""type"":""uint256""}
            ],
            ""outputs"":[{""name"":"""",""type"":""uint256[]""}]
        }]";

        var contract = _web3.Eth.GetContract(swapAbi, _routerAddress);
        var func = contract.GetFunction("swapExactETHForTokens");
        var encoded = func.GetData(amountOutMin, path, _walletAddress, deadline);

        return await SendMulticallAsync(deadline, new[] { encoded }, amountIn);
    }

    public async Task<BigInteger> GetAllowanceAsync(string tokenAddress, string owner, string spender)
    {
        var abi = @"[{""constant"":true,""inputs"":[{""name"":""owner"",""type"":""address""},{""name"":""spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":"""",""type"":""uint256""}],""type"":""function""}]";

        var contract = _web3.Eth.GetContract(abi, tokenAddress);
        var function = contract.GetFunction("allowance");
        return await function.CallAsync<BigInteger>(owner, spender);
    }

    public async Task<ResultMsg> ApproveAsync(string tokenAddress, web3script.Models.Wallet wallet, string spender, BigInteger amount)
    {
        var abi = @"[{""constant"":false,""inputs"":[{""name"":""spender"",""type"":""address""},{""name"":""value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""type"":""function""}]";

        var contract = _web3.Eth.GetContract(abi, tokenAddress);
        var function = contract.GetFunction("approve");
        var gas = await function.EstimateGasAsync(wallet.Address, null, null, spender, amount);
        var txInput = function.CreateTransactionInput(wallet.Address, gas, null, null, spender, amount);
        var txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);
        return new ResultMsg { success = true, message = txHash};
    }

    /// <summary>
    /// 构建交易数据
    /// </summary>
    public string BuildTransactionData(
        string tokenIn,
        string tokenOut,
        BigInteger amountIn,
        BigInteger amountOutMin,
        string toAddress)
    {
        try
        {
            // 1. 构建 swapExactTokensForTokens 调用数据
            var swapAbi = @"[{
                ""name"":""swapExactTokensForTokens"",
                ""type"":""function"",
                ""stateMutability"":""nonpayable"",
                ""inputs"":[
                    {""name"":""amountIn"",""type"":""uint256""},
                    {""name"":""amountOutMin"",""type"":""uint256""},
                    {""name"":""path"",""type"":""address[]""},
                    {""name"":""to"",""type"":""address""},
                    {""name"":""deadline"",""type"":""uint256""}
                ],
                ""outputs"":[{""name"":""amounts"",""type"":""uint256[]""}]
            }]";

            var contract = _web3.Eth.GetContract(swapAbi, _routerAddress);
            var swapFunction = contract.GetFunction("swapExactTokensForTokens");
            var deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;
            var swapData = swapFunction.GetData(amountIn, amountOutMin, new string[] { tokenIn, tokenOut }, toAddress, deadline);

            // 2. 构建 unwrapWETH9 调用数据
            var unwrapAbi = @"[{
                ""name"":""unwrapWETH9"",
                ""type"":""function"",
                ""stateMutability"":""nonpayable"",
                ""inputs"":[
                    {""name"":""amountMinimum"",""type"":""uint256""},
                    {""name"":""recipient"",""type"":""address""}
                ],
                ""outputs"":[]
            }]";

            var unwrapContract = _web3.Eth.GetContract(unwrapAbi, _routerAddress);
            var unwrapFunction = unwrapContract.GetFunction("unwrapWETH9");
            var unwrapData = unwrapFunction.GetData(amountOutMin, toAddress);

            // 3. 构建 multicall 调用数据
            var multicallAbi = @"[{
                ""name"":""multicall"",
                ""type"":""function"",
                ""stateMutability"":""nonpayable"",
                ""inputs"":[
                    {""name"":""deadline"",""type"":""uint256""},
                    {""name"":""data"",""type"":""bytes[]""}
                ],
                ""outputs"":[{""name"":""results"",""type"":""bytes[]""}]
            }]";

            var multicallContract = _web3.Eth.GetContract(multicallAbi, _routerAddress);
            var multicallFunction = multicallContract.GetFunction("multicall");
            var multicallData = multicallFunction.GetData(deadline, new byte[][] { swapData.HexToByteArray(), unwrapData.HexToByteArray() });

            return multicallData;
        }
        catch (Exception ex)
        {
            LogService.AppendLog($"BuildTransactionData错误: {ex.Message}");
            if (ex.InnerException != null)
            {
                LogService.AppendLog($"内部错误: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    /// <summary>
    /// 将USDC换成Wp
    /// </summary>
    //public async Task<ResultMsg> SwapTokenForWETH(string tokenIn, string tokenOut, decimal amountInDecimal, decimal minOutDecimal)
    //{
    //    int retryCount = 0;
    //    while (true)
    //    {
    //        try
    //        {
    //            LogService.AppendLog("=== 开始执行代币交换 ===");
    //            LogService.AppendLog($"输入代币: {tokenIn}");
    //            LogService.AppendLog($"输出代币: {tokenOut}");
    //            LogService.AppendLog($"输入金额: {amountInDecimal}");
    //            LogService.AppendLog($"最小输出: {minOutDecimal}"); 
    //            BigInteger amountIn = Web3.Convert.ToWei(amountInDecimal);
    //            BigInteger amountOutMin = Web3.Convert.ToWei(minOutDecimal);
    //            BigInteger deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600; 
    //            var tokenService = new StandardTokenService(_web3, tokenIn);
    //            var balance = await tokenService.BalanceOfQueryAsync(_walletAddress);
    //            LogService.AppendLog($"代币余额: {Web3.Convert.FromWei(balance)}");

    //            if (balance < amountIn)
    //            {
    //                throw new Exception($"代币余额不足。需要: {amountInDecimal}, 可用: {Web3.Convert.FromWei(balance)}");
    //            } 
    //            var allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
    //            LogService.AppendLog($"当前授权额度: {(allowance)}");

    //            var requiredAllowance = BigInteger.Multiply(amountIn, 150) / 100; // 增加50%缓冲
    //            if (allowance < requiredAllowance)
    //            {
    //                LogService.AppendLog($"正在授权代币... (当前: {(allowance)}, 需要: {(requiredAllowance)})");
    //                try
    //                {
    //                    var maxApproval = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935"); 
    //                    var receipt = await tokenService.ApproveRequestAndWaitForReceiptAsync(_routerAddress, maxApproval);
    //                    if (receipt.Status.Value == 0)
    //                    {
    //                        throw new Exception("代币授权失败");
    //                    }
    //                    LogService.AppendLog("代币授权成功");

    //                    // 等待区块确认
    //                    await Task.Delay(10000); // 等待10秒

    //                    // 再次检查授权
    //                    allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
    //                    if (allowance < requiredAllowance)
    //                    {
    //                        throw new Exception($"授权后额度仍然不足。当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)}");
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //                    LogService.AppendLog($"授权过程出错: {ex.Message}");
    //                    throw;
    //                }
    //            }
    //            else
    //            {
    //                LogService.AppendLog("授权额度充足,无需重新授权。");
    //            } 
    //            LogService.AppendLog("正在构建交易数据...");
    //            string recipientAddress = _walletAddress; 
    //            string swapData =  web3script.SwapDataBuilder.BuildSwapData(
    //                tokenIn,
    //                tokenOut,
    //                recipientAddress,
    //                amountInDecimal,
    //                minOutDecimal); 
    //            LogService.AppendLog("正在估算 gas...");
    //            HexBigInteger gasLimit;
    //            try
    //            {
    //                var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput
    //                {
    //                    From = _walletAddress,
    //                    To = _routerAddress,
    //                    Data = swapData,
    //                    Value = new HexBigInteger(0)
    //                });
    //                LogService.AppendLog($"Gas 估算结果: {gasEstimate}");
    //                gasLimit = new HexBigInteger(BigInteger.Multiply(gasEstimate.Value, 150) / 100); // 增加50%缓冲
    //            }
    //            catch (Exception ex)
    //            {
    //                LogService.AppendLog($"Gas 估算失败，使用默认值: {ex.Message}");
    //                gasLimit = new HexBigInteger(500000);
    //            } 
    //            var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
    //            LogService.AppendLog($"Gas Price: {gasPrice}"); 
    //            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
    //            {
    //                From = _walletAddress,
    //                To = _routerAddress,
    //                Gas = gasLimit,
    //                GasPrice = gasPrice,
    //                Value = new HexBigInteger(0),
    //                Data = swapData
    //            }; 
    //            var ethBalance = await _web3.Eth.GetBalance.SendRequestAsync(_walletAddress);
    //            var requiredBalance = gasLimit.Value * gasPrice;
    //            LogService.AppendLog($"原生代币余额: {Web3.Convert.FromWei(ethBalance)}");
    //            LogService.AppendLog($"所需原生代币: {Web3.Convert.FromWei(requiredBalance)}"); 
    //            if (ethBalance < requiredBalance)
    //            {
    //                return new ResultMsg { success = false, message = "原生代币余额不足" };
    //                throw new Exception($"原生代币余额不足。需要: {Web3.Convert.FromWei(requiredBalance)}, 可用: {Web3.Convert.FromWei(ethBalance)}");
    //            } 
    //            string txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);
    //            var receiptService = new TransactionReceiptPollingService(_web3.TransactionManager);
    //            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    //            try
    //            {
    //                var receipt = await receiptService.PollForReceiptAsync(txHash, cts.Token);
    //                LogService.AppendLog($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
    //                return new ResultMsg { success = true, message = txHash };
    //            }
    //            catch (TaskCanceledException)
    //            {
    //                throw;
    //            } 
    //        }
    //        catch (Exception ex)
    //        {
    //            LogService.AppendLog($"SwapToken  错误: {ex.Message}");
    //            if (ex.InnerException != null)
    //            {
    //                LogService.AppendLog($"内部错误: {ex.InnerException.Message}");
    //                if (ex.InnerException.InnerException != null)
    //                {
    //                    LogService.AppendLog($"更深层错误: {ex.InnerException.InnerException.Message}");
    //                }
    //            }
    //            await Task.Delay(3000);
    //            if (retryCount > 3)
    //            {
    //                throw;
    //            }
    //            retryCount++;

    //        }
    //    }

    //}
    public async Task<ResultMsg> SwapTokenForWETH(string tokenIn, string tokenOut, decimal amountInDecimal, decimal minOutDecimal, int maxRetries = 5)
    {
        int retryCount = 0;
        while (retryCount <= maxRetries)
        {
            try
            {
                LogService.AppendLog("=== 开始执行代币交换 ===");
                LogService.AppendLog($"输入代币: {tokenIn}");
                LogService.AppendLog($"输出代币: {tokenOut}");
                LogService.AppendLog($"输入金额: {amountInDecimal}");
                LogService.AppendLog($"最小输出: {minOutDecimal}");

                BigInteger amountIn = Web3.Convert.ToWei(amountInDecimal);
                BigInteger amountOutMin = Web3.Convert.ToWei(minOutDecimal);
                BigInteger deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;
                var tokenService = new StandardTokenService(_web3, tokenIn);

                var balance = await tokenService.BalanceOfQueryAsync(_walletAddress);
                LogService.AppendLog($"代币余额: {Web3.Convert.FromWei(balance)}");

                if (balance < amountIn)
                    throw new Exception($"代币余额不足。需要: {amountInDecimal}, 可用: {Web3.Convert.FromWei(balance)}");

                var allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
                var requiredAllowance = amountIn * 150 / 100;

                if (allowance < requiredAllowance)
                {
                    LogService.AppendLog($"授权额度不足，当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)}");
                    var maxApproval = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");

                    var approveReceipt = await tokenService.ApproveRequestAndWaitForReceiptAsync(_routerAddress, maxApproval);
                    if (approveReceipt.Status.Value == 0)
                        throw new Exception("授权交易失败");

                    LogService.AppendLog("授权成功，等待10秒确认...");
                    await Task.Delay(10000);

                    allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _routerAddress);
                    if (allowance < requiredAllowance)
                        throw new Exception($"授权后额度仍不足。当前: {Web3.Convert.FromWei(allowance)}");
                }
                else
                {
                    LogService.AppendLog("授权额度充足，无需授权");
                }

                string recipientAddress = _walletAddress;
                string swapData = web3script.SwapDataBuilder.BuildSwapData(tokenIn, tokenOut, recipientAddress, amountInDecimal, minOutDecimal);

                LogService.AppendLog("估算 gas...");
                HexBigInteger gasLimit;
                try
                {
                    var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput
                    {
                        From = _walletAddress,
                        To = _routerAddress,
                        Data = swapData,
                        Value = new HexBigInteger(0)
                    });
                    gasLimit = new HexBigInteger(BigInteger.Multiply(gasEstimate.Value, 150) / 100);
                    LogService.AppendLog($"Gas估算: {gasEstimate}, 增加缓冲后: {gasLimit.Value}");
                }
                catch (Exception ex)
                {
                    LogService.AppendLog($"Gas估算失败: {ex.Message}，使用默认值");
                    gasLimit = new HexBigInteger(500000);
                }

                var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                var ethBalance = await _web3.Eth.GetBalance.SendRequestAsync(_walletAddress);
                var requiredEth = gasLimit.Value * gasPrice;

                LogService.AppendLog($"钱包余额: {Web3.Convert.FromWei(ethBalance)}");
                LogService.AppendLog($"预计消耗: {Web3.Convert.FromWei(requiredEth)}");

                if (ethBalance < requiredEth)
                    return new ResultMsg { success = false, message = "余额不足用于Gas" };

                var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
                {
                    From = _walletAddress,
                    To = _routerAddress,
                    Gas = gasLimit,
                    GasPrice = gasPrice,
                    Value = new HexBigInteger(0),
                    Data = swapData
                };

                // ✅ 只发送一次交易
                string txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);
                LogService.AppendLog($"交易发送成功，txHash: {txHash}");

                TransactionReceipt receipt = null;
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    receipt = await new TransactionReceiptPollingService(_web3.TransactionManager).PollForReceiptAsync(txHash, cts.Token);
                }
                catch (Exception ex)
                {
                    LogService.AppendLog($"未能获取receipt: {ex.Message}");
                }

                if (receipt == null)
                {
                    return new ResultMsg
                    {
                        success = true,
                        message = $"交易已提交，等待链确认。txHash: {txHash}"
                    };
                }

                if (receipt.Status.Value != 1)
                {
                    return new ResultMsg
                    {
                        success = false,
                        message = $"交易执行失败，txHash: {txHash}"
                    };
                }

                return new ResultMsg { success = true, message = txHash };
            }
            catch (Exception ex)
            {
                LogService.AppendLog($"错误（第 {retryCount + 1} 次）: {ex.Message}");
                if (++retryCount > maxRetries)
                    return new ResultMsg { success = false, message = $"多次尝试失败: {ex.Message}" };

                await Task.Delay(3000); // 间隔后重试
            }
        }

        return new ResultMsg { success = false, message = "未知错误" }; // 永远不会触发，仅防编译警告
    }

    public async Task<ResultMsg> WithdrawWETH(BigInteger wethAmount)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                var currentGasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                var finalGasPrice = currentGasPrice.Value;
                var contract = _web3.Eth.GetContract(
                    @"[{'constant':false,'inputs':[{'name':'amount','type':'uint256'}],'name':'withdraw','outputs':[],'payable':false,'stateMutability':'nonpayable','type':'function'}]",
                    _wethAddress);
                var withdrawFunction = contract.GetFunction("withdraw");
                var receipt = await withdrawFunction.SendTransactionAndWaitForReceiptAsync(
                    from: _walletAddress,
                    gas: new HexBigInteger(300000),
                    gasPrice: new HexBigInteger(finalGasPrice),
                    value: new HexBigInteger(0),
                    receiptRequestCancellationToken: null,
                    wethAmount);
                LogService.AppendLog($"换取成功,{receipt.TransactionHash}");
                return new ResultMsg {success = true,message = receipt.TransactionHash };
            }
            catch (Exception ex)
            {
                await Task.Delay(3000);
                if (retryCount > 3)
                {
                    return new ResultMsg { success = false, message = "wPhrs换Phrs失败:"+ex.Message };
                    
                }
                retryCount++;
            }
        }

    }

    public async Task<ResultMsg> ConvertEthToWeth(BigInteger ethAmount)
    {
        int retryCount = 0;

       
        const string depositAbi = @"[
        {
            'constant': false,
            'inputs': [],
            'name': 'deposit',
            'outputs': [],
            'payable': true,
            'stateMutability': 'payable',
            'type': 'function'
        }
    ]";

        while (true)
        {
            try
            {
                
                var currentGasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                var finalGasPrice = currentGasPrice.Value;

                
                var contract = _web3.Eth.GetContract(depositAbi, _wethAddress);
                var depositFunction = contract.GetFunction("deposit");

                
                var receipt = await depositFunction.SendTransactionAndWaitForReceiptAsync(
                    from: _walletAddress,
                    gas: new HexBigInteger(100000), 
                    gasPrice: new HexBigInteger(finalGasPrice),
                    value: new HexBigInteger(ethAmount),  
                    receiptRequestCancellationToken: null
                );

                LogService.AppendLog($"PHAR 转换为 wPhar 成功，交易哈希：{receipt.TransactionHash}");
                return new ResultMsg
                {
                    success = true,
                    message = receipt.TransactionHash
                };
            }
            catch (Exception ex)
            {
                await Task.Delay(3000);
                if (++retryCount > 3)
                {
                    return new ResultMsg
                    {
                        success = false,
                        message = "PHAR 转换为 wPhar 失败: " + ex.Message
                    };
                }
            }
        }
    }


    public async Task<BigInteger> GetWETHBalance(string walletAddress)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                var contract = _web3.Eth.GetContract(
                    @"[{'constant':true,'inputs':[{'name':'account','type':'address'}],'name':'balanceOf','outputs':[{'name':'balance','type':'uint256'}],'payable':false,'stateMutability':'view','type':'function'}]",
                    _wethAddress);
                var balanceOfFunction = contract.GetFunction("balanceOf");
                var balanceInWei = await balanceOfFunction.CallAsync<BigInteger>(walletAddress);
                decimal balanceInEth = (decimal)balanceInWei / 1000000000000000000m;

                return balanceInWei;
            }
            catch (Exception ex)
            {
                await Task.Delay(3000);
                if (retryCount > 3)
                {
                    throw new Exception($"查询WETH余额失败: {ex.Message}", ex);
                }
                retryCount++;
            }
        }

    }




    public async Task<bool> approveToken(BigInteger amount, string tokenAddress)
    {


        var tokenService = new StandardTokenService(_web3, tokenAddress);
        var balance = await tokenService.BalanceOfQueryAsync(_walletAddress);
        Debug.WriteLine($"余额: {Web3.Convert.FromWei(balance)}");

        if (balance < amount)
        {
            Debug.WriteLine($"代币余额不足。需要: {Web3.Convert.FromWei(amount)}, 可用: {Web3.Convert.FromWei(balance)}");
            return false;

        }

        // 检查授权
        var allowance = await tokenService.AllowanceQueryAsync(_walletAddress, _liquidityAddress);
        Debug.WriteLine($"当前授权额度: {(allowance)}");
        var maxApproval = BigInteger.Parse("115792089237316195423570985008687907853269984665640564039457584007913129639935");
        var requiredAllowance = BigInteger.Multiply(amount, 150) / 100; // 增加50%缓冲
        if (allowance < requiredAllowance)
        {
            Debug.WriteLine($"正在授权代币... (当前: {(allowance)}, 需要: {(requiredAllowance)})");
            try
            {
                var receipt = await tokenService.ApproveRequestAndWaitForReceiptAsync(_liquidityAddress, maxApproval);
                if (receipt.Status.Value == 0)
                {
                    return false;
                }
                Debug.WriteLine("代币授权成功");

                // 等待区块确认
                await Task.Delay(10000); // 等待10秒

                // 再次检查授权
                allowance = await tokenService.AllowanceQueryAsync(_walletAddress, tokenAddress);
                if (allowance < requiredAllowance)
                {
                    Debug.WriteLine($"授权后额度仍然不足。当前: {Web3.Convert.FromWei(allowance)}, 需要: {Web3.Convert.FromWei(requiredAllowance)}");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"授权过程出错: {ex.Message}");
                return false; ;
            }
        }
        else
        {
            Debug.WriteLine("授权额度充足,无需重新授权。");
            return true;
        }




    }

    public async Task<ResultMsg> AddlquiditywPharUsdc()
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                Debug.WriteLine("=== 开始执行添加流动性 ===");

                var isApproved = await approveToken(new BigInteger(1000000000000000), _wethAddress);

                if (!isApproved)
                {
                    throw new Exception("WPhar授权失败");
                }
                else
                {
                    Debug.WriteLine("WPhar授权成功");
                }

                var usdcisApproved = await approveToken(new BigInteger(1000000000000000), _usdcAddress);
                
                if (!usdcisApproved)
                {
                    throw new Exception("USDC授权失败");
                }
                else
                {
                    Debug.WriteLine("USDC授权成功");
                } 
                Debug.WriteLine("正在构建交易数据...");
                //string inputdata = "0x8831645600000000000000000000000076aaada469d23216be5f7c596fa25f282ff9b364000000000000000000000000ad902cf99c2de2f1ba5ec4d642fd7e49cae9ee3700000000000000000000000000000000000000000000000000000000000001f4000000000000000000000000000000000000000000000000000000000000f2c6000000000000000000000000000000000000000000000000000000000001a6a800000000000000000000000000000000000000000000000000005af3107a400000000000000000000000000000000000000000000000000005cca43191554a660000000000000000000000000000000000000000000000000000433174085952000000000000000000000000000000000000000000000000033d869e10d75e8e00000000000000000000000076a3ed42692d49d77cc3ad6e1244a8fa4207408a00000000000000000000000000000000000000000000000000000000683abaa1";
                string recipientAddress = _walletAddress;

                string swapData = web3script.SwapDataBuilder.BuildAddLiquidityCall(_walletAddress);

               // Debug.WriteLine($"正确的数据:{inputdata}");
                //Debug.WriteLine($"构建的数据:{swapData}");


                
                
                Debug.WriteLine("正在估算 gas...");
                HexBigInteger gasLimit;
                try
                {
                    var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput
                    {
                        From = _walletAddress,
                        To = _liquidityAddress,
                        Data = swapData,
                        Value = new HexBigInteger(0)
                    });
                    Debug.WriteLine($"Gas 估算结果: {gasEstimate}");
                    gasLimit = new HexBigInteger(BigInteger.Multiply(gasEstimate.Value, 150) / 100); // 增加50%缓冲
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Gas 估算失败，使用默认值: {ex.Message}");
                    gasLimit = new HexBigInteger(1000000);
                }
                var nonce = await _web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(_walletAddress);


                Debug.WriteLine("当前nonce" + nonce);
                 
                var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                Debug.WriteLine($"Gas Price: {gasPrice}");
                gasLimit = new HexBigInteger(1000000);
                
                var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
                {
                    From = _walletAddress,
                    To = _liquidityAddress,
                    Gas = gasLimit,
                    GasPrice = gasPrice,
                    Value = new HexBigInteger(0),
                    Data = swapData,
                    Nonce = nonce
                };

               
                var ethBalance = await _web3.Eth.GetBalance.SendRequestAsync(_walletAddress);
                var requiredBalance = gasLimit.Value * gasPrice;
                Debug.WriteLine($"PHAR 余额: {Web3.Convert.FromWei(ethBalance)}");
                Debug.WriteLine($"所需 PHAR: {Web3.Convert.FromWei(requiredBalance)}");

                if (ethBalance < requiredBalance)
                { 
                    return new ResultMsg { success = false, message = $"余额不足,需要: {Web3.Convert.FromWei(requiredBalance)} PHAR, 可用: {Web3.Convert.FromWei(ethBalance)} PHAR" };
                }
                string txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);

                var receiptService = new TransactionReceiptPollingService(_web3.TransactionManager);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));  

                try
                {
                    Debug.WriteLine($"交易已发送：{txHash}，等待确认...");
                    var receipt = await receiptService.PollForReceiptAsync(txHash, cts.Token);
                    Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
                    return  new ResultMsg { success = receipt.Status.Value == 1, message = txHash };
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("等待超时：交易未在120秒内被打包。");
                }



            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SwapTokenForWETH 错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"内部错误: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        Debug.WriteLine($"更深层错误: {ex.InnerException.InnerException.Message}");
                    }
                }
                await Task.Delay(3000);
                if (retryCount > 3)
                {
                    return new ResultMsg { success = false, message = "添加流动性失败:" + ex.Message };
                }
                retryCount++;

            }
        }

    }
    public async Task<ResultMsg> AddlquiditywUsdcUsdt()
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                Debug.WriteLine("=== 开始执行添加流动性 ===");

                var isApproved = await approveToken(new BigInteger(1000000000000000), _usdcAddress);

                if (!isApproved)
                {
                    throw new Exception("USDC授权失败");
                }
                else
                {
                    Debug.WriteLine("USDC授权成功");
                }

                var usdcisApproved = await approveToken(new BigInteger(1000000000000000), _usdtAddress);
               
                if (!usdcisApproved)
                {
                    throw new Exception("USDT授权失败");
                }
                else
                {
                    Debug.WriteLine("USDT授权成功");
                }

                Debug.WriteLine("正在构建交易数据...");
                string inputdata = "0x88316456000000000000000000000000ad902cf99c2de2f1ba5ec4d642fd7e49cae9ee37000000000000000000000000ed59de2d7ad9c043442e381231ee3646fc3c293900000000000000000000000000000000000000000000000000000000000001f4ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff435e000000000000000000000000000000000000000000000000000000000000b3e20000000000000000000000000000000000000000000000000de0b6b3a763ffff00000000000000000000000000000000000000000000000004e59eedc2d1353a0000000000000000000000000000000000000000000000000b298d2a8670370a0000000000000000000000000000000000000000000000000338c21e47d98999000000000000000000000000b7f88d4dd2884f207ed249a6802c5210ff63e5dd00000000000000000000000000000000000000000000000000000000683b7315";
                string recipientAddress = _walletAddress;

                string swapData = web3script.SwapDataBuilder.BuildAddLiquidityCall(_walletAddress);

                Debug.WriteLine($"正确的数据:{inputdata}");
                Debug.WriteLine($"构建的数据:{swapData}"); 
                Debug.WriteLine("正在估算 gas...");
                HexBigInteger gasLimit;
                try
                {
                    var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(new Nethereum.RPC.Eth.DTOs.CallInput
                    {
                        From = _walletAddress,
                        To = _liquidityAddress,
                        Data = swapData,
                        Value = new HexBigInteger(0)
                    });
                    Debug.WriteLine($"Gas 估算结果: {gasEstimate}");
                    gasLimit = new HexBigInteger(BigInteger.Multiply(gasEstimate.Value, 150) / 100); // 增加50%缓冲
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Gas 估算失败，使用默认值: {ex.Message}");
                    gasLimit = new HexBigInteger(1000000);
                }
                var nonce = await _web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(_walletAddress);


                Debug.WriteLine("当前nonce" + nonce); 
                var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                Debug.WriteLine($"Gas Price: {gasPrice}");
                gasLimit = new HexBigInteger(1000000); 
                var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
                {
                    From = _walletAddress,
                    To = _liquidityAddress,
                    Gas = gasLimit,
                    GasPrice = gasPrice,
                    Value = new HexBigInteger(0),
                    Data = swapData,
                    Nonce = nonce
                }; 
                var ethBalance = await _web3.Eth.GetBalance.SendRequestAsync(_walletAddress);
                var requiredBalance = gasLimit.Value * gasPrice;
                Debug.WriteLine($"PHAR 余额: {Web3.Convert.FromWei(ethBalance)}");
                Debug.WriteLine($"所需 PHAR: {Web3.Convert.FromWei(requiredBalance)}");

                if (ethBalance < requiredBalance)
                {
                    return new ResultMsg { success = false, message = $"余额不足,需要: {Web3.Convert.FromWei(requiredBalance)} PHAR, 可用: {Web3.Convert.FromWei(ethBalance)} PHAR" };
                    throw new Exception($"原生代币余额不足。需要: {Web3.Convert.FromWei(requiredBalance)} PHAR, 可用: {Web3.Convert.FromWei(ethBalance)} PHAR");
                }
                string txHash = await _web3.TransactionManager.SendTransactionAsync(txInput);

                var receiptService = new TransactionReceiptPollingService(_web3.TransactionManager);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120)); 

                try
                {
                    Debug.WriteLine($"交易已发送：{txHash}，等待确认...");
                    var receipt = await receiptService.PollForReceiptAsync(txHash, cts.Token);
                    Debug.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
                    return new ResultMsg { success = receipt.Status.Value == 1, message = txHash };
                }
                catch (TaskCanceledException)
                {
                    Debug.WriteLine("等待超时：交易未在120秒内被打包。");
                }



            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SwapTokenForWETH 错误: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"内部错误: {ex.InnerException.Message}");
                    if (ex.InnerException.InnerException != null)
                    {
                        Debug.WriteLine($"更深层错误: {ex.InnerException.InnerException.Message}");
                    }
                }
                await Task.Delay(3000);
                if (retryCount > 3)
                {
                    return new ResultMsg { success = false, message = "添加流动性失败"+ ex.Message};
                }
                retryCount++;

            }
        }

    }



}