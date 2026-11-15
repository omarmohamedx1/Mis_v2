const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const ts = require('typescript');

// Exercise the production message formatter without starting a browser/API.
const source = fs.readFileSync(path.join(__dirname, '../src/features/collections/scoreCalculationText.ts'), 'utf8');
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const exportsObject = {};
vm.runInNewContext(compiled, { exports: exportsObject });
const { scoreCalculationText } = exportsObject;

test('missing data is explained in Arabic and English instead of a calculated-zero message', () => {
  const data = { calculationStatus: 'UNAVAILABLE', calculationIssues: ['SOURCE_MISSING'] };
  assert.match(scoreCalculationText(data, true), /لا توجد بيانات افتتاحية/);
  assert.match(scoreCalculationText(data, false), /No approved opening/);
});

test('manual history remains explicitly manual even when calculation issue codes exist', () => {
  const data = { calculationStatus: 'MANUAL', calculationIssues: ['SOURCE_MISSING'] };
  assert.match(scoreCalculationText(data, true), /لا يتم استبداله تلقائيًا/);
  assert.match(scoreCalculationText(data, false), /Preserved manual evaluation/);
});

test('calculated summaries state the BKT6 dues basis and distinguish table scores', () => {
  const text = scoreCalculationText({ calculationStatus: 'CALCULATED' }, false);
  assert.match(text, /opening BKT6 dues/);
  assert.match(text, /Table scores differ/);
  assert.match(text, /STAB has no weight/);
});

test('partial results explain zero denominators and unrecognized issues fail closed', () => {
  assert.match(scoreCalculationText({ calculationStatus: 'PARTIAL', calculationIssues: ['BKT2_DENOMINATOR_ZERO'] }, false), /denominator is zero/);
  assert.match(scoreCalculationText({ calculationStatus: 'UNAVAILABLE', calculationIssues: ['FUTURE_CODE'] }, true), /غير متاحة/);
});

test('unsupported portfolio models and exceptional rules are explicitly unavailable', () => {
  assert.match(scoreCalculationText({ calculationStatus: 'UNAVAILABLE', calculationIssues: ['MODEL_NOT_APPROVED'] }, false), /ACT rules are not assumed/);
  for (const code of ['SIGNED_DUES_REQUIRE_APPROVAL', 'MOVEMENT_OVERRIDE_UNAPPROVED', 'NEGATIVE_STAB_TARGET_REQUIRES_APPROVAL']) {
    assert.match(scoreCalculationText({ calculationStatus: 'UNAVAILABLE', calculationIssues: [code] }, true), /اعتماد/);
  }
});
