# Visual QA harness

Development-only tests use the application's real routes and components. Vite's production entry does not reference this directory. No QA route is added to `AppRoutes`.

Run commands from the repository root unless a command changes directory. Python 3, Selenium, Chrome and a compatible ChromeDriver are required. Use the existing frontend dependencies with `npm ci` when installing the project.

1. Start Vite in `frontend`: `npm run dev -- --host 127.0.0.1 --port 5179`.
2. Open `http://127.0.0.1:5179/qa/index.html?lang=ar` for the synthetic component lab; use `lang=en` for English.
3. Generate source coverage: `node frontend/qa/inventory.mjs` and `node frontend/qa/icon-actions.mjs`.
4. Run `python frontend/qa/check_states.py`, `python frontend/qa/check_extended.py`, and `python frontend/qa/inspect_lab.py`.
5. For the route sweep, set `MIS_QA_USERNAME` and `MIS_QA_PASSWORD` in the process environment, then run `python frontend/qa/run_app.py`. Do not commit credentials or auth responses.

`MIS_QA_PHASE` names evidence files; use a unique name for each run. Set `MIS_QA_OVERLAYS=1` to inspect safe dialog openers. The sweep uses a local read-only proxy to the configured MIS production API because that API does not permit direct localhost CORS access. The default target is explicitly visible in `run_app.py`; use a test API instead when available.

The harness and proxy independently reject mutations except authentication. Opening forms is allowed; saving, deleting, approving, importing and other writes are blocked. Forced-password UI coverage changes only the browser's test session flag. Missing detail data uses explicit local synthetic GET fixtures; the report distinguishes it from live data.

Artifacts are written to ignored `.runtime/visual-qa/`. They include viewport geometry, route/dynamic coverage, request status metadata, unnamed accessible actions, dialog bounds, navigation assertions and synthetic screenshots. Some live screenshots may contain internal business data; keep those private. Browser sessions and proxy processes close in `finally` blocks.

The route sweep discovers dynamic links and checks both bank and installment-company workspace patterns. It covers one representative organization/customer per pattern, not every record or permission combination. Source inventory flags are review prompts, not automatically confirmed defects.

Type-check the harness with `cd frontend` then `npx tsc -p qa/tsconfig.json`. Build the product with `npm run build`. Confirm `dist` contains no `qa` entry or mutation-blocking test code before packaging.

Run `python frontend/qa/check_action_menus.py` against the development server for focused row-action checks. It uses synthetic records, checks both languages across six viewport widths, and covers keyboard navigation, disabled actions, long labels, table-row click isolation, outside dismissal, links, nested dialogs and stateful deletion confirmation. It cancels the confirmation without submitting a mutation. Evidence is saved in `.runtime/visual-qa/action-menus/`.

For distribution, start Vite on port 5181 and run `python frontend/qa/check_distribution.py`. The dedicated `distribution.html` harness renders the real bank, installment-company and general distribution pages with an in-memory Axios adapter. All requests, including confirmations, stay inside the fixture; no business API is contacted. Checks cover both languages, responsive setup and review, required collectors, capacity validation, filtered queues, reviewed case IDs, pagination and selection reset, manual assignment, unassignment, the 500-case limit, and error/empty states. Screenshots and results are saved in `.runtime/distribution-qa/`.

On the same port, run `python frontend/qa/check_company_style.py` for the interface conventions. It checks the actual accounting, finance, legal, data-entry and collections formatters, the payroll page, the calendar and distribution tables using local fixtures. Both languages use Latin digits. Text-range measurements verify that Arabic currency labels follow the amount in Arabic reading order, including negative, decimal and compact amounts inside both RTL and LTR containers. Browser font inspection confirms Noto Sans Arabic and Inter load from bundled assets; cell geometry checks compare header/body alignment, padding and column boundaries across six viewport widths. Evidence is saved in `.runtime/company-style-qa/`. These checks cover representative screens and shared styles, rather than every live record or report.

The company-style check also renders the actual overview, creditor and collector dashboards with the reported 25.2-million / 10.2-million amounts and 1,418 cases. It verifies the number → scale → currency reading order, translated count/percentage labels, complete numeric cells, and card containment. The dashboard summary tables scroll within their sections at narrow widths instead of splitting numbers or words.

Run `python frontend/qa/check_collections_workspace.py` on port 5181 for the full collections sidebar and overview. The company-style entry accepts `page=workspace` with `scenario=busy|empty|summary-error|queue-error|queue-slow`, `lang=ar|en`, and a synthetic `role`. It uses an offline test identity and intercepts every API request locally. Checks cover seven viewport widths, both writing directions, sidebar groups and icon rail, mobile drawer and Escape focus restoration, role boundaries, organization search/type filters and ranking, priority expansion, queue deduplication, independent loading/failures, refresh and language changes. Evidence is saved in `.runtime/collections-workspace-qa/`.

The component lab deliberately includes long Arabic/English names, unbroken emails/IDs, large negative amounts, disabled options, validation messages, many tags, empty/error/loading content and nested dialogs. The extended checks also exercise empty, one-row and 200-row table bodies. These tests supplement human screenshot review; they do not certify every browser, device or possible data combination.
