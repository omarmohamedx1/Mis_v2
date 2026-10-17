/** Keep a quantity's number, scale and unit in reading order in any surrounding text. */
export function formatNumberWithUnit(amount: string, unit: string, direction: 'rtl' | 'ltr' = /[\u0600-\u06ff]/.test(unit) ? 'rtl' : 'ltr'): string {
  const isolate = direction === 'rtl' ? '\u2067' : '\u2066';
  // A compact amount also contains a unit (for example "25.2 مليون").
  // Isolate only numeric runs as LTR; words must retain their own direction.
  const clean = amount.replace(/[\u061c\u200e\u200f\u2066-\u2069]/g, '');
  const number = /([+\-\u2212]?(?:[0-9]+(?:[.,][0-9]+)*|\u221e))/g;
  const parts = clean.split(number).filter(Boolean).map(part => {
    if (/^[+\-\u2212]?(?:[0-9]+(?:[.,][0-9]+)*|\u221e)$/.test(part)) return `\u2066${part}\u2069`;
    // Keep spaces outside the word's isolate so they cannot move before it.
    return part.split(/(\s+)/).filter(Boolean).map(piece => /^\s+$/.test(piece) ? '\u00a0' : `\u2068${piece}\u2069`).join('');
  }).join('');
  return `${isolate}${parts}\u00a0\u2068${unit}\u2069\u2069`;
}

export function formatCurrencyAmount(amount: string, currency: string, direction?: 'rtl' | 'ltr'): string {
  return formatNumberWithUnit(amount, currency, direction);
}

export function formatMoney(value: number, locale: string, options: Intl.NumberFormatOptions = {}): string {
  const currency = options.currency ?? 'EGP';
  const formatter = new Intl.NumberFormat(locale, { ...options, style: 'currency', currency, numberingSystem: 'latn' });
  if (!/^ar(?:-|$)/i.test(locale)) return formatter.format(value);

  const parts = formatter.formatToParts(value);
  const amount = parts.filter(part => part.type !== 'currency').map(part => part.value).join('')
    .replace(/[\u061c\u200e\u200f]/g, '').trim();
  const label = currency.toUpperCase() === 'EGP' ? 'ج.م' : parts.find(part => part.type === 'currency')?.value ?? currency;
  return formatCurrencyAmount(amount, label, 'rtl');
}
