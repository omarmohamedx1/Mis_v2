import type { LegalClientDetails } from './types/legal';

export class LegalClientResponseError extends Error {}

// A list row, HTML fallback, or another customer's response must never populate an edit form.
export function requireLegalClientDetails(value: unknown, customerId: string): LegalClientDetails {
  const details = value as LegalClientDetails | null;
  if (!details || typeof details !== 'object'
    || typeof details.client?.customerId !== 'string'
    || details.client.customerId.toLowerCase() !== customerId.toLowerCase()
    || typeof details.client.name !== 'string'
    || typeof details.client.status !== 'string'
    || typeof details.canManage !== 'boolean'
    || (details.notes != null && typeof details.notes !== 'string')) {
    throw new LegalClientResponseError('The Legal client details response does not match the requested client contract.');
  }
  return details;
}
