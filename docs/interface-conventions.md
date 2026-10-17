The interface keeps Arabic labels and right-to-left navigation while displaying
all formatted numbers with Latin digits (0–9). Arabic number, currency, date and
time formatters must use `ar-EG-u-nu-latn`; keep each formatter's existing precision,
currency, time zone and calendar options. Do not change API language headers.

The application bundles Inter and Noto Sans Arabic under `frontend/src/assets/fonts`.
Use the shared `--font-interface` token or Tailwind's `font-sans`; avoid external
font stylesheets and per-page font overrides.

Tables use shared header/body insets and align cells according to the table's
writing direction. A Latin amount or identifier may use `data-bidi="ltr"` or a
`bdi` element without moving to the opposite edge of its column. Apply the same
`text-start`, `text-end` or `text-center` choice to a column's header and data cells.
Keep wide tables within a horizontal scrolling container.
