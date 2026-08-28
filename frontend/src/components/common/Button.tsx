import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { LoadingSpinner } from './LoadingSpinner';

export type ButtonVariant = 'primary' | 'secondary' | 'outline' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg' | 'icon';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  children: ReactNode;
  fullWidth?: boolean;
  isLoading?: boolean;
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
  size?: ButtonSize;
  variant?: ButtonVariant;
}

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-mis-primary text-white shadow-sm hover:bg-mis-deep disabled:bg-slate-300 disabled:text-slate-600',
  secondary: 'border border-mis-sky/60 bg-mis-pale text-mis-deep shadow-sm hover:border-mis-primary hover:bg-white disabled:bg-slate-100 disabled:text-slate-400',
  outline: 'border border-slate-300 bg-white text-slate-800 shadow-sm hover:border-mis-primary hover:bg-mis-pale/40 hover:text-mis-deep disabled:bg-slate-50 disabled:text-slate-400',
  ghost: 'bg-transparent text-slate-600 hover:bg-slate-100 hover:text-mis-primary disabled:text-slate-400',
  danger: 'bg-red-600 text-white shadow-sm hover:bg-red-700 disabled:bg-red-200 disabled:text-red-500',
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: 'h-9 rounded-lg px-3 text-xs',
  md: 'h-10 rounded-xl px-4 text-sm',
  lg: 'h-12 rounded-form px-4 text-sm',
  icon: 'h-10 w-10 rounded-xl p-0 text-sm',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    children,
    className = '',
    disabled,
    fullWidth = true,
    isLoading = false,
    leftIcon,
    rightIcon,
    size = 'md',
    variant = 'primary',
    ...props
  },
  ref,
) {
  const widthClass = size !== 'icon' && fullWidth ? 'w-full' : '';

  return (
    <button
      aria-busy={isLoading || undefined}
      className={`inline-flex items-center justify-center gap-2 font-bold transition duration-150 disabled:cursor-not-allowed ${sizeClasses[size]} ${variantClasses[variant]} ${widthClass} ${className}`}
      disabled={disabled || isLoading}
      ref={ref}
      {...props}
    >
      {isLoading ? <LoadingSpinner className="h-4 w-4" /> : leftIcon}
      <span>{children}</span>
      {!isLoading && rightIcon}
    </button>
  );
});
