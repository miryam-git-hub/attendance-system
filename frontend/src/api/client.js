import axios from 'axios'
import { tokenStore } from './tokenStore'

const baseURL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5134'

// Main client used by the whole app.
const api = axios.create({ baseURL })

// A bare client for the refresh call so its 401s never trigger the
// response interceptor below (which would cause infinite recursion).
const refreshClient = axios.create({ baseURL })

// Attach the access token to every outgoing request.
api.interceptors.request.use((config) => {
  const token = tokenStore.getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// --- Automatic refresh handling -------------------------------------------
// When a request fails with 401 we try to refresh the access token once.
// Concurrent 401s are queued so we only hit /refresh a single time.
let isRefreshing = false
let pendingQueue = []

const processQueue = (error, token = null) => {
  pendingQueue.forEach(({ resolve, reject }) => {
    if (error) reject(error)
    else resolve(token)
  })
  pendingQueue = []
}

// Called when refresh ultimately fails — log the user out.
let onAuthFailure = () => {}
export const setAuthFailureHandler = (fn) => { onAuthFailure = fn }

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    // Only handle 401s that we haven't already retried, and never retry
    // the auth endpoints themselves.
    const isAuthCall = originalRequest?.url?.includes('/api/auth/')
    if (
      error.response?.status !== 401 ||
      originalRequest?._retry ||
      isAuthCall
    ) {
      return Promise.reject(error)
    }

    const refreshToken = tokenStore.getRefreshToken()
    if (!refreshToken) {
      onAuthFailure()
      return Promise.reject(error)
    }

    originalRequest._retry = true

    // A refresh is already in flight — wait for it, then retry.
    if (isRefreshing) {
      return new Promise((resolve, reject) => {
        pendingQueue.push({ resolve, reject })
      })
        .then((token) => {
          originalRequest.headers.Authorization = `Bearer ${token}`
          return api(originalRequest)
        })
        .catch((err) => Promise.reject(err))
    }

    isRefreshing = true
    try {
      const { data } = await refreshClient.post('/api/auth/refresh', {
        refreshToken,
      })
      tokenStore.setTokens(data.accessToken, data.refreshToken)
      processQueue(null, data.accessToken)
      originalRequest.headers.Authorization = `Bearer ${data.accessToken}`
      return api(originalRequest)
    } catch (refreshError) {
      processQueue(refreshError, null)
      tokenStore.clear()
      onAuthFailure()
      return Promise.reject(refreshError)
    } finally {
      isRefreshing = false
    }
  }
)

export default api
