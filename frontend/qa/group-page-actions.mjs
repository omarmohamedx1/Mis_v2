import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
function files(dir){return fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):e.name.endsWith('.tsx')?[path.join(dir,e.name)]:[]);}
for(const file of files('frontend/src/pages')){
  let text=fs.readFileSync(file,'utf8');const ast=ts.createSourceFile(file,text,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);const edits=[];
  function visit(n){
    if(ts.isJsxElement(n)&&n.openingElement.tagName.getText(ast)==='header'){
      const children=n.children.filter(x=>ts.isJsxElement(x)||ts.isJsxSelfClosingElement(x)||ts.isJsxExpression(x)&&x.expression);
      if(children.length>=3&&children[0].getText(ast).includes('<h1')){
        const start=children[1].getStart(ast),end=children.at(-1).end;
        edits.push([start,end,'<PageActions>'+text.slice(start,end)+'</PageActions>']);return;
      }
    }
    ts.forEachChild(n,visit);
  }
  visit(ast);
  for(const [start,end,value] of edits.sort((a,b)=>b[0]-a[0]))text=text.slice(0,start)+value+text.slice(end);
  if(edits.length){text="import { PageActions } from '../../components/common/PageActions';\n"+text;fs.writeFileSync(file,text);console.log(file);}
}
