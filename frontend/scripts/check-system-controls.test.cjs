const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { checkSystemControls } = require('./check-system-controls.cjs');

test('UI guard rejects browser UI, including input wrappers, while allowing local business functions and hidden upload inputs', () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'mis-system-controls-'));
  const sourceDirectory = path.join(root, 'src');
  fs.mkdirSync(sourceDirectory);
  const config = path.join(root, 'tsconfig.app.json');
  fs.writeFileSync(config, JSON.stringify({ compilerOptions: { target: 'ES2022', jsx: 'react-jsx', lib: ['ES2022', 'DOM'] }, include: ['src'] }));
  const fixture = path.join(sourceDirectory, 'controls.tsx');
  const run = () => checkSystemControls(root);
  fs.writeFileSync(fixture, `
    const Field = (props: any) => <input {...props} />;
    export const BadMenu = () => <select><option>Test</option></select>;
    export const BadWrappedDate = () => <Field type="date" />;
    export const BadUpload = () => <input type="file" />;
    export const BadSuggestions = () => <datalist />;
    export const BadDialog = () => window.confirm('Test');
    export const BadGlobalDialog = () => alert('Test');
    export const BadHelp = () => <button title="Help">Test</button>;
  `, { flag: 'wx' });
  try {
    const bad = run().join('\n');
    assert.match(bad, /visible native select/);
    assert.match(bad, /including inside input wrappers/);
    assert.match(bad, /designed upload trigger/);
    assert.match(bad, /browser-rendered UI/);
    assert.match(bad, /browser title tooltips/);
    assert.equal((bad.match(/instead of browser dialogs/g) ?? []).length, 2);
    fs.writeFileSync(fixture, `
      function confirm() { return true; }
      export const businessConfirmation = confirm();
      export const HiddenUpload = () => <input type="file" className="sr-only" />;
      export const Help = () => <button data-tooltip="Help">Test</button>;
      export const Preview = () => <iframe title="Document preview" />;
    `);
    assert.deepEqual(run(), []);
  } finally {
    fs.unlinkSync(fixture);
    fs.unlinkSync(config);
    fs.rmdirSync(sourceDirectory);
    fs.rmdirSync(root);
  }
});
