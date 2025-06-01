using web3script.Services;
using web3script.ucontrols;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NadFun.Api
{
    /// <summary>
    /// 提供对Nad.fun API的访问功能
    /// </summary>
    public class NadFunApiClient
    {
        private   HttpClient _httpClient;
        private   string _baseUrl;
        private   JsonSerializerOptions _jsonOptions; 
        public NadFunApiClient(ProxyViewModel proxyViewModel,string baseUrl = "https://testnet-bot-api-server.nad.fun")
        {

            var httphandler = sHttpHandler.GetHttpClientHandler(proxyViewModel); 

            _baseUrl = baseUrl;
             

            _httpClient = new HttpClient(httphandler);
           
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = null,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                Converters = { new JsonStringEnumConverter() }
            };
        }
        public NadFunApiClient(string baseUrl = "https://testnet-bot-api-server.nad.fun")
        {
            _baseUrl = baseUrl;

            var handler = new HttpClientHandler
            { 
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,  
            };

            _httpClient = new HttpClient(handler);
            
            _httpClient.Timeout = TimeSpan.FromSeconds(30); 

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = null,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
                Converters = { new JsonStringEnumConverter() }
            };
        }


        #region 代币排序端点

        /// <summary>
        /// 获取按创建时间排序的代币（最新的在前）
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>按创建时间排序的代币列表</returns>
        public async Task<TokenOrderResult> GetTokensByCreationTimeAsync(int page = 1, int limit = 10)
        {
            int retryCount = 0;
            while (true)
            {
                await Task.Delay(2000);
                try
                {
                    string endpoint = $"/order/creation_time?page={page}&limit={limit}";
                    return await GetAsync<TokenOrderResult>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >=3)
                    {
                            throw;
                    }
                    retryCount++;
                    Debug.WriteLine($"API请求失败，重试中...{retryCount}");

                }
            }
          
         
        }

        /// <summary>
        /// 获取按市值排序的代币（从高到低）
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>按市值排序的代币列表</returns>
        public async Task<TokenOrderResult> GetTokensByMarketCapAsync(int page = 1, int limit = 10)
        {
            int retryCount = 0;
            
                while (true)
                {
                        try
                        {
                            await Task.Delay(2000);
                            string endpoint = $"/order/market_cap?page={page}&limit={limit}";
                            return await GetAsync<TokenOrderResult>(endpoint);
                        }
                        catch (Exception)
                        {
                                if (retryCount >= 3)
                                {
                                    throw;
                                }
                                 Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                                 retryCount++;
                        }
                  
                }
           
          
        }

        /// <summary>
        /// 获取按最近交易排序的代币
        /// </summary>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>按最近交易排序的代币列表</returns>
        public async Task<TokenOrderResult> GetTokensByLatestTradeAsync(int page = 1, int limit = 10)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    string endpoint = $"/order/latest_trade?page={page}&limit={limit}";
                    return await GetAsync<TokenOrderResult>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                    retryCount++;
                }

            }

        }

        #endregion

        #region 代币信息端点

        /// <summary>
        /// 获取代币元数据
        /// </summary>
        /// <param name="tokenAddress">代币合约地址</param>
        /// <returns>代币元数据</returns>
        public async Task<TokenMetadata> GetTokenMetadataAsync(string tokenAddress)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    string endpoint = $"/token/{tokenAddress}";
                    return await GetAsync<TokenMetadata>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                    retryCount++;
                }

            }
          
        }

        /// <summary>
        /// 获取代币价格图表数据
        /// </summary>
        /// <param name="tokenAddress">代币合约地址</param>
        /// <param name="interval">图表间隔(1m, 5m, 15m, 30m, 1h, 4h, 1d, 1w)</param>
        /// <param name="baseTimestamp">基本时间戳，默认为当前时间</param>
        /// <returns>代币价格图表数据</returns>
        public async Task<TokenChartData> GetTokenChartDataAsync(string tokenAddress, string interval = "1h", long? baseTimestamp = null)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    baseTimestamp ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    string endpoint = $"/token/chart/{tokenAddress}?interval={interval}&base_timestamp={baseTimestamp}";
                    return await GetAsync<TokenChartData>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                    retryCount++;
                }

            }
          
        }

        /// <summary>
        /// 获取代币市场信息
        /// </summary>
        /// <param name="tokenAddress">代币合约地址</param>
        /// <returns>代币市场数据</returns>
        public async Task<TokenMarketInfo> GetTokenMarketInfoAsync(string tokenAddress)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    string endpoint = $"/token/market/{tokenAddress}";
                    return await GetAsync<TokenMarketInfo>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                    retryCount++;
                }

            }

          
        }

        /// <summary>
        /// 获取代币兑换历史记录
        /// </summary>
        /// <param name="tokenAddress">代币合约地址</param>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>代币兑换历史记录</returns>
        public async Task<TokenSwapResult> GetTokenSwapHistoryAsync(string tokenAddress, int page = 1, int limit = 10)
        {
            string endpoint = $"/token/swap/{tokenAddress}?page={page}&limit={limit}";
            return await GetAsync<TokenSwapResult>(endpoint);
        }

        /// <summary>
        /// 获取代币持有者列表
        /// </summary>
        /// <param name="tokenAddress">代币合约地址</param>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>代币持有者列表</returns>
        public async Task<TokenHolderResult> GetTokenHoldersAsync(string tokenAddress, int page = 1, int limit = 10)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    string endpoint = $"/token/holder/{tokenAddress}?page={page}&limit={limit}";
                    return await GetAsync<TokenHolderResult>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                    retryCount++;
                }

            }

         
        }

        #endregion

        #region 账户端点

        /// <summary>
        /// 获取账户代币仓位
        /// </summary>
        /// <param name="accountAddress">账户EOA地址</param>
        /// <param name="positionType">仓位类型过滤(all, open, close)</param>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>账户代币仓位</returns>
        public async Task<AccountPositionResult> GetAccountPositionsAsync(string accountAddress, string positionType = "open", int page = 1, int limit = 10)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    await Task.Delay(2000);
                    string endpoint = $"/account/position/{accountAddress}?position_type={positionType}&page={page}&limit={limit}";

                    // 打印原始API响应进行调试
                    var response = await _httpClient.GetAsync(_baseUrl + endpoint);
                    response.EnsureSuccessStatusCode();
                    string rawContent = await response.Content.ReadAsStringAsync();
                    // Debug.WriteLine($"API响应: {rawContent}");

                    var result = JsonSerializer.Deserialize<AccountPositionResult>(rawContent, _jsonOptions);
                    return result;
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"获取市值排序的代币失败，重试中...{retryCount}");
                    retryCount++;
                }

            }
          
        }

        /// <summary>
        /// 获取账户创建的代币
        /// </summary>
        /// <param name="accountAddress">账户EOA地址</param>
        /// <param name="page">页码，默认为1</param>
        /// <param name="limit">每页的项目数，默认为10</param>
        /// <returns>账户创建的代币列表</returns>
        public async Task<AccountCreatedTokensResult> GetAccountCreatedTokensAsync(string accountAddress, int page = 1, int limit = 10)
        {
            int retryCount = 0;
            while (true)
            {
                try
                {   await Task.Delay(2000);
                    string endpoint = $"/account/create_token/{accountAddress}?page={page}&limit={limit}";
                    return await GetAsync<AccountCreatedTokensResult>(endpoint);
                }
                catch (Exception)
                {
                    if (retryCount >= 3)
                    {
                        throw;
                    }
                    Debug.WriteLine($"API请求失败，重试中...{retryCount}"); 
                }
            }
          
          
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 发送GET请求并反序列化响应
        /// </summary>
        /// <typeparam name="T">响应类型</typeparam>
        /// <param name="endpoint">API端点</param>
        /// <returns>反序列化后的响应</returns>
        private async Task<T> GetAsync<T>(string endpoint)
        {
            try
            {
                string url = _baseUrl + endpoint;
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                // 添加更详细的错误信息
                string errorMessage = $"API请求失败: {ex.Message}";
                Debug.WriteLine(errorMessage);
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n内部错误: {ex.InnerException.Message}";
                    Debug.WriteLine(errorMessage);
                }
                throw new NadFunApiException(errorMessage, ex);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"JSON解析错误: {ex.Message}");
                throw new NadFunApiException($"JSON解析错误: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"请求超时: {ex.Message}");
                throw new NadFunApiException("请求超时", ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"调用API时发生错误: {ex.Message}");
                throw new NadFunApiException($"调用API时发生错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将科学计数法格式化为可读字符串
        /// </summary>
        public static string FormatScientificNotation(string value)
        {
            if (string.IsNullOrEmpty(value) || (!value.Contains('e') && !value.Contains('E')))
                return value;

            try
            {
                // 尝试解析为双精度浮点数并格式化
                if (double.TryParse(value, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out double doubleValue))
                {
                    if (doubleValue >= 0.01 && doubleValue < 1000000)
                        return doubleValue.ToString("0.####"); // 使用普通格式

                    // 否则保留科学计数法，但格式化为更可读的形式
                    return doubleValue.ToString("0.####E+0");
                }
            }
            catch
            {
                // 如果解析失败，返回原始值
            }

            return value;
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// API 异常
    /// </summary>
    public class NadFunApiException : Exception
    {
        public NadFunApiException(string message) : base(message) { }
        public NadFunApiException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// 代币基本信息
    /// </summary>
    public class TokenInfo
    {
        public string token_address { get; set; }
        public string name { get; set; }
        public string symbol { get; set; }
        public string image_uri { get; set; }
        public string creator { get; set; }
        public string total_supply { get; set; }
        public long created_at { get; set; }

        // 格式化方法
        public string FormatTotalSupply()
        {
            return NadFunApiClient.FormatScientificNotation(total_supply);
        }
    }

    /// <summary>
    /// 市场信息
    /// </summary>
    public class MarketInfo
    {
        public string market_address { get; set; }
        public string market_type { get; set; }
        public string price { get; set; }

        // 格式化方法
        public string FormatPrice()
        {
            return NadFunApiClient.FormatScientificNotation(price);
        }
    }

    /// <summary>
    /// 代币排序响应
    /// </summary>
    public class TokenOrderResult
    {
        public string order_type { get; set; }
        public List<OrderToken> order_token { get; set; }
        public int total_count { get; set; }
    }

    /// <summary>
    /// 排序的代币
    /// </summary>
    public class OrderToken
    {
        public TokenInfo token_info { get; set; }
        public MarketInfo market_info { get; set; }
    }

    /// <summary>
    /// 代币元数据
    /// </summary>
    public class TokenMetadata
    {
        public string token_address { get; set; }
        public string name { get; set; }
        public string symbol { get; set; }
        public string image_uri { get; set; }
        public string creator_address { get; set; }
        public string description { get; set; }
        public string twitter { get; set; }
        public string telegram { get; set; }
        public string website { get; set; }
        public bool is_listing { get; set; }
        public string total_supply { get; set; }
        public long created_at { get; set; }
        public string create_transaction_hash { get; set; }
    }

    /// <summary>
    /// 代币市场信息
    /// </summary>
    public class TokenMarketInfo
    {
        public string market_id { get; set; }
        public string market_type { get; set; }
        public string token_address { get; set; }
        public string virtual_native { get; set; }
        public string virtual_token { get; set; }
        public string reserve_token { get; set; }
        public string reserve_native { get; set; }
        public string price { get; set; }
        public long latest_trade_at { get; set; }
        public long created_at { get; set; }
        public bool is_listing { get; set; }
        public string target_token { get; set; }
    }

    /// <summary>
    /// 图表数据
    /// </summary>
    public class ChartDataPoint
    {
        public long time_stamp { get; set; }
        public string open_price { get; set; }
        public string close_price { get; set; }
        public string high_price { get; set; }
        public string low_price { get; set; }
        public string volume { get; set; }
    }

    /// <summary>
    /// 代币图表数据
    /// </summary>
    public class TokenChartData
    {
        public List<ChartDataPoint> data { get; set; }
        public string token_id { get; set; }
        public string interval { get; set; }
        public long base_timestamp { get; set; }
        public int total_count { get; set; }
    }

    /// <summary>
    /// 代币交换记录
    /// </summary>
    public class TokenSwap
    {
        public int swap_id { get; set; }
        public string account_address { get; set; }
        public string token_address { get; set; }
        public bool is_buy { get; set; }
        public string mon_amount { get; set; }
        public string token_amount { get; set; }
        public long created_at { get; set; }
        public string transaction_hash { get; set; }
    }

    /// <summary>
    /// 代币交换历史
    /// </summary>
    public class TokenSwapResult
    {
        public List<TokenSwap> swaps { get; set; }
        public int total_count { get; set; }
    }

    /// <summary>
    /// 代币持有者
    /// </summary>
    public class TokenHolder
    {
        public string current_amount { get; set; }
        public string account_address { get; set; }
        public bool is_dev { get; set; }
    }

    /// <summary>
    /// 代币持有者结果
    /// </summary>
    public class TokenHolderResult
    {
        public List<TokenHolder> holders { get; set; }
        public int total_count { get; set; }
    }

    /// <summary>
    /// 账户代币仓位
    /// </summary>
    public class AccountPositionResult
    {
        public string account_address { get; set; }
        public List<PositionItem> positions { get; set; }
        public int total_count { get; set; }
    }

    /// <summary>
    /// 仓位项
    /// </summary>
    public class PositionItem
    {
        public TokenInfo token { get; set; }
        public Position position { get; set; }
        public MarketInfo market { get; set; }
    }

    /// <summary>
    /// 仓位数据
    /// </summary>
    public class Position
    {
        public string total_bought_native { get; set; }
        public string total_bought_token { get; set; }
        public string current_token_amount { get; set; }
        public string realized_pnl { get; set; }
        public string unrealized_pnl { get; set; }
        public string total_pnl { get; set; }
        public long created_at { get; set; }
        public long last_traded_at { get; set; }

        // 格式化大数值的辅助方法 - 直接返回原始值
        private string FormatLargeNumber(BigInteger value)
        {
            // 直接返回原始数值的字符串表示
            return value.ToString();
        }

        // 格式化输出方法 - 直接返回原始值
        public string FormatTokenAmount()
        {
            // 直接返回原始值，不进行任何处理
            return current_token_amount ?? "0";
        }

        // 格式化输出PNL的方法 - 直接返回原始值
        public string FormatUnrealizedPnl()
        {
            // 直接返回原始值，不进行任何处理
            return unrealized_pnl ?? "0";
        }

        // 格式化输出PNL的方法
      
    }

    /// <summary>
    /// 账户创建的代币
    /// </summary>
    public class AccountCreatedTokensResult
    {
        public List<CreatedToken> tokens { get; set; }
        public int total_count { get; set; }
    }

    /// <summary>
    /// 创建的代币
    /// </summary>
    public class CreatedToken
    {
        public TokenInfo token { get; set; }
        public bool is_listing { get; set; }
        public string market_cap { get; set; }
        public string price { get; set; }
        public string current_amount { get; set; }
        public string description { get; set; }
    }

    #endregion
}