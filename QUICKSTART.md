# ⚡ 快速開始 - 5 分鐘上手

## 🎯 目標
在 5 分鐘內啟動 Copilot CLI Web 介面。

## 📋 前置檢查（2 分鐘）

### 步驟 1: 執行系統檢查

**Windows:**
```cmd
cd CopilotCliWebApp
check-system.bat
```

**Linux/Mac:**
```bash
cd CopilotCliWebApp
./check-system.sh
```

### 步驟 2: 確認必要工具

✅ 必須全部通過：
- [ ] .NET SDK 10.0+
- [ ] Node.js 18+
- [ ] Copilot CLI 已安裝
- [ ] GitHub 已認證

❌ 如果有任何項目失敗，請先解決：

**未安裝 Copilot CLI?**
```bash
gh extension install github/gh-copilot
```

**未登入 GitHub?**
```bash
gh auth login
```

**未安裝 .NET?**
下載：https://dotnet.microsoft.com/download

**未安裝 Node.js?**
下載：https://nodejs.org/

## 🚀 啟動應用（2 分鐘）

### 方法 1: 一鍵啟動（推薦）

**Windows:**
```cmd
start.bat
```

**Linux/Mac:**
```bash
./start.sh
```

腳本會自動：
1. ✓ 檢查 Copilot CLI
2. ✓ 啟動後端 API
3. ✓ 安裝前端依賴
4. ✓ 啟動前端伺服器

等待約 30 秒...

### 方法 2: 手動啟動

**終端 1 - 啟動後端：**
```bash
cd Backend
dotnet run
```

等待看到：
```
Now listening on: http://localhost:5000
```

**終端 2 - 啟動前端：**
```bash
cd Frontend
npm install    # 第一次需要
npm run dev
```

等待看到：
```
Local: http://localhost:5173/
```

## 🎨 開始使用（1 分鐘）

### 步驟 1: 開啟瀏覽器
訪問：http://localhost:5173

### 步驟 2: 等待初始化
應用會自動建立第一個會話（約 5-10 秒）

### 步驟 3: 開始聊天！
在輸入框輸入訊息，例如：
```
Hello! What can you do?
```

按 Enter 或點擊「📤 發送」

### 步驟 4: 嘗試不同功能
- 切換模型（下拉選單）
- 點擊「新對話」開始新會話
- 問更複雜的問題

## 🧪 測試範例

### 範例 1: 簡單問候
```
你好！請介紹你自己
```

### 範例 2: 程式碼問題
```
如何在 Python 中讀取 JSON 檔案？
```

### 範例 3: 除錯協助
```
解釋這個錯誤：Cannot read property 'map' of undefined
```

### 範例 4: 系統命令
```
如何查看目前目錄的檔案列表？
```

## ❗ 遇到問題？

### 問題 1: 後端無法啟動
**錯誤：** "Failed to start Copilot CLI client"

**快速修復：**
```bash
# 測試 Copilot CLI
copilot -p "test"

# 如果失敗，重新登入
gh auth login

# 更新 Copilot CLI
copilot update
```

### 問題 2: 模型不支援
**錯誤：** "The requested model is not supported"

**快速修復：**
確認使用正確的模型名稱：
- ✅ `claude-sonnet-4.5` (推薦)
- ✅ `gpt-4.1`
- ❌ ~~`GPT-5`~~ (錯誤)

### 問題 3: 前端無法連接
**錯誤：** 網路錯誤或 CORS 錯誤

**快速修復：**
```bash
# 1. 確認後端正在運行
curl http://localhost:5000/api/chat/sessions

# 2. 重啟後端
# 按 Ctrl+C 停止，然後重新執行
cd Backend
dotnet run
```

### 問題 4: Port 已被占用
**錯誤：** "Port 5000 already in use"

**快速修復：**
```bash
# Windows - 找出並關閉佔用程序
netstat -ano | findstr :5000
taskkill /PID <PID> /F

# Linux/Mac
lsof -ti:5000 | xargs kill -9
```

## 📚 下一步

完成快速開始後，您可以：

1. **閱讀完整文檔**
   - `README.md` - 完整功能說明
   - `PROJECT_GUIDE.md` - 開發者指南

2. **深入了解故障排除**
   - `TROUBLESHOOTING.md` - 詳細問題解決

3. **查看修正歷史**
   - `FIX_SUMMARY.md` - 問題修正記錄

4. **探索程式碼**
   - `FILE_LIST.md` - 完整檔案清單

## 🎯 成功指標

當您看到以下畫面時，表示成功！

```
✅ 後端終端顯示：
   info: Copilot CLI client started successfully
   info: Created session: {sessionId}

✅ 前端瀏覽器顯示：
   - 🤖 GitHub Copilot CLI Web Interface
   - 歡迎訊息
   - 輸入框可用

✅ 能夠發送訊息並收到回應
```

## ⏱️ 時間檢查

如果您在 5 分鐘內完成所有步驟 - **恭喜！** 🎉

如果超過時間，可能的原因：
- 首次安裝依賴需要更多時間
- 網路速度較慢
- 系統資源不足

不用擔心，這些都是正常的！

## 💡 專業提示

1. **首次啟動較慢**
   - Copilot CLI 第一次需要初始化
   - npm install 需要下載套件
   - 之後會快很多！

2. **保持終端開啟**
   - 前後端需要持續運行
   - 不要關閉終端視窗

3. **瀏覽器推薦**
   - Chrome / Edge (最佳)
   - Firefox (良好)
   - Safari (可用)

4. **網路需求**
   - 需要網際網路連線
   - Copilot API 需要線上存取

## 🎊 完成！

現在您已經成功啟動 Copilot CLI Web 介面！

開始探索 AI 助手的強大功能吧！ 🚀

---

**需要幫助？** 查看 TROUBLESHOOTING.md 或提交 Issue。
