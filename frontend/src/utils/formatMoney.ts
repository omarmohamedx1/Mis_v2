/** Read the number first, then its currency, independently of the surrounding direction. */
export function formatCurrencyAmount(amount: string, currency: string, direction: 'rtl' | 'ltr' = /[\u0600-\u06ff]/.test(currency) ? 'rtl' : 'ltr'): string {
  const isolate = direction === 'rtl' ? '\u2067' : '\u2066';
  return `${isolate}\u2066${amount}\u2069\u00a0\u2068${currency}\u2069\u2069`;
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
