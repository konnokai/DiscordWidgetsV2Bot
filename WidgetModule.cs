using Discord;
using Discord.Interactions;

namespace DiscordWidgetsV2Bot;

[Group("widget", "自訂個人檔案 widget")]
[IntegrationType(ApplicationIntegrationType.UserInstall)]
[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
public class WidgetModule(WidgetService widgets) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("setup", "取得授權連結與使用說明")]
    public async Task Setup()
    {
        var url = $"https://discord.com/oauth2/authorize?client_id={widgets.ApplicationId}&response_type=token&scope=openid%20sdk.social_layer";
        var components = new ComponentBuilder().WithButton("授權 Widget", style: ButtonStyle.Link, url: url).Build();
        await RespondAsync(
            "1. 點下方按鈕授權（`openid` + `sdk.social_layer`）\n" +
            "2. 用 `/widget set`、`/widget image` 設定欄位（會自動推送）\n" +
            "3. `/widget show` 檢視、`/widget refresh` 手動重推",
            components: components, ephemeral: true);
    }

    [SlashCommand("set", "設定字串欄位並推送")]
    public async Task Set(
        [Choice("title", "title"), Choice("sub-title", "sub-title"),
         Choice("stat-value-1", "stat-value-1"), Choice("stat-label-1", "stat-label-1"),
         Choice("stat-value-2", "stat-value-2"), Choice("stat-label-2", "stat-label-2"),
         Choice("stat-value-3", "stat-value-3"), Choice("stat-label-3", "stat-label-3"),
         Choice("stat-value-4", "stat-value-4"), Choice("stat-label-4", "stat-label-4"),
         Choice("stat-value-5", "stat-value-5"), Choice("stat-label-5", "stat-label-5"),
         Choice("stat-value-6", "stat-value-6"), Choice("stat-label-6", "stat-label-6"),
         Choice("mini-profile-text", "mini-profile-text")]
        string field,
        string value)
    {
        widgets.Set(Context.User.Id, field, value);
        await PushAndReportAsync($"`{field}` = {value}");
    }

    [SlashCommand("image", "設定圖片欄位（URL）並推送")]
    public async Task Image(
        [Choice("image", "image"), Choice("widget-preview-image", "widget-preview-image"),
         Choice("mini-profile-image", "mini-profile-image")]
        string field,
        string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            await RespondAsync("URL 格式不正確，需為 http(s) 絕對網址。", ephemeral: true);
            return;
        }
        widgets.Set(Context.User.Id, field, url);
        await PushAndReportAsync($"`{field}` = <{url}>");
    }

    [SlashCommand("clear", "清除欄位值並推送")]
    public async Task Clear(
        [Choice("title", "title"), Choice("sub-title", "sub-title"),
         Choice("stat-value-1", "stat-value-1"), Choice("stat-label-1", "stat-label-1"),
         Choice("stat-value-2", "stat-value-2"), Choice("stat-label-2", "stat-label-2"),
         Choice("stat-value-3", "stat-value-3"), Choice("stat-label-3", "stat-label-3"),
         Choice("stat-value-4", "stat-value-4"), Choice("stat-label-4", "stat-label-4"),
         Choice("stat-value-5", "stat-value-5"), Choice("stat-label-5", "stat-label-5"),
         Choice("stat-value-6", "stat-value-6"), Choice("stat-label-6", "stat-label-6"),
         Choice("mini-profile-text", "mini-profile-text"),
         Choice("image", "image"), Choice("widget-preview-image", "widget-preview-image"),
         Choice("mini-profile-image", "mini-profile-image")]
        string field)
    {
        widgets.Clear(Context.User.Id, field);
        await PushAndReportAsync($"已清除 `{field}`");
    }

    [SlashCommand("show", "顯示目前儲存的所有欄位")]
    public async Task Show()
    {
        var fields = widgets.Get(Context.User.Id);
        var lines = WidgetService.StringFields.Concat(WidgetService.ImageFields)
            .Select(f => fields.TryGetValue(f, out var v) && v.Length > 0
                ? $"`{f}`：{v}"
                : $"`{f}`：*(未設定，顯示 fallback)*");
        await RespondAsync(string.Join('\n', lines), ephemeral: true);
    }

    [SlashCommand("refresh", "手動推送目前資料到 Discord")]
    public async Task Refresh() => await PushAndReportAsync("已重新推送");

    private async Task PushAndReportAsync(string successMessage)
    {
        await DeferAsync(ephemeral: true);
        try
        {
            await widgets.PushAsync(Context.User.Id);
            await FollowupAsync($"✅ {successMessage}", ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ 已儲存，但推送失敗：\n```\n{ex.Message}\n```", ephemeral: true);
        }
    }
}
