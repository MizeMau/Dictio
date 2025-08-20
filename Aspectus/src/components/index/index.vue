<template>
  <div>
    <chat-component />
  </div>
</template>

<script>
  import ChatComponent from './chat.vue'

  export default {
    components: {
      'chat-component': ChatComponent
    },
    data() {
      return {

      }
    },
    methods: {
      connectToWebsocket() {

      }
    },
  }
</script>