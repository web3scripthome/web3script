using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using web3script.ucontrols;
using Nethereum.JsonRpc.Client;
using web3script.ContractScript;

namespace web3script.Services
{
    public class sHttpHandler
    {

        public static HttpClientHandler GetHttpClientHandler (ProxyViewModel proxy)
        {
             
            HttpClientHandler handler = null;

            
             
                string proxyUrl = $"socks5://{proxy.ServerAddress}:{proxy.Port}";

                if (proxy.HasAuthentication)
                {
              
                    handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                        Proxy = new WebProxy(proxyUrl)
                        { 
                            Credentials = new NetworkCredential(proxy.Username, proxy.Password)
                        },
                        UseProxy = true
                    };
                }
                else
                {
            
                    handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                        Proxy = new WebProxy(proxyUrl),
                        UseProxy = true
                    };
                }
            
            return handler;
        }

        public static RpcClient GetRpcClient(HttpClientHandler httpClientHandler,string rpcUrl)
        {
           
            var httpClient = new HttpClient(httpClientHandler);
            return new RpcClient(new Uri(rpcUrl), httpClient); 
        }

    }
}

