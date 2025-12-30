using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using FF14RisingstoneCheckIn.Models;
using FF14RisingstoneCheckIn.Utils;

namespace FF14RisingstoneCheckIn.Services;

public class ApiClient : IDisposable
{
    private readonly Settings _settings;
    private HttpClient? _loginHttpClient;
    private HttpClient? _signInHttpClient;
    private CookieContainer _loginCookieContainer = new();

    public event Action<string>? OnLogMessage;
    public event Action<string>? OnStatusChanged;
    public event Action? OnCookieExpired;
    public event Action? OnLoginSuccess;

    public ApiClient(Settings settings)
    {
        _settings = settings;
        InitializeHttpClients();
    }

    #region HTTP Client Management

    private void InitializeHttpClients()
    {
        // 登录用的 HttpClient（使用 CookieContainer 来跟踪重定向中的 Cookie）
        var handler = new HttpClientHandler
        {
            CookieContainer = _loginCookieContainer,
            AllowAutoRedirect = false
        };
        _loginHttpClient = new HttpClient(handler);
        SetLoginHeaders();

        // 签到用的 HttpClient（直接使用 Cookie Header）
        _signInHttpClient = new HttpClient();
        UpdateSignInHttpClientCookie();
    }

    private void SetLoginHeaders()
    {
        if (_loginHttpClient == null) return;

        _loginHttpClient.DefaultRequestHeaders.Clear();
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua", "\"Microsoft Edge\";v=\"137\", \"Chromium\";v=\"137\", \"Not/A)Brand\";v=\"24\"");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://ff14risingstones.web.sdo.com/");
        _loginHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _settings.SavedUserAgent);
    }

    public void UpdateSignInHttpClientCookie()
    {
        if (_signInHttpClient == null) return;

        _signInHttpClient.DefaultRequestHeaders.Clear();
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cache-Control", "no-cache");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Pragma", "no-cache");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua", "\"Microsoft Edge\";v=\"137\", \"Chromium\";v=\"137\", \"Not/A)Brand\";v=\"24\"");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://ff14risingstones.web.sdo.com/");
        _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _settings.SavedUserAgent);

        if (!string.IsNullOrWhiteSpace(_settings.SavedRisingstoneCookie))
            _signInHttpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", _settings.SavedRisingstoneCookie);
    }

    private void ExtractAndSaveRisingstoneCookie()
    {
        try
        {
            var allCookies = _loginCookieContainer.GetAllCookies();
            var risingstoneCookie = allCookies.FirstOrDefault(c =>
                c.Name.Equals("ff14risingstones", StringComparison.OrdinalIgnoreCase));

            if (risingstoneCookie != null && !string.IsNullOrWhiteSpace(risingstoneCookie.Value))
            {
                _settings.SavedRisingstoneCookie = $"ff14risingstones={risingstoneCookie.Value}";
                _settings.CookieSavedTime = DateTime.Now;
                _settings.Save();

                UpdateSignInHttpClientCookie();
                Log("Cookie 已保存");
            }
        }
        catch (Exception ex)
        {
            Log($"保存 Cookie 时出错: {ex.Message}");
        }
    }

    #endregion

    #region Login Methods

    public async Task<bool> ExecutePushLoginAsync(string account)
    {
        try
        {
            if (_loginHttpClient == null)
            {
                OnStatusChanged?.Invoke("错误: HttpClient 未初始化");
                return false;
            }

            OnStatusChanged?.Invoke("正在发送登录请求...");
            Log("正在发送登录请求...");

            // 检查账号类型
            var baseUrl = "https://w.cas.sdo.com/authen/checkAccountType.jsonp";
            var restData = BuildRestData(account);
            var restUrl = $"{baseUrl}?{restData}";
            await _loginHttpClient.GetAsync(restUrl);

            // 随机延迟模拟用户行为
            await Task.Delay(new Random().Next(1000, 3000));

            // 发送推送登录
            var baseUrl2 = "https://w.cas.sdo.com/authen/sendPushMessage.jsonp";
            var response = await _loginHttpClient.GetAsync($"{baseUrl2}?{restData}");
            var responseText = await response.Content.ReadAsStringAsync();

            var jsonText = GetJsonFromJsonp(responseText);
            if (string.IsNullOrEmpty(jsonText))
            {
                OnStatusChanged?.Invoke("发送登录请求失败: 无法解析响应");
                return false;
            }

            var responseData = JsonSerializer.Deserialize<ResponseData>(jsonText);

            if (responseData.ReturnCode != 0)
            {
                if (responseData.ReturnCode == -14001710)
                {
                    OnStatusChanged?.Invoke("请打开手机叨鱼APP后再尝试");
                    Log("请打开手机叨鱼APP后再尝试");
                    return false;
                }
                OnStatusChanged?.Invoke($"({responseData.ReturnCode}){responseData.ReturnMessage}");
                Log($"登录失败: ({responseData.ReturnCode}){responseData.ReturnMessage}");
                return false;
            }

            OnStatusChanged?.Invoke("已发送推送，请在手机上确认登录...");
            Log("已发送推送，请在手机上确认登录...");

            // 轮询获取 ticket
            return await PollForLoginConfirmationAsync(restData);
        }
        catch (Exception ex)
        {
            OnStatusChanged?.Invoke($"登录失败: {ex.Message}");
            Log($"登录异常: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> PollForLoginConfirmationAsync(string restData)
    {
        if (_loginHttpClient == null) return false;

        var baseUrl = "https://w.cas.sdo.com/authen/pushMessageLogin.jsonp";
        var retryCount = 0;

        while (retryCount < 30)
        {
            try
            {
                var response = await _loginHttpClient.GetAsync($"{baseUrl}?{restData}");
                var responseText = await response.Content.ReadAsStringAsync();
                var data = GetJsonFromJsonp(responseText);

                var responseData = JsonSerializer.Deserialize<ResponseData>(data);

                if (!string.IsNullOrEmpty(responseData.Data.Ticket) && responseData.Data.Ticket.Length > 5)
                {
                    OnStatusChanged?.Invoke("登录成功，正在获取 Cookie...");
                    Log("登录成功，正在获取 Cookie...");

                    var loginSuccess = await FinalizeLoginSessionAsync(responseData.Data.Ticket);

                    if (!loginSuccess)
                    {
                        OnStatusChanged?.Invoke("登录失败");
                        Log("获取 Cookie 失败");
                        return false;
                    }

                    OnStatusChanged?.Invoke("登录成功！Cookie 已保存");
                    Log("登录成功！Cookie 已保存");
                    OnLoginSuccess?.Invoke();
                    return true;
                }

                if (responseData.Data.MappedErrorCode != 0 &&
                    responseData.Data.MappedErrorCode != -10516808)
                {
                    OnStatusChanged?.Invoke($"({responseData.Data.MappedErrorCode}){responseData.Data.FailReason}");
                    Log($"登录失败: ({responseData.Data.MappedErrorCode}){responseData.Data.FailReason}");
                    return false;
                }

                await Task.Delay(1000);
                retryCount++;
                OnStatusChanged?.Invoke($"等待确认... ({retryCount}/30)");
            }
            catch
            {
                OnStatusChanged?.Invoke($"等待确认... ({retryCount + 1}/30)");
                await Task.Delay(1000);
                retryCount++;
            }
        }

        OnStatusChanged?.Invoke("登录超时，请重试");
        Log("登录超时，请重试");
        return false;
    }

    private async Task<bool> FinalizeLoginSessionAsync(string ticket)
    {
        if (_loginHttpClient == null) return false;

        var initialUrl = $"https://apiff14risingstones.web.sdo.com/api/home/GHome/login?redirectUrl=https://ff14risingstones.web.sdo.com/pc/index.html&ticket={ticket}";

        try
        {
            string currentUrl = initialUrl;
            const int maxRedirects = 20;

            for (var i = 0; i < maxRedirects; i++)
            {
                OnStatusChanged?.Invoke($"跟随重定向 ({i + 1}/{maxRedirects})");

                using var req = new HttpRequestMessage(HttpMethod.Get, currentUrl);
                using var resp = await _loginHttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if (resp.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
                {
                    foreach (var sc in setCookieValues)
                    {
                        try
                        {
                            _loginCookieContainer.SetCookies(new Uri(currentUrl), sc);
                        }
                        catch { }
                    }
                }

                if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400 && resp.Headers.Location != null)
                {
                    currentUrl = resp.Headers.Location.IsAbsoluteUri
                        ? resp.Headers.Location.ToString()
                        : new Uri(new Uri(currentUrl), resp.Headers.Location).ToString();
                    continue;
                }

                break;
            }

            const string redirectUrl = "https://ff14risingstones.web.sdo.com/pc/index.html";
            OnStatusChanged?.Invoke("请求目标站点以完成会话...");

            using var finalReq = new HttpRequestMessage(HttpMethod.Get, redirectUrl);
            using var finalResp = await _loginHttpClient.SendAsync(finalReq, HttpCompletionOption.ResponseHeadersRead);

            if (finalResp.Headers.TryGetValues("Set-Cookie", out var finalSetCookie))
            {
                foreach (var sc in finalSetCookie)
                {
                    try
                    {
                        _loginCookieContainer.SetCookies(new Uri(redirectUrl), sc);
                    }
                    catch { }
                }
            }

            var initPaths = new[]
            {
                "/api/common/getHotSearchList",
                "/api/home/GHome/isLogin",
                "/api/home/groupAndRole/getCharacterBindInfo?platform=2",
                "/api/home/recruit/getJobConfigList",
                "/api/home/sysMsg/getTip",
                "/api/home/sign/signRewardList?month=" + DateTime.Now.ToString("yyyy-MM")
            };

            foreach (var path in initPaths)
            {
                try
                {
                    var tempsuid = Guid.NewGuid().ToString();
                    var sep = path.Contains('?') ? "&" : "?";
                    var full = $"https://apiff14risingstones.web.sdo.com{path}{sep}tempsuid={tempsuid}";

                    using var initReq = new HttpRequestMessage(HttpMethod.Get, full);
                    initReq.Headers.TryAddWithoutValidation("Origin", "https://ff14risingstones.web.sdo.com");
                    initReq.Headers.TryAddWithoutValidation("Referer", "https://ff14risingstones.web.sdo.com/");
                    initReq.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

                    using var initResp = await _loginHttpClient.SendAsync(initReq, HttpCompletionOption.ResponseHeadersRead);

                    if (initResp.Headers.TryGetValues("Set-Cookie", out var scs))
                    {
                        foreach (var sc in scs)
                        {
                            try
                            {
                                _loginCookieContainer.SetCookies(new Uri(full), sc);
                            }
                            catch { }
                        }
                    }

                    await Task.Delay(200);
                }
                catch { }
            }

            ExtractAndSaveRisingstoneCookie();
            OnStatusChanged?.Invoke("登录会话已完成");

            return !string.IsNullOrWhiteSpace(_settings.SavedRisingstoneCookie);
        }
        catch (Exception ex)
        {
            Log($"完成登录会话时出错: {ex.Message}");
            return false;
        }
    }

    private static string BuildRestData(string account)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        query["callback"] = "checkAccountType_JSONPMethod";
        query["inputUserId"] = account;
        query["appId"] = "6788";
        query["areaId"] = "1";
        query["serviceUrl"] = "https://apiff14risingstones.web.sdo.com/api/home/GHome/login?redirectUrl=https://ff14risingstones.web.sdo.com/pc/index.html";
        query["productVersion"] = "v5";
        query["frameType"] = "3";
        query["locale"] = "zh_CN";
        query["version"] = "21";
        query["tag"] = "20";
        query["authenSource"] = "2";
        query["productId"] = "2";
        query["scene"] = "login";
        query["usage"] = "aliCode";
        query["bizType"] = "";
        query["source"] = "pc";
        query["_"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        return query.ToString() ?? string.Empty;
    }

    private static string GetJsonFromJsonp(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var match = Regex.Match(input, @"^[^(]*\((.*)\)$");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    #endregion

    #region Sign In Methods

    public async Task<bool> ValidateAndSignInWithSavedCookieAsync()
    {
        if (_signInHttpClient == null || string.IsNullOrWhiteSpace(_settings.SavedRisingstoneCookie))
        {
            OnCookieExpired?.Invoke();
            return false;
        }

        try
        {
            var isLoginUrl = $"https://apiff14risingstones.web.sdo.com/api/home/GHome/isLogin?tempsuid={Guid.NewGuid()}";
            var isLoginResp = await _signInHttpClient.GetAsync(isLoginUrl);
            var isLoginText = await isLoginResp.Content.ReadAsStringAsync();

            var loginDoc = JsonDocument.Parse(isLoginText);

            if (loginDoc.RootElement.TryGetProperty("code", out var codeElement))
            {
                var code = codeElement.GetInt32();
                if (code != 10000)
                {
                    Log("Cookie 已过期，需要重新登录");
                    OnCookieExpired?.Invoke();
                    return false;
                }
            }
            else
            {
                OnCookieExpired?.Invoke();
                return false;
            }

            Log("Cookie 验证成功，正在执行签到...");
            await ExecuteSignInAndClaimRewardsAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log($"验证 Cookie 时出错: {ex.Message}");
            OnCookieExpired?.Invoke();
            return false;
        }
    }

    public async Task<(bool Success, string Message)> ExecuteSignInAndClaimRewardsAsync()
    {
        try
        {
            if (_signInHttpClient == null || string.IsNullOrWhiteSpace(_settings.SavedRisingstoneCookie))
            {
                OnCookieExpired?.Invoke();
                return (false, "未登录或 Cookie 已失效");
            }

            var (success, message, isCookieExpired) = await RequestSignInAsync();

            if (!success)
            {
                if (isCookieExpired)
                {
                    OnCookieExpired?.Invoke();
                }
                return (false, message);
            }

            var results = new List<string> { $"签到: {message}" };
            Log($"签到结果: {message}");

            // 自动领取奖励
            var currentMonth = DateTimeHelper.GetCurrentMonth();
            var rewardList = await GetSignRewardListAsync(currentMonth);

            if (rewardList.Count > 0)
            {
                foreach (var reward in rewardList.Where(r => r.IsGet == (int)SignRewardItemGetType.Available))
                {
                    var result = await GetSignRewardAsync(reward.Id, currentMonth);
                    results.Add($"领取 {reward.ItemName}: {result}");
                    Log($"领取奖励 {reward.ItemName}: {result}");
                    await Task.Delay(1000);
                }
            }

            _settings.LastSignInTime = DateTime.Now;
            _settings.Save();

            return (true, string.Join("\n", results));
        }
        catch (Exception ex)
        {
            Log($"签到异常: {ex.Message}");
            return (false, $"签到异常: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message, bool IsCookieExpired)> RequestSignInAsync()
    {
        if (_signInHttpClient == null) return (false, "HttpClient 未初始化", false);

        var tempSuid = Guid.NewGuid().ToString();
        var url = $"{_settings.BaseUrl}/api/home/sign/signIn?tempsuid={tempSuid}";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("tempsuid", tempSuid)
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        req.Headers.TryAddWithoutValidation("Origin", "https://ff14risingstones.web.sdo.com");
        req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");

        var response = await _signInHttpClient.SendAsync(req);
        var responseText = await response.Content.ReadAsStringAsync();

        try
        {
            var jsonDoc = JsonDocument.Parse(responseText);
            var code = 0;
            var msg = string.Empty;

            if (jsonDoc.RootElement.TryGetProperty("code", out var codeElement))
                code = codeElement.GetInt32();

            if (jsonDoc.RootElement.TryGetProperty("msg", out var msgElement))
                msg = msgElement.GetString() ?? string.Empty;

            var formattedMessage = $"({code}){msg}";

            // 10000: 签到成功, 10001: 今日已签到, 10301: 操作太快
            if (code == 10000 || code == 10001 || code == 10301)
                return (true, formattedMessage, false);

            return (false, formattedMessage, false);
        }
        catch
        {
            return (false, "解析响应失败", false);
        }
    }

    public async Task<List<SignRewardItem>> GetSignRewardListAsync(string month)
    {
        if (_signInHttpClient == null) return [];

        var tempSuid = Guid.NewGuid().ToString();
        var url = $"{_settings.BaseUrl}/api/home/sign/signRewardList?month={month}&tempsuid={tempSuid}";

        var response = await _signInHttpClient.GetAsync(url);
        var responseText = await response.Content.ReadAsStringAsync();

        var result = JsonSerializer.Deserialize<SignRewardListResponse>(responseText);
        return result?.Data ?? [];
    }

    public async Task<string> GetSignRewardAsync(int id, string month)
    {
        if (_signInHttpClient == null) return "错误: HttpClient 未初始化";

        var tempSuid = Guid.NewGuid().ToString();
        var url = $"{_settings.BaseUrl}/api/home/sign/getSignReward?tempsuid={tempSuid}";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("id", id.ToString()),
            new KeyValuePair<string, string>("month", month),
            new KeyValuePair<string, string>("tempsuid", tempSuid)
        });

        var response = await _signInHttpClient.PostAsync(url, content);
        var responseText = await response.Content.ReadAsStringAsync();

        try
        {
            var jsonDoc = JsonDocument.Parse(responseText);
            if (jsonDoc.RootElement.TryGetProperty("msg", out var msgElement))
                return msgElement.GetString() ?? "成功";
        }
        catch { }

        return responseText;
    }

    #endregion

    private void Log(string message)
    {
        OnLogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    public void Dispose()
    {
        _loginHttpClient?.Dispose();
        _signInHttpClient?.Dispose();
    }
}
