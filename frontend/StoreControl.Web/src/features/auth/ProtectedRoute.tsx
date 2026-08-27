import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './useAuth'

export function ProtectedRoute() {
  const { user, isBootstrapping } = useAuth()
  const location = useLocation()

  if (isBootstrapping) {
    return <div className="page-loader">Restaurando sessão segura…</div>
  }

  return user ? <Outlet /> : <Navigate to="/login" replace state={{ from: location.pathname }} />
}

export function PermissionRoute({ permission }: { permission: string }) {
  const { hasPermission } = useAuth()
  return hasPermission(permission) ? <Outlet /> : <Navigate to="/access-denied" replace />
}
