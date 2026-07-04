# DiscordWidgetsV2Bot

讓 Discord application 擁有者透過 `/widget` slash command 自訂個人檔案上的「Widgets V2」小工具內容。C# / [Discord.Net](https://github.com/discord-net/Discord.Net)，user-install bot，資料以 JSON 檔持久化。

> ⚠️ Widgets V2 是 Social SDK 的**實驗功能**，本 bot 呼叫的是未文件化端點，Discord 可能隨時變更。且 2026/6/4 起僅 application 擁有者能把 widget 加到自己的個人檔案。

## 運作方式

```
PATCH https://discord.com/api/v9/applications/{appId}/users/{userId}/identities/0/profile
Body: { "data": { "dynamic": [
  { "type": 1, "name": "title", "value": "字串" },
  { "type": 3, "name": "image", "value": { "url": "https://..." } } ] } }
```

`name` 對應 Developer Portal widget 編輯器中設定的 Data Field 名稱（type 1=字串、3=圖片）。欄位定義寫死在 `WidgetService.cs`，指令選項在 `WidgetModule.cs`。

## 前置步驟（Developer Portal / Discord 客戶端手動操作）

1. 建立 application → Games → Social SDK 填表
2. 開啟 Widget 編輯頁需要啟用 `2026-03-widget-config-editor` experiment override。可用 [油猴腳本](#油猴腳本自動啟用-widget-編輯器)自動套用，或手動在 Developer Portal 的 DevTools console 執行 snippet
3. Widget 編輯器建立欄位（User Data 型別，名稱須與本專案欄位一致）、設 fallback，Save + Publish
4. OAuth2 頁加 redirect URI，之後透過 `/widget setup` 的按鈕授權（`openid` + `sdk.social_layer` scope）
5. Bot 頁取得 bot token
6. Installation 頁勾 **User Install**（不需 Guild Install）
7. 用 Discord Previews 的 console snippet 把 widget 加到個人檔案（`2026-03-application-widget-v2-renderer` experiment Variant 1）

詳細操作見下方參考文章。

## 油猴腳本：自動啟用 Widget 編輯器

`discord-widget-config-editor.user.js` 會在你進入 Developer Portal 應用頁面時，自動套用 `2026-03-widget-config-editor` experiment override，省去每次手動貼 console snippet。

**安裝：**

1. 先安裝 [Tampermonkey](https://www.tampermonkey.net/)（或 Violentmonkey）瀏覽器擴充
2. 點此一鍵安裝 → **[discord-widget-config-editor.user.js](https://raw.githubusercontent.com/konnokai/DiscordWidgetsV2Bot/master/discord-widget-config-editor.user.js)**（Tampermonkey 會攔截並跳出安裝畫面）
3. 重新整理 Developer Portal 的應用頁面（`https://discord.com/developers/applications/*`），即可看到 Widget 編輯入口

腳本每次頁面載入只會在 experiment store 就緒後套用一次，30 秒內未就緒則自動停止輪詢；已內建 `@updateURL`，之後可自動更新。

## 設定與執行

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
| `/widget clear <field>` | 清除欄位並推送 |
| `/widget show` | 顯示目前儲存的所有欄位 |
| `/widget refresh` | 手動重新推送 |

## 參考文案

- [Discord Widgets — chloecinders](https://chloecinders.com/blog/discord-widgets)：完整啟用流程、experiment override snippet、C#/Discord.Net 範例
- [Discord Widgets — rohan.run](https://www.rohan.run/writing/discord-widgets)：identity profile API 的 payload 結構整理
- [chloecinders/xivwidget](https://github.com/chloecinders/xivwidget)：同作者的 C# 參考實作
