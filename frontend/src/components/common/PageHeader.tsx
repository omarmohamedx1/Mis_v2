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
    <header className={`mb-7 ${className}`}>
      {breadcrumbs ? <div className="mb-4">{breadcrumbs}</div> : null}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div className="min-w-0">
          {eyebrow ? <p className="text-sm font-semibold text-mis-primary">{eyebrow}</p> : null}
          <h1 className={`${eyebrow ? 'mt-2' : ''} break-words text-2xl font-bold leading-tight text-mis-navy sm:text-3xl`}>{title}</h1>
          {description ? <div className="mt-2 text-sm leading-6 text-slate-500">{description}</div> : null}
        </div>
        {actions ? <div className="flex w-full flex-col gap-2 [&>*]:w-full sm:w-auto sm:flex-row sm:flex-wrap sm:items-center sm:justify-end sm:gap-3 sm:[&>*]:w-auto">{actions}</div> : null}
      </div>
    </header>
  );
}
