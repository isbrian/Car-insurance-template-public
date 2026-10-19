import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        onlyExplicitManualChunks: true,
        manualChunks(id) {
          if (!id.includes('node_modules')) return undefined
          if (/node_modules\/(react|react-dom|react-router|react-router-dom|scheduler)(\/|$)/.test(id)) {
            return 'react-vendor'
          }
          if (id.includes('/node_modules/@tanstack/')) return 'query-vendor'
          if (
            id.includes('/node_modules/@rc-component/')
            || /node_modules\/rc-[^/]+\//.test(id)
            || id.includes('/node_modules/resize-observer-polyfill/')
          ) {
            return 'rc-vendor'
          }
          return undefined
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': 'http://127.0.0.1:5080',
    },
  },
})
