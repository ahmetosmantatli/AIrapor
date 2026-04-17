import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

// Geliştirme: VITE_API_BASE_URL boş → /api istekleri bu proxy ile 5195'e gider.
// Üretim (API ile aynı kök): build sonrası npm run publish:api ile dist → API wwwroot; VITE_API_BASE_URL tanımlamayın.
// Üretim (API ayrı domain): .env.production içinde VITE_API_BASE_URL=https://api.sizin-domain.com
export default defineConfig({
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5195',
        changeOrigin: true,
      },
    },
  },
})
