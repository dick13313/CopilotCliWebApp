# 專案快速指南

## 📁 專案檔案結構

```
CopilotCliWebApp/
│
├── Backend/                          # .NET 10.0 Web API
│   ├── Controllers/
│   │   └── ChatController.cs        # Chat API endpoints
│   ├── Models/
│   │   └── ChatMessage.cs           # 資料模型定義
│   ├── Services/
│   │   └── CopilotService.cs        # Copilot SDK 整合服務
│   ├── Program.cs                   # 主程式進入點
│   ├── CopilotApi.csproj            # 專案設定
│   └── Properties/
│       └── launchSettings.json      # 啟動設定 (Port: 5000)
│
├── Frontend/                         # Vue 3 + Vite
│   ├── src/
│   │   ├── components/
│   │   │   └── ChatInterface.vue    # 主要聊天介面元件
│   │   ├── services/
│   │   │   └── copilotService.js    # API 通訊層
│   │   ├── App.vue                  # 根元件
│   │   ├── main.js                  # 應用程式進入點
│   │   └── style.css                # 全域樣式
│   ├── index.html                   # HTML 模板
│   ├── vite.config.js               # Vite 設定
│   └── package.json                 # npm 依賴
│
├── start.sh                         # Linux/Mac 啟動腳本
├── start.bat                        # Windows 啟動腳本
├── README.md                        # 完整說明文件
└── .gitignore                       # Git 忽略檔案
```

## 🎯 核心功能實作

### 後端 (.NET)

1. **CopilotService.cs** - Copilot SDK 封裝
   - 初始化 Copilot CLI client
   - 管理 session 生命週期
   - 處理訊息傳送與接收
   - 支援串流回應

2. **ChatController.cs** - RESTful API
   - `POST /api/chat/session` - 建立新會話
   - `POST /api/chat/send` - 發送訊息
   - `GET /api/chat/sessions` - 取得所有會話
   - `DELETE /api/chat/session/{id}` - 刪除會話

3. **CORS 設定** - 允許前端跨域請求

### 前端 (Vue 3)

1. **ChatInterface.vue** - 聊天介面
   - 即時訊息顯示
   - Markdown 風格設計
   - 載入動畫效果
   - 錯誤處理與提示

2. **copilotService.js** - API 客戶端
   - Axios HTTP 請求
   - 統一錯誤處理
   - RESTful API 封裝

## 🚀 啟動方式

### 選項 1：使用啟動腳本（推薦）

**Windows:**
```cmd
start.bat
```

**Linux/Mac:**
```bash
./start.sh
```

### 選項 2：手動啟動

**終端 1 - 後端：**
```bash
cd Backend
dotnet run
```

**終端 2 - 前端：**
```bash
cd Frontend
npm install
npm run dev
```

## 📊 端口配置

| 服務 | 端口 | URL |
|------|------|-----|
| Frontend | 5173 | http://localhost:5173 |
| Backend | 5000 | http://localhost:5000 |

## 🔧 依賴套件

### Backend (.NET)
- `GitHub.Copilot.SDK` (0.1.19) - Copilot SDK 核心
- `Microsoft.Extensions.AI` (10.2.0) - AI 函式工具

### Frontend (Vue)
- `vue` (^3.5.13) - Vue 3 框架
- `axios` (^1.7.9) - HTTP 客戶端
- `vite` (^6.0.7) - 建置工具

## 📝 API 使用範例

### 建立會話
```bash
curl -X POST http://localhost:5000/api/chat/session \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-5"}'

# Response:
# {
#   "sessionId": "abc123",
#   "model": "gpt-5",
#   "createdAt": "2024-01-29T12:00:00Z"
# }
```

### 發送訊息
```bash
curl -X POST http://localhost:5000/api/chat/send \
  -H "Content-Type: application/json" \
  -d '{
    "sessionId": "abc123",
    "prompt": "Hello Copilot!"
  }'

# Response:
# {
#   "sessionId": "abc123",
#   "content": "Hello! How can I help you today?",
#   "isComplete": true
# }
```

## 🎨 UI 特性

- **深色主題** - VS Code 風格設計
- **響應式布局** - 適應不同螢幕尺寸
- **即時反饋** - 打字動畫與載入指示器
- **錯誤提示** - 使用者友善的錯誤訊息
- **模型切換** - 支援多種 AI 模型

## ⚠️ 重要提醒

1. **必須先安裝 Copilot CLI**
   ```bash
   gh extension install github/gh-copilot
   ```

2. **需要 Copilot 訂閱**
   - 確保 GitHub 帳號有有效的 Copilot 訂閱

3. **環境需求**
   - .NET SDK 10.0+
   - Node.js 18+
   - Copilot CLI 已驗證

## 🐛 除錯技巧

### 檢查 Copilot CLI
```bash
copilot --version
gh auth status
```

### 檢查後端日誌
```bash
cd Backend
dotnet run --verbosity detailed
```

### 檢查前端控制台
打開瀏覽器開發者工具 (F12) 查看 Console 和 Network 標籤

## 📚 延伸開發建議

1. **加入 WebSocket** - 實現真正的即時串流
2. **持久化會話** - 使用資料庫儲存對話記錄
3. **使用者認證** - 加入登入機制
4. **檔案上傳** - 支援附件功能
5. **Markdown 渲染** - 更好的訊息格式化
6. **對話匯出** - 下載對話記錄
7. **多語言支援** - i18n 國際化

## 💡 技術亮點

- ✅ 使用最新 .NET 10.0 和 Vue 3
- ✅ 完整的錯誤處理機制
- ✅ 非同步程式設計模式
- ✅ RESTful API 設計
- ✅ 元件化開發
- ✅ 現代化 UI/UX

---

**開發完成！** 🎉

如有問題請查看 README.md 或提交 Issue。
