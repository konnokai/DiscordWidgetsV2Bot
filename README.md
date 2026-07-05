# DiscordWidgetsV2Bot

讓 Discord application 擁有者透過 `/widget` slash command 自訂個人檔案上的「Widgets V2」小工具內容。C# / [Discord.Net](https://github.com/discord-net/Discord.Net)，user-install bot，資料以 JSON 檔持久化。

> ⚠️ Widgets V2 是 Social SDK 的**實驗功能**，本 bot 呼叫的是未文件化端點，Discord 可能隨時變更。且 2026/6/4 起僅 application 擁有者能把 widget 加到自己的個人檔案。

## 運作方式

```
PATCH https://discord.com/api/v9/applications/{appId}/users/{userId}/identities/0/profile
Body: { "data": { "dynamic": [
  { "type": 1, "name": "top-title", "value": "字串" },
  { "type": 3, "name": "top-image", "value": { "url": "https://..." } } ] } }
```

`name` 對應 Developer Portal widget 編輯器中設定的 Data Field 名稱（type 1=字串、2=數值、3=圖片）。欄位定義集中在 `WidgetService.cs` 的 `StringField` / `ImageField` enum（`[ChoiceDisplay]` 即 API 欄位名），指令選項自動由 enum 產生，改欄位只需要動 enum。

### 欄位一覽（共 19 個，需與 widget 編輯器的 Data Field 名稱一致）

| 區塊 | 字串欄位（type 1） | 圖片欄位（type 3） |
|---|---|---|
| Widget Top | `top-title`、`top-sub-title-1` | `top-image`、`top-sub-icon-1` |
| Widget Bottom（項目 1–4） | `bottom-name-N`、`bottom-description-N` | `bottom-image-N` |
| Mini Profile | `mini-profile-stat-text` | `mini-profile-stat-icon`、`mini-profile-contained-image` |

## 前置步驟（Developer Portal / Discord 客戶端手動操作）

1. 建立應用程式 → 遊戲 → Social SDK 填表啟用
2. 啟用 Widget 編輯頁 (可安裝 [油猴腳本](#油猴腳本自動啟用-widget-編輯器) 自動套用，或手動在 Developer Portal 的 DevTools console 執行腳本)

DevTools console 腳本
```js
let _mods = webpackChunkdiscord_developers.push([[Symbol()],{},r=>r.c]);
webpackChunkdiscord_developers.pop();

let findByProps = (...props) => {
    for (let m of Object.values(_mods)) {
        try {
            if (!m.exports || m.exports === window) continue;
            if (props.every((x) => m.exports?.[x])) return m.exports;

            for (let ex in m.exports) {
                if (props.every((x) => m.exports?.[ex]?.[x]) && m.exports[ex][Symbol.toStringTag] !== 'IntlMessagesProxy') return m.exports[ex];
            }
        } catch {}
    }
}

findByProps("getAll").getAll().find(e=>e.getName() === "ApexExperimentStore").createOverride("2026-03-widget-config-editor", 1)
```
（跑完腳本後記得點左上角的倒退箭頭再重新點到你的應用程式頁面，切記不可重整頁面，不然腳本會被覆寫）

3. 開啟 Widget 編輯頁並建立欄位（User Data 型別，名稱須與本專案欄位一致）、設定 fallback 避免忘記設定導致欄位空白，保存後記得發布
4. OAuth2 頁設定 redirect URI (重新導向) 為 `https://discord.com` 後保存
5. OAuth2 URL 產生器 → 勾選 `openid` 以及 `sdk.social_layer` → `選擇重新導向的 URI` 選擇剛剛設定的重新導向網址（應該要是 `https://discord.com`）
6. 複製產生的 URL，將 URL 中的 `response_type=code` 改為 `response_type=token`
7. 開啟修改後的 URL，並授權 Discord 權限
8. Bot 頁取得 bot token
9. 安裝頁勾 `User Install`（不需 Guild Install），安裝連結選 `Discord 提供的連結`，預設安裝設定的範圍選 `applications.commands`
10. 複製安裝連結並開啟，然後點選授權
11. 回到 Widget 編輯頁編輯到滿意為止（建議每次發布後可以重開 Discord 讓他更快的生效）

詳細操作見下方參考文章。

## 油猴腳本：自動啟用 Widget 編輯器

`discord-widget-config-editor.user.js` 會在你進入 Developer Portal 應用頁面時，自動套用 `2026-03-widget-config-editor` experiment override，省去每次手動貼 console snippet。

**安裝：**

1. 先安裝 [Tampermonkey](https://www.tampermonkey.net/)（或 Violentmonkey）瀏覽器擴充
2. 點此一鍵安裝 → **[discord-widget-config-editor.user.js](https://raw.githubusercontent.com/konnokai/DiscordWidgetsV2Bot/master/discord-widget-config-editor.user.js)**（Tampermonkey 會攔截並跳出安裝畫面）
3. 重新整理 Developer Portal 的應用頁面（`https://discord.com/developers/applications/*`），即可看到 Widget 編輯入口

腳本每次頁面載入只會在 experiment store 就緒後套用一次，30 秒內未就緒則自動停止輪詢；已內建 `@updateURL`，之後可自動更新。

## 機器人設定與執行

```powershell
# 本機
$env:DISCORD__TOKEN = '...'
$env:DISCORD__APPLICATIONID = '...'
dotnet run

# Docker
cp .env.example .env   # 填入 token 與 application ID
docker compose up -d --build
```

欄位資料存在 `data/widgets.json`（Docker 以 volume 掛載）。

## 指令

| 指令 | 說明 |
|---|---|
| `/widget setup` | 授權連結與使用說明 |
| `/widget set <field> <value>` | 設定字串欄位並推送 |
| `/widget image <field> <url>` | 設定圖片欄位並推送 |
| `/widget clear <field>` | 清除字串欄位並推送 |
| `/widget clear-image <field>` | 清除圖片欄位並推送 |
| `/widget show` | 顯示目前儲存的所有欄位 |
| `/widget refresh` | 手動重新推送 |
| `/twitch bind <channel> [field]` | 綁定 Twitch 頻道（接受名稱或網址），啟用開台偵測；可指定要更新的字串欄位 |
| `/twitch unbind` | 解除綁定，停止開台偵測 |

## Twitch 開台偵測（選用）

綁定 Twitch 頻道後，bot 每 30 秒輪詢 [Twitch Helix API](https://dev.twitch.tv/docs/api/)，自動更新綁定時指定的字串欄位（預設 `bottom-description-3`）：

- 開台中 → `正在 Twitch 開台中!`
- 未開台 → `上次直播時間: yyyy/MM/dd HH:mm`（綁定時以最新 VOD 時間初始化，之後於下台當下記錄）

僅在欄位內容實際變化時才推送到 Discord（開台／下台狀態轉換），不會每 30 秒都打 identity API。若推送失敗（例如欄位與 Developer Portal 設定不符），會略過該次更新並私訊通知，同一次狀態轉換只通知一次。

**啟用方式**：到 [Twitch Developer Console](https://dev.twitch.tv/console/apps) 建立 application，取得 Client ID 與 Client Secret，設定 `TWITCH__CLIENTID` / `TWITCH__CLIENTSECRET` 環境變數。啟動時會用 client credentials flow 驗證憑證，**未設定或驗證失敗只會停用 Twitch 功能**（console 有 log），`/widget` 等其他指令不受影響。

綁定資料存於 `data/twitch.json`。時間以容器時區顯示（docker-compose 已設 `TZ=Asia/Taipei`）。

## 參考文案

- [Discord Widgets — chloecinders](https://chloecinders.com/blog/discord-widgets)：完整啟用流程、experiment override snippet、C#/Discord.Net 範例
- [Discord Widgets — rohan.run](https://www.rohan.run/writing/discord-widgets)：identity profile API 的 payload 結構整理
- [chloecinders/xivwidget](https://github.com/chloecinders/xivwidget)：同作者的 C# 參考實作
