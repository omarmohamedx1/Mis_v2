import { useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { Button } from '../../../components/common/Button';
import { FormError } from '../../../components/common/FormError';
import { Checkbox } from '../../../components/forms/Checkbox';
import { PasswordInput } from '../../../components/forms/PasswordInput';
import { TextInput } from '../../../components/forms/TextInput';
import { useAuth } from '../../../context/AuthContext';
import { getApiErrorMessage } from '../../../services/apiClient';
import type { LoginFormValues } from '../types/auth';
import { validateLogin, type LoginValidationErrors } from '../validation/loginValidation';
import { useLocalization } from '../../../context/LocalizationContext';

interface RouteState {
  from?: {
    pathname?: string;
  };
}

export function LoginForm() {
  const { login } = useAuth();
  const { t } = useLocalization();
  const navigate = useNavigate();
  const location = useLocation();
  const routeState = location.state as RouteState | null;
  const redirectTo = routeState?.from?.pathname ?? '/';

  const [values, setValues] = useState<LoginFormValues>({
    username: '',
    password: '',
    rememberMe: false,
  });
  const [errors, setErrors] = useState<LoginValidationErrors>({});
  const [formError, setFormError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError('');

    const validationErrors = validateLogin(values, { usernameRequired: t('usernameRequired'), passwordRequired: t('passwordRequired') });
    setErrors(validationErrors);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      const signedIn = await login(
        {
          username: values.username.trim(),
          password: values.password,
        },
        values.rememberMe,
      );
      const blockedReturn = redirectTo === '/login' || redirectTo === '/change-password' || redirectTo === '/unauthorized';
      navigate(signedIn.mustChangePassword ? '/change-password' : blockedReturn ? '/' : redirectTo, { replace: true });
    } catch (error) {
      setFormError(getApiErrorMessage(error, t('invalidCredentials')));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <form className="space-y-5" noValidate onSubmit={handleSubmit}>
      <FormError message={formError} />

      <TextInput
        autoComplete="username"
        error={errors.username}
        label={t('username')}
        name="username"
        onChange={(event) => setValues((current) => ({ ...current, username: event.target.value }))}
        placeholder={t('usernamePlaceholder')}
        required
        value={values.username}
      />

      <PasswordInput
        autoComplete="current-password"
        error={errors.password}
        label={t('password')}
        name="password"
        onChange={(event) => setValues((current) => ({ ...current, password: event.target.value }))}
        placeholder={t('passwordPlaceholder')}
        required
        value={values.password}
      />

      <div className="flex items-center justify-between gap-4">
        <Checkbox
          checked={values.rememberMe}
          label={t('rememberMe')}
          name="rememberMe"
          onChange={(event) => setValues((current) => ({ ...current, rememberMe: event.target.checked }))}
        />
        <a className="text-sm font-medium text-slate-500 transition hover:text-mis-primary" href="/login">
          {t('forgotPassword')}
        </a>
      </div>

      <Button className="mt-1 !rounded-lg !bg-[#0a1e36] hover:!bg-[#071627]" isLoading={isSubmitting} size="lg" type="submit">
        {isSubmitting ? t('signingIn') : t('signIn')}
      </Button>
    </form>
  );
}
