import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'
import { clockIn, clockOut, getMyShifts } from '../api/endpoints'
import ShiftHistory from '../components/ShiftHistory'
import { formatTime } from '../utils/format'

export default function Dashboard() {
  const { user, signOut } = useAuth()

  const [shifts, setShifts] = useState([])
  const [loadingShifts, setLoadingShifts] = useState(true)
  const [acting, setActing] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const loadShifts = useCallback(async () => {
    setLoadingShifts(true)
    setError('')
    try {
      const data = await getMyShifts()
      setShifts(Array.isArray(data) ? data : [])
    } catch (err) {
      setError(readError(err, 'Could not load shift history.'))
    } finally {
      setLoadingShifts(false)
    }
  }, [])

  useEffect(() => {
    loadShifts()
  }, [loadShifts])

  // Determine current state from the shift list: an open shift has no clockOut.
  const openShift = useMemo(
    () => shifts.find((s) => !s.clockOut),
    [shifts]
  )
  const isClockedIn = !!openShift

  const handleClockIn = async () => {
    setActing(true)
    setError('')
    setNotice('')
    try {
      const res = await clockIn()
      setNotice(res?.message || 'Clocked in successfully.')
      await loadShifts()
    } catch (err) {
      setError(readError(err, 'Clock in failed.'))
    } finally {
      setActing(false)
    }
  }

  const handleClockOut = async () => {
    setActing(true)
    setError('')
    setNotice('')
    try {
      const res = await clockOut()
      setNotice(res?.message || 'Clocked out successfully.')
      await loadShifts()
    } catch (err) {
      setError(readError(err, 'Clock out failed.'))
    } finally {
      setActing(false)
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
          <Link to="/security" className="btn btn-ghost">
            Security
          </Link>
          {user?.role === 'Admin' && (
            <Link to="/employees/new" className="btn btn-ghost">
              + Add employee
            </Link>
          )}
          <button className="btn btn-ghost" onClick={signOut}>
            Sign out
          </button>
        </div>
      </header>

      <main className="content">
        <section className="card status-card">
          <div className="status-head">
            <div>
              <h2>Hello, {user?.fullName?.split(' ')[0] || 'there'} 👋</h2>
              <p className="muted">
                {isClockedIn
                  ? `On the clock since ${formatTime(openShift.clockIn)}.`
                  : 'You are currently clocked out.'}
              </p>
            </div>
            <span className={`pill ${isClockedIn ? 'pill-on' : 'pill-off'}`}>
              <span className="dot" />
              {isClockedIn ? 'Clocked in' : 'Clocked out'}
            </span>
          </div>

          <div className="actions">
            <button
              className="btn btn-primary"
              onClick={handleClockIn}
              disabled={acting || isClockedIn}
            >
              {acting && !isClockedIn ? 'Working…' : 'Clock in'}
            </button>
            <button
              className="btn btn-danger"
              onClick={handleClockOut}
              disabled={acting || !isClockedIn}
            >
              {acting && isClockedIn ? 'Working…' : 'Clock out'}
            </button>
          </div>

          {notice && <div className="alert alert-success">{notice}</div>}
          {error && <div className="alert alert-error">{error}</div>}
        </section>

        <section className="card">
          <div className="card-head">
            <h3>Shift history</h3>
            <button
              className="btn btn-ghost btn-sm"
              onClick={loadShifts}
              disabled={loadingShifts}
            >
              {loadingShifts ? 'Refreshing…' : 'Refresh'}
            </button>
          </div>
          <ShiftHistory shifts={shifts} loading={loadingShifts} />
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
