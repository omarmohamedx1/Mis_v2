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

The component lab deliberately includes long Arabic/English names, unbroken emails/IDs, large negative amounts, disabled options, validation messages, many tags, empty/error/loading content and nested dialogs. The extended checks also exercise empty, one-row and 200-row table bodies. These tests supplement human screenshot review; they do not certify every browser, device or possible data combination.
