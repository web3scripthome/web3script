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

            //if (tur)
            //{
              //  Debug.WriteLine($" 使用.NET 6+原生支持的SOCKS5代理 socks5://{proxy.ServerAddress}:{proxy.Port}-{proxy.Username}-{proxy.Password} - {proxy.HasAuthentication}");
                string proxyUrl = $"socks5://{proxy.ServerAddress}:{proxy.Port}";

                if (proxy.HasAuthentication)
                {
                //    Debug.WriteLine($"socks5://{proxy.ServerAddress}:{proxy.Port} 需要身份验证");
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
              //      Debug.WriteLine($"socks5://{proxy.ServerAddress}:{proxy.Port} 不需要身份验证");
                    handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
                        Proxy = new WebProxy(proxyUrl),
                        UseProxy = true
                    };
                }
            //}
            //else
            //{
            //    // HTTP或HTTPS代理
            //    string proxyUrl = $"{proxy.ProxyType.ToLower()}://{proxy.ServerAddress}:{proxy.Port}";

            //    if (proxy.HasAuthentication)
            //    {
            //        handler = new HttpClientHandler
            //        {
            //            Proxy = new WebProxy(proxyUrl)
            //            {
            //                Credentials = new NetworkCredential(proxy.Username, proxy.Password)
            //            },
            //            UseProxy = true
            //        };
            //    }
            //    else
            //    {
            //        handler = new HttpClientHandler
            //        {
            //            Proxy = new WebProxy(proxyUrl),
            //            UseProxy = true
            //        };
            //    }
            //}

            return handler;
        }

        public static RpcClient GetRpcClient(HttpClientHandler httpClientHandler,string rpcUrl)
        {
           
            var httpClient = new HttpClient(httpClientHandler);
            return new RpcClient(new Uri(rpcUrl), httpClient); 
        }

    }
}
