import type { ReactNode } from 'react';

/** Keep related page actions together instead of distributing them across a header. */
export function PageActions({ children }: { children: ReactNode }) {
  return <div className="flex min-w-0 max-w-full flex-wrap items-end justify-start gap-3 sm:max-w-xl sm:justify-end">{children}</div>;
}
