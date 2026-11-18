import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';

const API_PROXY_TARGET_VARIABLE = 'API_PROXY_TARGET';

export default defineConfig(({ mode }) => {
  // Empty prefix: load every variable, not only VITE_ ones, because this value is used by the
  // dev server only and must never be bundled into the client.
  const environment = loadEnv(mode, process.cwd(), '');
  const apiProxyTarget = environment[API_PROXY_TARGET_VARIABLE];
  if (mode === 'development' && !apiProxyTarget) {
    throw new Error(`Missing ${API_PROXY_TARGET_VARIABLE}; set it in frontend/.env.development or the environment.`);
  }

  return {
    plugins: [react()],
    server: {
      // Same origin for SPA and API in development, as in production (cookie session, DEC-21).
      proxy: apiProxyTarget ? { '/api': { target: apiProxyTarget, changeOrigin: false } } : {},
    },
    test: {
      environment: 'jsdom',
      globals: true,
      setupFiles: ['./src/testSetup.ts'],
    },
  };
});
