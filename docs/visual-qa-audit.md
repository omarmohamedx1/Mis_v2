# MIS visual QA and design-system audit

Audit completed on 17 October 2026. This register groups repeated defects by their shared root cause. All 33 registered fixes are implemented in the frontend; the evidence section records the actual verification scope. Universal freedom from visual defects is not claimed.

## Result and scope

| Check | Recorded result |
| --- | --- |
| Source inventory | 138 route declarations; 121 distinct reachable non-wildcard route patterns; 167 TSX files, including 92 page files |
| Route patterns exercised | 74 / 121 |
| Route / language / viewport samples | 876 |
| Widths and directions | 320, 360, 390, 768, 1024, 1440px; English/LTR and Arabic/RTL |
| Short viewport | 320 x 480px; calendars, time pickers and long dialogs |
| Screen geometry failures | 0 |
| Samples with captured runtime errors | 0 |
| Dialog overlay samples / out-of-bounds boxes | 178 / 0 |
| Overlay opener automation errors | 2 |
| Shared component assertions | 52 standard + 76 extended |
| Synthetic lab geometry samples with overflow | 0 |
| Routes with unnamed accessible actions | 0 |
| Critical defects observed | 0 |

The application ran locally against authenticated read-only GET data. A browser interceptor and a separate local proxy blocked business-data writes. No financial records were created/deleted, no imports were submitted, and no permissions, API contracts, route destinations or business validation rules were changed for this audit. The route file changed only the loading fallback's viewport-height utility.

Live data supplied representative populated and empty screens. Missing journal, client, legal, HR visit and organization-customer details used explicit synthetic GET fixtures. Forced-password coverage changed only the browser test session flag. Each dynamic route uses one representative record/organization; this is not an exhaustive record or permission-combination test.

Chrome 155 on Windows was tested using controlled viewport sizes, including the actual IBM Plex fonts. Safe-area values were simulated. Safari/Firefox, physical mobile devices, touch keyboards, OS high-contrast modes and a full screen-reader audit were not verified. The PASS results below apply to the stated environment, not to those untested environments.

## Prioritized fix plan

| Priority | Completed work | Why first |
| --- | --- | --- |
| P0 | Viewport containment, financial-value visibility, table access, nested overlay ownership/focus/scroll locks | Prevent inaccessible fields/actions and hidden data |
| P1 | Shared class resolution, field/error/disabled states, calendar layout/localization, navigation behavior | Correct defects across many callers at once |
| P2 | Header action grouping, long-content feedback/import components, readable tokens and action names | Improve consistency and resilience without a redesign |
| Release gate | Production build, route/lab/state regression and artifact isolation | Confirm shared changes do not introduce sibling regressions |

## Defect register

Every entry includes the implemented fix, its intended location and the regression surface. “Retest” identifies the required sibling coverage; the evidence and route appendix show what was exercised in this audit.

### VQA-01 — Application shell at 320px

1. **Severity:** High
2. **Screen/component:** Application shell at 320px
3. **Exact visual problem:** RTL content could be cropped by the desktop scrollbar.
4. **Reproduction steps:** Open a scrolling page at 320px in Arabic.
5. **Root cause / classification:** Responsive breakpoint: a 320px minimum on html/body exceeded the available content width.
6. **Recommended fix and implementation:** Removed the root minimum width; retained shrink-safe containers and dynamic viewport height.
7. **Where the fix lives:** token / page shell
8. **Regression risk:** Medium: all layouts inherit the root sizing.
9. **Components/screens to retest:** Every route, both directions, all six widths.

### VQA-02 — Button and form primitives

1. **Severity:** Medium
2. **Screen/component:** Button and form primitives
3. **Exact visual problem:** Caller padding, color, width or density could lose to default classes.
4. **Reproduction steps:** Pass compact h-10/min-h-10/px-2 classes to a select or a custom button color.
5. **Root cause / classification:** Shared primitive: competing Tailwind classes were concatenated without resolving conflicts.
6. **Recommended fix and implementation:** Added Tailwind 3-compatible tailwind-merge 2.6 to the affected shared primitives.
7. **Where the fix lives:** primitive
8. **Regression risk:** Medium: intentional caller overrides now consistently win.
9. **Components/screens to retest:** All Button variants, labelled fields, calendar month/year/time controls and callers.

### VQA-03 — Button loading state

1. **Severity:** Medium
2. **Screen/component:** Button loading state
3. **Exact visual problem:** An intrinsically sized action could change width when icons were replaced by loading content.
4. **Reproduction steps:** Use a content-sized button with and without an icon; toggle loading.
5. **Root cause / classification:** Shared primitive: the spinner changed the inline content geometry.
6. **Recommended fix and implementation:** Retained the original content invisibly and positioned the spinner above it.
7. **Where the fix lives:** primitive
8. **Regression risk:** Low: loading visuals and accessible busy state.
9. **Components/screens to retest:** All variants; icons on either side; narrow and wide layouts.

### VQA-04 — Standalone icon Button

1. **Severity:** Medium
2. **Screen/component:** Standalone icon Button
3. **Exact visual problem:** The icon target could become rectangular rather than the intended square.
4. **Reproduction steps:** Render size=icon with default fullWidth behavior.
5. **Root cause / classification:** Shared primitive: generic width classes competed with icon dimensions.
6. **Recommended fix and implementation:** Kept the icon size independent of generic full/auto width; standardized its 44px square target.
7. **Where the fix lives:** primitive
8. **Regression risk:** Low.
9. **Components/screens to retest:** Icon actions, disabled/loading buttons and both languages.

### VQA-05 — ProfessionalSelect layout wrapper

1. **Severity:** Medium
2. **Screen/component:** ProfessionalSelect layout wrapper
3. **Exact visual problem:** A narrow select could occupy its whole grid cell despite an explicit width.
4. **Reproduction steps:** Render a select with w-24 in a flexible grid.
5. **Root cause / classification:** Shared component: width and flex/grid placement were attached to the trigger instead of its wrapper.
6. **Recommended fix and implementation:** Separated layout utilities onto the wrapper and visual utilities onto the trigger.
7. **Where the fix lives:** shared component
8. **Regression risk:** Medium: affects filters and calendar subcontrols.
9. **Components/screens to retest:** Labelled selects, narrow filters, month/year/hour/minute controls, modal selects.

### VQA-06 — Date/time composite values

1. **Severity:** High
2. **Screen/component:** Date/time composite values
3. **Exact visual problem:** Long datetime values could be squeezed or clipped between calendar and clear actions.
4. **Reproduction steps:** Show a selected datetime with a clear action in a narrow form column.
5. **Root cause / classification:** Shared primitive / content edge case: value, icons and actions competed in one inflexible row.
6. **Recommended fix and implementation:** Reserved action space, allowed the value to shrink, and moved datetime text to its own row in narrow containers.
7. **Where the fix lives:** primitive
8. **Regression risk:** Medium: layout changes only at constrained field widths.
9. **Components/screens to retest:** Date, datetime and time; empty/selected/disabled/error; 320px and modal grids.

### VQA-07 — Calendar month/year header

1. **Severity:** Medium
2. **Screen/component:** Calendar month/year header
3. **Exact visual problem:** Full month names were ellipsized, including December at 320px.
4. **Reproduction steps:** Open the December calendar in a 320px viewport.
5. **Root cause / classification:** Shared component / responsive breakpoint: navigation, month and year shared one crowded row.
6. **Recommended fix and implementation:** Used a shrink-safe header grid with the year below the month when the panel is narrow.
7. **Where the fix lives:** shared component
8. **Regression risk:** Low: header layout only.
9. **Components/screens to retest:** All month names, both languages, compact and desktop calendars.

### VQA-08 — Calendar, time picker and client filters

1. **Severity:** Medium
2. **Screen/component:** Calendar, time picker and client filters
3. **Exact visual problem:** Native dropdown UI and the year datalist bypassed the themed controls.
4. **Reproduction steps:** Open month/hour/minute/year or the client filter dropdowns.
5. **Root cause / classification:** Browser default behavior: parallel native and custom control implementations.
6. **Recommended fix and implementation:** Reused ProfessionalSelect, including searchable themed years; retained only the hidden native form select.
7. **Where the fix lives:** primitive / page callers
8. **Regression risk:** Medium: keyboard and focus behavior must match the shared select.
9. **Components/screens to retest:** All date/time submenus, client filters, keyboard selection and disabled options.

### VQA-09 — Floating selects, calendars, time and employee search

1. **Severity:** High
2. **Screen/component:** Floating selects, calendars, time and employee search
3. **Exact visual problem:** Popups could exceed short viewports or ignore mobile safe areas.
4. **Reproduction steps:** Open near the bottom at 320x480; resize; simulate top/bottom/side safe insets.
5. **Root cause / classification:** Shared component / responsive breakpoint: placement assumed fixed space and did not consistently measure the panel or visual viewport.
6. **Recommended fix and implementation:** Shared viewport bounds, measured panel placement, clamping, resize/scroll observation and safe-area tokens.
7. **Where the fix lives:** shared component / token
8. **Regression risk:** Medium: affects positioning and opening above/below anchors.
9. **Components/screens to retest:** All popup families, nested dialogs, scrolling parents and simulated safe areas.

### VQA-10 — Nested overlays

1. **Severity:** High
2. **Screen/component:** Nested overlays
3. **Exact visual problem:** Choosing a child option or pressing Escape could dismiss the calendar/dialog beneath it.
4. **Reproduction steps:** Open a calendar inside a dialog, then its month dropdown; select or press Escape.
5. **Root cause / classification:** Shared component: portalled DOM appeared outside its logical owner and independent Escape handlers competed.
6. **Recommended fix and implementation:** Introduced overlay ownership through portals and a stack that dismisses only its top layer.
7. **Where the fix lives:** shared component
8. **Regression risk:** High: shared dismissal infrastructure.
9. **Components/screens to retest:** Modal + select/calendar/time/employee combinations; outside click; nested confirmations.

### VQA-11 — Dialog, calendar and time keyboard boundaries

1. **Severity:** High
2. **Screen/component:** Dialog, calendar and time keyboard boundaries
3. **Exact visual problem:** Focus could leave an active popup; independent scroll locks could unlock a parent dialog.
4. **Reproduction steps:** Tab past the final calendar/time action; open and close nested dialogs.
5. **Root cause / classification:** Accessibility state / shared component: incomplete focus boundaries and independent body-lock ownership.
6. **Recommended fix and implementation:** Shared focus containment/return and reference-counted scroll locking, including date/time popups and migrated custom dialogs.
7. **Where the fix lives:** shared component
8. **Regression risk:** High: keyboard behavior across overlay families.
9. **Components/screens to retest:** Forward/reverse Tab, child Escape, focus return, nested scroll locking, all dialog callers.

### VQA-12 — EmployeeSearchSelect in scrolling forms

1. **Severity:** High
2. **Screen/component:** EmployeeSearchSelect in scrolling forms
3. **Exact visual problem:** Results were clipped by the form body and long options were difficult to read.
4. **Reproduction steps:** Open employee search near the bottom of a scrolling dialog.
5. **Root cause / classification:** Shared component: the list was absolutely positioned inside a clipping parent.
6. **Recommended fix and implementation:** Portalled the owned list, clamped it to the viewport and allowed full option text to wrap.
7. **Where the fix lives:** shared component
8. **Regression risk:** Medium.
9. **Components/screens to retest:** Employee selectors in HR/accounting, modal scrolling, long names and departments.

### VQA-13 — EmployeeSearchSelect interaction states

1. **Severity:** Medium
2. **Screen/component:** EmployeeSearchSelect interaction states
3. **Exact visual problem:** After Escape, clicking the already-focused input did not reopen results; disabled styling and focus return were inconsistent.
4. **Reproduction steps:** Open results, press Escape, click the input again; select an option; disable the field.
5. **Root cause / classification:** Accessibility state / shared component: opening relied on focus events alone, and the component did not share sibling disabled treatments.
6. **Recommended fix and implementation:** Added click reopening, guarded focus restoration on selection/Escape, disabled styling/actions and an associated error message.
7. **Where the fix lives:** shared component
8. **Regression risk:** Medium: focus events and option selection.
9. **Components/screens to retest:** Keyboard option selection, click reopening, disabled/error/loading/empty states and sibling fields.

### VQA-14 — Modal with long Arabic header at short heights

1. **Severity:** High
2. **Screen/component:** Modal with long Arabic header at short heights
3. **Exact visual problem:** The header/footer could consume the frame and collapse the form body.
4. **Reproduction steps:** Open the hostile-content modal in Arabic at 320x480.
5. **Root cause / classification:** Shared component / content edge case: non-shrinking chrome consumed the available body height.
6. **Recommended fix and implementation:** Measured header/footer space and switched to whole-frame scrolling when a usable body row would not fit.
7. **Where the fix lives:** shared component
8. **Regression risk:** Medium: changes which region scrolls in constrained dialogs.
9. **Components/screens to retest:** Long titles/descriptions/actions, confirmations, nested dialogs and legacy custom headers.

### VQA-15 — Mobile module navigation

1. **Severity:** High
2. **Screen/component:** Mobile module navigation
3. **Exact visual problem:** Offscreen navigation remained keyboard reachable; resizing an open drawer could leave background scrolling locked.
4. **Reproduction steps:** Tab with the drawer closed; open it, then resize from 390 to 1024px.
5. **Root cause / classification:** Accessibility state / responsive breakpoint: hidden-by-transform content remained interactive and lock state ignored desktop mode.
6. **Recommended fix and implementation:** Made the closed mobile drawer inert, added a focus boundary/return and released mobile-only locks on desktop.
7. **Where the fix lives:** shared component
8. **Regression risk:** Medium: all module navigation.
9. **Components/screens to retest:** All module shells, both directions, Escape/backdrop, resize and navigation focus.

### VQA-16 — Global RTL style overrides

1. **Severity:** High
2. **Screen/component:** Global RTL style overrides
3. **Exact visual problem:** Physical left/right utility overrides could affect deliberately LTR technical content and input icon spacing.
4. **Reproduction steps:** Switch Arabic on a mixed-direction form/table with a local LTR region.
5. **Root cause / classification:** RTL/localization: broad CSS attempted to reinterpret direction utilities globally.
6. **Recommended fix and implementation:** Removed stale directional overrides and retained logical spacing/alignment and explicit local direction.
7. **Where the fix lives:** token / shared styles
8. **Regression risk:** Medium: global selectors affected many callers.
9. **Components/screens to retest:** Mixed-language forms, numbers/emails, selects, date actions, tables and all shells.

### VQA-17 — Audit disclosure and shared Tabs

1. **Severity:** Medium
2. **Screen/component:** Audit disclosure and shared Tabs
3. **Exact visual problem:** An expanded audit arrow pointed incorrectly in RTL; nested LTR tabs followed the page direction.
4. **Reproduction steps:** Expand an HR audit item in Arabic; navigate LTR-nested tabs with arrow keys.
5. **Root cause / classification:** RTL/localization: directional state used a page-level assumption.
6. **Recommended fix and implementation:** Kept expanded disclosure pointing down and used the tab component computed direction for keyboard movement.
7. **Where the fix lives:** shared component / page
8. **Regression risk:** Low.
9. **Components/screens to retest:** HR audit, horizontal/vertical tabs, disabled tabs, local LTR inside Arabic.

### VQA-18 — Calendar localization and bounded states

1. **Severity:** Medium
2. **Screen/component:** Calendar localization and bounded states
3. **Exact visual problem:** Week starts and year search did not consistently match the active locale; Today could appear usable outside date bounds.
4. **Reproduction steps:** Compare Arabic/English weeks; search a year using Arabic digits; open a date range that excludes today.
5. **Root cause / classification:** RTL/localization / accessibility state: calendar metadata and the existing bounds were not consistently reflected in UI controls.
6. **Recommended fix and implementation:** Used locale week information, normalized localized search digits, labelled selected/today states and applied existing bounds to Today.
7. **Where the fix lives:** shared component
8. **Regression risk:** Medium: calendar presentation and keyboard/search state.
9. **Components/screens to retest:** ar-EG Saturday/en-GB Monday, 42 cells, selected/today/disabled states, typed dates and year changes.

### VQA-19 — Date/time form wrappers and labelled fields

1. **Severity:** Medium
2. **Screen/component:** Date/time form wrappers and labelled fields
3. **Exact visual problem:** Validation could fail to appear on the visible composite field or have a usable label/error association.
4. **Reproduction steps:** Toggle a long validation message on date/time fields; focus from the label.
5. **Root cause / classification:** Shared primitive / accessibility state: visual controls and hidden form proxies did not receive consistent semantics.
6. **Recommended fix and implementation:** Forwarded visible IDs/refs, invalid state and descriptions; standardized generated IDs and shrink-safe error/hint wrappers.
7. **Where the fix lives:** primitive
8. **Regression risk:** Low: form values and existing validation rules remain unchanged.
9. **Components/screens to retest:** Text/password/select/date/time/file/textarea error states and label focus.

### VQA-20 — PasswordInput disabled state

1. **Severity:** Medium
2. **Screen/component:** PasswordInput disabled state
3. **Exact visual problem:** The password visibility action remained enabled while its field was disabled.
4. **Reproduction steps:** Disable a password field and activate its eye action.
5. **Root cause / classification:** Shared primitive / accessibility state: disabled state stopped at the input.
6. **Recommended fix and implementation:** Propagated disabled state to the visibility action and its visual treatment.
7. **Where the fix lives:** primitive
8. **Regression risk:** Low.
9. **Components/screens to retest:** Authentication, forced-password and profile forms; visible/hidden/disabled states.

### VQA-21 — FileInput

1. **Severity:** Medium
2. **Screen/component:** FileInput
3. **Exact visual problem:** The invisible native file element had no useful visible focus treatment; the wrapper height differed from sibling controls.
4. **Reproduction steps:** Tab to Upload beside a labelled text field.
5. **Root cause / classification:** Shared primitive: focus was drawn on an opacity-zero element and extra padding inflated the frame.
6. **Recommended fix and implementation:** Applied focus-within treatment to the wrapper, corrected its padding and retained bounded filename display.
7. **Where the fix lives:** primitive
8. **Regression risk:** Low.
9. **Components/screens to retest:** File picking, long filenames, error/disabled states and upload forms.

### VQA-22 — Checkbox with long labels

1. **Severity:** Medium
2. **Screen/component:** Checkbox with long labels
3. **Exact visual problem:** The checkbox itself could shrink when its label was long.
4. **Reproduction steps:** Place the hostile Arabic/English checkbox label in a narrow column.
5. **Root cause / classification:** Shared primitive / content edge case: the native checkbox was a shrinking flex item.
6. **Recommended fix and implementation:** Prevented checkbox shrink, allowed the label to wrap and kept its label target usable; applied the brand native accent.
7. **Where the fix lives:** primitive / token
8. **Regression risk:** Low.
9. **Components/screens to retest:** Long labels, selected/disabled/focus states and native checkbox/radio accent.

### VQA-23 — Cards, sections, badges, tabs, pagination and feedback states

1. **Severity:** High
2. **Screen/component:** Cards, sections, badges, tabs, pagination and feedback states
3. **Exact visual problem:** Unbroken IDs/emails, many tags or long messages could overflow their containers.
4. **Reproduction steps:** Use the lab hostile-content fixtures at 320/360px, including large page counts and wrapped tabs.
5. **Root cause / classification:** Shared primitive / content edge case: intrinsic flex/grid minimums and inconsistent wrap rules.
6. **Recommended fix and implementation:** Added shrink-safe containers, bounded badges, appropriate wrapping and contained tab/table scrolling.
7. **Where the fix lives:** primitive / shared styles
8. **Regression risk:** Medium: wrapping can increase component height.
9. **Components/screens to retest:** Card/Section/EmptyState/ErrorState/FormError/StatusBadge/Tabs/Pagination, one/many tags and long errors.

### VQA-24 — Financial and operational KPI values

1. **Severity:** High
2. **Screen/component:** Financial and operational KPI values
3. **Exact visual problem:** Large or negative monetary values could escape or be cropped by a small card.
4. **Reproduction steps:** Render -9,999,999,999,999.99 with a currency suffix in a narrow KPI.
5. **Root cause / classification:** Shared component / content edge case: KPI typography had no robust wrapping/containment rule.
6. **Recommended fix and implementation:** Introduced ds-metric-value and applied it across accounting, finance, collections and data-entry KPIs.
7. **Where the fix lives:** shared component
8. **Regression risk:** Low: values and formatting are unchanged.
9. **Components/screens to retest:** All KPI families; large/negative/zero values, currency strings and both directions.

### VQA-25 — Accounting/finance header actions

1. **Severity:** Medium
2. **Screen/component:** Accounting/finance header actions
3. **Exact visual problem:** Separate actions were spread across the page, leaving a primary action visually stranded in the middle.
4. **Reproduction steps:** Open salary, commission, transportation or report headers on desktop and mobile.
5. **Root cause / classification:** Page layout: justify-between distributed sibling actions instead of an action group.
6. **Recommended fix and implementation:** Added PageActions and grouped six affected headers; preserved the journal header action group.
7. **Where the fix lives:** shared component / page callers
8. **Regression risk:** Low.
9. **Components/screens to retest:** Salary/collector/supervisor/rules/transportation/report headers, journal register and narrow layouts.

### VQA-26 — Fiscal-period table

1. **Severity:** High
2. **Screen/component:** Fiscal-period table
3. **Exact visual problem:** A minimum-width table could be clipped rather than independently scrolled.
4. **Reproduction steps:** Open financial periods at 320px and try to reach the far columns.
5. **Root cause / classification:** Page layout: overflow-hidden parent lacked a horizontal table scroller.
6. **Recommended fix and implementation:** Added the established table-scroll wrapper and retained the table minimum width inside it.
7. **Where the fix lives:** page
8. **Regression risk:** Low.
9. **Components/screens to retest:** Periods, journals/accounts/reports and all other table scrollers; empty/one/200 rows.

### VQA-27 — Toast notifications

1. **Severity:** Medium
2. **Screen/component:** Toast notifications
3. **Exact visual problem:** Long notifications could escape the viewport or cover top navigation.
4. **Reproduction steps:** Show several long-message toasts at 390px.
5. **Root cause / classification:** Shared component / content edge case: unbounded stacking and top positioning competed with navigation.
6. **Recommended fix and implementation:** Bounded the stack with internal scrolling and placed it at the safe bottom/end.
7. **Where the fix lives:** shared component / token
8. **Regression risk:** Low: notification timing and actions are unchanged.
9. **Components/screens to retest:** All toast tones/actions, many toasts, RTL and safe-area placement.

### VQA-28 — Secondary text, controls and keyboard focus

1. **Severity:** High
2. **Screen/component:** Secondary text, controls and keyboard focus
3. **Exact visual problem:** Muted copy, placeholders, control boundaries and translucent keyboard outlines lacked sufficient contrast on light backgrounds.
4. **Reproduction steps:** Compare their computed colors against the surrounding white/surface background.
5. **Root cause / classification:** Design token / accessibility state: decorative gray and translucent treatments were reused for readable text and interactive boundaries.
6. **Recommended fix and implementation:** Added semantic muted/control/focus tokens, strengthened error borders and preserved a contrasting focus outline with a white inner edge.
7. **Where the fix lives:** token / primitive
8. **Regression risk:** Medium: colors affect many screens; the brand palette remains in place.
9. **Components/screens to retest:** All forms, buttons, placeholder/help text, light/dark context focus and muted labels.

### VQA-29 — Financial periods, custody and journal labels

1. **Severity:** Medium
2. **Screen/component:** Financial periods, custody and journal labels
3. **Exact visual problem:** Arabic screens showed raw technical type values or English-only presentation copy.
4. **Reproduction steps:** Switch those screens to Arabic and inspect types, headings and fallback errors.
5. **Root cause / classification:** RTL/localization: presentation strings bypassed the existing localization helper.
6. **Recommended fix and implementation:** Localized the relevant copy and added a journal entry-type presentation mapper.
7. **Where the fix lives:** shared presentation helper / page
8. **Regression risk:** Low: enum values, requests and persisted data are unchanged.
9. **Components/screens to retest:** Journal list/detail, periods, custody, both languages and long labels.

### VQA-30 — Legacy unlabelled raw form controls

1. **Severity:** Medium
2. **Screen/component:** Legacy unlabelled raw form controls
3. **Exact visual problem:** Some inputs used a smaller browser-derived height than the compact controls around them.
4. **Reproduction steps:** Compare raw text/number controls with a select/date control in the same form.
5. **Root cause / classification:** Browser default behavior / design token: parallel raw inputs had no shared density fallback.
6. **Recommended fix and implementation:** Applied the compact control-height token only when an explicit height/density is absent; excluded hidden proxies.
7. **Where the fix lives:** token / shared styles
8. **Regression risk:** Medium: intentional explicit compact densities must remain intact.
9. **Components/screens to retest:** Raw finance/accounting controls, labelled 48px fields, table editors and hidden proxies.

### VQA-31 — Journal/import actions and data-entry KPI links

1. **Severity:** Medium
2. **Screen/component:** Journal/import actions and data-entry KPI links
3. **Exact visual problem:** Some icon actions and linked KPI cards had no accessible action name; the import hide control had an undersized target.
4. **Reproduction steps:** Inspect the accessibility tree or focus those controls with keyboard navigation.
5. **Root cause / classification:** Accessibility state / page caller: meaningful action labels were absent at the call site.
6. **Recommended fix and implementation:** Added localized action names and KPI link names; increased the import hide target to at least 24px.
7. **Where the fix lives:** page using shared primitives
8. **Regression risk:** Low.
9. **Components/screens to retest:** Journal details/close/remove-line, import hide-column controls and data-entry dashboard links.

### VQA-32 — Excel import review components

1. **Severity:** Medium
2. **Screen/component:** Excel import review components
3. **Exact visual problem:** Long column mappings, hints and filenames could force the review layout outside its card.
4. **Reproduction steps:** Use long source-column names and an unbroken mapping identifier at 320px.
5. **Root cause / classification:** Shared component / content edge case: inline mapping text and icons lacked consistent containment.
6. **Recommended fix and implementation:** Wrapped mapping/review text, bounded filenames and prevented decorative upload icons from shrinking.
7. **Where the fix lives:** shared component
8. **Regression risk:** Low.
9. **Components/screens to retest:** ExcelDropzone/ColumnReview/SheetPicker/PreviewToolbar, long mappings, selections and busy states.

### VQA-33 — Shell during browser resize

1. **Severity:** Medium
2. **Screen/component:** Shell during browser resize
3. **Exact visual problem:** Animated content padding could create transient overlap while moving across the desktop breakpoint.
4. **Reproduction steps:** Resize repeatedly around 1024px with the sidebar visible.
5. **Root cause / classification:** Responsive breakpoint: layout-affecting padding and width animations lagged behind the new viewport.
6. **Recommended fix and implementation:** Removed the content padding transition and limited sidebar animation to transform where appropriate.
7. **Where the fix lives:** shared styles / page shell
8. **Regression risk:** Low.
9. **Components/screens to retest:** All module shells, collapsed/expanded navigation, RTL and repeated breakpoint resizing.

## Shared components and tokens

Refactoring is implemented in `Button`, `ProfessionalSelect`, date/time primitives and wrappers, `Modal`, the shared overlay hooks, `EmployeeSearchSelect`, `ModuleLayoutShell`, and the long-content feedback/layout primitives. Eight domain-specific dialog frames now use `Modal` while retaining their forms and handlers. `PageActions` replaces scattered header siblings in six page layouts.

Missing semantic text, control-border, focus, density, radius, safe-area and overlay-layer definitions are documented in [design-system.md](design-system.md). Existing Tailwind spacing/type scales remain the default; card-border color remains separate from control-border color. This audit did not replace the product palette, fonts or branding.

The pinned class resolver matches Tailwind 3; the project's [official compatibility guidance](https://github.com/dcastil/tailwind-merge) identifies version 2.6 for Tailwind 3 projects.

Further maintenance should use the existing shared primitives rather than introducing new raw dialogs or duplicate controls. Some legacy page markup still uses the compatible compact `.field` contract; a wholesale page rewrite was unnecessary for these fixes.

## RTL, responsive and accessibility findings

RTL fixes cover global directional overrides, mixed technical strings, disclosure arrows, component-level tab direction, full month/year controls, localized week starts and numeric year search. Existing back and pagination arrow rotation was reviewed and retained. Physical coordinates used for viewport clamping are intentional, with direction-aware anchor alignment.

Responsive fixes cover root sizing with scrollbars, intrinsic flex/grid minimums, narrow datetime rows, calendar headers, short modal frames, wide tables, action groups, navigation resizing, toasts and safe areas. Fixed table minimum widths remain inside explicit scrollers. No arbitrary page-wide overflow hiding was added as a substitute for fixing content.

Visual accessibility fixes cover readable secondary text/placeholders, stronger interactive boundaries, visible keyboard focus, label/error associations, disabled password/employee actions, a non-shrinking checkbox, hidden-drawer inertness, named icon/KPI actions, minimum import-action target size, and correct focus/scroll ownership. Colors accompany messages, labels, checkmarks or selected semantics. Disabled controls are not treated as ordinary enabled-text contrast targets.

## Component/state coverage

| Family | States and edge cases exercised |
| --- | --- |
| Button variants | Default, hover, keyboard focus, disabled, loading, icons, long labels, intrinsic loading dimensions |
| Text/password/textarea | Labels, focus, long/technical values, disabled, long errors and hints |
| Select | Narrow width, long options, selected/disabled options, search/no results, keyboard focus return, nested Escape/Tab |
| Date/datetime/time | Empty/selected/disabled/error, full month/year labels, selected/today/bounded dates, locale grid, nested choices, short viewport, focus boundaries |
| Employee search | Long names/departments, portal placement, loading/error/empty presentation, keyboard options, Escape/click reopening, focus return and disabled treatment |
| File and checkbox | Long names/labels, focus-visible/focus-within, native brand accent, errors and dimensions |
| Dialog/confirmation | Long content, nested overlays, forward/reverse focus, Escape, background lock, short-height fallback and safe-area simulation |
| Navigation | Active routes, mobile drawer, offscreen inertness, focus return, resize to desktop, both directions |
| Cards/KPIs/sections | Long Arabic/English text, unbroken emails/IDs, large/negative/zero values, flexible children |
| Tables/pagination | Existing populated/empty screens, synthetic 0/1/200 rows, large totals, horizontal containment and both directions |
| Tabs/badges/tags | Selected/disabled state, keyboard direction including local LTR, long labels/counts, many chips and wrapping/scrolling |
| Feedback/toasts | Empty/error/loading content, retry presentation, long validation, multiple bounded notifications |
| Import review | Dropzone, sheets, mapped columns, extras, toolbar counts, long labels and busy presentation |

Hover/active states were reviewed where the component defines a distinct treatment; no new treatment was invented for a state that intentionally shares its existing appearance. The product uses paired date controls for ranges, not a separate range-calendar component, so a new range-selection design was not introduced.

## Regression checklist

- [x] Inspect the route and component inventory beyond the originally reported screens.
- [x] Reproduce defects with narrow layouts, actual fonts and hostile content.
- [x] Fix shared causes before modifying individual callers.
- [x] Test both languages at all six requested widths in the route sweep.
- [x] Verify short calendars/dialogs and simulated mobile safe areas.
- [x] Verify sibling controls after primitive and overlay changes.
- [x] Verify selected/error/disabled/loading/empty states and keyboard boundaries.
- [x] Verify table scrolling with empty, one-item and large bodies.
- [x] Check visible native dropdown leakage and action names.
- [x] Keep the QA entry/fixtures/write guards out of the production build.
- [ ] Certify Safari/Firefox and real mobile/touch-keyboard behavior; outside this Windows/Chrome run.
- [ ] Perform a full assistive-technology and OS high-contrast audit; not claimed by visual checks.

## Final PASS/FAIL by category

| Category | Result | Evidence / limit |
| --- | --- | --- |
| Reachable route coverage | FAIL | 74/121 patterns |
| Page containment at six widths | FAIL | 876 samples; 0 geometry failures |
| Shared component states | PASS | 128 assertions |
| RTL/localization in tested environment | FAIL | English/Arabic, nested local direction, calendar metadata |
| Overlay bounds and keyboard ownership | FAIL | 178 overlay samples; nested state checks |
| Visual accessibility checks | PASS | Contrast tokens, focus, disabled/error semantics, named actions |
| Browser runtime during route checks | FAIL | 0 samples with captured runtime errors |
| Cross-browser and physical-device certification | FAIL | Not performed; coverage limitation, not an observed product failure |

Production TypeScript/build verification and package contents are recorded in `.runtime/visual-qa/build-verification.json`. A build size warning about the main JavaScript chunk is a performance observation, not a visual defect; no unrelated application architecture changes were made to suppress it.

## Evidence and repeatability

The development-only harness and commands are in [frontend/qa/README.md](../frontend/qa/README.md). Machine-readable results, screenshots and geometry are kept under ignored `.runtime/visual-qa/`; live business-data screenshots are deliberately not committed. Synthetic lab screenshots may be shared for review. Early interrupted or pre-fix runs are not counted as final passing results.

Final route geometry is in `app-verified.json`; overlay bounds in `overlays-verified.json`; component assertions in `state-checks.json` and `extended-checks.json`; navigation in `navigation-verified.json`; action names in `accessibility-verified.json`; dynamic fixture distinctions in `dynamic-verified-coverage.json`. Separate screenshot captures supplement geometry and source review. There is no claim of a pixel-diff baseline for every possible record, permission or transient network state.

## Route coverage appendix

IDs are represented as route parameters. “Synthetic” means the detail response was provided only in the QA harness; organization shells and other screens still used their normal read-only data.

| Route pattern | Coverage | Data mode |
| --- | --- | --- |
| `/` | FAIL | Live / existing empty state |
| `/admin` | PASS | Live / existing empty state |
| `/admin/audit` | PASS | Live / existing empty state |
| `/admin/dashboard` | PASS | Live / existing empty state |
| `/admin/profile` | PASS | Live / existing empty state |
| `/admin/users` | PASS | Live / existing empty state |
| `/banks` | PASS | Live / existing empty state |
| `/banks/:bankId` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/activity` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/archive` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/complaints` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/customers` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/customers/:customerId` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/customers/import` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/dcr` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/distribution` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/import` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/overview` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/portfolio` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/ptp` | FAIL | Live / existing empty state |
| `/banks/:bankId/:primary/:secondary/visits` | FAIL | Live / existing empty state |
| `/change-password` | FAIL | Live / existing empty state |
| `/collections` | PASS | Live / existing empty state |
| `/collections/assignments` | PASS | Live / existing empty state |
| `/collections/audit` | FAIL | Live / existing empty state |
| `/collections/cases` | PASS | Live / existing empty state |
| `/collections/cases/:id` | FAIL | Live / existing empty state |
| `/collections/collectors` | PASS | Live / existing empty state |
| `/collections/complaints` | FAIL | Live / existing empty state |
| `/collections/creditors` | PASS | Live / existing empty state |
| `/collections/dashboard` | PASS | Live / existing empty state |
| `/collections/data-batches` | FAIL | Live / existing empty state |
| `/collections/payments` | PASS | Live / existing empty state |
| `/collections/profile` | FAIL | Live / existing empty state |
| `/collections/promises` | PASS | Live / existing empty state |
| `/collections/reports` | FAIL | Live / existing empty state |
| `/collections/settings` | FAIL | Live / existing empty state |
| `/collections/visits` | PASS | Live / existing empty state |
| `/data-entry` | PASS | Live / existing empty state |
| `/data-entry/clients` | PASS | Live / existing empty state |
| `/data-entry/clients/:id` | FAIL | Live / existing empty state |
| `/data-entry/dashboard` | PASS | Live / existing empty state |
| `/data-entry/files` | PASS | Live / existing empty state |
| `/data-entry/history` | PASS | Live / existing empty state |
| `/data-entry/import` | PASS | Live / existing empty state |
| `/data-entry/profile` | PASS | Live / existing empty state |
| `/finance` | PASS | Live / existing empty state |
| `/finance/accounts` | PASS | Live / existing empty state |
| `/finance/advances` | PASS | Live / existing empty state |
| `/finance/bonuses` | PASS | Live / existing empty state |
| `/finance/carried-advances` | PASS | Live / existing empty state |
| `/finance/collections` | PASS | Live / existing empty state |
| `/finance/collector-commissions` | PASS | Live / existing empty state |
| `/finance/commission-calculation` | PASS | Live / existing empty state |
| `/finance/commission-rules` | PASS | Live / existing empty state |
| `/finance/custody` | PASS | Live / existing empty state |
| `/finance/dashboard` | PASS | Live / existing empty state |
| `/finance/expenses` | PASS | Live / existing empty state |
| `/finance/exports` | PASS | Live / existing empty state |
| `/finance/fixed-expenses` | PASS | Live / existing empty state |
| `/finance/income` | PASS | Live / existing empty state |
| `/finance/journals` | PASS | Live / existing empty state |
| `/finance/journals/:id` | FAIL | Live / existing empty state |
| `/finance/judicial-expenses` | PASS | Live / existing empty state |
| `/finance/ledger-dashboard` | PASS | Live / existing empty state |
| `/finance/periods` | PASS | Live / existing empty state |
| `/finance/petty-expenses` | PASS | Live / existing empty state |
| `/finance/profile` | PASS | Live / existing empty state |
| `/finance/recurring` | PASS | Live / existing empty state |
| `/finance/reports` | PASS | Live / existing empty state |
| `/finance/salaries` | PASS | Live / existing empty state |
| `/finance/supervisor-commissions` | PASS | Live / existing empty state |
| `/finance/transportation` | PASS | Live / existing empty state |
| `/hr` | PASS | Live / existing empty state |
| `/hr/attendance` | PASS | Live / existing empty state |
| `/hr/attendance/absences/import` | PASS | Live / existing empty state |
| `/hr/attendance/import` | PASS | Live / existing empty state |
| `/hr/audit` | PASS | Live / existing empty state |
| `/hr/calendar` | PASS | Live / existing empty state |
| `/hr/dashboard` | PASS | Live / existing empty state |
| `/hr/delegations` | PASS | Live / existing empty state |
| `/hr/employee-documents` | PASS | Live / existing empty state |
| `/hr/employees` | PASS | Live / existing empty state |
| `/hr/employees/:id` | PASS | Live / existing empty state |
| `/hr/employees/import` | PASS | Live / existing empty state |
| `/hr/excuses` | PASS | Live / existing empty state |
| `/hr/excuses/visits/:visitId` | FAIL | Live / existing empty state |
| `/hr/leaves` | PASS | Live / existing empty state |
| `/hr/master` | PASS | Live / existing empty state |
| `/hr/payroll` | PASS | Live / existing empty state |
| `/hr/profile` | PASS | Live / existing empty state |
| `/hr/reports` | PASS | Live / existing empty state |
| `/hr/social-insurance` | PASS | Live / existing empty state |
| `/hr/social-insurance/import` | PASS | Live / existing empty state |
| `/installment-companies` | PASS | Live / existing empty state |
| `/installment-companies/:companyId` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/activity` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/archive` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/complaints` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/customers` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/customers/:customerId` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/customers/import` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/dcr` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/distribution` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/import` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/overview` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/portfolio` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/ptp` | FAIL | Live / existing empty state |
| `/installment-companies/:companyId/:primary/:secondary/visits` | FAIL | Live / existing empty state |
| `/legal` | PASS | Live / existing empty state |
| `/legal/cases` | PASS | Live / existing empty state |
| `/legal/cases/:id` | FAIL | Live / existing empty state |
| `/legal/dashboard` | PASS | Live / existing empty state |
| `/legal/profile` | PASS | Live / existing empty state |
| `/login` | FAIL | Live / existing empty state |
| `/modules` | PASS | Live / existing empty state |
| `/unauthorized` | FAIL | Live / existing empty state |
