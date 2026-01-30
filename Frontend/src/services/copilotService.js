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

  async deleteSession(sessionId) {
    const response = await axios.delete(`${API_BASE_URL}/chat/session/${sessionId}`);
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
  }
};
