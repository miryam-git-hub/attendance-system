import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The backend CORS policy only allows http://localhost:3000,
// so the dev server must run on port 3000.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    strictPort: true,
  },
})
