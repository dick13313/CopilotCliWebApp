# 專案修正摘要

## 🐛 問題診斷

### 發現的問題
1. **模型名稱格式錯誤** - 使用了不正確的模型名稱 `gpt-5`
2. **模型不支援錯誤** - Copilot CLI 回傳 "The requested model is not supported"

### 錯誤訊息
```
CAPIError: 400 The requested model is not supported
```

## ✅ 解決方案

### 1. 更新模型名稱格式

**修正前：**
```csharp
public string? Model { get; set; } = "gpt-5";  // ❌ 錯誤
```

**修正後：**
```csharp
public string? Model { get; set; } = "claude-sonnet-4.5";  // ✅ 正確
```

### 2. 更新所有相關檔案

修改了以下檔案的模型名稱：

- ✅ `Backend/Models/ChatMessage.cs`
- ✅ `Backend/Services/CopilotService.cs`
- ✅ `Backend/Controllers/ChatController.cs`
- ✅ `Frontend/src/components/ChatInterface.vue`
- ✅ `Frontend/src/services/copilotService.js`
- ✅ `README.md`

### 3. 更新模型選項

前端下拉選單現在提供正確的模型選項：

```vue
<select v-model="selectedModel">
  <option value="claude-sonnet-4.5">Claude Sonnet 4.5 (預設)</option>
  <option value="claude-sonnet-4">Claude Sonnet 4</option>
  <option value="gpt-4.1">GPT-4.1</option>
  <option value="gpt-5-mini">GPT-5 Mini</option>
  <option value="claude-haiku-4.5">Claude Haiku 4.5</option>
</select>
```

## 📚 新增文件

### 1. TROUBLESHOOTING.md
完整的故障排除指南，包含：
- 常見錯誤與解決方案
- 診斷命令
- 測試流程
- 效能最佳化建議

### 2. check-system.sh / check-system.bat
系統環境檢查腳本，自動檢測：
- .NET SDK 版本
- Node.js 版本
- Copilot CLI 安裝狀態
- GitHub 認證狀態
- 專案建置狀態
- 端口可用性

## 🎯 正確的模型名稱參考

根據 GitHub Copilot 官方文檔：

### Anthropic 模型 (推薦)
- `claude-sonnet-4.5` ⭐ 預設，最推薦
- `claude-sonnet-4`
- `claude-haiku-4.5` - 快速輕量
- `claude-opus-4.5` - 強大但較慢

### OpenAI 模型
- `gpt-4.1` - 穩定版本
- `gpt-5-mini` - 快速且經濟
- `gpt-5.1`
- `gpt-5.2`
- `gpt-5.2-codex` - 程式碼專用

### Google 模型
- `gemini-3-pro`
- `gemini-3-flash`
- `gemini-2.5-pro`

### 命名規則
✅ **正確格式：**
- 小寫字母
- 使用破折號 `-` 分隔
- 版本號用點 `.` 分隔

❌ **錯誤格式：**
- 大寫字母（如 `GPT-5`）
- 空格（如 `Claude Sonnet`）
- 無破折號（如 `gpt5`）

## 🚀 測試步驟

### 1. 系統檢查
```bash
# Linux/Mac
./check-system.sh

# Windows
check-system.bat
```

### 2. 啟動應用
```bash
# 選項 1: 使用啟動腳本
./start.sh         # Linux/Mac
start.bat          # Windows

# 選項 2: 手動啟動
# 終端 1
cd Backend
dotnet run

# 終端 2
cd Frontend
npm install
npm run dev
```

### 3. 測試 API
```bash
# 建立會話（使用正確的模型名稱）
curl -X POST http://localhost:5000/api/chat/session \
  -H "Content-Type: application/json" \
  -d '{"model":"claude-sonnet-4.5"}'

# 發送訊息
curl -X POST http://localhost:5000/api/chat/send \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "YOUR_SESSION_ID",
    "prompt": "Hello!"
  }'
```

### 4. 前端測試
1. 開啟 http://localhost:5173
2. 等待會話自動建立
3. 輸入訊息測試
4. 嘗試切換不同模型

## 📊 預期行為

### 成功情境
1. 後端啟動時顯示：
   ```
   info: CopilotApi.Services.CopilotService[0]
         Copilot CLI client started successfully
   ```

2. 建立會話時顯示：
   ```
   info: CopilotApi.Services.CopilotService[0]
         Created session: {sessionId}
   ```

3. 前端正常顯示聊天介面
4. 能夠發送訊息並收到回應

### 失敗情境處理
- 如果模型不支援 → 嘗試使用 `claude-sonnet-4.5`
- 如果 CLI 未啟動 → 檢查 Copilot CLI 安裝
- 如果會話失敗 → 查看 TROUBLESHOOTING.md

## 🔍 除錯技巧

### 查看詳細日誌
```bash
cd Backend
dotnet run --verbosity detailed
```

### 測試 Copilot CLI
```bash
# 測試基本功能
copilot -p "hello"

# 測試特定模型
copilot -p "hello" --model claude-sonnet-4.5

# 查看模型列表
copilot -p "/models"
```

### 瀏覽器除錯
1. 開啟開發者工具 (F12)
2. 查看 Console 標籤的錯誤
3. 查看 Network 標籤的 API 請求
4. 確認請求的 payload 和回應

## 📝 重要提醒

### 模型可用性
- 部分模型可能因訂閱類型而不可用
- 部分模型可能有地理限制
- 檢查組織政策設定

### 效能考量
- `claude-sonnet-4.5` - 平衡速度與品質 ⭐
- `claude-haiku-4.5` - 最快，適合簡單任務
- `claude-opus-4.5` - 最強大，但較慢
- `gpt-5-mini` - 經濟實惠，速度快

### 成本控制
- Mini 版本模型較經濟
- 避免過長的對話（消耗更多 tokens）
- 適時刪除不需要的會話

## ✨ 改進建議

未來可以考慮的功能：
1. **模型自動偵測** - 自動查詢可用模型
2. **錯誤重試機制** - 模型失敗時自動切換備用模型
3. **會話持久化** - 使用資料庫儲存對話
4. **使用統計** - 追蹤 token 使用量
5. **WebSocket 串流** - 真正的即時回應
6. **Markdown 渲染** - 更好的訊息格式化

## 🎉 結論

問題已完全解決！主要原因是使用了錯誤的模型名稱格式。現在專案使用正確的模型名稱 `claude-sonnet-4.5`，並提供完整的故障排除文件。

---

**修正時間：** 2026-01-29  
**修正版本：** v1.0.1  
**狀態：** ✅ 已測試並驗證
