import { useState } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { isPasskeySupported } from '../api/webauthn'

export default function Login() {
  const { signIn, signInWithPasskey } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = location.state?.from?.pathname || '/'

  const [idNumber, setIdNumber] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [passkeyLoading, setPasskeyLoading] = useState(false)

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setLoading(true)
    try {
      await signIn(idNumber.trim(), password)
      navigate(from, { replace: true })
    } catch (err) {
      const msg =
        err.response?.data ||
        err.response?.data?.message ||
        'ת.ז או סיסמה שגויים'
      setError(typeof msg === 'string' ? msg : 'ת.ז או סיסמה שגויים')
    } finally {
      setLoading(false)
    }
  }

  const handlePasskey = async () => {
    setError('')
    setPasskeyLoading(true)
    try {
      await signInWithPasskey()
      navigate(from, { replace: true })
    } catch (err) {
      if (err?.name === 'NotAllowedError' || err?.name === 'AbortError') {
        setError('הזדהות בוטלה')
      } else {
        const msg = err.response?.data
        setError(typeof msg === 'string' && msg ? msg : 'הזדהות עם טביעת אצבע נכשלה')
      }
    } finally {
      setPasskeyLoading(false)
    }
  }

  return (
    <div className="auth-shell">
      <div className="auth-card">
        <div className="brand">
          <div className="brand-mark">⏱</div>
          <h1>מערכת נוכחות</h1>
          <p className="muted">התחברי כדי להחתים שעון</p>
        </div>

        <form onSubmit={handleSubmit} className="form" noValidate>
          <label className="field">
            <span>תעודת זהות</span>
            <input
              type="text"
              value={idNumber}
              onChange={(e) => setIdNumber(e.target.value)}
              placeholder="123456789"
              autoComplete="username"
              required
              autoFocus
            />
          </label>

          <label className="field">
            <span>סיסמה</span>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              autoComplete="current-password"
              required
            />
          </label>

          {error && <div className="alert alert-error">{error}</div>}

          <button type="submit" className="btn btn-primary btn-block" disabled={loading}>
            {loading ? 'מתחבר…' : 'התחבר'}
          </button>

          {isPasskeySupported() && (
            <>
              <div className="divider"><span>או</span></div>
              <button
                type="button"
                className="btn btn-ghost btn-block"
                onClick={handlePasskey}
                disabled={passkeyLoading}
              >
                {passkeyLoading ? 'ממתין לטביעת אצבע…' : '🔑 התחבר עם טביעת אצבע'}
              </button>
            </>
          )}
        </form>
      </div>
    </div>
  )
}