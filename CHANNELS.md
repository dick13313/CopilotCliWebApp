# 📡 通訊軟體整合

此專案支援多通訊軟體通道擴充，目前已實作 **Telegram**。

## ✅ 已支援通道

| 通道 | 狀態 | 說明 |
|------|------|------|
| Telegram | ✅ 已實作 | 使用 Telegram Bot 與 Copilot CLI 對話 |
| Discord | 🕒 預留 | 尚未實作，可擴充 |
| Slack | 🕒 預留 | 尚未實作，可擴充 |
| LINE | 🕒 預留 | 尚未實作，可擴充 |

## 🧩 通道介面設計

所有通訊通道皆實作 `IChatChannel` 介面：

```csharp
public interface IChatChannel
{
    string Name { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

通道由 `ChannelService` 統一管理生命週期。

## 🤖 Telegram Bot 設定

### 1. 取得 Bot Token

1. 在 Telegram 搜尋 `@BotFather`
2. 輸入 `/newbot`
3. 取得 Bot Token

### 2. 設定 appsettings.json

```json
{
  "Telegram": {
    "BotToken": "YOUR_BOT_TOKEN",
    "AllowedChatId": 123456789,
    "DefaultModel": "claude-sonnet-4.5"
  }
}
```

| 欄位 | 說明 |
|------|------|
| BotToken | Telegram Bot Token |
| AllowedChatId | 限制可用的聊天 ID（可為 null 代表不限制） |
| DefaultModel | 預設模型名稱 |

### 3. 取得 Chat ID

1. 與 Bot 發送任意訊息
2. 查看後端日誌
3. 找到 log 中的 chatId

### 4. 啟動服務

```bash
cd Backend
dotnet run
```

## 🧪 測試 Telegram

1. 打開 Telegram
2. 搜尋你的 Bot
3. 發送訊息
4. Bot 會回應 Copilot CLI 結果

## 💬 Telegram 指令

### Session 管理
- `/session list` - 列出所有 session（含狀態/模型）
- `/session use <編號|sessionId>` - 切換目前 session
- `/session new` - 建立並切換到新 session
- `/session close <編號|sessionId>` - 關閉指定 session
- `/session status [編號|sessionId]` - 查詢 session 狀態與最近回覆
- `/new` - 建立新 session 並切換
- `/use <編號|sessionId>` - 切換 session
- `/list` - 列出所有 session
- `/close <編號|sessionId>` - 關閉指定 session
- `/status [編號|sessionId]` - 查詢 session 狀態與最近回覆

### 批次任務
- `/task <prompt>` - 自動建立新 session 指派任務
- `/task <編號1,編號2> <prompt>` - 同時指派多個 session（並行）

### 回覆格式
- 每次回覆都會附上 SessionId 與狀態，方便識別任務歸屬。

### 其他
- `/help` - 顯示指令列表

## 🛠️ 擴充新通道（範例）

建立新的通道類別，例如 `DiscordChannel`：

```csharp
public class DiscordChannel : IChatChannel
{
    public string Name => "discord";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // TODO: connect Discord bot
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // TODO: disconnect
        return Task.CompletedTask;
    }
}
```

然後在 `Program.cs` 註冊：

```csharp
builder.Services.AddSingleton<IChatChannel, DiscordChannel>();
```

## 📚 API 端點

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/channel` | 取得所有通道狀態 |
| GET | `/api/channel/telegram` | 取得 Telegram 設定 |

## ✅ 完成狀態

- [x] 通道介面設計
- [x] Telegram 實作
- [x] 通道狀態 API
- [x] 前端 Channels UI 頁面
- [ ] Discord
- [ ] Slack
- [ ] LINE

---

如需擴充其他通訊軟體，請依照上述模式新增通道類別與設定。

## 🗂️ Copilot CLI 預設目錄

如果你希望 Copilot CLI 使用指定工作目錄，可在 `appsettings.json` 設定：

```json
{
  "CopilotCli": {
    "WorkingDirectory": "C:\\Projects\\MyWorkspace"
  }
}
```

> Linux/Mac 範例：`/home/user/projects`
