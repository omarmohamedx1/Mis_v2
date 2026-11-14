const path = require('node:path');
const ts = require('typescript');

function checkSystemControls(root = path.resolve(__dirname, '..')) {
const configPath = path.join(root, 'tsconfig.app.json');
const config = ts.readConfigFile(configPath, ts.sys.readFile);
if (config.error) throw new Error(ts.flattenDiagnosticMessageText(config.error.messageText, '\n'));
const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, root);
const program = ts.createProgram(parsed.fileNames, { ...parsed.options, noEmit: true });
const checker = program.getTypeChecker();
const failures = [];
const browserDialogs = new Set(['alert', 'confirm', 'prompt']);

for (const source of program.getSourceFiles()) {
  const relative = path.relative(path.join(root, 'src'), source.fileName).replaceAll('\\', '/');
  if (relative.startsWith('..') || source.isDeclarationFile) continue;
  const fail = (node, reason) => failures.push(`${relative}:${source.getLineAndCharacterOfPosition(node.getStart(source)).line + 1}: ${reason}`);
  const attr = (node, name) => node.attributes.properties.find(item => ts.isJsxAttribute(item) && item.name.getText(source) === name);
  const literal = item => item?.initializer && ts.isStringLiteral(item.initializer) ? item.initializer.text : undefined;
  function visit(node) {
    if (ts.isJsxOpeningElement(node) || ts.isJsxSelfClosingElement(node)) {
      const tag = node.tagName.getText(source);
      const hidden = /sr-only|opacity-0|\bhidden\b/.test(attr(node, 'className')?.initializer?.getText(source) ?? '') || Boolean(attr(node, 'hidden'));
      if (attr(node, 'title') && ((/^[a-z]/.test(tag) && tag !== 'iframe') || ['Button', 'Link', 'NavLink'].includes(tag)))
        fail(node, 'Use data-tooltip for system-designed help instead of browser title tooltips.');
      if (tag === 'select' && !(relative === 'components/forms/ProfessionalSelect.tsx' && hidden && literal(attr(node, 'aria-hidden')) === 'true'))
        fail(node, 'Use ProfessionalSelect or SelectInput; visible native select menus are not part of the MIS design.');
      const type = literal(attr(node, 'type'));
      if (['date', 'datetime-local', 'time', 'month', 'week', 'color', 'range'].includes(type) && !hidden)
        fail(node, 'Use a system-designed control instead of a native picker, including inside input wrappers.');
      if (tag === 'input') {
        if (type === 'file' && !hidden)
          fail(node, 'Use FileInput, ExcelDropzone, or a designed upload trigger with a hidden file input.');
      }
      if (['datalist', 'progress', 'meter', 'dialog'].includes(tag))
        fail(node, 'Use an MIS component instead of browser-rendered UI.');
    }
    if (ts.isCallExpression(node)) {
      const call = node.expression;
      if (ts.isPropertyAccessExpression(call) && ['showPicker', 'showModal'].includes(call.name.text))
        fail(node, 'Use an MIS popover or Modal instead of a native popup.');
      if (ts.isPropertyAccessExpression(call) && ['window', 'globalThis', 'self'].includes(call.expression.getText(source)) && browserDialogs.has(call.name.text))
        fail(node, 'Use useActionDialog, ConfirmDialog, or Toast instead of browser dialogs.');
      if (ts.isIdentifier(call) && browserDialogs.has(call.text)) {
        const symbol = checker.getSymbolAtLocation(call);
        if (symbol?.declarations?.some(declaration => /lib\.dom\.d\.ts$/.test(declaration.getSourceFile().fileName)))
          fail(node, 'Use useActionDialog, ConfirmDialog, or Toast instead of browser dialogs.');
      }
    }
    ts.forEachChild(node, visit);
  }
  visit(source);
}
return failures;
}

module.exports = { checkSystemControls };
if (require.main === module) {
  const failures = checkSystemControls();
  if (failures.length) {
    console.error(`System control check failed:\n${failures.join('\n')}`);
    process.exitCode = 1;
  } else console.log('System control check passed: no visible browser menus, pickers, tooltips, or dialogs.');
}
