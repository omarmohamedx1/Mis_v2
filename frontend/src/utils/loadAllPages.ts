export type PagedLike<T> = {
  items: T[];
  page?: number;
  pageSize?: number;
  totalCount?: number;
  total?: number;
  totalPages?: number;
};

/**
 * Operational workspaces should show the full filtered result instead of
 * forcing users to click through numbered pages.
 */
export async function loadAllPages<T, TPage extends PagedLike<T>>(
  loadPage: (page: number, pageSize: number) => Promise<TPage>,
  pageSize = 100,
): Promise<TPage> {
  const first = await loadPage(1, pageSize);
  const totalCount = first.totalCount ?? first.total ?? first.items.length;
  const totalPages = first.totalPages ?? Math.max(totalCount === 0 ? 0 : 1, Math.ceil(totalCount / Math.max(pageSize, 1)));
  const finish = (items: T[]): TPage => ({
    ...first,
    items,
    page: 1,
    pageSize: Math.max(items.length, 1),
    totalCount,
    total: totalCount,
    totalPages: totalCount > 0 ? 1 : 0,
  });

  if (totalPages <= 1) return finish(first.items);

  const items = [...first.items];
  const concurrency = 4;
  for (let start = 2; start <= totalPages; start += concurrency) {
    const pages = Array.from({ length: Math.min(concurrency, totalPages - start + 1) }, (_, index) => start + index);
    const results = await Promise.all(pages.map((page) => loadPage(page, pageSize)));
    results.forEach((result) => items.push(...result.items));
  }

  return finish(items);
}

export function withoutPaging(values: Record<string, unknown> = {}) {
  const { page: _page, pageSize: _pageSize, ...rest } = values;
  return rest;
}
