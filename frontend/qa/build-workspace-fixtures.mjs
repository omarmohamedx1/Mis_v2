// Synthetic UI data derived from the existing DTO shapes; never a production adapter.
import fs from 'node:fs';
import ts from 'typescript';
const types=ts.createSourceFile('collections.ts',fs.readFileSync('frontend/src/features/collections/types/collections.ts','utf8'),ts.ScriptTarget.Latest,true);
const interfaces=new Map(types.statements.filter(ts.isInterfaceDeclaration).map(x=>[x.name.text,x]));
const base=JSON.parse(fs.readFileSync('frontend/qa/detail-fixtures.json','utf8'));
const bankId='00000000-0000-4000-8000-00000000009a',companyId='00000000-0000-4000-8000-00000000009b',fid=base.id;
function value(type,key,org,depth=0,owner=''){
 if(!type||depth>9)return null;
 if(ts.isUnionTypeNode(type))return value(type.types.find(x=>x.kind!==ts.SyntaxKind.NullKeyword&&x.kind!==ts.SyntaxKind.UndefinedKeyword)??type.types[0],key,org,depth,owner);
 if(ts.isLiteralTypeNode(type))return ts.isStringLiteral(type.literal)?type.literal.text:type.literal.getText(types)==='true';
 if(type.kind===ts.SyntaxKind.BooleanKeyword)return true;
 if(type.kind===ts.SyntaxKind.NumberKeyword)return ['page','totalPages','totalCount','caseCount','totalCases','assignedCases','rowNumber','lineNumber'].includes(key)?1:key==='pageSize'?20:/amount|outstanding|remaining|paid|balance|dues/i.test(key)?9999999999999.99:0;
 if(type.kind===ts.SyntaxKind.StringKeyword){
  if(key==='timeZoneId')return 'UTC';
  if(key==='id'||/Id$/.test(key))return /bank|organization|company/i.test(key)?org:fid;
  if(/At$/.test(key))return '2026-10-17T09:00:00Z';
  if(/date|today/i.test(key))return '2026-10-17';
  if(/time$/i.test(key))return '09:00';
  if(key==='status')return /Visit/.test(owner)?'SCHEDULED':/Ptp/.test(owner)?'PENDING':/Archive/.test(owner)?'ARCHIVED':/PortfolioCase/.test(owner)?'ACTIVE':'OPEN';
  if(key==='activityType')return 'CALL';if(key==='outcome')return 'CONNECTED';
  if(key==='organizationType')return org===bankId?'BANK':'INSTALLMENT_COMPANY';
  if(/logoUrl|url|path/i.test(key))return '';
  if(/English/.test(key))return base.longEnglish;
  if(/name|customer|employee|collector|department|description|notes|comment|address|performedBy|createdBy|uploadedBy|assignedTo/i.test(key))return base.longArabic;
  if(/currency/i.test(key))return 'EGP';
  if(/mobile|phone/i.test(key))return '01012345678';
  return base.unbroken;
 }
 if(ts.isArrayTypeNode(type)){
  if(type.elementType.kind===ts.SyntaxKind.StringKeyword)return /activityTypes|filterActivityTypes/.test(key)?['CALL','VISIT']:key==='callOutcomes'?['CONNECTED']:key==='statuses'?['ACTIVE','CLOSED']:[];
  return depth>5?[]:[value(type.elementType,key,org,depth+1,owner)];
 }
 if(ts.isTypeReferenceNode(type))return shape(type.typeName.getText(types),org,depth+1);
 if(ts.isTypeLiteralNode(type))return members(type.members,org,depth+1,owner);
 return null;
}
function members(nodes,org,depth,owner){return Object.fromEntries(nodes.filter(ts.isPropertySignature).map(x=>[x.name.getText(types),value(x.type,x.name.getText(types),org,depth,owner)]));}
function shape(name,org,depth=0){const node=interfaces.get(name);if(!node)return {};
 let result={};for(const clause of node.heritageClauses??[])for(const parent of clause.types)Object.assign(result,shape(parent.expression.getText(types),org,depth+1));
 return Object.assign(result,members(node.members,org,depth,name));
}
const services=fs.readFileSync('frontend/src/features/collections/services/collectionsService.ts','utf8');const responses={};
const calls=[...services.matchAll(/apiClient\.get<([^>]+)>\s*\(\s*`(\/banks\/[^`]+)`/g)];
for(const org of [bankId,companyId]){
 const prefix=org===bankId?'banks':'installment-companies';
 const parent={id:org,code:'QA-ORGANIZATION-12345678901234567890',nameArabic:base.longArabic,nameEnglish:base.longEnglish,logoUrl:''};
 responses[`/${prefix}/${org}`]=parent;responses[`/banks/${org}`]=parent;
 for(const [,type,template] of calls){
  let paths=[template];if(template.includes("assigned ?"))paths=['/banks/${bankId}/distribution/assigned','/banks/${bankId}/distribution/unassigned'];
  for(const path of paths){const url=path.replace(/\$\{([^}]+)\}/g,(_,name)=>/bankId|organizationId/.test(name)?org:fid);const array=type.endsWith('[]');const name=type.replace(/\[\]$/,'');const response=array?[shape(name,org)]:shape(name,org);responses[url]=response;responses[url.replace('/banks/','/'+prefix+'/')]=response;}
 }
}
fs.writeFileSync('frontend/qa/workspace-fixtures.json',JSON.stringify({bankId,companyId,id:fid,responses},null,2));console.log('Synthetic workspace GET fixtures:',Object.keys(responses).length);
