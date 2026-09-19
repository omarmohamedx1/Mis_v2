import { Navigate } from 'react-router-dom';
import { AuthLayout } from '../../components/layout/AuthLayout';
import { LoginForm } from '../../features/auth/components/LoginForm';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';

export function LoginPage() {
  const { isAuthenticated, user } = useAuth();
  const { t } = useLocalization();

  if (isAuthenticated) {
    return <Navigate to={user?.mustChangePassword ? '/change-password' : '/'} replace />;
  }

  return (
    <AuthLayout>
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-mis-primary">{t('internalSystem')}</p>
      <h2 className="mt-3 text-[1.75rem] font-semibold leading-snug text-[#0a1e36]">{t('welcomeBack')}</h2>
      <p className="mt-2 text-sm leading-6 text-slate-500">{t('signInSubtitle')}</p>
      <div className="mt-8">
        <LoginForm />
      </div>
    </AuthLayout>
  );
}
