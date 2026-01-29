# 故障排除指南 (Troubleshooting Guide)

## 常見錯誤與解決方案

### 1. ❌ "Failed to start Copilot CLI client"

**錯誤訊息：**
```
System.IO.IOException: Communication error with Copilot CLI
StreamJsonRpc.ConnectionLostException: 與遠端合作物件的 JSON-RPC 連線在要求完成之前已中斷
```

**可能原因：**
- Copilot CLI 未安裝或不在 PATH 中
- Copilot CLI 版本過舊
- 未登入 GitHub 帳號
- 權限問題

**解決方法：**

1. **檢查 Copilot CLI 是否已安裝**
   ```bash
   copilot --version
   ```
   
   如果未安裝：
   ```bash
   # 使用 gh CLI 安裝
   gh extension install github/gh-copilot
   
   # 或下載獨立版本
   # https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli
   ```

2. **檢查 GitHub 認證狀態**
   ```bash
   gh auth status
   ```
   
   如果未登入：
   ```bash
   gh auth login
   ```

3. **更新 Copilot CLI**
   ```bash
   copilot update
   ```

4. **測試 Copilot CLI**
   ```bash
   copilot -p "hello"
   ```

---

### 2. ❌ "The requested model is not supported"

**錯誤訊息：**
```
CAPIError: 400 The requested model is not supported
```

**原因：**
使用了不正確的模型名稱格式

**正確的模型名稱：**

✅ **正確格式（小寫 + 破折號）：**
- `claude-sonnet-4.5` ✓
- `claude-sonnet-4` ✓
- `gpt-4.1` ✓
- `gpt-5-mini` ✓
- `claude-haiku-4.5` ✓

❌ **錯誤格式：**
- `GPT-5` ✗ (大寫)
- `gpt5` ✗ (缺少破折號)
- `Claude Sonnet 4.5` ✗ (有空格)

**解決方法：**

檢查並更新程式碼中的模型名稱：
```csharp
// Backend/Models/ChatMessage.cs
public string? Model { get; set; } = "claude-sonnet-4.5"; // 正確
```

```javascript
// Frontend/src/components/ChatInterface.vue
const selectedModel = ref('claude-sonnet-4.5'); // 正確
```

---

### 3. ❌ "Session not found"

**錯誤訊息：**
```
InvalidOperationException: Session {sessionId} not found
```

**原因：**
- 會話已過期或被刪除
- SessionId 不正確
- 後端重啟導致會話丟失

**解決方法：**
1. 點擊前端的「新對話」按鈕建立新會話
2. 確認 SessionId 正確傳遞
3. 檢查後端日誌確認會話狀態

---

### 4. ❌ CORS 錯誤

**錯誤訊息（瀏覽器控制台）：**
```
Access to fetch at 'http://localhost:5000/api/chat/session' from origin 'http://localhost:5173' 
has been blocked by CORS policy
```

**解決方法：**

確認 `Backend/Program.cs` 中的 CORS 設定：
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ...

app.UseCors("AllowAll");
```

---

### 5. ❌ 前端無法連接後端

**症狀：**
- 發送訊息無回應
- 控制台顯示網路錯誤

**檢查清單：**

1. **確認後端正在運行**
   ```bash
   # 檢查 port 5000 是否在監聽
   netstat -ano | findstr :5000   # Windows
   lsof -i :5000                   # Linux/Mac
   ```

2. **檢查 API 位址**
   ```javascript
   // Frontend/src/services/copilotService.js
   const API_BASE_URL = 'http://localhost:5000/api'; // 確認正確
   ```

3. **測試 API 端點**
   ```bash
   curl -X POST http://localhost:5000/api/chat/session \
     -H "Content-Type: application/json" \
     -d '{"model":"claude-sonnet-4.5"}'
   ```

---

## 診斷命令

### 系統環境檢查

```bash
# 檢查 .NET 版本
dotnet --version
# 應該 >= 10.0

# 檢查 Node.js 版本
node --version
# 應該 >= 18.0

# 檢查 npm 版本
npm --version

# 檢查 Copilot CLI
copilot --version
# 應該 >= 0.0.398

# 檢查 GitHub 認證
gh auth status
```

### 後端診斷

```bash
cd Backend

# 清理並重建
dotnet clean
dotnet restore
dotnet build

# 詳細日誌模式執行
dotnet run --verbosity detailed

# 檢查套件
dotnet list package
```

### 前端診斷

```bash
cd Frontend

# 清理並重新安裝
rm -rf node_modules package-lock.json
npm install

# 檢查套件版本
npm list

# 建置測試
npm run build
```

---

## 測試流程

### 1. 測試 Copilot CLI

```bash
# 基本測試
copilot -p "hello"

# 測試特定模型
copilot -p "hello" --model claude-sonnet-4.5

# 查看可用模型
copilot -p "/models"
```

### 2. 測試後端 API

```bash
# 建立會話
curl -X POST http://localhost:5000/api/chat/session \
  -H "Content-Type: application/json" \
  -d '{"model":"claude-sonnet-4.5"}'

# 記下回傳的 sessionId，然後發送訊息
curl -X POST http://localhost:5000/api/chat/send \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "YOUR_SESSION_ID",
    "prompt": "Hello, what can you do?"
  }'

# 查看所有會話
curl http://localhost:5000/api/chat/sessions
```

### 3. 測試前端

1. 開啟瀏覽器開發者工具 (F12)
2. 切換到 Console 標籤
3. 切換到 Network 標籤
4. 嘗試發送訊息
5. 查看請求/回應內容

---

## 效能最佳化

### 1. 會話管理

如果會話過多導致記憶體問題：

```csharp
// Backend/Services/CopilotService.cs
// 加入會話清理邏輯
private Timer _cleanupTimer;

public CopilotService(ILogger<CopilotService> logger)
{
    _logger = logger;
    // 每 30 分鐘清理閒置會話
    _cleanupTimer = new Timer(CleanupIdleSessions, null, 
        TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
}

private void CleanupIdleSessions(object? state)
{
    // 實作清理邏輯
}
```

### 2. 連接池管理

目前使用 Singleton 模式，如需更細緻控制：

```csharp
// 改用 Scoped 生命週期
builder.Services.AddScoped<CopilotService>();
```

---

## 日誌分析

### 啟用詳細日誌

在 `Backend/appsettings.Development.json` 中：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "CopilotApi": "Debug"
    }
  }
}
```

### 查看日誌

後端日誌會顯示：
- Session 建立/刪除
- 訊息發送/接收
- 錯誤堆疊追蹤

---

## 常見問題 FAQ

### Q: 為什麼有些模型我無法使用？

A: 模型可用性取決於：
- 您的 Copilot 訂閱類型（個人版/商業版）
- 地理位置
- 組織政策設定
- 模型是否已被標記為退役

### Q: 如何切換到自己的 API Key (BYOK)？

A: 修改 `Backend/Services/CopilotService.cs`：

```csharp
var session = await _client!.CreateSessionAsync(new SessionConfig
{
    Model = model,
    Streaming = true,
    Provider = new ProviderConfig
    {
        Type = "openai",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = "your-api-key"
    }
});
```

### Q: 如何實作 WebSocket 即時串流？

A: 當前版本使用輪詢，如需 WebSocket：
1. 安裝 `Microsoft.AspNetCore.SignalR` 套件
2. 建立 SignalR Hub
3. 修改前端使用 `@microsoft/signalr` 客戶端

### Q: 回應很慢怎麼辦？

A: 可能原因：
- 模型回應時間較長（嘗試使用 mini 版本）
- 網路延遲
- Copilot CLI 第一次啟動較慢（之後會快）

---

## 獲取幫助

如果問題仍未解決：

1. **查看後端日誌** - 詳細錯誤堆疊
2. **查看瀏覽器控制台** - 前端錯誤訊息
3. **測試 Copilot CLI 獨立運作** - 排除 CLI 本身問題
4. **檢查 GitHub 狀態** - https://www.githubstatus.com/
5. **查看 SDK 文檔** - https://github.com/github/copilot-sdk
6. **提交 Issue** - 包含完整錯誤訊息和環境資訊

---

最後更新：2026-01-29
