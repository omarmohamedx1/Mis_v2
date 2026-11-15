const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

function load(relative) {
  const source = fs.readFileSync(path.join(__dirname, relative), 'utf8');
  const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
  const exports = {}; vm.runInNewContext(compiled, { exports }); return exports;
}
const { targetDraft, parseTargetDraft, targetTotal, validTargetNumber, validTargetPeriod } = load('../src/features/collections/bankTargetDraft.ts');
const { bankTargetsText } = load('../src/features/collections/localization/bankTargetsText.ts');

test('new configurations remain empty instead of silently applying a template or zeroes', () => {
  const draft = targetDraft();
  assert.equal(draft.rows.length, 14);
  assert.equal(draft.rows[0].targetPercent, '');
  assert.equal(draft.moreTargetPercent, '');
  assert.equal(parseTargetDraft(draft), null);
  assert.equal(targetTotal(draft.rows.map(row => row.weight)), null);
});
test('weights use decimal hundredths and the exact reference sums to 100', () => {
  assert.equal(targetTotal(['10', '0', '17', '9', '4', '16', '9', '3', '6', '7', '3', '6', '7', '3']), 100);
  assert.equal(targetTotal(['33.33', '33.33', '33.34']), 100);
  assert.equal(targetTotal(['90', '10']), 100);
  assert.equal(targetTotal(['90', '']), null);
});
test('blank, out-of-range, non-finite and excess-precision entries are invalid', () => {
  for (const raw of ['', ' ', '-1', '101', 'NaN', 'Infinity', '1e2', '0.001']) assert.equal(validTargetNumber(raw), false, raw);
  for (const raw of ['0', '0.00', '4.0', '33.33', '100.00']) assert.equal(validTargetNumber(raw), true, raw);
});
test('draft parsing preserves explicit zero and does not mutate loaded data', () => {
  const values = { rows: targetDraft().rows.map(row => ({ ...row, targetPercent: 10, weight: 0 })),
    moreTargetPercent: 4, moreWeight: 100, activeClassWeight: 90, moreClassWeight: 10, wOffBelow90: 20, wOffAbove90: 40 };
  const draft = targetDraft(values); const parsed = parseTargetDraft(draft);
  assert.equal(parsed.rows[0].weight, 0); assert.equal(parsed.moreTargetPercent, 4);
  draft.rows[0].weight = '20'; assert.equal(values.rows[0].weight, 0);
  draft.moreWeight = ''; assert.equal(parseTargetDraft(draft), null);
});
test('reporting periods validate calendar months and full years', () => {
  for (const period of ['2026-01', '2026-12', '1900-01', '9998-12']) assert.equal(validTargetPeriod(period), true);
  for (const period of ['', '2026-00', '2026-13', '2026-1', '1899-12', '9999-01', '2026-01-01']) assert.equal(validTargetPeriod(period), false);
});
test('both languages explain missing configuration, scope mismatches and the undefined W-Off boundary', () => {
  assert.match(bankTargetsText('TARGETS_MISSING', false), /missing/);
  assert.match(bankTargetsText('TARGETS_NOT_APPROVED', true), /غير معتمدة/);
  assert.match(bankTargetsText('TARGETS_SNAPSHOT_MISMATCH', false), /do not match/);
  assert.match(bankTargetsText('boundary', false), /Exactly 90%.*disabled.*not treated as commission/);
  assert.match(bankTargetsText('boundary', true), /90%.*غير مفعّل/);
  assert.match(bankTargetsText('templateConfirm', true), /القالب المرجعي/);
  assert.match(bankTargetsText('referenceWarning', true), /الأوزان الكمية/);
});
