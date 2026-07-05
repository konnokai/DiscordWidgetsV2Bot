using Discord;
using Discord.Interactions;

namespace DiscordWidgetsV2Bot;

[Group("twitch", "Twitch 開台狀態偵測")]
[IntegrationType(ApplicationIntegrationType.UserInstall)]
[CommandContextType(InteractionContextType.Guild, InteractionContextType.BotDm, InteractionContextType.PrivateChannel)]
public class TwitchModule(TwitchService twitch) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("bind", "綁定 Twitch 頻道，開台時自動更新指定欄位")]
    public async Task Bind(string channel, StringField field = StringField.BottomDescription3)
    {
        if (!twitch.IsEnabled)
        {
            await RespondAsync("Twitch 功能未啟用（缺少或無效的 Twitch API 設定）。", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        // 接受頻道名稱或整段頻道網址
        var login = channel.Trim().TrimEnd('/');
        login = login[(login.LastIndexOf('/') + 1)..].ToLowerInvariant();

        try
        {
            var binding = await twitch.BindAsync(Context.User.Id, login, field.ApiName());
            if (binding is null)
            {
                await FollowupAsync($"找不到 Twitch 頻道 `{login}`。", ephemeral: true);
                return;
            }

            var last = binding.LastLiveAt is { } t
                ? $"上次直播時間: {t.ToLocalTime():yyyy/MM/dd HH:mm}"
                : "查無直播紀錄（頻道可能未保留 VOD），將於下次開台後開始記錄";
            await FollowupAsync(
                $"✅ 已綁定 **{binding.DisplayName}**（`{binding.Login}`）\n{last}\n" +
                $"每 30 秒檢查開台狀態並自動更新 `{binding.Field}`；更新失敗時會私訊通知。",
                ephemeral: true);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ 綁定失敗：\n```\n{ex.Message}\n```", ephemeral: true);
        }
    }

    [SlashCommand("unbind", "解除 Twitch 頻道綁定，停止開台偵測")]
    public async Task Unbind()
    {
        var removed = twitch.Unbind(Context.User.Id);
        await RespondAsync(removed ? "✅ 已解除綁定，停止開台偵測。" : "你尚未綁定任何 Twitch 頻道。", ephemeral: true);
    }
}
