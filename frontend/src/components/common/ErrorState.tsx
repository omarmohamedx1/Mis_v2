import { AlertTriangle, RefreshCw } from 'lucide-react';
import type { ReactNode } from 'react';
import { useLocalization } from '../../context/LocalizationContext';
import { Button } from './Button';

export interface ErrorStateProps {
  className?: string;
  compact?: boolean;
  description?: ReactNode;
  isRetrying?: boolean;
  message?: ReactNode;
  onRetry?: () => void;
  retryLabel?: ReactNode;
  title: ReactNode;
}

export function ErrorState({ className = '', compact = false, description, isRetrying = false, message, onRetry, retryLabel, title }: ErrorStateProps) {
  const { t } = useLocalization();
  const content = message ?? description;
  return (
    <div className={`flex items-center justify-center rounded-2xl border border-red-200 bg-white p-6 text-center ${compact ? 'min-h-40' : 'min-h-64'} ${className}`} role="alert">
      <div className="min-w-0 max-w-md [overflow-wrap:anywhere]">
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-red-50 text-red-600">
          <AlertTriangle className="h-6 w-6" aria-hidden="true" />
        </div>
        <h2 className="mt-4 font-bold text-mis-navy">{title}</h2>
        {content ? <div className="mt-2 text-sm leading-6 text-slate-500">{content}</div> : null}
        {onRetry ? (
          <div className="mt-5 flex justify-center">
            <Button fullWidth={false} isLoading={isRetrying} leftIcon={<RefreshCw className="h-4 w-4" aria-hidden="true" />} onClick={onRetry} size="md" type="button">
              {retryLabel ?? t('tryAgain')}
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  );
}
