using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace web3script.ConScript
{
    public class GetEVMBalance
    {
        public static async Task<decimal> GetEthBalanceAsync(string rpcUrl, string ethAddress)
        {
            try
            {
                var web3 = new Web3(rpcUrl);
                BigInteger balanceWei = await web3.Eth.GetBalance.SendRequestAsync(ethAddress);
                decimal balanceEth = Web3.Convert.FromWei(balanceWei);
                return balanceEth;
            }
            catch (Exception ex)
            {
               // Debug.WriteLine($"Error fetching ETH balance: {ex.Message}");
                return -1;
            }
        }
    }
}
