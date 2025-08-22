<template>
  <div class="chat-container">
    <div class="connection-status">
      <span :class="['status-indicator', connectionStatus]"></span>
      {{ connectionStatusText }}
    </div>

    <div class="messages-container" ref="messagesContainer">
      <div v-for="(message, index) in messages"
           :key="index"
           class="message"
           :class="{ 'system-message': message.type === 'system' }">
        <span class="timestamp">{{ formatTimestamp(message.timestamp) }}</span>
        <span class="content">{{ message.content }}</span>
      </div>
    </div>

    <div class="input-container">
      <input v-model="inputMessage"
             @keypress.enter="sendMessage"
             placeholder="Type a message..."
             :disabled="!isConnected"
             class="message-input" />
      <button @click="sendMessage"
              :disabled="!isConnected || !inputMessage.trim()"
              class="send-button">
        Send
      </button>
    </div>

    <div class="controls">
      <button @click="connectWebSocket"
              :disabled="isConnected"
              class="control-button connect">
        Connect
      </button>
      <button @click="disconnectWebSocket"
              :disabled="!isConnected"
              class="control-button disconnect">
        Disconnect
      </button>
      <button @click="clearMessages"
              class="control-button clear">
        Clear Messages
      </button>
    </div>
  </div>
</template>

<script>
  export default {
    name: 'ChatComponent',
    data() {
      return {
        websocket: null,
        isConnected: false,
        connectionStatus: 'disconnected', // disconnected, connecting, connected, error
        messages: [],
        inputMessage: '',
        reconnectAttempts: 0,
        maxReconnectAttempts: 5,
        reconnectDelay: 3000, // 3 seconds
        websocketUrl: 'ws://127.0.0.1:9656/' // Replace with your WebSocket server URL
      }
    },
    computed: {
      connectionStatusText() {
        const statusMap = {
          disconnected: 'Disconnected',
          connecting: 'Connecting...',
          connected: 'Connected',
          error: 'Connection Error'
        }
        return statusMap[this.connectionStatus]
      }
    },
    mounted() {
      // Auto-connect when component mounts (optional)
      //this.connectWebSocket();
    },
    beforeUnmount() {
      // Clean up WebSocket connection when component is destroyed
      this.disconnectWebSocket();
    },
    methods: {
      connectWebSocket() {
        if (this.isConnected) return;

        this.connectionStatus = 'connecting';
        this.reconnectAttempts = 0;

        try {
          this.websocket = new WebSocket(this.websocketUrl);

          this.websocket.onopen = () => {
            this.isConnected = true;
            this.connectionStatus = 'connected';
            this.reconnectAttempts = 0;
            this.addSystemMessage('Connected to WebSocket server');
          };

          this.websocket.onmessage = (event) => {
            this.handleIncomingMessage(event.data);
          };

          this.websocket.onclose = (event) => {
            this.isConnected = false;
            this.connectionStatus = 'disconnected';

            if (!event.wasClean) {
              this.addSystemMessage(`Connection closed unexpectedly: ${event.code} ${event.reason}`);
              //this.attemptReconnect();
            } else {
              this.addSystemMessage('Connection closed');
            }
          };

          this.websocket.onerror = (error) => {
            this.connectionStatus = 'error';
            this.addSystemMessage('WebSocket error occurred');
            console.error('WebSocket error:', error);
          };

        } catch (error) {
          this.connectionStatus = 'error';
          this.addSystemMessage('Failed to create WebSocket connection');
          console.error('WebSocket connection error:', error);
        }
      },

      disconnectWebSocket() {
        if (this.websocket) {
          this.websocket.close(1000, 'User disconnected');
          this.websocket = null;
        }
        this.isConnected = false;
        this.connectionStatus = 'disconnected';
      },

      attemptReconnect() {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
          this.reconnectAttempts++;
          this.addSystemMessage(`Attempting to reconnect (${this.reconnectAttempts}/${this.maxReconnectAttempts})...`);

          setTimeout(() => {
            //this.connectWebSocket();
          }, this.reconnectDelay);
        } else {
          this.addSystemMessage('Max reconnection attempts reached. Please connect manually.');
        }
      },

      sendMessage() {
        if (!this.isConnected || !this.inputMessage.trim()) return;

        try {
          this.websocket.send(this.inputMessage);
          this.addMessage(this.inputMessage, 'outgoing');
          this.inputMessage = '';
        } catch (error) {
          this.addSystemMessage('Failed to send message');
          console.error('Send message error:', error);
        }
      },

      handleIncomingMessage(data) {
        try {
          // Try to parse as JSON if it's a JSON string
          let parsedData;
          try {
            parsedData = JSON.parse(data);
          } catch {
            parsedData = data; // If not JSON, use as plain text
          }

          const messageContent = typeof parsedData === 'object'
            ? JSON.stringify(parsedData, null, 2)
            : parsedData.toString();

          this.addMessage(messageContent, 'incoming');
        } catch (error) {
          console.error('Error processing incoming message:', error);
          this.addSystemMessage('Error processing incoming message');
        }
      },

      addMessage(content, type = 'incoming') {
        this.messages.push({
          content,
          type,
          timestamp: new Date()
        });
        this.scrollToBottom();
      },

      addSystemMessage(content) {
        this.messages.push({
          content,
          type: 'system',
          timestamp: new Date()
        });
        this.scrollToBottom();
      },

      clearMessages() {
        this.messages = [];
      },

      scrollToBottom() {
        this.$nextTick(() => {
          const container = this.$refs.messagesContainer;
          if (container) {
            container.scrollTop = container.scrollHeight;
          }
        });
      },

      formatTimestamp(timestamp) {
        return new Date(timestamp).toLocaleTimeString();
      }
    }
  }
</script>

<style scoped>
  .chat-container {
    max-width: 600px;
    margin: 0 auto;
    padding: 20px;
    border: 1px solid #ddd;
    border-radius: 8px;
    background-color: #f9f9f9;
  }

  .connection-status {
    display: flex;
    align-items: center;
    margin-bottom: 15px;
    padding: 8px;
    border-radius: 4px;
    background-color: #f5f5f5;
    font-weight: bold;
  }

  .status-indicator {
    width: 12px;
    height: 12px;
    border-radius: 50%;
    margin-right: 8px;
  }

    .status-indicator.disconnected {
      background-color: #dc3545;
    }

    .status-indicator.connecting {
      background-color: #ffc107;
    }

    .status-indicator.connected {
      background-color: #28a745;
    }

    .status-indicator.error {
      background-color: #dc3545;
    }

  .messages-container {
    height: 300px;
    overflow-y: auto;
    border: 1px solid #ddd;
    border-radius: 4px;
    padding: 10px;
    background-color: white;
    margin-bottom: 15px;
  }

  .message {
    margin-bottom: 10px;
    padding: 8px;
    border-radius: 4px;
    background-color: #e3f2fd;
  }

    .message.system-message {
      background-color: #fff3cd;
      font-style: italic;
    }

  .timestamp {
    font-size: 0.8em;
    color: #666;
    margin-right: 8px;
  }

  .input-container {
    display: flex;
    gap: 10px;
    margin-bottom: 15px;
  }

  .message-input {
    flex: 1;
    padding: 8px 12px;
    border: 1px solid #ddd;
    border-radius: 4px;
    font-size: 14px;
  }

    .message-input:disabled {
      background-color: #f5f5f5;
      cursor: not-allowed;
    }

  .send-button {
    padding: 8px 16px;
    background-color: #007bff;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
  }

    .send-button:disabled {
      background-color: #6c757d;
      cursor: not-allowed;
    }

    .send-button:hover:not(:disabled) {
      background-color: #0056b3;
    }

  .controls {
    display: flex;
    gap: 10px;
    justify-content: center;
  }

  .control-button {
    padding: 8px 16px;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 14px;
  }

    .control-button.connect {
      background-color: #28a745;
      color: white;
    }

      .control-button.connect:disabled {
        background-color: #6c757d;
        cursor: not-allowed;
      }

    .control-button.disconnect {
      background-color: #dc3545;
      color: white;
    }

      .control-button.disconnect:disabled {
        background-color: #6c757d;
        cursor: not-allowed;
      }

    .control-button.clear {
      background-color: #6c757d;
      color: white;
    }

    .control-button:hover:not(:disabled) {
      opacity: 0.9;
    }
</style>