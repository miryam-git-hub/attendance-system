import { Navigate } from 'react-router-dom'
import { useAuth } from '../context/AuthContext'

// Gate routes that require authentication.
export default function ProtectedRoute({ children }) {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) return <Navigate to="/login" replace />
  return children
}
