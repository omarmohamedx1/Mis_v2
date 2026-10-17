The interface keeps Arabic labels and right-to-left navigation while displaying
all formatted numbers with Latin digits (0–9). Arabic number, currency, date and
time formatters must use `ar-EG-u-nu-latn`; keep each formatter's existing precision,
currency, time zone and calendar options. Do not change API language headers.

Use `formatMoney` from `frontend/src/utils/formatMoney.ts` for currency values.
Arabic amounts read the number first, then the currency (`55 ج.م`): the number is
on the right and the currency on the left. Unicode isolation preserves this order
in both RTL text and Latin numeric cells, including negative amounts.
For an already formatted amount, use `formatCurrencyAmount` instead of concatenating
the currency directly. Keep number precision and the actual currency unchanged.
Compact amounts contain three separate runs: number, scale (thousand/million/
billion), then currency. Do not force the entire compact amount to LTR. For counts
or percentages followed by a translated label, use `formatNumberWithUnit`.

Collections summary tables use `collection-summary-table` and a readable minimum
width within a horizontal scroller. Keep numeric cells and short headers unbroken.
Use `collection-metric`/`collection-metric-value` for dashboard cards so amounts
scale to the card width and stay aligned with their label.

The application bundles Inter and Noto Sans Arabic under `frontend/src/assets/fonts`.
Use the shared `--font-interface` token or Tailwind's `font-sans`; avoid external
font stylesheets and per-page font overrides.

Tables use shared header/body insets and align cells according to the table's
writing direction. A Latin amount or identifier may use `data-bidi="ltr"` or a
`bdi` element without moving to the opposite edge of its column. Apply the same
`text-start`, `text-end` or `text-center` choice to a column's header and data cells.
Keep wide tables within a horizontal scrolling container.

The collections overview uses four concise indicators, actionable priorities,
one ranked client-portfolio table, and a deduplicated personal follow-up queue.
Load the overview and personal queue independently; a slow or failed queue must
not hide valid balances. Empty queues use compact messages. Tables scroll inside
their section; position the scroller so screen-reader-only headings stay inside
its clipping boundary. Avoid `overflow-wrap: anywhere` on the module shell.

Collections navigation groups performance, operations, client organizations,
and reports/control. Keep stable group IDs across translations, retain role
filters, and give icon-only links accessible names. The mobile drawer is inert
and removed from layout while closed; Escape returns focus to its opener.
