import { useId, type HTMLAttributes, type ReactNode } from 'react';

export interface SectionProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  action?: ReactNode;
  bodyClassName?: string;
  children: ReactNode;
  description?: ReactNode;
  title: ReactNode;
}

export function Section({ action, bodyClassName = 'p-5 sm:p-6', children, className = '', description, title, ...props }: SectionProps) {
  const titleId = useId();

  return (
    <section aria-labelledby={titleId} className={`overflow-hidden rounded-2xl border border-mis-border bg-white shadow-sm ${className}`} {...props}>
      <header className="flex min-h-16 items-start justify-between gap-4 border-b border-mis-border px-5 py-4 sm:px-6">
        <div className="min-w-0">
          <h2 className="font-bold leading-snug text-mis-navy" id={titleId}>{title}</h2>
          {description ? <div className="mt-1 text-sm leading-6 text-slate-500">{description}</div> : null}
        </div>
        {action ? <div className="flex-none">{action}</div> : null}
      </header>
      <div className={bodyClassName}>{children}</div>
    </section>
  );
}
