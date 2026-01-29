# 📁 完整專案檔案清單

## 專案結構

```
CopilotCliWebApp/
│
├── 📄 README.md                          完整使用說明文件
├── 📄 PROJECT_GUIDE.md                   開發者快速指南
├── 📄 TROUBLESHOOTING.md                 故障排除指南
├── 📄 FIX_SUMMARY.md                     問題修正摘要
├── 📄 FILE_LIST.md                       本檔案
├── 📄 .gitignore                         Git 忽略設定
│
├── 🚀 start.sh                           Linux/Mac 啟動腳本
├── 🚀 start.bat                          Windows 啟動腳本
├── 🔍 check-system.sh                    Linux/Mac 系統檢查
├── 🔍 check-system.bat                   Windows 系統檢查
│
├── 📂 Backend/                           .NET 10.0 Web API
│   ├── 📄 CopilotApi.csproj              專案設定檔
│   ├── 📄 Program.cs                     應用程式進入點
│   ├── 📄 appsettings.json               應用程式設定
│   ├── 📄 appsettings.Development.json   開發環境設定
│   │
│   ├── 📂 Properties/
│   │   └── 📄 launchSettings.json        啟動設定 (Port 5000)
│   │
│   ├── 📂 Controllers/
│   │   └── 📄 ChatController.cs          Chat API 端點
│   │       • POST /api/chat/session      建立會話
│   │       • POST /api/chat/send         發送訊息
│   │       • GET  /api/chat/sessions     取得會話列表
│   │       • DELETE /api/chat/session/{id} 刪除會話
│   │
│   ├── 📂 Models/
│   │   └── 📄 ChatMessage.cs             資料模型定義
│   │       • ChatMessage                  聊天訊息
│   │       • ChatRequest                  請求模型
│   │       • ChatResponse                 回應模型
│   │       • SessionInfo                  會話資訊
│   │
│   └── 📂 Services/
│       └── 📄 CopilotService.cs          Copilot SDK 整合服務
│           • InitializeAsync()            初始化 Copilot CLI
│           • CreateSessionAsync()         建立會話
│           • SendMessageAsync()           發送訊息
│           • GetActiveSessions()          取得活動會話
│           • DeleteSessionAsync()         刪除會話
│
└── 📂 Frontend/                          Vue 3 + Vite
    ├── 📄 package.json                   npm 依賴設定
    ├── 📄 package-lock.json              npm 鎖定版本
    ├── 📄 vite.config.js                 Vite 建置設定
    ├── 📄 index.html                     HTML 模板
    │
    └── 📂 src/
        ├── 📄 main.js                    應用程式進入點
        ├── 📄 App.vue                    根元件
        ├── 📄 style.css                  全域樣式
        │
        ├── 📂 components/
        │   └── 📄 ChatInterface.vue      主要聊天介面元件
        │       • 訊息顯示區域
        │       • 模型選擇器
        │       • 輸入框與發送按鈕
        │       • 錯誤處理與提示
        │       • 載入動畫
        │
        └── 📂 services/
            └── 📄 copilotService.js      API 通訊服務
                • createSession()          建立會話 API
                • sendMessage()            發送訊息 API
                • getSessions()            取得會話列表 API
                • deleteSession()          刪除會話 API
```

## 核心檔案說明

### 後端檔案 (.NET)

#### 1. Program.cs
- 應用程式主要進入點
- 設定 CORS 政策
- 註冊服務（Controllers, CopilotService）
- 初始化 Copilot CLI

#### 2. Controllers/ChatController.cs
```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
```
提供 RESTful API 端點供前端呼叫。

#### 3. Services/CopilotService.cs
```csharp
public class CopilotService : IDisposable
```
封裝 GitHub Copilot SDK 的核心邏輯：
- 管理 CopilotClient 生命週期
- 處理多個並發會話
- 實作串流回應處理
- 事件訂閱與錯誤處理

#### 4. Models/ChatMessage.cs
定義所有資料傳輸物件 (DTO)。

### 前端檔案 (Vue 3)

#### 1. main.js
```javascript
import { createApp } from 'vue'
import App from './App.vue'
```
Vue 應用程式初始化。

#### 2. App.vue
根元件，包含 ChatInterface 元件。

#### 3. components/ChatInterface.vue
主要 UI 元件：
- **Template**: HTML 結構
- **Script**: Vue 3 Composition API 邏輯
- **Style**: Scoped CSS 樣式

#### 4. services/copilotService.js
使用 Axios 進行 HTTP 請求的 API 客戶端。

### 設定檔案

#### Backend/Properties/launchSettings.json
```json
{
  "profiles": {
    "http": {
      "applicationUrl": "http://localhost:5000"
    }
  }
}
```

#### Frontend/vite.config.js
```javascript
export default defineConfig({
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000'
      }
    }
  }
})
```

### 文件檔案

#### README.md (主要文檔)
- 功能特性
- 系統需求
- 安裝步驟
- API 端點說明
- 常見問題

#### PROJECT_GUIDE.md (開發指南)
- 檔案結構詳解
- 核心功能實作
- API 使用範例
- UI 特性說明
- 延伸開發建議

#### TROUBLESHOOTING.md (故障排除)
- 常見錯誤解決方案
- 診斷命令
- 測試流程
- 除錯技巧

#### FIX_SUMMARY.md (修正摘要)
- 問題診斷
- 解決方案
- 模型名稱參考
- 測試步驟

## 執行檔案

### 啟動腳本

#### start.sh / start.bat
自動啟動前後端：
1. 檢查 Copilot CLI
2. 啟動後端 (port 5000)
3. 安裝前端依賴
4. 啟動前端 (port 5173)

#### check-system.sh / check-system.bat
環境檢查：
- ✓ .NET SDK 版本
- ✓ Node.js 版本
- ✓ Copilot CLI 安裝
- ✓ GitHub 認證
- ✓ 專案建置狀態
- ✓ 端口可用性

## 依賴套件

### 後端 NuGet 套件
```xml
<PackageReference Include="GitHub.Copilot.SDK" Version="0.1.19" />
<PackageReference Include="Microsoft.Extensions.AI" Version="10.2.0" />
```

### 前端 npm 套件
```json
{
  "vue": "^3.5.13",
  "axios": "^1.7.9",
  "@vitejs/plugin-vue": "^5.2.1",
  "vite": "^6.0.7"
}
```

## 重要設定

### 預設模型
```csharp
// Backend
public string? Model { get; set; } = "claude-sonnet-4.5";
```

```javascript
// Frontend
const selectedModel = ref('claude-sonnet-4.5');
```

### 可用模型列表
- claude-sonnet-4.5 (預設) ⭐
- claude-sonnet-4
- claude-haiku-4.5
- gpt-4.1
- gpt-5-mini

### 端口配置
| 服務 | 端口 | 說明 |
|------|------|------|
| Backend API | 5000 | HTTP API 端點 |
| Frontend | 5173 | Vite 開發伺服器 |

## 檔案大小統計

```
後端：
- 總程式碼行數: ~450 行
- C# 檔案: 5 個
- 設定檔: 3 個

前端：
- 總程式碼行數: ~350 行
- Vue 檔案: 2 個
- JavaScript 檔案: 2 個
- CSS 檔案: 1 個

文件：
- Markdown 檔案: 5 個
- 總文件行數: ~1200 行
```

## Git 忽略規則

### .gitignore 內容
```
# .NET
bin/
obj/
*.user

# Frontend
node_modules/
dist/
*.log

# IDE
.vscode/
.idea/
```

## 授權

所有檔案均採用 MIT License。

---

**專案版本：** v1.0.1  
**最後更新：** 2026-01-29  
**總檔案數：** 22 個核心檔案  
**狀態：** ✅ 已測試並驗證
