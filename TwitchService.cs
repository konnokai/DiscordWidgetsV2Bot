using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace DiscordWidgetsV2Bot;

public class TwitchBinding
{
    public string Login { get; set; } = "";
    public string TwitchUserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Field { get; set; } = "bottom-description-3";
    public DateTime? LastLiveAt { get; set; }
    public bool LiveNow { get; set; }
}

/// <summary>
/// Twitch 開台偵測：啟動時驗證 API 憑證（失敗則停用，不影響其他功能），
/// 之後每 30 秒輪詢綁定頻道的開台狀態並更新綁定時指定的 widget 欄位。
/// </summary>
public class TwitchService(IConfiguration config, WidgetService widgets, DiscordSocketClient client) : BackgroundService
{
    private static readonly string DataPath = Path.Combine("data", "twitch.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly HttpClient _http = new();
    private readonly string? _clientId = config["Twitch:ClientId"];
    private readonly string? _clientSecret = config["Twitch:ClientSecret"];
    // userId → 綁定的 Twitch 頻道資訊
    private readonly Dictionary<ulong, TwitchBinding> _data = File.Exists(DataPath)
        ? JsonSerializer.Deserialize<Dictionary<ulong, TwitchBinding>>(File.ReadAllText(DataPath)) ?? []
        : [];

    private string _token = "";
    private DateTime _tokenExpiry = DateTime.MinValue;

    public bool IsEnabled { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
        {
            Log("未設定 Twitch:ClientId / Twitch:ClientSecret，Twitch 功能停用");
            return;
        }

        try
        {
            await RefreshTokenAsync(ct);
            IsEnabled = true;
            Log("Twitch API 驗證成功，開台偵測已啟用（每 30 秒輪詢）");
        }
        catch (Exception ex)
        {
            Log($"Twitch API 驗證失敗，Twitch 功能停用：{ex.Message}");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            foreach (var (userId, binding) in _data.ToArray())
            {
                try { await CheckAsync(userId, binding, ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex) { Log($"檢查 {binding.Login} 失敗：{ex.Message}"); }
            }
        }
    }

    /// <summary>驗證頻道存在並記錄綁定；回傳 null 表示頻道不存在。</summary>
    public async Task<TwitchBinding?> BindAsync(ulong userId, string channelLogin, string field, CancellationToken ct = default)
    {
        var users = await HelixGetAsync($"users?login={Uri.EscapeDataString(channelLogin)}", ct);
        if (users.GetArrayLength() == 0) return null;
        var user = users[0];

        var binding = new TwitchBinding
        {
            Login = user.GetProperty("login").GetString()!,
            TwitchUserId = user.GetProperty("id").GetString()!,
            DisplayName = user.GetProperty("display_name").GetString()!,
            Field = field,
        };

        // 用最新 VOD 時間當「上次直播時間」初始值；頻道未保留 VOD 就留空
        var videos = await HelixGetAsync($"videos?user_id={binding.TwitchUserId}&type=archive&first=1", ct);
        if (videos.GetArrayLength() > 0 && videos[0].TryGetProperty("created_at", out var created))
            binding.LastLiveAt = created.GetDateTime().ToUniversalTime();

        _data[userId] = binding;
        Save();
        return binding;
    }

    public bool Unbind(ulong userId)
    {
        if (!_data.Remove(userId)) return false;
        Save();
        return true;
    }

    private async Task CheckAsync(ulong userId, TwitchBinding b, CancellationToken ct)
    {
        var streams = await HelixGetAsync($"streams?user_id={b.TwitchUserId}&first=1", ct);
        var live = streams.GetArrayLength() > 0;

        // 只在狀態轉換時寫檔；下台當下記為上次直播時間
        if (live && !b.LiveNow) { b.LiveNow = true; Save(); }
        else if (!live && b.LiveNow) { b.LiveNow = false; b.LastLiveAt = DateTime.UtcNow; Save(); }

        var text = live
            ? "正在 Twitch 開台中!"
            : b.LastLiveAt is { } t ? $"上次直播時間: {t.ToLocalTime():yyyy/MM/dd HH:mm}" : null;
        if (text is null) return; // 查無直播紀錄，沒有資訊可顯示

        if (widgets.Get(userId).TryGetValue(b.Field, out var current) && current == text) return;
        widgets.Set(userId, b.Field, text);
        try
        {
            await widgets.PushAsync(userId);
        }
        catch (Exception ex)
        {
            // 值已儲存、下個 tick 因值相同不會重試，因此每次狀態轉換最多通知一次
            Log($"{b.Login} 推送失敗：{ex.Message}");
            await TryDmAsync(userId,
                $"⚠️ Twitch 資訊更新失敗（欄位 `{b.Field}`），可能是欄位填錯或其他問題：\n```\n{ex.Message}\n```");
            return;
        }
        Log($"{b.Login} → {text}");
    }

    /// <summary>私訊通知使用者；失敗只記 log，不影響輪詢。</summary>
    private async Task TryDmAsync(ulong userId, string message)
    {
        try
        {
            var user = await client.Rest.GetUserAsync(userId);
            if (user is null) return;
            var dm = await user.CreateDMChannelAsync();
            await dm.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            Log($"私訊通知 {userId} 失敗：{ex.Message}");
        }
    }

    /// <summary>GET Helix API，回傳回應中的 data 陣列；401 時重取 token 再試一次。</summary>
    private async Task<JsonElement> HelixGetAsync(string pathAndQuery, CancellationToken ct)
    {
        var res = await SendAsync(pathAndQuery, ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized)
        {
            res.Dispose();
            await RefreshTokenAsync(ct);
            res = await SendAsync(pathAndQuery, ct);
        }

        using (res)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new HttpRequestException($"{(int)res.StatusCode} {res.StatusCode}: {body}");
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("data").Clone();
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string pathAndQuery, CancellationToken ct)
    {
        if (DateTime.UtcNow >= _tokenExpiry) await RefreshTokenAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/{pathAndQuery}");
        req.Headers.Add("Client-Id", _clientId);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return await _http.SendAsync(req, ct);
    }

    /// <summary>Client credentials flow 取得 app access token。</summary>
    private async Task RefreshTokenAsync(CancellationToken ct)
    {
        var res = await _http.PostAsync(
            $"https://id.twitch.tv/oauth2/token?client_id={_clientId}&client_secret={_clientSecret}&grant_type=client_credentials",
            null, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)res.StatusCode} {res.StatusCode}: {body}");
        var json = JsonDocument.Parse(body).RootElement;
        _token = json.GetProperty("access_token").GetString()!;
        // 提前 5 分鐘視為過期，避免壓線失效
        _tokenExpiry = DateTime.UtcNow.AddSeconds(json.GetProperty("expires_in").GetInt32() - 300);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        File.WriteAllText(DataPath, JsonSerializer.Serialize(_data, JsonOptions));
    }

    private static void Log(string message) =>
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} Twitch      {message}");
}
