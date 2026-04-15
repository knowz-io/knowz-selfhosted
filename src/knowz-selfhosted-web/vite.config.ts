import { defineConfig } from 'vitest/config'
import { loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  // Load env vars from .env, .env.local, .env.{mode}, .env.{mode}.local
  // This lets `npm run dev:cloud` (mode=cloud) load .env.cloud with VITE_PROXY_TARGET set
  const env = loadEnv(mode, '.', '')
  const apiTarget = env.VITE_PROXY_TARGET || 'http://localhost:5000'

  return {
    plugins: [react()],
    server: {
      port: 5173,
      host: '0.0.0.0',
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
        },
        '/healthz': {
          target: apiTarget,
          changeOrigin: true,
        },
      },
    },
    build: {
      outDir: 'dist',
    },
    test: {
      globals: true,
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      css: false,
      // Only run unit tests under src/. The `tests/` directory holds Playwright
      // end-to-end specs which use a different test runner and will fail to
      // parse if vitest tries to load them.
      include: ['src/**/*.{test,spec}.{js,ts,jsx,tsx}'],
      exclude: ['node_modules', 'dist', 'tests'],
    },
  }
})
