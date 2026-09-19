export interface HrPagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/**
 * HR screens are operational workspaces, so they load the complete filtered
 * result instead of making the user move between numbered pages.
 */
export async function loadAllHrPages<T, TPage extends HrPagedResult<T>>(
  loadPage: (page: number, pageSize: number) => Promise<TPage>,
  pageSize = 100,
): Promise<TPage> {
  const first = await loadPage(1, pageSize);
  if (first.totalPages <= 1) {
    return { ...first, page: 1, totalPages: first.totalCount > 0 ? 1 : 0 };
  }

  const items = [...first.items];
  const concurrency = 4;
  for (let start = 2; start <= first.totalPages; start += concurrency) {
    const pages = Array.from(
      { length: Math.min(concurrency, first.totalPages - start + 1) },
      (_, index) => start + index,
    );
    const results = await Promise.all(pages.map((page) => loadPage(page, pageSize)));
    results.forEach((result) => items.push(...result.items));
  }

  return {
    ...first,
    items,
    page: 1,
    pageSize: Math.max(items.length, 1),
    totalCount: first.totalCount,
    totalPages: first.totalCount > 0 ? 1 : 0,
  };
}
