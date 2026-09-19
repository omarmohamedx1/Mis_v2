import type { ReactNode } from 'react';

export interface PageHeaderProps {
  actions?: ReactNode;
  breadcrumbs?: ReactNode;
  className?: string;
  description?: ReactNode;
  eyebrow?: ReactNode;
  title: ReactNode;
}

export function PageHeader({ actions, breadcrumbs, className = '', description, eyebrow, title }: PageHeaderProps) {
  return (
    <header className={`mb-7 min-w-0 ${className}`}>
      {breadcrumbs ? <div className="mb-4 min-w-0 overflow-hidden">{breadcrumbs}</div> : null}
      <div className="flex min-w-0 flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div className="min-w-0 flex-1">
          {eyebrow ? <p className="text-sm font-semibold text-mis-primary [overflow-wrap:anywhere]">{eyebrow}</p> : null}
          <h1 className={`${eyebrow ? 'mt-2' : ''} break-words text-2xl font-bold leading-snug text-mis-navy sm:text-3xl`}>{title}</h1>
          {description ? <div className="mt-2 max-w-3xl text-sm leading-7 text-slate-500 [overflow-wrap:anywhere]">{description}</div> : null}
        </div>
        {actions ? <div className="flex w-full min-w-0 flex-col gap-2 [&>*]:w-full sm:w-auto sm:max-w-md sm:flex-row sm:flex-wrap sm:items-center sm:justify-end sm:gap-3 sm:[&>*]:w-auto">{actions}</div> : null}
      </div>
    </header>
  );
}
