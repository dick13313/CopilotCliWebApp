<template>
  <div class="app">
    <nav class="nav">
      <button
        :class="{ active: activeTab === 'chat' }"
        @click="activeTab = 'chat'"
      >
        💬 Chat
      </button>
      <button
        :class="{ active: activeTab === 'channels' }"
        @click="activeTab = 'channels'"
      >
        🔌 Channels
      </button>
      <button
        :class="{ active: activeTab === 'operations' }"
        @click="activeTab = 'operations'"
      >
        🛠️ Operations
      </button>
    </nav>

    <ChatInterface v-if="activeTab === 'chat'" />
    <ChannelSettings v-else-if="activeTab === 'channels'" />
    <OperationsPanel v-else />
  </div>
</template>

<script>
import { ref } from 'vue';
import ChatInterface from './components/ChatInterface.vue';
import ChannelSettings from './components/ChannelSettings.vue';
import OperationsPanel from './components/OperationsPanel.vue';

export default {
  name: 'App',
  components: {
    ChatInterface,
    ChannelSettings,
    OperationsPanel
  },
  setup() {
    const activeTab = ref('chat');
    return { activeTab };
  }
};
</script>

<style scoped>
.app {
  height: 100vh;
  display: flex;
  flex-direction: column;
}

.nav {
  display: flex;
  gap: 0.5rem;
  padding: 0.75rem 1.5rem;
  background: #1e1e1e;
  border-bottom: 1px solid #3e3e42;
}

.nav button {
  padding: 0.5rem 1rem;
  border-radius: 4px;
  background: #2d2d30;
  color: #e4e4e4;
  border: 1px solid #3e3e42;
}

.nav button.active {
  background: #0078d4;
  color: white;
  border-color: #0078d4;
}
</style>
