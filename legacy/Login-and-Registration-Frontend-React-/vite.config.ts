import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'fs'

// Read package.json for version
const packageJson = JSON.parse(readFileSync('./package.json', 'utf8'))

// https://vite.dev/config/
export default defineConfig(({ mode }) => ({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        secure: false,
      }
    }
  },
  build: {
    outDir: 'dist',
    sourcemap: mode === 'development',
    minify: mode === 'development' ? false : 'esbuild',
    target: 'esnext',
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          router: ['react-router-dom'],
          ui: ['@dnd-kit/core', '@dnd-kit/sortable', '@dnd-kit/utilities', 'flowbite'],
          utils: ['axios']
        }
      }
    },
    chunkSizeWarningLimit: 1000,
    reportCompressedSize: mode === 'analyze'
  },
  define: {
    __APP_VERSION__: JSON.stringify(packageJson.version)
  },
  envPrefix: 'VITE_'
}))
