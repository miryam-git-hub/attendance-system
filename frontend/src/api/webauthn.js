// Helpers to convert between base64url strings (what the Fido2 backend sends/expects)
// and ArrayBuffers (what the browser WebAuthn API works with).

export function coerceToArrayBuffer(base64url) {
  if (typeof base64url !== 'string') return base64url
  // base64url -> base64
  let base64 = base64url.replace(/-/g, '+').replace(/_/g, '/')
  // pad to a multiple of 4
  const pad = base64.length % 4
  if (pad === 2) base64 += '=='
  else if (pad === 3) base64 += '='
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)
  return bytes.buffer
}

export function coerceToBase64Url(buffer) {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i])
  return btoa(binary)
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=/g, '')
}

// True when the browser supports WebAuthn.
export const isPasskeySupported = () =>
  typeof window !== 'undefined' && !!window.PublicKeyCredential
