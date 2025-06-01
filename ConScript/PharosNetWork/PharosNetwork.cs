using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks; 
using Nethereum.Signer;
using Newtonsoft.Json;
using web3script.Models;
using web3script.Services;
using web3script.ucontrols;

public class FaucetRequester
{
    
    private readonly HttpClient client;
    private string _jwtToken; // 存储登录后获取的JWT令牌
    private string _rpcUrl= "https://testnet.dplabs-internal.com";
    private int _chainId = 688688;

    public FaucetRequester(ProxyViewModel proxyViewModel=null, string rpcUrl = "https://testnet.dplabs-internal.com", int chainId = 688688)
    {
        if (proxyViewModel != null)
        {
            var handler = sHttpHandler.GetHttpClientHandler(proxyViewModel);
            _rpcUrl = rpcUrl;
            _chainId = chainId;
            client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));
            client.DefaultRequestHeaders.Add("Origin", "https://testnet.pharosnetwork.xyz");
            client.DefaultRequestHeaders.Add("Referer", "https://testnet.pharosnetwork.xyz/");
            client.DefaultRequestHeaders.Add("Sec-CH-UA", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Microsoft Edge\";v=\"132\"");
            client.DefaultRequestHeaders.Add("Sec-CH-UA-Mobile", "?0");
            client.DefaultRequestHeaders.Add("Sec-CH-UA-Platform", "\"Windows\"");
        }
        else
        {
            // 不使用代理，直接连接
          var  handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,
                UseDefaultCredentials = false,
                Proxy = null,
                UseProxy = false,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                 System.Net.DecompressionMethods.Deflate |
                                 System.Net.DecompressionMethods.Brotli
            };
            client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
            client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));
            client.DefaultRequestHeaders.Add("Origin", "https://testnet.pharosnetwork.xyz");
            client.DefaultRequestHeaders.Add("Referer", "https://testnet.pharosnetwork.xyz/");
            client.DefaultRequestHeaders.Add("Sec-CH-UA", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Microsoft Edge\";v=\"132\"");
            client.DefaultRequestHeaders.Add("Sec-CH-UA-Mobile", "?0");
            client.DefaultRequestHeaders.Add("Sec-CH-UA-Platform", "\"Windows\"");
        }
       
    }

    public class ResultMsg
    {
        public string message { get; set; }
        public bool success { get; set; }
    }

    // 登录接口响应数据结构
    public class LoginResponse
    {
        public int code { get; set; }
        public LoginData data { get; set; }
        public string msg { get; set; }
    }

    public class LoginData
    {
        public string jwt { get; set; }
    }

    // 签到接口响应数据结构
    public class SignInResponse
    {
        public int code { get; set; }
        public string msg { get; set; }
    }

    // 用户资料响应数据结构
    public class ProfileResponse
    {
        public int code { get; set; }
        public ProfileData data { get; set; }
        public string msg { get; set; }
    }

    public class ProfileData
    {
        public UserInfo user_info { get; set; }
    }

    public class UserInfo
    {
        public long ID { get; set; }
        public string Address { get; set; }
        public string XId { get; set; }
        public string TwitterAccessToken { get; set; }
        public string DiscordId { get; set; }
        public string UserName { get; set; }
        public string FatherAddress { get; set; }
        public string GrandpaAddress { get; set; }
        public int TotalPoints { get; set; }
        public int TaskPoints { get; set; }
        public int InvitePoints { get; set; }
        public bool IsKol { get; set; }
        public string InviteCode { get; set; }
        public string CreateTime { get; set; }
        public string UpdateTime { get; set; }
    }

    public async Task<ResultMsg> RequestFaucetAsync(string tokenAddress, string userAddress)//领USDC
    {
        var url = "https://testnet-router.zenithswap.xyz/api/v1/faucet";
        var jsonBody = $"{{\"tokenAddress\":\"{tokenAddress}\",\"userAddress\":\"{userAddress}\"}}"; 

        // 设置请求头
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
        client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
        // client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("zstd"));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
        client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));
        client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        client.DefaultRequestHeaders.Add("Pragma", "no-cache");
        client.DefaultRequestHeaders.Add("Origin", "https://testnet.zenithswap.xyz");
        client.DefaultRequestHeaders.Add("Referer", "https://testnet.zenithswap.xyz/");
        client.DefaultRequestHeaders.Add("Sec-CH-UA", "\"Chromium\";v=\"136\", \"Google Chrome\";v=\"136\", \"Not.A/Brand\";v=\"99\"");
        client.DefaultRequestHeaders.Add("Sec-CH-UA-Mobile", "?0");
        client.DefaultRequestHeaders.Add("Sec-CH-UA-Platform", "\"Windows\"");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36");

        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(url, content);
            var responseText = await response.Content.ReadAsStringAsync();

            return new ResultMsg
            {
                message = responseText,
                success = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            return new ResultMsg
            {
                message = $"请求出错: {ex.Message}",
                success = false
            };
        }
    }

    
    public async Task<ResultMsg> LoginAsync(Wallet wallet, string inviteCode)
    {
        try
        {
            // 1. 使用Nethereum签名 "pharos" 消息
            var signer = new EthereumMessageSigner();
            string message = "pharos";
            string signature = signer.EncodeUTF8AndSign(message, new Nethereum.Signer.EthECKey(wallet.PrivateKey));
            //  Console.WriteLine($"签名结果: {signature}");    
            // 2. 构建登录请求URL

            string loginUrl = $"https://api.pharosnetwork.xyz/user/login?address={wallet.Address}&signature={signature}&invite_code={inviteCode}";

            // 配置HTTP请求
            var request = new HttpRequestMessage(HttpMethod.Post, loginUrl);

            // 设置完整的请求头
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.8));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-GB", 0.7));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.6));

            // 设置授权头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "null");

            // 设置内容长度
            request.Content = new StringContent("", Encoding.UTF8);
            request.Content.Headers.ContentLength = 0;

            // 设置其他请求头
            request.Headers.Add("Origin", "https://testnet.pharosnetwork.xyz");
            request.Headers.Add("Referer", "https://testnet.pharosnetwork.xyz/");
            request.Headers.Add("Sec-CH-UA", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Microsoft Edge\";v=\"132\"");
            request.Headers.Add("Sec-CH-UA-Mobile", "?0");
            request.Headers.Add("Sec-CH-UA-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Site", "same-site");
            request.Headers.Add("Priority", "u=1, i");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36 Edg/132.0.0.0");

            // 发送POST请求
            //  await Console.Out.WriteLineAsync(   $"发送登录请求: {loginUrl}");
            var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            //  await Console.Out.WriteLineAsync($"登录响应: {responseText}");
            if (response.IsSuccessStatusCode)
            {
                // 解析响应获取JWT令牌
                var loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseText);
                if (loginResponse?.code == 0 && loginResponse?.data?.jwt != null)
                {
                    _jwtToken = loginResponse.data.jwt;
                    return new ResultMsg
                    {
                        message = $"登录成功: {responseText}",
                        success = true
                    };
                }
                else
                {
                    return new ResultMsg
                    {
                        message = $"登录失败，服务器响应: {responseText}",
                        success = false
                    };
                }
            }
            else
            {
                return new ResultMsg
                {
                    message = $"登录请求失败，状态码: {response.StatusCode}, 响应: {responseText}",
                    success = false
                };
            }
        }
        catch (Exception ex)
        {
            return new ResultMsg
            {
                message = $"登录过程出错: {ex.Message}",
                success = false
            };
        }
    }

    
    public async Task<ResultMsg> SignInAsync(string walletAddress)
    {
        try
        {
            // 检查是否已登录获取到JWT令牌
            if (string.IsNullOrEmpty(_jwtToken))
            {
                return new ResultMsg
                {
                    message = "未登录，无法执行签到",
                    success = false
                };
            }
            // Console.WriteLine($"_jwtToken:{_jwtToken}");
            // 构建签到请求URL
            string signInUrl = $"https://api.pharosnetwork.xyz/sign/in?address={walletAddress}";

            // 配置HTTP请求
            var request = new HttpRequestMessage(HttpMethod.Post, signInUrl);

            // 设置完整的请求头
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.8));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-GB", 0.7));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.6));

            // 设置授权头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // 设置内容长度
            request.Content = new StringContent("", Encoding.UTF8);
            request.Content.Headers.ContentLength = 0;

            // 设置其他请求头
            request.Headers.Add("Origin", "https://testnet.pharosnetwork.xyz");
            request.Headers.Add("Referer", "https://testnet.pharosnetwork.xyz/");
            request.Headers.Add("Sec-CH-UA", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Microsoft Edge\";v=\"132\"");
            request.Headers.Add("Sec-CH-UA-Mobile", "?0");
            request.Headers.Add("Sec-CH-UA-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Site", "same-site");
            request.Headers.Add("Priority", "u=1, i");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36 Edg/132.0.0.0");

            // 发送POST请求
            var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            //   await Console.Out.WriteLineAsync($"签到响应: {responseText}");
            if (response.IsSuccessStatusCode)
            {
                var signInResponse = JsonConvert.DeserializeObject<SignInResponse>(responseText);
                if (signInResponse?.code == 0)
                {
                    return new ResultMsg
                    {
                        message = $"签到成功: {responseText}",
                        success = true
                    };
                }
                else
                {
                    return new ResultMsg
                    {
                        message = $"签到失败，服务器响应: {responseText}",
                        success = false
                    };
                }
            }
            else
            {
                return new ResultMsg
                {
                    message = $"签到请求失败，状态码: {response.StatusCode}, 响应: {responseText}",
                    success = false
                };
            }
        }
        catch (Exception ex)
        {
            return new ResultMsg
            {
                message = $"签到过程出错: {ex.Message}",
                success = false
            };
        }
    }

    
    public async Task<ResultMsg> LoginAndSignInAsync(Wallet wallet, string inviteCode)
    {
        // 1. 先登录
        var loginResult = await LoginAsync(wallet, inviteCode);
        if (!loginResult.success)
        {
            return loginResult; // 登录失败，直接返回错误信息
        }

        // 2. 登录成功后执行签到
        var signInResult = await SignInAsync(wallet.Address);

        return signInResult;
    }

    public async Task<ResultMsg> GetInviteCodeAsync(string walletAddress)//获取邀请码
    {
        try
        {
            // 检查是否已登录获取到JWT令牌
            if (string.IsNullOrEmpty(_jwtToken))
            {
                return new ResultMsg
                {
                    message = "未登录，无法获取邀请码",
                    success = false
                };
            }

            // 构建获取用户资料的请求URL
            string profileUrl = $"https://api.pharosnetwork.xyz/user/profile?address={walletAddress}";

            // 配置HTTP请求
            var request = new HttpRequestMessage(HttpMethod.Get, profileUrl);

            // 设置完整的请求头
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh-CN"));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("zh", 0.9));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
            request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));

            // 设置授权头
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _jwtToken);

            // 设置其他请求头
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Pragma", "no-cache");
            request.Headers.Add("Origin", "https://testnet.pharosnetwork.xyz");
            request.Headers.Add("Referer", "https://testnet.pharosnetwork.xyz/");
            request.Headers.Add("Sec-CH-UA", "\"Chromium\";v=\"136\", \"Google Chrome\";v=\"136\", \"Not.A/Brand\";v=\"99\"");
            request.Headers.Add("Sec-CH-UA-Mobile", "?0");
            request.Headers.Add("Sec-CH-UA-Platform", "\"Windows\"");
            request.Headers.Add("Sec-Fetch-Dest", "empty");
            request.Headers.Add("Sec-Fetch-Mode", "cors");
            request.Headers.Add("Sec-Fetch-Site", "same-site");
            request.Headers.Add("Priority", "u=1, i");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36");

            // 发送GET请求
            var response = await client.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // 解析响应获取邀请码
                var profileResponse = JsonConvert.DeserializeObject<ProfileResponse>(responseText);
                if (profileResponse?.code == 0 && profileResponse?.data?.user_info != null)
                {
                    string inviteCode = profileResponse.data.user_info.InviteCode;

                    if (!string.IsNullOrEmpty(inviteCode))
                    {
                        return new ResultMsg
                        {
                            message = inviteCode,
                            success = true
                        };
                    }
                    else
                    {
                        return new ResultMsg
                        {
                            message = "获取到的邀请码为空",
                            success = false
                        };
                    }
                }
                else
                {
                    return new ResultMsg
                    {
                        message = $"获取邀请码失败，服务器响应: {responseText}",
                        success = false
                    };
                }
            }
            else
            {
                return new ResultMsg
                {
                    message = $"获取邀请码请求失败，状态码: {response.StatusCode}, 响应: {responseText}",
                    success = false
                };
            }
        }
        catch (Exception ex)
        {
            return new ResultMsg
            {
                message = $"获取邀请码过程出错: {ex.Message}",
                success = false
            };
        }
    }
}