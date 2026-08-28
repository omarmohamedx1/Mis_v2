import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { getAccessibleModules } from '../features/modules/moduleAccess';

export function DepartmentHome() {
  const { isAuthenticated, user } = useAuth();

  if (!isAuthenticated) return <Navigate to="/login" replace />;
  if (!user) return <Navigate to="/unauthorized" replace />;
  const modules = getAccessibleModules(user);
  if (!modules.length) return <Navigate to="/unauthorized" replace />;
  if (modules.length === 1) return <Navigate to={modules[0].homePath} replace />;
  return <Navigate to="/modules" replace />;
}
