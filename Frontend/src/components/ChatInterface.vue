<template>
  <div class="chat-container">
    <header class="header">
      <div class="header-left">
        <h1>🤖 GitHub Copilot CLI Web Interface</h1>
        <div class="directory-selector">
          <label>📂</label>
          <select v-model="selectedDirectory" class="directory-select" @change="handleDirectoryChange">
            <option :value="currentDirectory" disabled>{{ getDirectoryName(currentDirectory) }}</option>
            <option v-for="dir in availableDirectories" :key="dir.fullPath" :value="dir.fullPath">
              {{ dir.name }}
            </option>
          </select>
        </div>
      </div>
      <div class="session-info">
        <select v-model="selectedModel" class="model-select" @change="handleModelChange">
          <option value="claude-sonnet-4.5">Claude Sonnet 4.5 (預設, 平衡型)</option>
          <option value="claude-haiku-4.5">Claude Haiku 4.5 (快速/經濟)</option>
          <option value="claude-opus-4.5">Claude Opus 4.5 (進階)</option>
          <option value="claude-sonnet-4">Claude Sonnet 4 (標準)</option>
          <option value="gemini-3-pro-preview">Gemini 3 Pro Preview (標準)</option>
          <option value="gpt-5.2-codex">GPT-5.2 Codex (標準)</option>
          <option value="gpt-5.2">GPT-5.2 (標準)</option>
          <option value="gpt-5.1-codex-max">GPT-5.1 Codex Max (標準)</option>
          <option value="gpt-5.1-codex">GPT-5.1 Codex (標準)</option>
          <option value="gpt-5.1">GPT-5.1 (標準)</option>
          <option value="gpt-5">GPT-5 (標準)</option>
          <option value="gpt-5.1-codex-mini">GPT-5.1 Codex Mini (快速/經濟)</option>
          <option value="gpt-5-mini">GPT-5 Mini (快速/經濟)</option>
          <option value="gpt-4.1">GPT-4.1 (快速/經濟)</option>
        </select>
        <button @click="createNewSession" class="new-session-btn">新對話</button>
        <span v-if="activeSessionId" class="session-indicator">✓ {{ formatSessionBadge(activeSessionId) }}</span>
      </div>
    </header>

    <div class="content">
      <aside class="session-panel">
        <div class="session-panel-header">
          <h3>🧾 Sessions</h3>
          <button @click="refreshSessions" class="refresh-btn">↻</button>
        </div>
        <div class="session-list">
          <div
            v-for="session in sessions"
            :key="session.sessionId"
            class="session-card"
            :class="{ active: session.sessionId === activeSessionId }"
            role="button"
            @click="switchSession(session.sessionId)"
          >
            <div class="session-card-header">
              <span class="session-id">{{ formatSessionBadge(session.sessionId) }}</span>
              <span class="session-status" :class="session.status">{{ getStatusLabel(session.status) }}</span>
              <button class="session-delete-btn" @click.stop="deleteSession(session.sessionId)">🗑</button>
            </div>
            <div class="session-meta">{{ session.model }}</div>
            <div class="session-preview" v-if="session.lastResponsePreview">
              {{ session.lastResponsePreview }}
            </div>
            <div class="session-error" v-if="session.lastError">❌ {{ session.lastError }}</div>
          </div>
        </div>
      </aside>

      <section class="chat-main">
        <div class="messages-container" ref="messagesContainer">
          <div v-if="messages.length === 0" class="welcome-message">
            <h2>👋 歡迎使用 Copilot CLI</h2>
            <p>輸入您的問題，開始與 AI 助手對話</p>
          </div>
          
          <div v-for="(msg, index) in messages" :key="index" class="message" :class="msg.role">
            <div class="message-header">
              <span class="role-badge">{{ msg.role === 'user' ? '👤 您' : '🤖 Copilot' }}</span>
              <span class="timestamp">{{ formatTime(msg.timestamp) }}</span>
            </div>
            <div class="message-content">{{ msg.content }}</div>
          </div>

          <div v-if="isLoading" class="message assistant loading">
            <div class="message-header">
              <span class="role-badge">🤖 Copilot</span>
            </div>
            <div class="message-content">
              <div class="typing-indicator">
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
          </div>
        </div>

        <div class="input-container">
          <div v-if="error" class="error-message">
            ❌ {{ error }}
          </div>
          <div class="input-wrapper">
            <textarea
              v-model="inputMessage"
              @keydown.enter.prevent="handleSend"
              placeholder="輸入訊息... (Enter 發送)"
              class="message-input"
              rows="3"
            ></textarea>
            <button @click="handleSend" :disabled="!inputMessage.trim() || isLoading" class="send-btn">
              📤 發送
            </button>
          </div>
        </div>
      </section>
    </div>
  </div>
</template>

<script>
import { ref, onMounted, nextTick } from 'vue';
import { copilotService } from '../services/copilotService.js';

export default {
  name: 'ChatInterface',
  setup() {
    const messages = ref([]);
    const inputMessage = ref('');
    const isLoading = ref(false);
    const error = ref('');
    const sessions = ref([]);
    const sessionMessages = ref({});
    const activeSessionId = ref('');
    const selectedModel = ref('claude-sonnet-4.5');
    const previousModel = ref('claude-sonnet-4.5');
    const messagesContainer = ref(null);
    const availableDirectories = ref([]);
    const currentDirectory = ref('');
    const selectedDirectory = ref('');

    const scrollToBottom = () => {
      nextTick(() => {
        if (messagesContainer.value) {
          messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
        }
      });
    };

    const loadDirectories = async () => {
      try {
        const response = await copilotService.getDirectories();
        availableDirectories.value = response.directories;
        currentDirectory.value = response.currentDirectory;
        selectedDirectory.value = response.currentDirectory;
      } catch (err) {
        console.error('Failed to load directories:', err);
      }
    };

    const getDirectoryName = (path) => {
      if (!path) return '選擇目錄';
      const parts = path.split(/[/\\]/);
      return parts[parts.length - 1] || path;
    };

    const handleDirectoryChange = async () => {
      if (!selectedDirectory.value || selectedDirectory.value === currentDirectory.value) {
        return;
      }

      try {
        error.value = '';
        isLoading.value = true;
        await copilotService.switchDirectory(selectedDirectory.value);
        currentDirectory.value = selectedDirectory.value;
        sessionMessages.value = {};
        sessions.value = [];
        activeSessionId.value = '';
        await createNewSession();
        
        messages.value.push({
          role: 'assistant',
          content: `✅ 已切換到目錄: ${getDirectoryName(selectedDirectory.value)}`,
          timestamp: new Date()
        });
        scrollToBottom();
      } catch (err) {
        error.value = '切換目錄失敗: ' + err.message;
        selectedDirectory.value = currentDirectory.value;
        console.error('Switch directory error:', err);
      } finally {
        isLoading.value = false;
      }
    };

    const refreshSessions = async () => {
      try {
        sessions.value = await copilotService.getSessionStatuses();
        if (activeSessionId.value && !sessions.value.some((item) => item.sessionId === activeSessionId.value)) {
          activeSessionId.value = '';
          messages.value = [];
        }
      } catch (err) {
        console.error('Failed to fetch sessions:', err);
      }
    };

    const selectSession = (sessionId) => {
      activeSessionId.value = sessionId;
      if (!sessionMessages.value[sessionId]) {
        sessionMessages.value[sessionId] = [];
      }
      messages.value = sessionMessages.value[sessionId];
      const session = sessions.value.find((item) => item.sessionId === sessionId);
      if (session?.model) {
        selectedModel.value = session.model;
        previousModel.value = session.model;
      }
    };

    const createNewSession = async () => {
      try {
        error.value = '';
        isLoading.value = true;
        const response = await copilotService.createSession(selectedModel.value);
        await refreshSessions();
        if (!sessionMessages.value[response.sessionId]) {
          sessionMessages.value[response.sessionId] = [];
        }
        selectSession(response.sessionId);
        previousModel.value = selectedModel.value;
        console.log('Session created:', activeSessionId.value, 'Model:', selectedModel.value);
      } catch (err) {
        error.value = '無法建立會話: ' + err.message;
        console.error('Create session error:', err);
      } finally {
        isLoading.value = false;
      }
    };

    const handleModelChange = async () => {
      if (!activeSessionId.value) {
        await createNewSession();
        return;
      }

      try {
        error.value = '';
        isLoading.value = true;
        await copilotService.switchModel(activeSessionId.value, selectedModel.value);
        previousModel.value = selectedModel.value;
        await refreshSessions();
        console.log('Model switched in current session:', selectedModel.value);
      } catch (err) {
        error.value = '切換模型失敗: ' + err.message;
        selectedModel.value = previousModel.value;
        console.error('Switch model error:', err);
      } finally {
        isLoading.value = false;
      }
    };

    const handleSend = async () => {
      if (!inputMessage.value.trim() || isLoading.value) return;

      // 確保有 session
      if (!activeSessionId.value) {
        error.value = '會話未初始化，請稍候...';
        await createNewSession();
        if (!activeSessionId.value) return;
      }

      const userMessage = inputMessage.value.trim();
      inputMessage.value = '';

      messages.value.push({
        role: 'user',
        content: userMessage,
        timestamp: new Date()
      });

      scrollToBottom();
      isLoading.value = true;
      error.value = '';

      try {
        console.log('Sending message to session:', activeSessionId.value);
        const response = await copilotService.sendMessage(activeSessionId.value, userMessage);
        
        messages.value.push({
          role: 'assistant',
          content: response.content,
          timestamp: new Date()
        });

        await refreshSessions();
        scrollToBottom();
        console.log('Message sent successfully');
      } catch (err) {
        error.value = '發送失敗: ' + err.message;
        console.error('Send message error:', err);
        
        // 如果是 session 不存在的錯誤，嘗試重新建立
        if (err.message.includes('not found')) {
          error.value += ' - 正在重新建立會話...';
          await createNewSession();
        }
      } finally {
        isLoading.value = false;
      }
    };

    const switchSession = async (sessionId) => {
      if (activeSessionId.value === sessionId) return;
      selectSession(sessionId);
      error.value = '';
      await refreshSessions();
    };

    const deleteSession = async (sessionId) => {
      try {
        await copilotService.deleteSession(sessionId);
        await refreshSessions();
        if (activeSessionId.value === sessionId) {
          activeSessionId.value = '';
          messages.value = [];
        }
        delete sessionMessages.value[sessionId];
      } catch (err) {
        error.value = '刪除 session 失敗: ' + err.message;
      }
    };

    const getStatusLabel = (status) => {
      switch (status) {
        case 'running': return '運行中';
        case 'idle': return '待命';
        case 'error': return '錯誤';
        default: return status || '未知';
      }
    };

    const formatSessionBadge = (sessionId) => {
      if (!sessionId) return '';
      return sessionId.slice(0, 8);
    };

    const formatTime = (date) => {
      if (!date) return '';
      const d = new Date(date);
      return d.toLocaleTimeString('zh-TW', { hour: '2-digit', minute: '2-digit' });
    };

    onMounted(() => {
      loadDirectories();
      refreshSessions().then(() => {
        if (sessions.value.length > 0) {
          selectSession(sessions.value[0].sessionId);
        } else {
          createNewSession();
        }
      });
    });

    return {
      messages,
      inputMessage,
      isLoading,
      error,
      selectedModel,
      messagesContainer,
      sessions,
      sessionMessages,
      activeSessionId,
      availableDirectories,
      currentDirectory,
      selectedDirectory,
      handleSend,
      createNewSession,
      handleModelChange,
      handleDirectoryChange,
      refreshSessions,
      switchSession,
      deleteSession,
      getStatusLabel,
      formatSessionBadge,
      getDirectoryName,
      formatTime
    };
  }
};
</script>

<style scoped>
.chat-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  max-width: 100%;
  margin: 0;
  background: #252526;
}

.header {
  padding: 1rem 2rem;
  background: #2d2d30;
  border-bottom: 1px solid #3e3e42;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-left {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.header h1 {
  font-size: 1.5rem;
  color: #e4e4e4;
}

.directory-selector {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.directory-selector label {
  font-size: 1.2rem;
}

.directory-select {
  padding: 0.4rem 0.8rem;
  border-radius: 4px;
  font-size: 0.85rem;
  background: #1e1e1e;
  color: #e4e4e4;
  border: 1px solid #3e3e42;
  min-width: 200px;
}

.session-info {
  display: flex;
  gap: 1rem;
  align-items: center;
}

.model-select {
  padding: 0.5rem 1rem;
  border-radius: 4px;
  font-size: 0.9rem;
}

.new-session-btn {
  padding: 0.5rem 1.5rem;
  background: #0078d4;
  color: white;
  border-radius: 4px;
  font-size: 0.9rem;
}

.session-indicator {
  color: #16825d;
  font-size: 0.85rem;
  font-weight: 600;
  padding: 0.5rem 1rem;
  background: rgba(22, 130, 93, 0.1);
  border-radius: 4px;
}

.content {
  flex: 1;
  display: flex;
  min-height: 0;
}

.session-panel {
  width: 280px;
  border-right: 1px solid #3e3e42;
  background: #1f1f1f;
  display: flex;
  flex-direction: column;
}

.session-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem;
  border-bottom: 1px solid #3e3e42;
}

.session-panel-header h3 {
  margin: 0;
  font-size: 1rem;
  color: #e4e4e4;
}

.refresh-btn {
  background: #2d2d30;
  color: #e4e4e4;
  border-radius: 4px;
  padding: 0.3rem 0.6rem;
}

.session-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  overflow-y: auto;
}

.session-card {
  text-align: left;
  padding: 0.6rem;
  border-radius: 6px;
  background: #2b2b2f;
  border: 1px solid transparent;
  color: #e4e4e4;
}

.session-card.active {
  border-color: #0078d4;
  background: rgba(0, 120, 212, 0.15);
}

.session-card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.35rem;
  gap: 0.5rem;
}

.session-id {
  font-weight: 600;
}

.session-status {
  font-size: 0.75rem;
  padding: 0.15rem 0.5rem;
  border-radius: 10px;
}

.session-status.running {
  background: #0f5a3f;
}

.session-status.idle {
  background: #444;
}

.session-status.error {
  background: #7a2d2d;
}

.session-delete-btn {
  background: transparent;
  color: #cfcfcf;
  border: none;
  cursor: pointer;
  font-size: 0.8rem;
}

.chat-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.session-meta {
  font-size: 0.75rem;
  color: #bfbfbf;
}

.session-preview {
  margin-top: 0.35rem;
  font-size: 0.75rem;
  color: #cfcfcf;
}

.session-error {
  margin-top: 0.35rem;
  font-size: 0.75rem;
  color: #f48771;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.welcome-message {
  text-align: center;
  margin-top: 4rem;
}

.welcome-message h2 {
  font-size: 2rem;
  margin-bottom: 0.5rem;
  color: #e4e4e4;
}

.welcome-message p {
  color: #999;
}

.message {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-width: 80%;
  animation: fadeIn 0.3s;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.message.user {
  align-self: flex-end;
}

.message.assistant {
  align-self: flex-start;
}

.message-header {
  display: flex;
  gap: 0.5rem;
  align-items: center;
  font-size: 0.85rem;
}

.role-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-weight: 600;
}

.message.user .role-badge {
  background: #0078d4;
  color: white;
}

.message.assistant .role-badge {
  background: #16825d;
  color: white;
}

.timestamp {
  color: #999;
  font-size: 0.75rem;
}

.message-content {
  padding: 1rem;
  border-radius: 8px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-wrap: break-word;
}

.message.user .message-content {
  background: #094771;
  color: #e4e4e4;
}

.message.assistant .message-content {
  background: #1a1a1a;
  color: #e4e4e4;
  border: 1px solid #3e3e42;
}

.typing-indicator {
  display: flex;
  gap: 0.3rem;
}

.typing-indicator span {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #999;
  animation: typing 1.4s infinite;
}

.typing-indicator span:nth-child(2) {
  animation-delay: 0.2s;
}

.typing-indicator span:nth-child(3) {
  animation-delay: 0.4s;
}

@keyframes typing {
  0%, 60%, 100% {
    opacity: 0.3;
  }
  30% {
    opacity: 1;
  }
}

.input-container {
  padding: 1rem 2rem 2rem;
  background: #2d2d30;
  border-top: 1px solid #3e3e42;
  flex-shrink: 0;
}

.error-message {
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: #5a1d1d;
  color: #f48771;
  border-radius: 4px;
  font-size: 0.9rem;
}

.input-wrapper {
  display: flex;
  gap: 1rem;
  align-items: flex-end;
}

.message-input {
  flex: 1;
  padding: 0.75rem;
  border-radius: 4px;
  resize: vertical;
  min-height: 60px;
  font-size: 1rem;
}

.send-btn {
  padding: 0.75rem 2rem;
  background: #0078d4;
  color: white;
  border-radius: 4px;
  font-size: 1rem;
  height: 60px;
}

.send-btn:disabled {
  background: #555;
  cursor: not-allowed;
  opacity: 0.5;
}
</style>
