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
    public async Task Set(StringField field, string value)
    {
        var name = field.ApiName();
        widgets.Set(Context.User.Id, name, value);
        await PushAndReportAsync($"`{name}` = {value}");
    }

    [SlashCommand("image", "設定圖片欄位（URL）並推送")]
    public async Task Image(ImageField field, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            await RespondAsync("URL 格式不正確，需為 http(s) 絕對網址。", ephemeral: true);
            return;
        }
        var name = field.ApiName();
        widgets.Set(Context.User.Id, name, url);
        await PushAndReportAsync($"`{name}` = <{url}>");
    }

    [SlashCommand("clear", "清除字串欄位值並推送")]
    public async Task Clear(StringField field)
    {
        var name = field.ApiName();
        widgets.Clear(Context.User.Id, name);
        await PushAndReportAsync($"已清除 `{name}`");
    }

    [SlashCommand("clear-image", "清除圖片欄位值並推送")]
    public async Task ClearImage(ImageField field)
    {
        var name = field.ApiName();
        widgets.Clear(Context.User.Id, name);
        await PushAndReportAsync($"已清除 `{name}`");
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
