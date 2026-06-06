// Single source of truth for tokens + user, persisted to localStorage.
const ACCESS_KEY = 'as_accessToken'
const REFRESH_KEY = 'as_refreshToken'
const USER_KEY = 'as_user'

export const tokenStore = {
  getAccessToken: () => localStorage.getItem(ACCESS_KEY),
  getRefreshToken: () => localStorage.getItem(REFRESH_KEY),

  getUser: () => {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? JSON.parse(raw) : null
  },

  setTokens: (accessToken, refreshToken) => {
    if (accessToken) localStorage.setItem(ACCESS_KEY, accessToken)
    if (refreshToken) localStorage.setItem(REFRESH_KEY, refreshToken)
  },

  setUser: (user) => {
    if (user) localStorage.setItem(USER_KEY, JSON.stringify(user))
  },

  clear: () => {
    localStorage.removeItem(ACCESS_KEY)
    localStorage.removeItem(REFRESH_KEY)
    localStorage.removeItem(USER_KEY)
  },
}
