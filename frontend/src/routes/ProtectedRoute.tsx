import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { canAccessDepartment } from '../features/modules/moduleAccess';

interface ProtectedRouteProps {
  department?: string;
  requiredRole?: string;
}

export function ProtectedRoute({ department, requiredRole }: ProtectedRouteProps) {
  const { isAuthenticated, user } = useAuth();
  const location = useLocation();

  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  if (department && (!user || !canAccessDepartment(user, department))) {
    return <Navigate to="/unauthorized" replace />;
  }

  if (requiredRole && !user?.roles.includes(requiredRole)) {
    return <Navigate to="/unauthorized" replace />;
  }

  return <Outlet />;
}
