using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Diagnostics;

public enum WalletType
{
    METAMASK,
    KEPLR,
    BACKPACK,
    HAHA,
    PHANTOM,
    RABBY,
    OTHER
}

public class ReferralService
{
    private readonly HttpClient _httpClient;
    private string? _sessionCookie;

    public ReferralService()
    {

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true,
        };
        _httpClient = new HttpClient(handler);
        _httpClient.BaseAddress = new Uri("https://testnet-api-server.nad.fun");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private void SetCommonHeaders()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("Origin", "https://testnet.nad.fun");
        _httpClient.DefaultRequestHeaders.Add("Referer", "https://testnet.nad.fun/");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Priority", "u=1, i");
        _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"132\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"132\"");
        _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
        _httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
        _httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");

        if (!string.IsNullOrEmpty(_sessionCookie))
        {
            _httpClient.DefaultRequestHeaders.Add("Cookie", _sessionCookie);
        }
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string endpoint, object? data = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        SetCommonHeaders();

        if (data != null)
        {
            var jsonContent = JsonConvert.SerializeObject(data);
            var content = new StringContent(jsonContent, Encoding.UTF8);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Content = content;
        }

        return await _httpClient.SendAsync(request);
    }

    public async Task<NonceResponse> GetNonce(string address)
    {
        try
        {
            var requestData = new { address };
            Debug.WriteLine("Request Content: " + JsonConvert.SerializeObject(requestData));

            var response = await SendRequestAsync(HttpMethod.Post, "/auth/nonce", requestData);
            var responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Nonce Response Status: " + response.StatusCode);
            Debug.WriteLine("Nonce Response: " + responseBody);

            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _sessionCookie = string.Join("; ", cookies);
            }

            var nonceResponse = JsonConvert.DeserializeObject<NonceResponse>(responseBody);
            if (nonceResponse == null || string.IsNullOrEmpty(nonceResponse.Nonce))
            {
                throw new Exception("Invalid nonce response");
            }

            return nonceResponse;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting nonce: {ex.Message}");
            throw;
        }
    }

    public async Task<SessionResponse> CreateSession(string address, string nonce, string privateKey, int chainId)
    {
        try
        {
            var signature = GenerateSignature(nonce, privateKey);
            var requestData = new
            {
                nonce,
                signature,
                chain_id = chainId
            };

            Debug.WriteLine("Request Content: " + JsonConvert.SerializeObject(requestData));

            var response = await SendRequestAsync(HttpMethod.Post, "/auth/session", requestData);
            var responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Session Response Status: " + response.StatusCode);
            Debug.WriteLine("Session Response: " + responseBody);

            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _sessionCookie = string.Join("; ", cookies);
            }

            return JsonConvert.DeserializeObject<SessionResponse>(responseBody) ?? throw new Exception("Failed to deserialize session response");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error creating session: {ex.Message}");
            throw;
        }
    }

    public async Task<WalletRegistrationResponse> RegisterWallet(string address, int chainId, WalletType walletType = WalletType.METAMASK)
    {
        try
        {
            var requestData = new
            {
                wallet = walletType.ToString(),
                address,
                chain_id = chainId
            };

            Debug.WriteLine("Wallet Registration Request Content: " + JsonConvert.SerializeObject(requestData));
            var response = await SendRequestAsync(HttpMethod.Patch, "/account/register_wallet", requestData);
            var responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Wallet Registration Response Status: " + response.StatusCode);
            Debug.WriteLine("Wallet Registration Raw Response: " + responseBody);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to register wallet: {response.StatusCode} - {responseBody}");
            }

            return JsonConvert.DeserializeObject<WalletRegistrationResponse>(responseBody) ??
                   throw new Exception($"Failed to deserialize wallet registration response: {responseBody}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error registering wallet: {ex.Message}");
            throw;
        }
    }

    public async Task<ReferralResponse> RegisterReferral(string parentReferralCode)
    {
        try
        {
            var requestData = new
            {
                parent_referral_code = parentReferralCode
            };

            var response = await SendRequestAsync(HttpMethod.Post, "/referral/register", requestData);
            var responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Referral Registration Response: " + responseBody);

            return JsonConvert.DeserializeObject<ReferralResponse>(responseBody) ?? throw new Exception("Failed to deserialize referral response");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error registering referral: {ex.Message}");
            throw;
        }
    }

    private string GenerateSignature(string message, string privateKey)
    {
        try
        {
            var signer = new EthereumMessageSigner();
            var signature = signer.EncodeUTF8AndSign(message, new EthECKey(privateKey));
            Debug.WriteLine("Generated Signature: " + signature);
            return signature;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating signature: {ex.Message}");
            throw;
        }
    }

    public async Task<ReferralCheckResponse> CheckReferralRegistration()
    {
        try
        {
            var response = await SendRequestAsync(HttpMethod.Get, "/referral/register_check");
            var responseBody = await response.Content.ReadAsStringAsync();
            Debug.WriteLine("Referral Check Response: " + responseBody);

            return JsonConvert.DeserializeObject<ReferralCheckResponse>(responseBody) ??
                   throw new Exception("Failed to deserialize referral check response");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking referral registration: {ex.Message}");
            throw;
        }
    }
}

public class NonceResponse
{
    [JsonProperty("nonce")]
    public string? Nonce { get; set; }
}

public class SessionResponse
{
    [JsonProperty("account")]
    public AccountData? Account { get; set; }
}

public class AccountData
{
    [JsonProperty("account_id")]
    public string? AccountId { get; set; }

    [JsonProperty("nickname")]
    public string? Nickname { get; set; }

    [JsonProperty("image_uri")]
    public string? ImageUri { get; set; }

    [JsonProperty("bio")]
    public string? Bio { get; set; }

    [JsonProperty("follower_count")]
    public int FollowerCount { get; set; }

    [JsonProperty("following_count")]
    public int FollowingCount { get; set; }

    [JsonProperty("mutual")]
    public bool? Mutual { get; set; }
}

public class WalletRegistrationResponse
{
    [JsonProperty("wallet")]
    public WalletData? Wallet { get; set; }
}

public class WalletData
{
    [JsonProperty("address")]
    public string? Address { get; set; }

    [JsonProperty("chain_id")]
    public int ChainId { get; set; }

    [JsonProperty("created_at")]
    public string? CreatedAt { get; set; }

    [JsonProperty("updated_at")]
    public string? UpdatedAt { get; set; }
}

public class ReferralResponse
{
    [JsonProperty("parent_account_id")]
    public string? ParentAccountId { get; set; }

    [JsonProperty("child_account_id")]
    public string? ChildAccountId { get; set; }
}

public class ReferralCheckResponse
{
    [JsonProperty("account_id")]
    public string? AccountId { get; set; }

    [JsonProperty("is_registered")]
    public bool IsRegistered { get; set; }
}