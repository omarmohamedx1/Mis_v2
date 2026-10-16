// One-time source migration. Limit changes to control className attributes.
import fs from 'node:fs';
import ts from 'typescript';
function scan(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const p=dir+'/'+entry.name;if(entry.isDirectory())scan(p);else if(p.endsWith('.tsx'))change(p);}}
function change(file){const text=fs.readFileSync(file,'utf8');const ast=ts.createSourceFile(file,text,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);const edits=[];
 function visit(n){if(ts.isJsxOpeningElement(n)||ts.isJsxSelfClosingElement(n)){if(['input','textarea','ProfessionalSelect'].includes(n.tagName.getText(ast))){const a=n.attributes.properties.find(a=>a.name?.text==='className');if(a?.initializer){const start=a.initializer.getStart(ast),end=a.initializer.end,old=text.slice(start,end);const value=old.replace(/(?<![-\w])border-(?:mis-border|slate-(?:100|200|300))(?![-\w/])/g,'border-mis-control').replace(/border-red-400/g,'border-red-500');if(old!==value)edits.push({start,end,value});}}}ts.forEachChild(n,visit);}visit(ast);
 if(edits.length){let next=text;for(const e of edits.reverse())next=next.slice(0,e.start)+e.value+next.slice(e.end);fs.writeFileSync(file,next);}
}
scan('frontend/src');
for(const file of ['frontend/src/components/forms/FileInput.tsx','frontend/src/components/forms/SelectInput.tsx']){const s=fs.readFileSync(file,'utf8').replaceAll('border-mis-border','border-mis-control').replaceAll('border-red-400','border-red-500');fs.writeFileSync(file,s);}
const field='frontend/src/components/forms/FormField.tsx';fs.writeFileSync(field,fs.readFileSync(field,'utf8').replace('rounded-form border bg-white','rounded-form border border-mis-control bg-white'));
const css='frontend/src/styles.css';fs.writeFileSync(css,fs.readFileSync(css,'utf8').replace('.field {\n    @apply min-h-11 w-full rounded-xl border border-mis-border','.field {\n    @apply min-h-11 w-full rounded-xl border border-mis-control'));
