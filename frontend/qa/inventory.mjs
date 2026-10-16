import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
const root=path.resolve('frontend');
const source=fs.readFileSync(path.join(root,'src/routes/AppRoutes.tsx'),'utf8');
const ast=ts.createSourceFile('AppRoutes.tsx',source,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);
const routes=[];
function attr(node,name){const a=node.attributes.properties.find(x=>x.name?.text===name);return a?.initializer?.text ?? a?.initializer?.getText(ast);}
function walk(node,prefix=''){
  const opening=ts.isJsxElement(node)?node.openingElement:ts.isJsxSelfClosingElement(node)?node:null;
  if(opening?.tagName.getText(ast)==='Route'){
    const p=attr(opening,'path');
    const full=p?(p.startsWith('/')?p:[prefix,p].filter(Boolean).join('/')):prefix;
    const element=attr(opening,'element')??'';
    if(p)routes.push({path:full,element,redirect:element.includes('Navigate')||element.includes('LegacyAccountingRedirect'),dynamic:full.includes(':')});
    ts.forEachChild(node,child=>walk(child,full));return;
  }
  ts.forEachChild(node,child=>walk(child,prefix));
}
walk(ast);
const files=[];
function scan(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,entry.name);if(entry.isDirectory())scan(p);else if(/\.tsx$/.test(p))files.push(p);}}
scan(path.join(root,'src'));
const patterns={physicalDirection:/\b(?:left|right|pl|pr|ml|mr)-[\w\[.-]+/g,viewportHeight:/\b(?:h-screen|min-h-screen)|100vh/g,nativeSelect:/<select\b/g,adHocDialog:/role="dialog"|fixed inset-0/g,fixedMinWidth:/min-w-\[[^\]]+\]/g,hiddenOverflow:/overflow-[xy]-hidden|overflow-hidden/g};
const inventory=files.map(file=>{const text=fs.readFileSync(file,'utf8');return {file:path.relative(root,file).replaceAll('\\','/'),lines:text.split('\n').length,findings:Object.fromEntries(Object.entries(patterns).map(([name,re])=>[name,[...text.matchAll(re)].map(m=>({line:text.slice(0,m.index).split('\n').length,text:m[0]}))]))};});
fs.mkdirSync('.runtime/visual-qa',{recursive:true});
fs.writeFileSync('.runtime/visual-qa/inventory.json',JSON.stringify({routes,files:inventory},null,2));
console.log(JSON.stringify({routes:routes.length,reachablePatterns:routes.filter(x=>!x.redirect&&!x.path.includes('*')).length,tsxFiles:files.length,screenFiles:inventory.filter(x=>x.file.startsWith('src/pages')).length,patterns:Object.fromEntries(Object.keys(patterns).map(k=>[k,inventory.reduce((n,x)=>n+x.findings[k].length,0)]))},null,2));
