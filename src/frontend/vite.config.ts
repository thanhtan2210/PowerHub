import react from '@vitejs/plugin-react';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

export default defineConfig(({ mode }) => {
  // In development the browser talks to Vite, which forwards API calls to the local gateway
  // so cookies stay same-origin exactly as they are behind the production ingress.
  const apiTarget = loadEnv(mode, '.', 'POWERHUB_').POWERHUB_DEV_API ?? 'http://localhost:8080';

  return {
    plugins: [react()],
    server: {
      port: 5173,
      proxy: { '/api': apiTarget },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test-setup.ts'],
      // Node's fetch needs absolute URLs; the browser build uses same-origin relative paths.
      env: { VITE_API_BASE_URL: 'http://localhost' },
    },
  };
});
