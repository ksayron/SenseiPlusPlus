import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  build: {
    rollupOptions: { input: {
      app: fileURLToPath(new URL('./index.html', import.meta.url)),
      demo: fileURLToPath(new URL('./ui-demo.html', import.meta.url)),
    } },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5062',
      '/health': 'http://localhost:5062',
    },
  },
})
