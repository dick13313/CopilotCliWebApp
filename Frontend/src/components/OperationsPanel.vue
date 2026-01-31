<template>
  <div class="operations-panel">
    <header class="panel-header">
      <h2>🛠️ Operations Control</h2>
      <button class="refresh-btn" @click="refreshStatus" :disabled="loading">↻</button>
    </header>

    <div v-if="error" class="error-message">❌ {{ error }}</div>

    <section class="status-card" v-if="status">
      <h3>Frontend</h3>
      <div class="status-row">
        <span class="label">狀態</span>
        <span class="value" :class="status.frontend.isRunning ? 'good' : 'bad'">
          {{ status.frontend.isRunning ? '運行中' : '已停止' }}
        </span>
      </div>
      <div class="status-row" v-if="status.frontend.pid">
        <span class="label">PID</span>
        <span class="value">{{ status.frontend.pid }}</span>
      </div>
      <div class="status-row" v-if="status.frontend.port">
        <span class="label">Port</span>
        <span class="value">
          {{ status.frontend.port }} ({{ status.frontend.portOpen ? 'open' : 'closed' }})
        </span>
      </div>
      <div class="status-row" v-if="status.frontend.lastOutput">
        <span class="label">Last Output</span>
        <span class="value">{{ status.frontend.lastOutput }}</span>
      </div>
      <div class="status-row" v-if="status.frontend.lastError">
        <span class="label">Last Error</span>
        <span class="value error">{{ status.frontend.lastError }}</span>
      </div>
    </section>

    <section class="actions">
      <button @click="runAction('start')" :disabled="loading">▶️ 啟動前端</button>
      <button @click="runAction('stop')" :disabled="loading">⏹ 停止前端</button>
      <button @click="runAction('restart')" :disabled="loading">🔄 重啟前端</button>
      <button @click="runDiagnostics" :disabled="loading">🩺 診斷</button>
      <button class="warn" @click="runHeal" :disabled="loading">🧯 自我修復</button>
    </section>

    <section class="diagnostics" v-if="diagnostics.length">
      <h3>Diagnostics</h3>
      <ul>
        <li v-for="(check, index) in diagnostics" :key="index">
          <span class="command">{{ check.command }}</span>
          <span class="result" :class="check.timedOut ? 'warn' : check.exitCode === 0 ? 'good' : 'bad'">
            {{ check.timedOut ? 'timeout' : `exit ${check.exitCode}` }}
          </span>
          <div class="detail" v-if="check.error || check.output">
            {{ (check.error || check.output).trim().slice(0, 160) }}
          </div>
        </li>
      </ul>
    </section>

    <section class="logs" v-if="logs.length">
      <h3>Recent Logs</h3>
      <ul>
        <li v-for="(entry, index) in logs" :key="index">
          <span class="timestamp">{{ formatTime(entry.timestamp) }}</span>
          <span class="source">[{{ entry.source }}]</span>
          <span class="message">{{ entry.message }}</span>
        </li>
      </ul>
    </section>
  </div>
</template>

<script>
import { ref, onMounted } from 'vue';
import { copilotService } from '../services/copilotService.js';

export default {
  name: 'OperationsPanel',
  setup() {
    const status = ref(null);
    const logs = ref([]);
    const diagnostics = ref([]);
    const loading = ref(false);
    const error = ref('');

    const refreshStatus = async () => {
      try {
        error.value = '';
        loading.value = true;
        status.value = await copilotService.getOperationsStatus();
        logs.value = await copilotService.getOperationsLogs();
      } catch (err) {
        error.value = '無法取得狀態: ' + err.message;
      } finally {
        loading.value = false;
      }
    };

    const runAction = async (action) => {
      try {
        error.value = '';
        loading.value = true;
        if (action === 'start') {
          await copilotService.startFrontend();
        } else if (action === 'stop') {
          await copilotService.stopFrontend();
        } else if (action === 'restart') {
          await copilotService.restartFrontend();
        }
        await refreshStatus();
      } catch (err) {
        error.value = '操作失敗: ' + err.message;
      } finally {
        loading.value = false;
      }
    };

    const runDiagnostics = async () => {
      try {
        error.value = '';
        loading.value = true;
        const result = await copilotService.runDiagnostics();
        diagnostics.value = result.checks || [];
      } catch (err) {
        error.value = '診斷失敗: ' + err.message;
      } finally {
        loading.value = false;
      }
    };

    const runHeal = async () => {
      try {
        error.value = '';
        loading.value = true;
        await copilotService.resetCopilotClient();
      } catch (err) {
        error.value = '自我修復失敗: ' + err.message;
      } finally {
        loading.value = false;
      }
    };

    const formatTime = (value) => {
      if (!value) return '';
      const date = new Date(value);
      return date.toLocaleTimeString('zh-TW', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    };

    onMounted(refreshStatus);

    return {
      status,
      logs,
      diagnostics,
      loading,
      error,
      refreshStatus,
      runAction,
      runDiagnostics,
      runHeal,
      formatTime
    };
  }
};
</script>

<style scoped>
.operations-panel {
  padding: 2rem;
  background: #252526;
  color: #e4e4e4;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.refresh-btn {
  background: #2d2d30;
  color: #e4e4e4;
  border-radius: 4px;
  padding: 0.3rem 0.6rem;
}

.status-card {
  background: #2d2d30;
  border: 1px solid #3e3e42;
  border-radius: 8px;
  padding: 1rem;
  margin-bottom: 1.5rem;
}

.status-row {
  display: flex;
  justify-content: space-between;
  padding: 0.35rem 0;
  border-bottom: 1px solid #3e3e42;
}

.status-row:last-child {
  border-bottom: none;
}

.label {
  color: #aaa;
}

.value.good {
  color: #1cc88a;
}

.value.bad {
  color: #f48771;
}

.value.error {
  color: #f48771;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 1.5rem;
}

.actions button {
  padding: 0.5rem 1rem;
  border-radius: 4px;
  background: #0078d4;
  color: white;
}

.actions .warn {
  background: #b63b3b;
}

.diagnostics,
.logs {
  background: #2d2d30;
  border: 1px solid #3e3e42;
  border-radius: 8px;
  padding: 1rem;
  margin-bottom: 1.5rem;
}

.diagnostics ul,
.logs ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.diagnostics li,
.logs li {
  padding: 0.5rem 0;
  border-bottom: 1px solid #3e3e42;
}

.diagnostics li:last-child,
.logs li:last-child {
  border-bottom: none;
}

.command {
  display: inline-block;
  min-width: 180px;
}

.result.good {
  color: #1cc88a;
}

.result.bad {
  color: #f48771;
}

.result.warn {
  color: #f6c343;
}

.detail {
  margin-top: 0.35rem;
  color: #bbb;
  font-size: 0.85rem;
}

.timestamp {
  color: #999;
  margin-right: 0.5rem;
}

.source {
  color: #7aa2f7;
  margin-right: 0.5rem;
}

.error-message {
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: #5a1d1d;
  color: #f48771;
  border-radius: 4px;
  font-size: 0.9rem;
}
</style>
