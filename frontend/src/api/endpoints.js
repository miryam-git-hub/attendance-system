import api from './client'

// --- Auth ------------------------------------------------------------------
export const login = (idNumber, password) =>
  api.post('/api/auth/login', { idNumber, password }).then((r) => r.data)

export const logout = (refreshToken) =>
  api.post('/api/auth/logout', { refreshToken }).then((r) => r.data)

// --- Passkeys (WebAuthn) ---------------------------------------------------
export const passkeyRegisterBegin = () =>
  api.post('/api/auth/passkey/register/begin').then((r) => r.data)

export const passkeyRegisterComplete = (attestationResponse, deviceName) =>
  api
    .post('/api/auth/passkey/register/complete', { attestationResponse, deviceName })
    .then((r) => r.data)

export const passkeyLoginBegin = () =>
  api.post('/api/auth/passkey/login/begin').then((r) => r.data)

export const passkeyLoginComplete = (flowId, assertionResponse) =>
  api
    .post('/api/auth/passkey/login/complete', { flowId, assertionResponse })
    .then((r) => r.data)

export const listPasskeys = () =>
  api.get('/api/auth/passkeys').then((r) => r.data)

export const deletePasskey = (id) =>
  api.delete(`/api/auth/passkeys/${id}`).then((r) => r.data)

// --- Employees -------------------------------------------------------------
// Creates an employee as the current (logged-in) user. Returns NO tokens,
// so it never affects the active session.
export const createEmployee = ({ fullName, email, password, role }) =>
  api
    .post('/api/employees', { fullName, email, password, role })
    .then((r) => r.data)

// --- Attendance ------------------------------------------------------------
export const clockIn = () =>
  api.post('/api/attendance/clock-in').then((r) => r.data)

export const clockOut = () =>
  api.post('/api/attendance/clock-out').then((r) => r.data)

export const getMyShifts = () =>
  api.get('/api/attendance/my-shifts').then((r) => r.data)
