import axios from 'axios';

const API_BASE_URL = 'http://localhost:5000/api';

export const copilotService = {
  async createSession(model = 'claude-sonnet-4.5') {
    const response = await axios.post(`${API_BASE_URL}/chat/session`, { model });
    return response.data;
  },

  async sendMessage(sessionId, prompt) {
    const response = await axios.post(`${API_BASE_URL}/chat/send`, {
      sessionId,
      prompt
    });
    return response.data;
  },

  async switchModel(sessionId, model) {
    const response = await axios.post(`${API_BASE_URL}/chat/model`, {
      sessionId,
      model
    });
    return response.data;
  },

  async getSessions() {
    const response = await axios.get(`${API_BASE_URL}/chat/sessions`);
    return response.data;
  },

  async getSessionStatuses() {
    const response = await axios.get(`${API_BASE_URL}/chat/sessions/status`);
    return response.data;
  },

  async getSessionStatus(sessionId) {
    const response = await axios.get(`${API_BASE_URL}/chat/session/${sessionId}`);
    return response.data;
  },

  async deleteSession(sessionId) {
    const response = await axios.delete(`${API_BASE_URL}/chat/session/${sessionId}`);
    return response.data;
  },

  async sendBatch(sessionIds, prompt) {
    const response = await axios.post(`${API_BASE_URL}/chat/batch`, {
      sessionIds,
      prompt
    });
    return response.data;
  },

  async getChannels() {
    const response = await axios.get(`${API_BASE_URL}/channel`);
    return response.data;
  },

  async getTelegramSettings() {
    const response = await axios.get(`${API_BASE_URL}/channel/telegram`);
    return response.data;
  },

  async getDirectories() {
    const response = await axios.get(`${API_BASE_URL}/directory`);
    return response.data;
  },

  async getCurrentDirectory() {
    const response = await axios.get(`${API_BASE_URL}/directory/current`);
    return response.data;
  },

  async switchDirectory(directoryPath) {
    const response = await axios.post(`${API_BASE_URL}/directory/switch`, {
      directoryPath
    });
    return response.data;
  },

  async getOperationsStatus() {
    const response = await axios.get(`${API_BASE_URL}/operations/status`);
    return response.data;
  },

  async getOperationsLogs(count = 50) {
    const response = await axios.get(`${API_BASE_URL}/operations/logs`, {
      params: { count }
    });
    return response.data;
  },

  async startFrontend() {
    const response = await axios.post(`${API_BASE_URL}/operations/frontend/start`);
    return response.data;
  },

  async stopFrontend() {
    const response = await axios.post(`${API_BASE_URL}/operations/frontend/stop`);
    return response.data;
  },

  async restartFrontend() {
    const response = await axios.post(`${API_BASE_URL}/operations/frontend/restart`);
    return response.data;
  },

  async runDiagnostics() {
    const response = await axios.post(`${API_BASE_URL}/operations/diagnostics`);
    return response.data;
  },

  async resetCopilotClient() {
    const response = await axios.post(`${API_BASE_URL}/operations/heal`);
    return response.data;
  }
};
