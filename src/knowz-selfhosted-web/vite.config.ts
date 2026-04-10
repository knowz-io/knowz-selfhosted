import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    host: '0.0.0.0',
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/healthz': {
        target: 'http://localhost:5000',
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
})
