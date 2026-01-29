<template>
  <div class="channel-settings">
    <h2>🔌 通訊軟體通道設定</h2>

    <div class="channel-list">
      <div v-for="channel in channels" :key="channel.name" class="channel-card">
        <div class="channel-header">
          <h3>{{ channel.name.toUpperCase() }}</h3>
          <span class="status-badge" :class="channel.status">
            {{ getStatusLabel(channel.status) }}
          </span>
        </div>

        <div class="channel-body">
          <p v-if="channel.name === 'telegram'">
            使用 Telegram Bot 讓你在通訊軟體中與 Copilot CLI 對話。
          </p>
          <p v-else>
            此通道尚未實作，保留接口以供未來擴充。
          </p>
        </div>
      </div>
    </div>

    <div class="telegram-config" v-if="telegram">
      <h3>🤖 Telegram 設定</h3>
      <div class="config-item">
        <label>狀態</label>
        <span class="value" :class="telegram.enabled ? 'enabled' : 'disabled'">
          {{ telegram.enabled ? '啟用中' : '未啟用' }}
        </span>
      </div>
      <div class="config-item">
        <label>Allowed Chat ID</label>
        <span class="value">{{ telegram.allowedChatId ?? '未設定 (允許所有聊天)' }}</span>
      </div>
      <div class="config-item">
        <label>Default Model</label>
        <span class="value">{{ telegram.defaultModel }}</span>
      </div>
      <div class="config-help">
        <p>設定 BotToken 與 AllowedChatId 請修改後端 appsettings.json</p>
        <code>
          "Telegram": {
            "BotToken": "YOUR_BOT_TOKEN",
            "AllowedChatId": 123456789,
            "DefaultModel": "claude-sonnet-4.5"
          }
        </code>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, onMounted } from 'vue';
import { copilotService } from '../services/copilotService.js';

export default {
  name: 'ChannelSettings',
  setup() {
    const channels = ref([]);
    const telegram = ref(null);

    const getStatusLabel = (status) => {
      switch (status) {
        case 'running': return '運行中';
        case 'disabled': return '未啟用';
        case 'not_implemented': return '待實作';
        default: return '未知';
      }
    };

    const fetchChannels = async () => {
      try {
        channels.value = await copilotService.getChannels();
        telegram.value = await copilotService.getTelegramSettings();
      } catch (err) {
        console.error('Failed to fetch channel settings:', err);
      }
    };

    onMounted(fetchChannels);

    return {
      channels,
      telegram,
      getStatusLabel
    };
  }
};
</script>

<style scoped>
.channel-settings {
  padding: 2rem;
  background: #252526;
  color: #e4e4e4;
}

.channel-list {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 1rem;
  margin: 1.5rem 0;
}

.channel-card {
  background: #2d2d30;
  border: 1px solid #3e3e42;
  border-radius: 8px;
  padding: 1rem;
}

.channel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.75rem;
}

.status-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 600;
}

.status-badge.running {
  background: #16825d;
  color: white;
}

.status-badge.disabled {
  background: #8a3a3a;
  color: white;
}

.status-badge.not_implemented {
  background: #666;
  color: white;
}

.telegram-config {
  background: #2d2d30;
  border: 1px solid #3e3e42;
  border-radius: 8px;
  padding: 1.5rem;
  margin-top: 2rem;
}

.config-item {
  display: flex;
  justify-content: space-between;
  padding: 0.5rem 0;
  border-bottom: 1px solid #3e3e42;
}

.config-item:last-child {
  border-bottom: none;
}

.value.enabled {
  color: #16825d;
}

.value.disabled {
  color: #d9534f;
}

.config-help {
  margin-top: 1rem;
  font-size: 0.9rem;
  color: #999;
}

.config-help code {
  display: block;
  background: #1a1a1a;
  padding: 0.75rem;
  border-radius: 4px;
  margin-top: 0.5rem;
  font-size: 0.8rem;
  white-space: pre-wrap;
}
</style>
