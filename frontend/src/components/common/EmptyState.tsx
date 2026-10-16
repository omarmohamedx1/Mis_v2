import type { ReactNode } from 'react';

export interface EmptyStateProps {
  action?: ReactNode;
  className?: string;
  compact?: boolean;
  description?: ReactNode;
  icon?: ReactNode;
  title: ReactNode;
}

export function EmptyState({ action, className = '', compact = false, description, icon, title }: EmptyStateProps) {
  return (
    <div className={`flex items-center justify-center p-6 text-center ${compact ? 'min-h-40' : 'min-h-64'} ${className}`}>
      <div className="min-w-0 max-w-md [overflow-wrap:anywhere]">
        {icon ? <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-mis-pale text-mis-primary">{icon}</div> : null}
        <h2 className={`${icon ? 'mt-4' : ''} font-bold text-mis-navy`}>{title}</h2>
        {description ? <div className="mt-2 text-sm leading-6 text-slate-500">{description}</div> : null}
        {action ? <div className="mt-5 flex justify-center">{action}</div> : null}
      </div>
    </div>
  );
}
