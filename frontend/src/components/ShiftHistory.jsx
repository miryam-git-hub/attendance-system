import { formatDate, formatTime, formatHours } from '../utils/format'

export default function ShiftHistory({ shifts, loading }) {
  if (loading) {
    return <div className="empty muted">Loading shifts…</div>
  }

  if (!shifts.length) {
    return <div className="empty muted">No shifts recorded yet.</div>
  }

  return (
    <div className="table-wrap">
      <table className="table">
        <thead>
          <tr>
            <th>Date</th>
            <th>Clock in</th>
            <th>Clock out</th>
            <th>Total</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {shifts.map((s) => {
            const open = !s.clockOut
            return (
              <tr key={s.recordId}>
                <td>{formatDate(s.shiftDate || s.clockIn)}</td>
                <td>{formatTime(s.clockIn)}</td>
                <td>{formatTime(s.clockOut)}</td>
                <td>{open ? '—' : formatHours(s.totalHours)}</td>
                <td>
                  <span className={`badge ${open ? 'badge-open' : 'badge-closed'}`}>
                    {open ? 'Open' : 'Closed'}
                  </span>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
