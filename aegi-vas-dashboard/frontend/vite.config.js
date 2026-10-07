import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// One origin: in production the ASP.NET Core API serves this build from its wwwroot folder.
// In development, Vite proxies API and health calls to the API on port 5080.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': 'http://localhost:5080', '/health': 'http://localhost:5080' },
  },
  build: {
    outDir: '../backend/src/Aegi.Vas.Dashboard.Api/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 1200,
  },
})
