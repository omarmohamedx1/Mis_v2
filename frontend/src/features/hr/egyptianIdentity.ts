export type NationalIdIssue = 'required' | 'length' | 'century' | 'birth' | 'future';

export interface ParsedEgyptianNationalId {
  nationalId: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female';
}

function toAsciiDigits(value: string): string {
  return Array.from(value, (character) => {
    const code = character.codePointAt(0) ?? 0;
    if (code >= 0x0660 && code <= 0x0669) return String(code - 0x0660);
    if (code >= 0x06f0 && code <= 0x06f9) return String(code - 0x06f0);
    return character;
  }).join('');
}

function isValidIsoDate(year: number, month: number, day: number): boolean {
  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day;
}

export function parseEgyptianNationalId(value: string | null | undefined): { ok: true; value: ParsedEgyptianNationalId } | { ok: false; error: NationalIdIssue } {
  const digits = toAsciiDigits(value ?? '').replace(/\D/g, '');
  if (!digits) return { ok: false, error: 'required' };
  if (digits.length !== 14) return { ok: false, error: 'length' };
  if (digits[0] !== '2' && digits[0] !== '3') return { ok: false, error: 'century' };

  const year = (digits[0] === '2' ? 1900 : 2000) + Number(digits.slice(1, 3));
  const month = Number(digits.slice(3, 5));
  const day = Number(digits.slice(5, 7));
  if (!isValidIsoDate(year, month, day)) return { ok: false, error: 'birth' };

  const dateOfBirth = `${String(year).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  if (dateOfBirth > new Date().toISOString().slice(0, 10)) return { ok: false, error: 'future' };

  return {
    ok: true,
    value: {
      nationalId: digits,
      dateOfBirth,
      gender: Number(digits[12]) % 2 === 0 ? 'Female' : 'Male',
    },
  };
}

export function normalizeEgyptianMobile(value: string | null | undefined): { ok: true; mobile: string | null } | { ok: false } {
  const raw = value?.trim() ?? '';
  if (!raw) return { ok: true, mobile: null };

  let normalized = toAsciiDigits(raw).replace(/[\s()-]/g, '');
  if (normalized.startsWith('0020')) normalized = `0${normalized.slice(4).replace(/^0+/, '')}`;
  else if (normalized.startsWith('+20')) normalized = `0${normalized.slice(3).replace(/^0+/, '')}`;
  else if (normalized.startsWith('20') && normalized.replace(/\D/g, '').length >= 12) normalized = `0${normalized.slice(2).replace(/^0+/, '')}`;
  normalized = normalized.replace(/\D/g, '');
  if (normalized.length === 10 && normalized.startsWith('1')) normalized = `0${normalized}`;
  if (!/^01[0125]\d{8}$/.test(normalized)) return { ok: false };
  return { ok: true, mobile: normalized };
}
