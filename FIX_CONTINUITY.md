# 🔧 對話連續性問題修正

## 📋 問題描述

用戶回報的問題：
1. **第一個對話後無回應**：第一次發送訊息正常，之後繼續對話沒有回應
2. **模型切換問題**：切換模型時無法繼續使用當前對話 session

## 🔍 問題分析

### 問題 1: 事件訂閱衝突

**根本原因：**
- 在 `CopilotService.SendMessageAsync` 方法中，每次發送訊息都會添加新的事件處理器
- 舊的事件處理器沒有被清理，導致多個處理器同時運行
- 這造成 `TaskCompletionSource` 可能被多次觸發或衝突

**原始程式碼問題：**
```csharp
public async Task<List<ChatMessage>> SendMessageAsync(string sessionId, string prompt)
{
    // ...
    session.On(evt => { ... });  // ❌ 每次都添加新的處理器！
    await session.SendAsync(new MessageOptions { Prompt = prompt });
    await completionSource.Task;
}
```

### 問題 2: 模型切換邏輯不當

**根本原因：**
- 前端的 `@change="createNewSession"` 會在模型選擇改變時立即創建新 session
- 這會清空當前對話，使用者體驗不佳
- 沒有提供選項讓使用者繼續當前對話

## ✅ 解決方案

### 修正 1: 後端 - 事件訂閱管理

**更新 `Backend/Services/CopilotService.cs`：**

1. **添加訂閱管理字典**
```csharp
private readonly ConcurrentDictionary<string, IDisposable> _eventSubscriptions = new();
```

2. **清理舊訂閱並創建新訂閱**
```csharp
public async Task<List<ChatMessage>> SendMessageAsync(string sessionId, string prompt)
{
    // 清理舊的事件訂閱
    if (_eventSubscriptions.TryRemove(sessionId, out var oldSubscription))
    {
        oldSubscription?.Dispose();
    }

    // 創建新的事件訂閱
    var subscription = session.On(evt => { ... });
    
    // 保存訂閱以便後續清理
    _eventSubscriptions[sessionId] = subscription;
    
    // 發送訊息
    await session.SendAsync(new MessageOptions { Prompt = prompt });
    await completionSource.Task;
}
```

3. **使用 TrySetResult 避免重複觸發**
```csharp
case SessionIdleEvent:
    completionSource.TrySetResult(true);  // ✅ 使用 Try 版本
    break;
```

4. **完整清理機制**
```csharp
public async Task DeleteSessionAsync(string sessionId)
{
    // 清理事件訂閱
    if (_eventSubscriptions.TryRemove(sessionId, out var subscription))
    {
        subscription?.Dispose();
    }
    // 刪除 session
    if (_sessions.TryRemove(sessionId, out var session))
    {
        await session.DisposeAsync();
    }
}
```

### 修正 2: 前端 - 模型切換優化

**更新 `Frontend/src/components/ChatInterface.vue`：**

1. **分離模型切換和新對話邏輯**
```vue
<!-- 原始：模型改變直接創建新 session -->
<select v-model="selectedModel" @change="createNewSession">

<!-- 修正：模型改變時提示使用者 -->
<select v-model="selectedModel" @change="handleModelChange">
```

2. **添加模型切換處理**
```javascript
const handleModelChange = async () => {
  if (messages.value.length > 0) {
    const confirmChange = confirm(
      `切換到 ${selectedModel.value} 將開始新對話。\n當前對話將被清除，確定要繼續嗎？`
    );
    if (!confirmChange) {
      return; // 使用者取消
    }
  }
  await createNewSession();
};
```

3. **增強錯誤處理**
```javascript
const handleSend = async () => {
  // 確保有 session
  if (!sessionId.value) {
    error.value = '會話未初始化，請稍候...';
    await createNewSession();
    if (!sessionId.value) return;
  }

  try {
    const response = await copilotService.sendMessage(sessionId.value, userMessage);
    // ...
  } catch (err) {
    // 如果 session 不存在，自動重新建立
    if (err.message.includes('not found')) {
      error.value += ' - 正在重新建立會話...';
      await createNewSession();
    }
  }
};
```

4. **添加連接狀態指示器**
```vue
<span v-if="sessionId" class="session-indicator">✓ 已連接</span>
```

### 修正 3: 額外改進

1. **添加 Console 日誌**
   - 方便除錯和追蹤問題
   - 記錄 session 建立、訊息發送等關鍵事件

2. **改進載入狀態**
   - 在建立 session 時也顯示載入狀態
   - 避免使用者在初始化期間發送訊息

## 📊 修正前後對比

### 問題場景 1: 連續對話

**修正前：**
```
User: 第一條訊息
Bot:  正常回應 ✓

User: 第二條訊息
Bot:  (無回應) ✗
```

**修正後：**
```
User: 第一條訊息
Bot:  正常回應 ✓

User: 第二條訊息
Bot:  正常回應 ✓

User: 第三條訊息
Bot:  正常回應 ✓
```

### 問題場景 2: 模型切換

**修正前：**
```
Model: Claude Sonnet 4.5
[對話 1, 2, 3...]

使用者切換到 GPT-4.1
→ 立即清空對話 ✗
→ 沒有提示 ✗
```

**修正後：**
```
Model: Claude Sonnet 4.5
[對話 1, 2, 3...]

使用者切換到 GPT-4.1
→ 顯示確認對話框 ✓
→ 使用者確認後才清空 ✓
→ 使用者可以取消切換 ✓
```

## 🧪 測試步驟

### 測試 1: 連續對話

1. 啟動應用
2. 等待 session 初始化完成（看到 "✓ 已連接"）
3. 發送第一條訊息，等待回應
4. 發送第二條訊息，確認有回應
5. 繼續發送多條訊息，確認都能正常回應

### 測試 2: 模型切換

1. 開始一個對話，發送幾條訊息
2. 在下拉選單中切換模型
3. 確認顯示確認對話框
4. 點擊「確定」，確認開始新對話
5. 發送訊息，確認使用新模型回應

### 測試 3: 錯誤恢復

1. 開始對話
2. 手動停止後端（模擬連線中斷）
3. 嘗試發送訊息
4. 確認顯示錯誤訊息
5. 重啟後端
6. 發送訊息，確認自動恢復

## 🔍 除錯技巧

### 查看瀏覽器 Console

開啟開發者工具 (F12)，查看 console 日誌：
```
Session created: abc123 Model: claude-sonnet-4.5
Sending message to session: abc123
Message sent successfully
```

### 查看後端日誌

後端會記錄關鍵事件：
```
info: Created session: abc123
info: Session error: ...
info: Deleted session: abc123
```

### 常見錯誤訊息

| 錯誤 | 原因 | 解決方法 |
|------|------|----------|
| "Session not found" | Session 已過期或被刪除 | 自動重新建立 |
| "會話未初始化" | 頁面剛載入 | 等待初始化完成 |
| "發送失敗" | 網路或後端問題 | 檢查後端狀態 |

## 📝 技術細節

### 事件訂閱生命週期

```
1. 創建 Session
   └─> 添加到 _sessions 字典

2. 發送訊息
   ├─> 清理舊的事件訂閱 (如果存在)
   ├─> 創建新的事件訂閱
   ├─> 保存到 _eventSubscriptions 字典
   └─> 發送訊息並等待完成

3. 刪除 Session
   ├─> 從 _eventSubscriptions 移除並 Dispose
   └─> 從 _sessions 移除並 DisposeAsync
```

### TaskCompletionSource 使用

```csharp
// ✅ 正確：使用 Try 版本避免重複設置
completionSource.TrySetResult(true);
completionSource.TrySetException(ex);

// ❌ 錯誤：直接設置可能拋出例外
completionSource.SetResult(true);  // 如果已經設置會拋出例外
```

## 🎯 效果驗證

修正後應該觀察到：

✅ **連續對話正常**
- 可以無限制地連續發送訊息
- 每次都能收到正確回應
- 沒有延遲或卡住

✅ **模型切換友善**
- 切換前會提示使用者
- 使用者可以取消切換
- 新 session 使用正確的模型

✅ **錯誤處理完善**
- 顯示清楚的錯誤訊息
- 能夠自動恢復
- 提供重試機制

## 📦 檔案變更摘要

```
修改的檔案:
├── Backend/Services/CopilotService.cs
│   ├── 添加 _eventSubscriptions 字典
│   ├── 修改 SendMessageAsync 方法
│   ├── 修改 DeleteSessionAsync 方法
│   └── 修改 Dispose 方法
│
└── Frontend/src/components/ChatInterface.vue
    ├── 修改 header section（添加狀態指示器）
    ├── 添加 handleModelChange 方法
    ├── 修改 handleSend 方法（增強錯誤處理）
    ├── 修改 createNewSession 方法（添加載入狀態）
    └── 添加 .session-indicator CSS 樣式
```

## 🚀 部署更新

```bash
# 1. 重新編譯後端
cd Backend
dotnet build

# 2. 重啟後端
# 按 Ctrl+C 停止現有進程
dotnet run

# 3. 前端會自動熱重載
# 如果沒有，刷新瀏覽器頁面 (F5)
```

## ✨ 後續建議

1. **WebSocket 串流**
   - 實作真正的即時串流回應
   - 更好的使用者體驗

2. **會話持久化**
   - 保存對話歷史到資料庫
   - 頁面重新整理後恢復對話

3. **對話匯出**
   - 允許使用者下載對話記錄
   - 支援多種格式（Markdown, JSON, PDF）

---

**修正完成！** 🎉

測試結果：
- ✅ 連續對話正常運作
- ✅ 模型切換更加友善
- ✅ 錯誤處理更完善
- ✅ 編譯通過

**版本：** v1.0.2
**日期：** 2026-01-29
