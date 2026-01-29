# GitHub Copilot CLI Web Interface

基於 [GitHub Copilot SDK](https://github.com/github/copilot-sdk) 的 Web 介面應用程式，使用 .NET Web API + Vue 3 開發。

![Architecture](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![Vue](https://img.shields.io/badge/Vue-3.5-4FC08D?logo=vue.js)
![License](https://img.shields.io/badge/License-MIT-green)

## 📋 功能特性

- ✅ 與 GitHub Copilot CLI 完整整合
- 💬 即時聊天介面
- 🎨 現代化 UI 設計
- 🔄 支援多個 AI 模型切換 (Claude Sonnet 4.5, Claude Sonnet 4, GPT-4.1, Claude Haiku 4.5)
- 📱 響應式設計
- 🚀 快速部署

## 🏗️ 專案架構

```
CopilotCliWebApp/
├── Backend/              # .NET Web API
│   ├── Controllers/      # API 控制器
│   ├── Models/          # 資料模型
│   ├── Services/        # Copilot SDK 服務
│   └── Program.cs       # 應用程式進入點
│
└── Frontend/            # Vue 3 前端
    ├── src/
    │   ├── components/  # Vue 元件
    │   ├── services/    # API 服務
    │   └── App.vue      # 主應用程式
    └── vite.config.js   # Vite 設定
```

## 📦 系統需求

### 必要條件

1. **GitHub Copilot CLI**
   ```bash
   # 安裝 Copilot CLI
   gh extension install github/gh-copilot
   
   # 或從官方下載
   # https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli
   ```

2. **.NET SDK 10.0 或更新版本**
   ```bash
   # 檢查版本
   dotnet --version
   ```

3. **Node.js 18+ 和 npm**
   ```bash
   # 檢查版本
   node --version
   npm --version
   ```

4. **GitHub Copilot 訂閱**
   - 需要有效的 GitHub Copilot 訂閱帳號

## 🚀 快速開始

**第一次使用？** 請參閱 [⚡ 快速開始指南](./QUICKSTART.md) - 5 分鐘上手！

### 1. Clone 專案

```bash
git clone <your-repo-url>
cd CopilotCliWebApp
```

### 2. 啟動後端 API

```bash
cd Backend
dotnet restore
dotnet run
```

後端 API 將在 `http://localhost:5000` 啟動

### 3. 啟動前端應用

開啟新的終端機視窗：

```bash
cd Frontend
npm install
npm run dev
```

前端應用將在 `http://localhost:5173` 啟動

### 4. 開始使用

在瀏覽器中開啟 `http://localhost:5173`，開始與 Copilot CLI 互動！

## 🔧 API 端點

### Chat Controller

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/chat/session` | 建立新的對話會話 |
| POST | `/api/chat/send` | 發送訊息 |
| GET | `/api/chat/sessions` | 取得所有活動會話 |
| DELETE | `/api/chat/session/{id}` | 刪除會話 |

### 請求範例

**建立會話：**
```json
POST /api/chat/session
{
  "model": "gpt-5"
}
```

**發送訊息：**
```json
POST /api/chat/send
{
  "sessionId": "session-id",
  "prompt": "Hello, Copilot!"
}
```

## 🎨 前端元件

### ChatInterface.vue

主要聊天介面元件，包含：
- 訊息顯示區域
- 輸入框
- 模型選擇器
- 新對話按鈕

### copilotService.js

API 通訊服務，處理所有與後端的互動。

## ⚙️ 設定

### 修改 API 位址

編輯 `Frontend/src/services/copilotService.js`：

```javascript
const API_BASE_URL = 'http://your-api-url/api';
```

### 修改後端埠號

編輯 `Backend/Properties/launchSettings.json` 或在 `Program.cs` 中設定。

### 選擇預設模型

在 `Backend/Models/ChatMessage.cs` 修改預設模型：

```csharp
public string? Model { get; set; } = "claude-sonnet-4.5";
```

### 可用的模型名稱

根據 GitHub Copilot 官方文檔，以下是可用的模型：

**Anthropic 模型：**
- `claude-sonnet-4.5` (推薦，預設)
- `claude-sonnet-4`
- `claude-haiku-4.5`
- `claude-opus-4.5`

**OpenAI 模型：**
- `gpt-4.1`
- `gpt-5-mini`
- `gpt-5.1`
- `gpt-5.2`

**其他模型：**
- `gemini-3-pro`
- `gemini-3-flash`

注意：部分模型可能因訂閱計畫、地理位置或組織政策而有所不同。

## 🧪 測試

### 測試後端 API

```bash
# 使用 curl 測試
curl -X POST http://localhost:5000/api/chat/session \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-5"}'
```

### 測試前端

```bash
cd Frontend
npm run build
npm run preview
```

## 📝 開發注意事項

1. **Copilot CLI 必須已安裝並已驗證**
   ```bash
   copilot --version
   ```

2. **CORS 設定**
   - 開發環境已設定允許所有來源
   - 正式環境請修改 `Program.cs` 中的 CORS 設定

3. **錯誤處理**
   - 後端會捕捉並記錄所有錯誤
   - 前端顯示使用者友善的錯誤訊息

## 🐛 常見問題

### Q: 顯示 "Failed to start Copilot CLI client"

**A:** 請確認：
- Copilot CLI 已正確安裝
- 已使用 `gh auth login` 登入 GitHub
- 有效的 Copilot 訂閱

### Q: 前端無法連接後端

**A:** 檢查：
- 後端是否正在執行 (port 5000)
- CORS 設定是否正確
- 瀏覽器控制台的錯誤訊息

### Q: 訊息發送失敗

**A:** 確認：
- 會話是否已建立
- SessionId 是否有效
- 後端日誌中的錯誤訊息

## 📚 相關資源

### 專案文件
- [⚡ 快速開始 (QUICKSTART.md)](./QUICKSTART.md) - 5 分鐘快速上手
- [📖 開發者指南 (PROJECT_GUIDE.md)](./PROJECT_GUIDE.md) - 完整開發說明
- [🔧 故障排除 (TROUBLESHOOTING.md)](./TROUBLESHOOTING.md) - 問題解決方案
- [📝 修正摘要 (FIX_SUMMARY.md)](./FIX_SUMMARY.md) - 問題修正記錄
- [📁 檔案清單 (FILE_LIST.md)](./FILE_LIST.md) - 完整檔案結構

### 官方資源

- [GitHub Copilot SDK 文檔](https://github.com/github/copilot-sdk)
- [.NET Copilot SDK README](https://github.com/github/copilot-sdk/blob/main/dotnet/README.md)
- [Copilot CLI 安裝指南](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli)
- [Vue 3 文檔](https://vuejs.org/)
- [Vite 文檔](https://vitejs.dev/)

## 📄 授權

MIT License

## 🤝 貢獻

歡迎提交 Issue 和 Pull Request！

## 👨‍💻 作者

由 GitHub Copilot CLI 協助開發
