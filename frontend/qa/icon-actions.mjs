import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
const findings=[];
function scan(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,entry.name);if(entry.isDirectory())scan(p);else if(p.endsWith('.tsx'))inspect(p);}}
function inspect(file){const text=fs.readFileSync(file,'utf8');const ast=ts.createSourceFile(file,text,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);const icons=new Set();
 for(const node of ast.statements)if(ts.isImportDeclaration(node)&&node.moduleSpecifier.text==='lucide-react')for(const e of node.importClause?.namedBindings?.elements??[])icons.add(e.name.text);
 const iconOnly=nodes=>nodes.every(n=>ts.isJsxText(n)?!n.text.trim():ts.isJsxSelfClosingElement(n)?icons.has(n.tagName.getText(ast))||n.tagName.getText(ast)==='svg':ts.isJsxElement(n)?['span','svg'].includes(n.openingElement.tagName.getText(ast))&&iconOnly(n.children):false);
 function walk(n){if(ts.isJsxElement(n)){const o=n.openingElement;if(['button','Button','Link','a'].includes(o.tagName.getText(ast))&&!o.attributes.properties.some(a=>['aria-label','aria-labelledby','title'].includes(a.name?.text))&&n.children.length&&iconOnly(n.children))findings.push({file:file.replaceAll('\\','/'),line:ast.getLineAndCharacterOfPosition(n.pos).line+1,tag:o.tagName.getText(ast),source:n.getText(ast).slice(0,450)});}ts.forEachChild(n,walk);}walk(ast);
}
scan('frontend/src');fs.mkdirSync('.runtime/visual-qa',{recursive:true});fs.writeFileSync('.runtime/visual-qa/icon-actions.json',JSON.stringify(findings,null,2));console.log(JSON.stringify(findings,null,2));
