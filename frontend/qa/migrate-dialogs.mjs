import fs from 'node:fs';
import ts from 'typescript';
const screens={
  'accounting/AccountingSalariesPage.tsx':{close:'setSelected(undefined)',title:'selected.employeeName',size:'lg'},
  'accounting/AccountingCollectorCommissionsPage.tsx':{close:'setDetails(undefined)',title:'details.summary.collectorName',size:'lg'},
  'accounting/AccountingSupervisorCommissionsPage.tsx':{close:'setSelected(undefined)',title:'selected.supervisorName',size:'md'},
  'accounting/AccountingTransportationPage.tsx':{close:'setEditing(undefined)',title:"a.text('الانتقالات', 'Transportation')",size:'md'},
  'accounting/AccountingCommissionRulesPage.tsx':{close:'setShowCreate(false)',title:"a.text('قواعد العمولات', 'Commission rules')",size:'md'},
  'finance/FinanceCollectionsPage.tsx':{close:'close()',title:'value.referenceNumber',size:'xl'},
  'finance/FinanceCustodyPage.tsx':{close:'close()',title:'value.summary.collectorName',size:'xl'},
  'finance/FinanceJournalsPage.tsx':{close:'close()',title:"f.text('قيد يدوي جديد', 'New manual journal')",size:'xl'},
};
for(const [file,config] of Object.entries(screens)){
  const path='frontend/src/pages/'+file;let source=fs.readFileSync(path,'utf8');
  const ast=ts.createSourceFile(file,source,ts.ScriptTarget.Latest,true,ts.ScriptKind.TSX);const patches=[];
  function visit(node){
    if(ts.isJsxElement(node)&&node.openingElement.tagName.getText(ast)==='div'){
      const cls=node.openingElement.attributes.properties.find(a=>a.name?.text==='className')?.initializer?.text??'';
      const child=node.children.find(n=>ts.isJsxElement(n));
      if(cls.includes('fixed inset-0')&&child&&['div','form'].includes(child.openingElement.tagName.getText(ast))){
        patches.push([node.openingElement.getStart(ast),node.openingElement.end,`<Modal open onClose={() => ${config.close}} title={${config.title}} size="${config.size}" hideHeader hideCloseButton bodyClassName="p-0">`]);
        patches.push([node.closingElement.getStart(ast),node.closingElement.end,'</Modal>']);
        const inner=child.openingElement.attributes.properties.find(a=>a.name?.text==='className');
        if(inner){const padding=(inner.initializer?.text??'').match(/\bp-\d+\b/)?.[0]??'';patches.push([inner.getStart(ast),inner.end,`className="min-w-0 ${padding}"`]);}
        return;
      }
    }
    ts.forEachChild(node,visit);
  }
  visit(ast);
  for(const [start,end,text] of patches.sort((a,b)=>b[0]-a[0]))source=source.slice(0,start)+text+source.slice(end);
  if(patches.length){source="import { Modal } from '../../components/common/Modal';\n"+source;fs.writeFileSync(path,source);console.log(file,'migrated');}
}
