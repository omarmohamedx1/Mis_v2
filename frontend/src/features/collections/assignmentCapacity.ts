/** Smallest resulting workload limit that can accommodate this batch. */
export function suggestedAssignmentCapacity(caseCount: number, workloads: readonly number[]): number {
  if (!workloads.length) return Math.max(1, caseCount);
  let low = 1;
  let high = Math.max(...workloads) + Math.max(0, caseCount);
  while (low < high) {
    const limit = Math.floor((low + high) / 2);
    const available = workloads.reduce((total, workload) => total + Math.max(0, limit - workload), 0);
    if (available >= caseCount) high = limit;
    else low = limit + 1;
  }
  return low;
}
