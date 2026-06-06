import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import {
  listPasskeys,
  deletePasskey,
  passkeyRegisterBegin,
  passkeyRegisterComplete,
} from '../api/endpoints'
import {
  coerceToArrayBuffer,
  coerceToBase64Url,
  isPasskeySupported,
} from '../api/webauthn'
import { formatDate, formatTime } from '../utils/format'

export default function Security() {
  const { user, signOut } = useAuth()

  const [passkeys, setPasskeys] = useState([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const data = await listPasskeys()
      setPasskeys(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(readError(err, 'Could not load passkeys.'))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    load()
  }, [load])

  const handleAdd = async () => {
    setError('')
    setNotice('')
    setBusy(true)
    try {
      // 1) Get creation options from the server.
      const options = await passkeyRegisterBegin()

      // 2) Convert the binary fields to ArrayBuffers for the browser.
      const publicKey = {
        ...options,
        challenge: coerceToArrayBuffer(options.challenge),
        user: { ...options.user, id: coerceToArrayBuffer(options.user.id) },
        excludeCredentials: (options.excludeCredentials || []).map((c) => ({
          ...c,
          id: coerceToArrayBuffer(c.id),
        })),
      }

      // 3) Prompt the authenticator to create a credential.
      const cred = await navigator.credentials.create({ publicKey })

      // 4) Serialize back to base64url for the server.
      const attestationResponse = {
        id: cred.id,
        rawId: coerceToBase64Url(cred.rawId),
        type: cred.type,
        extensions: cred.getClientExtensionResults(),
        response: {
          clientDataJSON: coerceToBase64Url(cred.response.clientDataJSON),
          attestationObject: coerceToBase64Url(cred.response.attestationObject),
        },
      }

      const deviceName =
        window.prompt('Name this passkey (e.g. "My laptop"):', 'My device') ||
        'Passkey'

      await passkeyRegisterComplete(attestationResponse, deviceName)
      setNotice('Passkey added successfully.')
      await load()
    } catch (err) {
      if (err?.name === 'NotAllowedError' || err?.name === 'AbortError') {
        setError('Passkey setup was cancelled.')
      } else {
        setError(readError(err, 'Could not add passkey.'))
      }
    } finally {
      setBusy(false)
    }
  }

  const handleDelete = async (id) => {
    if (!window.confirm('Remove this passkey?')) return
    setError('')
    setNotice('')
    try {
      await deletePasskey(id)
      setNotice('Passkey removed.')
      await load()
    } catch (err) {
      setError(readError(err, 'Could not remove passkey.'))
    }
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="topbar-brand">
          <span className="brand-mark sm">⏱</span>
          <span>Attendance</span>
        </div>
        <div className="topbar-user">
          <div className="user-meta">
            <span className="user-name">{user?.fullName}</span>
            <span className="muted user-role">{user?.role}</span>
          </div>
          <button className="btn btn-ghost" onClick={signOut}>
            Sign out
          </button>
        </div>
      </header>

      <main className="content">
        <div className="page-head">
          <div>
            <h2>Security</h2>
            <p className="muted">
              Manage passkeys (fingerprint / Windows Hello) for one-tap sign-in.
            </p>
          </div>
          <Link to="/" className="btn btn-ghost btn-sm">
            ← Back to dashboard
          </Link>
        </div>

        <section className="card">
          <div className="card-head">
            <h3>Your passkeys</h3>
            {isPasskeySupported() && (
              <button className="btn btn-primary btn-sm" onClick={handleAdd} disabled={busy}>
                {busy ? 'Working…' : '+ Add a passkey'}
              </button>
            )}
          </div>

          {!isPasskeySupported() && (
            <div className="alert alert-error">
              This browser does not support passkeys.
            </div>
          )}
          {notice && <div className="alert alert-success">{notice}</div>}
          {error && <div className="alert alert-error">{error}</div>}

          {loading ? (
            <div className="empty muted">Loading passkeys…</div>
          ) : passkeys.length === 0 ? (
            <div className="empty muted">
              No passkeys yet. Add one to enable fingerprint sign-in.
            </div>
          ) : (
            <div className="table-wrap">
              <table className="table">
                <thead>
                  <tr>
                    <th>Device</th>
                    <th>Added</th>
                    <th>Last used</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {passkeys.map((p) => (
                    <tr key={p.id}>
                      <td>{p.deviceName || 'Passkey'}</td>
                      <td>{formatDate(p.createdAt)}</td>
                      <td>
                        {p.lastUsedAt
                          ? `${formatDate(p.lastUsedAt)} ${formatTime(p.lastUsedAt)}`
                          : 'Never'}
                      </td>
                      <td>
                        <button
                          className="btn btn-danger btn-sm"
                          onClick={() => handleDelete(p.id)}
                        >
                          Remove
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </main>
    </div>
  )
}

function readError(err, fallback) {
  const data = err.response?.data
  if (typeof data === 'string' && data) return data
  if (data?.message) return data.message
  return fallback
}
