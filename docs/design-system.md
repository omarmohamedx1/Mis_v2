# MIS visual system maintenance

This document describes the existing MIS visual identity and the shared fixes from the October 2026 visual audit. It does not introduce a new theme.

## Tokens and density

| Token or scale | Value | Use |
| --- | --- | --- |
| `--control-height` | `2.75rem` / 44px at the default font size | Compact filters and default buttons |
| `--form-control-height` | `3rem` / 48px | Labelled form inputs |
| Button `sm` | Minimum 36px | Secondary compact actions |
| Button `icon` | 44 × 44px | Standalone icon actions |
| Calendar header controls | 40px | Compact calendar navigation |
| `--radius-control` | `0.75rem` | Compact controls |
| `--radius-form` | `0.875rem` | Labelled form controls |
| `--color-muted` | `#596b82` | Readable secondary text on light surfaces |
| `--color-placeholder` | Muted token | Input placeholders |
| `--color-control-border` | `#7b8da6` | Interactive control boundaries; keep decorative card borders light |
| `--color-focus` | `#0b638f` | Keyboard outline and native checkbox/radio accent |
| `--color-danger` | Red 500 / `#ef4444` | Error border, retained on focus and accompanied by a message |
| `--z-dialog` | 50 | Dialog frames |
| `--z-popover` | 100 | Calendar/time popups and select backdrops |
| `--z-select` | 110 | Nested option lists |
| `--z-toast` | 120 | Notifications |
| `--safe-top/bottom/left/right` | Browser safe-area environment values | Dialog, floating-panel, navigation and toast placement |
| Spacing, type and breakpoints | Existing Tailwind scales | Do not add arbitrary page-specific scales |

Tokens live in `frontend/src/styles.css`; Tailwind exposes the semantic colors through `frontend/tailwind.config.ts`. The navy/blue palette, IBM Plex fonts, card shapes and overall product identity remain in use. Some explicit utility densities remain for compatibility; keep them aligned with this table when extending a component.

## Controls

Use `TextInput`, `PasswordInput`, `SelectInput`, `DateInput`, `TimeInput`, `FileInput`, `TextAreaInput` and `Checkbox` for labelled forms. Use `DateControl` and `ProfessionalSelect` for compact filters. Do not add another visible native select or browser date dropdown.

The hidden native select preserves the existing form contract. Its visible trigger owns the label, focus ring, validation state and dropdown. Date/time wrappers forward refs to the visible field. Keep IDs unique and connect error/hint text with `aria-describedby`.

`Button`, select triggers and form primitives resolve competing utility classes with `tailwind-merge` 2.6, which matches the project's Tailwind 3 version. Appending conflicting classes alone is insufficient: CSS generation order can override the intended class. Put container width, margin and flex/grid placement on the select wrapper; put padding, typography, borders and density on its trigger.

Loading buttons retain their original label and icon geometry, hide that content visually, and place the spinner above it. Keep the accessible label available. Icon-only actions must have a meaningful localized `aria-label` or equivalent accessible name.

## Dialogs and floating content

Use `Modal` and `ConfirmDialog`. Do not implement another interactive `fixed inset-0` dialog. The shared frame owns Escape, background scroll locking, focus containment, focus return, viewport height and safe-area padding.

`hideHeader` supports existing domain forms that already render a header; it still supplies a screen-reader title. Preserve the form's existing handlers. Normally the body scrolls while header/footer remain visible. When long header/footer content would consume the viewport, the measured frame scrolls as a whole so the body and actions remain reachable.

Portals must retain their logical owner through `OverlayScope` and `data-overlay-owners`. The top overlay alone handles Escape and keyboard focus. A child select must close without closing the calendar or dialog below it. Scroll locks are reference-counted: closing one child must not unlock another open dialog.

Use `useFloatingPanel` for measured popups and `viewportBounds` for specialized placement. Both account for the visual viewport, scrolling, resizing and safe areas. Physical viewport coordinates are intentional here; align to the anchor's direction before clamping.

## Text and layout containment

Flexible children need `min-inline-size: 0`. Use `.ds-readable` for ordinary detail content and `.ds-metric-value` for large monetary values. Long identifiers and emails may wrap anywhere. Do not use `overflow: hidden` to conceal an unresolved layout problem.

Truncation is intentional only when the full value remains available through a detail view or title. Select options show their full text. Keep icons and clear/calendar actions from shrinking; reserve their space beside the value. Narrow date/time fields place the value on its own row.

Use `PageActions` to group related header actions. Keep the group beside the title on wide layouts and allow it to wrap below on narrow layouts. Do not distribute individual actions with `justify-between` across an entire page header.

Use `ActionMenu` for groups of record actions in tables and cards. It renders a 44px three-dot trigger, localized text with icons, and a viewport-aware portal. Keep permission conditions, disabled states and handlers on the existing buttons/links inside it. Icon-only actions need a localized `title` or `aria-label` for their visible menu text. Stateful action components must expose a menu-item trigger (`AccountingDeleteButton` uses `menuItem`); their dialogs stay mounted when the menu closes. Page creation buttons and form submit/cancel controls remain visible.

Wrap wide tables in `.table-scroll` or an explicit horizontal scroller. A table minimum width is acceptable inside that wrapper. Page-wide horizontal scrolling is not. Check header cells, empty bodies, one row, many rows and large numbers.

## RTL and localization

Use logical spacing and alignment (`start/end`, `ps/pe`, `ms/me`, `text-start/end`). Avoid global rules that reinterpret every left/right utility. A component nested inside an explicit `dir="ltr"` region must follow its own computed direction.

Rotate navigation arrows only where the action is directional; disclosure arrows point down when expanded in either language. Keep dates, reference IDs, emails and amounts readable with local `dir="ltr"` or bidi isolation where appropriate.

The calendar uses `ar-EG` or `en-GB` month/day labels and locale week starts. Its themed year search normalizes Arabic and Persian digits for matching. Changing the viewed year/month does not itself change the selected date. Existing min/max rules remain in force.

## Regression gate

Run the development-only lab and route sweep described in `frontend/qa/README.md` after changes to shared primitives, tokens or overlays. Cover English and Arabic at 320, 360, 390, 768, 1024 and 1440px, plus a short 320 × 480 viewport.

Verify hover, keyboard focus, selected/disabled/error/loading states, long content, nested overlays, safe-area simulation and resize while navigation is open. Review screenshots as well as geometry; an element can be inside the viewport while its own text is clipped.

The QA entry, synthetic fixtures and mutation-blocking interceptors must never be imported by the production entry. Browser emulation is not a substitute for real iOS/Android or Safari/Firefox verification.
