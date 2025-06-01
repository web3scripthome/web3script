using System.Numerics;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.Contracts;
using System.Threading.Tasks;
using web3script.ucontrols;
using web3script.Services;
using static FaucetRequester;
using Nethereum.RPC.TransactionReceipts;

public class MintCaller
{
    private   Web3 _web3;
    private   string _contractAddress;
    private   string _userAddress;
    private const int chainId = 688688;
    private const string rpcUrl = "https://testnet.dplabs-internal.com";
    public MintCaller(string privateKey, ProxyViewModel proxyViewModel = null)
    {
        if (proxyViewModel != null)
        {
            var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
            var rpcClient = sHttpHandler.GetRpcClient(httphandler, rpcUrl);

            var account = new Account(privateKey, chainId: chainId);
            _web3 = new Web3(account, rpcClient); 
            _contractAddress = "0x11de0e754f1df7c7b0d559721b334809a9c0dfb7";
            _userAddress = account.Address;
        }
        else
        {
            var account = new Account(privateKey, chainId: 688688);
            _web3 = new Web3(account, rpcUrl);
            _contractAddress = "0x11de0e754f1df7c7b0d559721b334809a9c0dfb7";
            _userAddress = account.Address;
        }
       
    }

    public async Task<ResultMsg> CallMintAsync(string assetAddress)
    {
        try
        {
            
            var abi = @"[
            {
              ""inputs"": [
                {""internalType"": ""address"", ""name"": ""_asset"", ""type"": ""address""},
                {""internalType"": ""address"", ""name"": ""_account"", ""type"": ""address""},
                {""internalType"": ""uint256"", ""name"": ""_amount"", ""type"": ""uint256""}
              ],
              ""name"": ""mint"",
              ""outputs"": [],
              ""stateMutability"": ""nonpayable"",
              ""type"": ""function""
            }
        ]";

            var contract = _web3.Eth.GetContract(abi, _contractAddress);
            var mintFunction = contract.GetFunction("mint");
            BigInteger amount = BigInteger.Parse("1000000000000000000000");
            var txHash = await mintFunction.SendTransactionAsync(
                from: _web3.TransactionManager.Account.Address,
                gas: new Nethereum.Hex.HexTypes.HexBigInteger(500000),
                value: null,
                functionInput: new object[] {
                assetAddress,
                _userAddress,
                amount
                });
            var receiptService = new TransactionReceiptPollingService(_web3.TransactionManager);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                var receipt = await receiptService.PollForReceiptAsync(txHash, cts.Token);
                Console.WriteLine($"确认状态：{(receipt.Status.Value == 1 ? "成功" : "失败")}");
                return new ResultMsg { success = true, message = txHash };
            }
            catch (TaskCanceledException)
            {
                return new ResultMsg { success = false, message = "等待超时：交易未在60秒内被打包" };
                
            }
           
        }
        catch (Exception e)
        {

            return new ResultMsg { success =false, message = $"{_userAddress}领取{assetAddress}失败:"+e.Message };
        }
     
    }
}
