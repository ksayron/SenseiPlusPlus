import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath, URL } from 'node:url'
import { readFileSync } from 'node:fs'

const proxyTarget = process.env.SENSEI_API_PROXY_TARGET ?? 'http://localhost:5062'
const backendProxy = { target: proxyTarget, secure: !/^https:\/\/localhost(?::\d+)?$/.test(proxyTarget) }

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
    https: process.env.SENSEI_WEB_HTTPS_CERT && process.env.SENSEI_WEB_HTTPS_KEY ? {
      cert: readFileSync(process.env.SENSEI_WEB_HTTPS_CERT), key: readFileSync(process.env.SENSEI_WEB_HTTPS_KEY),
    } : undefined,
    port: 5173,
    fs: { allow: [fileURLToPath(new URL('.', import.meta.url)), fileURLToPath(new URL('../docs', import.meta.url))] },
    proxy: {
      '/api': backendProxy,
      '/signin-google': backendProxy,
      '/signin-github': backendProxy,
      '^/connect/': backendProxy,
      '/.well-known': backendProxy,
      '/health': backendProxy,
    },
  },
})
