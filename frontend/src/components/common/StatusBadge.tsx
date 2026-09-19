import type { HTMLAttributes, ReactNode } from 'react';

export type StatusTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger' | 'purple';
export type StatusBadgeSize = 'sm' | 'md';

export interface StatusBadgeProps extends HTMLAttributes<HTMLSpanElement> {
  children: ReactNode;
  dot?: boolean;
  size?: StatusBadgeSize;
  tone?: StatusTone;
}

const toneClasses: Record<StatusTone, string> = {
  neutral: 'bg-slate-100 text-slate-600',
  info: 'bg-mis-pale text-mis-deep',
  success: 'bg-emerald-50 text-emerald-700',
  warning: 'bg-amber-50 text-amber-700',
  danger: 'bg-red-50 text-red-700',
  purple: 'bg-violet-50 text-violet-700',
};

const dotClasses: Record<StatusTone, string> = {
  neutral: 'bg-slate-400',
  info: 'bg-mis-blue',
  success: 'bg-emerald-500',
  warning: 'bg-amber-500',
  danger: 'bg-red-500',
  purple: 'bg-violet-500',
};

export function StatusBadge({ children, className = '', dot = false, size = 'sm', tone = 'neutral', ...props }: StatusBadgeProps) {
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-full font-semibold leading-snug ${size === 'sm' ? 'px-2.5 py-1 text-xs' : 'px-3 py-1.5 text-sm'} ${toneClasses[tone]} ${className}`} {...props}>
      {dot ? <span className={`h-1.5 w-1.5 rounded-full ${dotClasses[tone]}`} aria-hidden="true" /> : null}
      {children}
    </span>
  );
}
