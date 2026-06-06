import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { createEmployee } from '../api/endpoints'

const EMPTY = { fullName: '', email: '', password: '', role: 'Employee' }

export default function AddEmployee() {
  const { user, signOut } = useAuth()

  const [form, setForm] = useState(EMPTY)
  const [error, setError] = useState('')
  const [created, setCreated] = useState(null)
  const [saving, setSaving] = useState(false)

  const update = (key) => (e) =>
    setForm((f) => ({ ...f, [key]: e.target.value }))

  const handleSubmit = async (e) => {
    e.preventDefault()
    setError('')
    setCreated(null)
    setSaving(true)
    try {
      const emp = await createEmployee({
        fullName: form.fullName.trim(),
        email: form.email.trim(),
        password: form.password,
        role: form.role,
      })
      setCreated(emp)
      setForm(EMPTY) // clear so another employee can be added right away
    } catch (err) {
      setError(readError(err, 'Could not create employee.'))
    } finally {
      setSaving(false)
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
            <h2>Add employee</h2>
            <p className="muted">Create a new account. You stay signed in.</p>
          </div>
          <Link to="/" className="btn btn-ghost btn-sm">
            ← Back to dashboard
          </Link>
        </div>

        <section className="card form-card">
          <form onSubmit={handleSubmit} className="form" noValidate>
            <label className="field">
              <span>Full name</span>
              <input
                type="text"
                value={form.fullName}
                onChange={update('fullName')}
                placeholder="Dana Levi"
                required
                autoFocus
              />
            </label>

            <label className="field">
              <span>Email</span>
              <input
                type="email"
                value={form.email}
                onChange={update('email')}
                placeholder="dana@company.com"
                autoComplete="off"
                required
              />
            </label>

            <label className="field">
              <span>Password</span>
              <input
                type="password"
                value={form.password}
                onChange={update('password')}
                placeholder="At least 6 characters"
                autoComplete="new-password"
                minLength={6}
                required
              />
            </label>

            <label className="field">
              <span>Role</span>
              <select value={form.role} onChange={update('role')}>
                <option value="Employee">Employee</option>
                <option value="Admin">Admin</option>
              </select>
            </label>

            {error && <div className="alert alert-error">{error}</div>}
            {created && (
              <div className="alert alert-success">
                ✓ Created <strong>{created.fullName}</strong> ({created.email}) as{' '}
                {created.role}.
              </div>
            )}

            <button
              type="submit"
              className="btn btn-primary btn-block"
              disabled={saving}
            >
              {saving ? 'Creating…' : 'Create employee'}
            </button>
          </form>
        </section>
      </main>
    </div>
  )
}

function readError(err, fallback) {
  if (err.response?.status === 403)
    return 'You do not have permission to add employees (Admins only).'
  const data = err.response?.data
  if (typeof data === 'string' && data) return data
  if (data?.message) return data.message
  return fallback
}
