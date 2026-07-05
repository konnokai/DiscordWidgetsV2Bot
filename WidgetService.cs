using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Discord.Interactions;
using Microsoft.Extensions.Configuration;

namespace DiscordWidgetsV2Bot;

// 欄位定義的唯一來源：ChoiceDisplay 的名稱對應 Developer Portal widget 編輯器的 Data Field，
// slash command 選項（enum 參數自動產生 Choice）與 API payload 都由此推導
public enum StringField
{
    [ChoiceDisplay("top-title")] TopTitle,
    [ChoiceDisplay("top-sub-title-1")] TopSubTitle1,
    [ChoiceDisplay("bottom-name-1")] BottomName1,
    [ChoiceDisplay("bottom-description-1")] BottomDescription1,
    [ChoiceDisplay("bottom-name-2")] BottomName2,
    [ChoiceDisplay("bottom-description-2")] BottomDescription2,
    [ChoiceDisplay("bottom-name-3")] BottomName3,
    [ChoiceDisplay("bottom-description-3")] BottomDescription3,
    [ChoiceDisplay("bottom-name-4")] BottomName4,
    [ChoiceDisplay("bottom-description-4")] BottomDescription4,
    [ChoiceDisplay("mini-profile-stat-text")] MiniProfileStatText,
}

public enum ImageField
{
    [ChoiceDisplay("top-image")] TopImage,
    [ChoiceDisplay("top-sub-icon-1")] TopSubIcon1,
    [ChoiceDisplay("bottom-image-1")] BottomImage1,
    [ChoiceDisplay("bottom-image-2")] BottomImage2,
    [ChoiceDisplay("bottom-image-3")] BottomImage3,
    [ChoiceDisplay("bottom-image-4")] BottomImage4,
    [ChoiceDisplay("mini-profile-stat-icon")] MiniProfileStatIcon,
    [ChoiceDisplay("mini-profile-contained-image")] MiniProfileContainedImage,
}

public static class WidgetFieldExtensions
{
    /// <summary>取得 API 欄位名（即 ChoiceDisplay 上的名稱）。</summary>
    public static string ApiName(this StringField field) => Name(field);
    public static string ApiName(this ImageField field) => Name(field);

    private static string Name<T>(T value) where T : struct, Enum =>
        typeof(T).GetField(value.ToString())!.GetCustomAttribute<ChoiceDisplayAttribute>()!.Name;
}

public class WidgetService
{
    public static readonly string[] StringFields = [.. Enum.GetValues<StringField>().Select(f => f.ApiName())];
    public static readonly string[] ImageFields = [.. Enum.GetValues<ImageField>().Select(f => f.ApiName())];

    private static readonly string DataPath = Path.Combine("data", "widgets.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly HttpClient _http = new();
    private readonly string _appId;
    // userId → (欄位名 → 值)；圖片欄位存 URL 字串
    private readonly Dictionary<ulong, Dictionary<string, string>> _data;

    public WidgetService(IConfiguration config)
    {
        var appId = config["Discord:ApplicationId"];
        if (string.IsNullOrWhiteSpace(appId))
            throw new InvalidOperationException("缺少設定 Discord:ApplicationId（環境變數 DISCORD__APPLICATIONID）");
        _appId = appId;
        _http.DefaultRequestHeaders.Add("Authorization", $"Bot {config["Discord:Token"]}");
        _http.DefaultRequestHeaders.Add("User-Agent", "DiscordBot (https://github.com/discord/discord-api-docs, 1.0.0)");
        _data = File.Exists(DataPath)
            ? JsonSerializer.Deserialize<Dictionary<ulong, Dictionary<string, string>>>(File.ReadAllText(DataPath)) ?? []
            : [];
    }

    public string ApplicationId => _appId;

    public IReadOnlyDictionary<string, string> Get(ulong userId) =>
        _data.TryGetValue(userId, out var fields) ? fields : new Dictionary<string, string>();

    public void Set(ulong userId, string field, string value)
    {
        if (!_data.TryGetValue(userId, out var fields)) _data[userId] = fields = [];
        fields[field] = value;
        Save();
    }

    public void Clear(ulong userId, string field)
    {
        if (_data.TryGetValue(userId, out var fields) && fields.Remove(field)) Save();
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
        File.WriteAllText(DataPath, JsonSerializer.Serialize(_data, JsonOptions));
    }

    /// <summary>把目前儲存的欄位推送到 Discord identity profile API；非 2xx 擲出含 response body 的例外。</summary>
    public async Task PushAsync(ulong userId)
    {
        var fields = Get(userId);
        var dynamic = new List<object>();
        foreach (var name in StringFields)
            if (fields.TryGetValue(name, out var v) && v.Length > 0)
                dynamic.Add(new { type = 1, name, value = v });
        foreach (var name in ImageFields)
            if (fields.TryGetValue(name, out var v) && v.Length > 0)
                dynamic.Add(new { type = 3, name, value = new { url = v } });

        // ponytail: payload 只帶 data.dynamic；若 API 回報缺必填欄位（如 username）再補
        var res = await _http.PatchAsJsonAsync(
            $"https://discord.com/api/v9/applications/{_appId}/users/{userId}/identities/0/profile",
            new { data = new { dynamic } });
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)res.StatusCode} {res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
    }
}
