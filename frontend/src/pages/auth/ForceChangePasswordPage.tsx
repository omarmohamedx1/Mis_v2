import { ShieldCheck } from 'lucide-react';
import { type FormEvent, useState } from 'react';
import { Navigate, useNavigate } from 'react-router-dom';
import { Button } from '../../components/common/Button';
import { AuthLayout } from '../../components/layout/AuthLayout';
import { PasswordInput } from '../../components/forms/PasswordInput';
import { useAuth } from '../../context/AuthContext';
import { useLocalization } from '../../context/LocalizationContext';
import { profileService } from '../../features/auth/services/profileService';
import { getApiErrorMessage } from '../../services/apiClient';

const copy = {
  ar: {
    kicker: 'أمان الحساب',
    title: 'تغيير كلمة المرور',
    subtitle: 'تم تسليمك كلمة مرور مؤقتة. اختر كلمة مرور خاصة بك قبل الدخول إلى النظام.',
    current: 'كلمة المرور المؤقتة',
    next: 'كلمة المرور الجديدة',
    confirm: 'تأكيد كلمة المرور الجديدة',
    help: '١٠ أحرف على الأقل، مع حرف كبير وصغير ورقم ورمز.',
    save: 'حفظ ومتابعة',
    mismatch: 'كلمتا المرور غير متطابقتين.',
    same: 'اختر كلمة مرور مختلفة عن المؤقتة.',
    error: 'تعذر تغيير كلمة المرور. راجع البيانات وحاول مرة أخرى.',
    signout: 'تسجيل الخروج',
  },
  en: {
    kicker: 'Account security',
    title: 'Change password',
    subtitle: 'You signed in with a temporary password. Choose a private password before entering the system.',
    current: 'Temporary password',
    next: 'New password',
    confirm: 'Confirm new password',
    help: 'At least 10 characters with uppercase, lowercase, number, and symbol.',
    save: 'Save and continue',
    mismatch: 'The new passwords do not match.',
    same: 'Choose a password different from the temporary one.',
    error: 'Could not change the password. Review the details and try again.',
    signout: 'Sign out',
  },
} as const;

export function ForceChangePasswordPage() {
  const { language } = useLocalization();
  const t = copy[language];
  const { user, updateUser, logout } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  if (!user?.mustChangePassword) {
    return <Navigate to="/" replace />;
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const currentPassword = String(form.get('currentPassword') ?? '');
    const newPassword = String(form.get('newPassword') ?? '');
    const confirmPassword = String(form.get('confirmPassword') ?? '');
    if (newPassword !== confirmPassword) {
      setError(t.mismatch);
      return;
    }
    if (newPassword === currentPassword) {
      setError(t.same);
      return;
    }
    setBusy(true);
    setError('');
    try {
      await profileService.changePassword({ currentPassword, newPassword, confirmPassword });
      updateUser({ ...user, mustChangePassword: false });
      navigate('/', { replace: true });
    } catch (caught) {
      setError(getApiErrorMessage(caught, t.error));
    } finally {
      setBusy(false);
    }
  };

  return (
    <AuthLayout>
      <p className="text-xs font-semibold uppercase tracking-[0.16em] text-mis-primary">{t.kicker}</p>
      <h2 className="mt-3 text-[1.75rem] font-semibold leading-snug text-[#0a1e36]">{t.title}</h2>
      <p className="mt-2 text-sm leading-6 text-slate-500">{t.subtitle}</p>
      <form className="mt-8 space-y-5" onSubmit={submit}>
        {error && (
          <p className="rounded-md border border-rose-200 bg-rose-50 px-4 py-3 text-sm font-medium text-rose-800">{error}</p>
        )}
        <PasswordInput autoComplete="current-password" label={t.current} name="currentPassword" required />
        <PasswordInput autoComplete="new-password" label={t.next} minLength={10} name="newPassword" required />
        <PasswordInput autoComplete="new-password" label={t.confirm} minLength={10} name="confirmPassword" required />
        <p className="flex items-start gap-2 text-xs leading-5 text-slate-500">
          <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-[#0a1e36]" />
          {t.help}
        </p>
        <Button className="!rounded-lg !bg-[#0a1e36] hover:!bg-[#071627]" isLoading={busy} size="lg" type="submit">
          {t.save}
        </Button>
      </form>
      <button
        className="mt-5 w-full text-sm font-medium text-slate-500 hover:text-[#0a1e36]"
        onClick={() => { logout(); navigate('/login', { replace: true }); }}
        type="button"
      >
        {t.signout}
      </button>
    </AuthLayout>
  );
}
