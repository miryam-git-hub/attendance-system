import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { tokenStore } from '../api/tokenStore'
import { setAuthFailureHandler } from '../api/client'
import * as auth from '../api/endpoints'
import { coerceToArrayBuffer, coerceToBase64Url } from '../api/webauthn'

const AuthContext = createContext(null)

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => tokenStore.getUser())

  // Clear local state whenever a token refresh ultimately fails.
  useEffect(() => {
    setAuthFailureHandler(() => {
      tokenStore.clear()
      setUser(null)
    })
  }, [])

  const signIn = async (idNumber, password) => {
    const data = await auth.login(idNumber, password)
    return applyLogin(data)
  }
  // Store tokens + profile from a login-style response (shared by password & passkey).
  const applyLogin = (data) => {
    tokenStore.setTokens(data.accessToken, data.refreshToken)
    const profile = {
      employeeId: data.employeeId,
      fullName: data.fullName,
      email: data.email,
      role: data.role,
    }
    tokenStore.setUser(profile)
    setUser(profile)
    return profile
  }

  const signInWithPasskey = async () => {
    // 1) Ask the server for a challenge (usernameless).
    const { flowId, options } = await auth.passkeyLoginBegin()

    // 2) Convert the challenge to an ArrayBuffer for the browser API.
    const publicKey = {
      ...options,
      challenge: coerceToArrayBuffer(options.challenge),
      allowCredentials: [], // usernameless: let the authenticator pick the account
    }

    // 3) Prompt the authenticator (fingerprint / Windows Hello).
    const cred = await navigator.credentials.get({ publicKey })

    // 4) Serialize the assertion back to base64url for the server.
    const assertionResponse = {
      id: cred.id,
      rawId: coerceToBase64Url(cred.rawId),
      type: cred.type,
      extensions: cred.getClientExtensionResults(),
      response: {
        clientDataJSON: coerceToBase64Url(cred.response.clientDataJSON),
        authenticatorData: coerceToBase64Url(cred.response.authenticatorData),
        signature: coerceToBase64Url(cred.response.signature),
        userHandle: cred.response.userHandle
          ? coerceToBase64Url(cred.response.userHandle)
          : null,
      },
    }

    // 5) Verify on the server and receive the same token payload as password login.
    const data = await auth.passkeyLoginComplete(flowId, assertionResponse)
    return applyLogin(data)
  }

  const signOut = async () => {
    const refreshToken = tokenStore.getRefreshToken()
    try {
      if (refreshToken) await auth.logout(refreshToken)
    } catch {
      // Even if the server call fails we still clear locally.
    } finally {
      tokenStore.clear()
      setUser(null)
    }
  }

  const value = useMemo(
    () => ({ user, isAuthenticated: !!user, signIn, signInWithPasskey, signOut }),
    [user]
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used within an AuthProvider')
  return ctx
}
