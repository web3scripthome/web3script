using web3script.ContractScript;
using web3script.Services;
using web3script.ucontrols;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace web3script.ConScript.MitScript
{
    public class MintNFT
    {
       
        public string rpcUrl = "https://testnet-rpc.monad.xyz";
        public string Data = "0x9b4f3af5000000000000000000000000a48ca117e7b587fc6d3c0cf4dc44da4b69b381990000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000800000000000000000000000000000000000000000000000000000000000000000";
        public  async Task<string> SendTransactionAsync(string privateKey, string data, string NftAddress)
        {
            try
            {
                var chainId = 10143;
                var account = new Account(privateKey, chainId);
                var web3 = new Web3(account, "https://testnet-rpc.monad.xyz");
                string toAddress = NftAddress;
                var gasPrice = await web3.Eth.GasPrice.SendRequestAsync();
               Debug.WriteLine("GasPrice:" + gasPrice);
                // var gasLimit = new BigInteger(3000000);
                // 创建交易对象用于估算gas
                var estimateGasInput = new CallInput
                {
                    Type = new HexBigInteger(2), // EIP-1559交易类型
                    From = account.Address,
                    To = toAddress,
                    Data = data
                };

                // 使用estimateGas预估燃气量
                var estimatedGas = await web3.Eth.TransactionManager.EstimateGasAsync(estimateGasInput);

               Debug.WriteLine("预估gasLimit" + estimatedGas);
                var MaxGasfe = estimatedGas.Value * gasPrice.Value;
                var weiAmount = Web3.Convert.FromWei(MaxGasfe);
               Debug.WriteLine("消耗Gas" + weiAmount);

                var txnInput = new TransactionInput
                {
                    Type = new HexBigInteger(2), // EIP-1559交易类型
                    From = account.Address,
                    To = toAddress,
                    Gas = estimatedGas,
                    GasPrice = gasPrice,
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
        public static string ReplaceAddressInInput(string inputData, string newAddress)
        {
            // 检查输入地址是否符合要求
            if (string.IsNullOrWhiteSpace(newAddress) || !newAddress.StartsWith("0x") || newAddress.Length != 42)
            {
                throw new ArgumentException("无效的地址，必须是 0x 开头并且长度为 42 个字符。");
            }

            // 将 "0x" 去掉，补充成 64 个字符
            string paddedAddress = newAddress.Substring(2).PadLeft(64, '0').ToLower();

            // 输入数据去掉 "0x"
            string rawInput = inputData.Substring(2);

            // 地址部分的位置（地址从第8个字节开始，占64个字符）
            int addressStart = 8;
            int addressEnd = 8 + 64;

            // 替换地址部分
            string newInputData = rawInput.Substring(0, addressStart) + paddedAddress + rawInput.Substring(addressEnd);

            // 返回新的输入数据，前加 "0x"
            return "0x" + newInputData;
        }
        public async Task<ConScriptResult> MintNftAsync(string privateKey,string NftAddress)
        {
            try
            {
                Account account = new Account(privateKey);
                var address = account.Address;
                var newdata = ReplaceAddressInInput(Data, address);
                var result = await SendTransactionAsync(privateKey, newdata, NftAddress);
                return new ConScriptResult { Success = true, Hex = result };
            }
            catch (Exception ex)
            {

                return new ConScriptResult { Success = false, Hex = "", ErrorMessage = ex.Message };
            }
          
        }
        public async Task<ConScriptResult> MintNftAsync(string privateKey, string NftAddress,ProxyViewModel proxyViewModel)
        {
            try
            {
                var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
                var rpcClient = sHttpHandler.GetRpcClient(httphandler, rpcUrl);

                var account = new Account(privateKey);
                Web3 _web3 = new Web3(account, rpcClient); 
                 
                var address = account.Address;
                var newdata = ReplaceAddressInInput(Data, address);
                var result = await SendTransactionAsync(privateKey, newdata, NftAddress, _web3);
                return new ConScriptResult { Success = true, Hex = result };
            }
            catch (Exception ex)
            {

                return new ConScriptResult { Success = false, Hex = "", ErrorMessage = ex.Message };
            }

        }


        public async Task<string> SendTransactionAsync(string privateKey, string data, string NftAddress, Web3 _web3)
        {
            try
            {
                var chainId = 10143;
                var account = new Account(privateKey, chainId);
                
                string toAddress = NftAddress;
                var gasPrice = await _web3.Eth.GasPrice.SendRequestAsync();
                Debug.WriteLine("GasPrice:" + gasPrice);
                // var gasLimit = new BigInteger(3000000);
                // 创建交易对象用于估算gas
                var estimateGasInput = new CallInput
                {
                    Type = new HexBigInteger(2), // EIP-1559交易类型
                    From = account.Address,
                    To = toAddress,
                    Data = data
                };

                // 使用estimateGas预估燃气量
                var estimatedGas = await _web3.Eth.TransactionManager.EstimateGasAsync(estimateGasInput);

                Debug.WriteLine("预估gasLimit" + estimatedGas);
                var MaxGasfe = estimatedGas.Value * gasPrice.Value;
                var weiAmount = Web3.Convert.FromWei(MaxGasfe);
                Debug.WriteLine("消耗Gas" + weiAmount);

                var txnInput = new TransactionInput
                {
                    Type = new HexBigInteger(2), // EIP-1559交易类型
                    From = account.Address,
                    To = toAddress,
                    Gas = estimatedGas,
                    GasPrice = gasPrice,
                    Data = data
                };
                var receipt = await _web3.TransactionManager.SendTransactionAndWaitForReceiptAsync(txnInput);
                return receipt.TransactionHash;
            }
            catch (Exception)
            {

                throw;
            }

        }
    }
}

