import type { HTMLAttributes, ReactNode } from 'react';

export type CardPadding = 'none' | 'sm' | 'md' | 'lg';

export interface CardProps extends HTMLAttributes<HTMLDivElement> {
  children: ReactNode;
  padding?: CardPadding;
}

const paddingClasses: Record<CardPadding, string> = {
  none: '',
  sm: 'p-4',
  md: 'p-5',
  lg: 'p-6',
};

export function Card({ children, className = '', padding = 'md', ...props }: CardProps) {
  return (
    <div className={`min-w-0 rounded-2xl border border-mis-border bg-white shadow-sm ${paddingClasses[padding]} ${className}`} {...props}>
      {children}
    </div>
  );
}
