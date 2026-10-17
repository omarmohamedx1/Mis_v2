// Development-only audit entry. Not referenced by the production entry or build.
import { createRoot } from 'react-dom/client';
import { BrowserRouter, useNavigate } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { CalendarDays, Plus, Search } from 'lucide-react';
import { LocalizationProvider, useLocalization } from '../src/context/LocalizationContext';
import { AuthProvider } from '../src/context/AuthContext';
import { ToastProvider, useToast } from '../src/components/common/Toast';
import { AppRoutes } from '../src/routes/AppRoutes';
import { apiClient } from '../src/services/apiClient';
import { Button } from '../src/components/common/Button';
import { Card } from '../src/components/common/Card';
import { PageHeader } from '../src/components/common/PageHeader';
import { Section } from '../src/components/common/Section';
import { Tabs } from '../src/components/common/Tabs';
import { Pagination } from '../src/components/common/Pagination';
import { StatusBadge } from '../src/components/common/StatusBadge';
import { Modal } from '../src/components/common/Modal';
import { ConfirmDialog } from '../src/components/common/ConfirmDialog';
import { EmptyState } from '../src/components/common/EmptyState';
import { ErrorState } from '../src/components/common/ErrorState';
import { FormError } from '../src/components/common/FormError';
import { LoadingSpinner } from '../src/components/common/LoadingSpinner';
import { TextInput } from '../src/components/forms/TextInput';
import { PasswordInput } from '../src/components/forms/PasswordInput';
import { SelectInput } from '../src/components/forms/SelectInput';
import { TextAreaInput } from '../src/components/forms/TextAreaInput';
import { DateControl } from '../src/components/forms/DateControl';
import { DateInput } from '../src/components/forms/DateInput';
import { TimeInput } from '../src/components/forms/TimeInput';
import { FileInput } from '../src/components/forms/FileInput';
import { Checkbox } from '../src/components/forms/Checkbox';
import { ProfessionalSelect } from '../src/components/forms/ProfessionalSelect';
import { AccountingKpi } from '../src/features/accounting/accountingUi';
import { BookHeader } from '../src/features/accounting/bookUi';
import { EmployeeSearchSelect } from '../src/features/hr/components/EmployeeSearchSelect';
import { hrEmployeeService } from '../src/features/hr/services/hrEmployeeService';
import { LanguageSwitcher } from '../src/components/common/LanguageSwitcher';
import { ExcelDropzone } from '../src/features/import/ExcelDropzone';
import { ExcelSheetPicker } from '../src/features/import/ExcelSheetPicker';
import { ExcelColumnReview } from '../src/features/import/ExcelColumnReview';
import { ExcelPreviewToolbar } from '../src/features/import/ExcelPreviewToolbar';
import '../src/styles.css';

const params = new URLSearchParams(location.search);
const lab = params.get('mode') !== 'app';
if (!lab) history.replaceState(null, '', params.get('route') ?? '/modules');
localStorage.setItem('mis.language', params.get('lang') ?? 'en');
const qa: any = (window as any).__qa = { pending: 0, requests: [], blocked: [], errors: [], fixtures: (window as any).__qaFixtureSeed ?? {} };
if ((window as any).__qaApiOverride) apiClient.defaults.baseURL = (window as any).__qaApiOverride;
apiClient.interceptors.request.use(config => {
  if (!['get', 'head', 'options'].includes(config.method?.toLowerCase() ?? 'get') && !config.url?.includes('/auth/login')) {
    qa.blocked.push(config.url); return Promise.reject(new Error('Visual audit blocks data mutations'));
  }
  if (config.url && Object.hasOwn(qa.fixtures, config.url)) config.adapter = async cfg => ({data:qa.fixtures[config.url!], status:200, statusText:'OK', headers:{}, config:cfg});
  qa.pending++; return config;
});
apiClient.interceptors.response.use(r => {
  qa.pending = Math.max(0, qa.pending - 1); qa.requests.push({url:r.config.url,status:r.status}); return r;
}, e => { qa.pending = Math.max(0, qa.pending - 1); qa.requests.push({url:e.config?.url,status:e.response?.status ?? 0}); return Promise.reject(e); });
window.addEventListener('error', e => qa.errors.push(e.message));
window.addEventListener('unhandledrejection', e => qa.errors.push(String(e.reason)));
function Bridge() {
  const {setLanguage} = useLocalization(); const navigate=useNavigate();
  useEffect(() => {qa.language=setLanguage; qa.navigate=navigate; qa.api=apiClient; qa.ready=true;}, [navigate,setLanguage]);
  return null;
}
function Lab() {
  const {language} = useLocalization(); const ar=language==='ar'; const toast=useToast();
  const long=ar?'محمد عبدالرحمن أحمد محمود الاسم الطويل جدًا لاختبار وضوح جميع البيانات داخل مكونات النظام':'Alexandria Christopher Montgomery very long customer and employee name';
  const token='very-long-unbroken.email.identifier.and.department.name'.repeat(3)+'@example.com';
  const [loading,setLoading]=useState(false); const [tab,setTab]=useState('a'); const [modal,setModal]=useState(false); const [nested,setNested]=useState(false); const [confirm,setConfirm]=useState(false);
  const [error,setError]=useState(false); const [selected,setSelected]=useState('1');
  const [employee,setEmployee]=useState('employee'); const [file,setFile]=useState<File|null>(null);
  const [sheets,setSheets]=useState([0]); const [extras,setExtras]=useState<string[]>([]);
  if(lab) hrEmployeeService.getEmployees=async()=>({items:Array.from({length:12},(_,i)=>({id:'employee'+i,employeeNumber:'EMP-'+i,fullName:long+' '+i,departmentName:token})),page:1,pageSize:12,totalCount:12,totalPages:1}) as any;
  const opts=Array.from({length:30},(_,i)=><option key={i} value={String(i)} disabled={i===5}>{i===1?token:`${long} ${i}`}</option>);
  return <main className="mx-auto max-w-6xl space-y-6 p-4">
    <PageHeader title={long} description={token} actions={<><Button fullWidth={false} onClick={()=>setLoading(x=>!x)}>Toggle loading</Button><Button fullWidth={false} onClick={()=>setError(x=>!x)}>Toggle error</Button></>} />
    <section data-qa="buttons" className="grid gap-3 sm:grid-cols-2">
      {(['primary','secondary','outline','ghost','danger'] as const).map(variant=><div key={variant} className="flex min-w-0 flex-wrap gap-2"><Button variant={variant} fullWidth={false} isLoading={loading} leftIcon={<Plus/>}>{variant}</Button><Button variant={variant} disabled fullWidth={false}>Disabled</Button><Button variant={variant} fullWidth={false}>{token}</Button></div>)}
      <Button style={{justifySelf:'start'}} data-qa="loading-no-icon" fullWidth={false} isLoading={loading}>Save changes</Button><Button style={{justifySelf:'start'}} data-qa="loading-right-icon" fullWidth={false} isLoading={loading} rightIcon={<Plus/>}>Save changes</Button><Button size="icon" aria-label="Add"><Plus/></Button>
    </section>
    <Section title={long} description={token}><div data-qa="fields" className="grid gap-4 p-4 sm:grid-cols-2 lg:grid-cols-3">
      <TextInput label="Text" name="qa-text" defaultValue={token} error={error?token:undefined}/><TextInput label="Disabled" name="qa-disabled" disabled defaultValue={long}/>
      <TextInput label="Email" name="qa-email" type="email" defaultValue={token}/><PasswordInput label="Password" name="qa-password" defaultValue="verylongpassword0123456789" disabled={loading}/>
      <SelectInput label="Select" id="qa-select" value={selected} onChange={e=>setSelected(e.target.value)} error={error?token:undefined}>{opts}</SelectInput>
      <DateInput label="Date" id="qa-date" defaultValue="2026-12-31" min="2026-12-20" max="2026-12-31" error={error?token:undefined}/><label>Date/time<DateControl mode="datetime" value="2026-12-31T23:59"/></label>
      <TimeInput label="Time" id="qa-time" defaultValue="23:59" error={error?token:undefined}/><DateInput label="Disabled date" disabled defaultValue="2026-12-31"/><SelectInput label="Disabled select" disabled value="1">{opts}</SelectInput>
      <ProfessionalSelect aria-label="Narrow select" className="w-24" value="1">{opts}</ProfessionalSelect>
      <FileInput label="Upload" error={error?token:undefined}/><TextAreaInput label="Notes" defaultValue={long+' '+token} error={error?token:undefined}/><Checkbox label={long} name="qa-check"/>
      <EmployeeSearchSelect label="Employee search" value={employee} initialSelection={{id:'employee',employeeNumber:'EMP',fullName:long}} onChange={setEmployee} error={error?token:undefined}/>
    </div></Section>
    <div className="grid min-w-0 gap-3 sm:grid-cols-2"><AccountingKpi label={long} value="-9,999,999,999,999.99 EGP"/><BookHeader title={long} description={token}><Button fullWidth={false}>{long}</Button></BookHeader></div>
    <Tabs ariaLabel="QA tabs" items={['a','b','c','d'].map(id=>({id,label:long,badge:'123456789',disabled:id==='d'}))} value={tab} onChange={setTab}/>
    <Tabs wrap ariaLabel="Wrapped tabs" items={['a','b','c','d'].map(id=>({id,label:token,badge:'123456789'}))} value={tab} onChange={setTab}/>
    <div className="flex flex-wrap gap-2">{Array.from({length:12},(_,i)=><StatusBadge dot key={i} tone="success">{i===0?token:long}</StatusBadge>)}</div>
    <Pagination page={99999} pageSize={200} totalCount={20000000} totalPages={100000} onPageChange={()=>{}}/>
    <div className="table-scroll rounded-xl border"><table className="min-w-[900px] text-sm"><thead><tr>{Array.from({length:6},(_,i)=><th className="p-4" key={i}>{long}</th>)}</tr></thead><tbody>{Array.from({length:3},(_,i)=><tr key={i}>{Array.from({length:6},(_,j)=><td className="p-4" key={j}>{token}</td>)}</tr>)}</tbody></table></div>
    <div className="flex flex-wrap gap-3"><Button fullWidth={false} onClick={()=>setModal(true)}>Open modal</Button><Button fullWidth={false} onClick={()=>setConfirm(true)}>Open confirmation</Button><Button fullWidth={false} onClick={()=>toast.error(token,{title:long,duration:0})}>Show toast</Button></div>
    <Card><EmptyState title={long} description={token} action={<Button>{long}</Button>}/><ErrorState title={long} description={token} onRetry={()=>{}}/><FormError message={token}/><LoadingSpinner/></Card>
    <LanguageSwitcher/><LanguageSwitcher tone="dark"/>
    <ExcelDropzone arabic={ar} file={file} onFile={setFile} busy={loading} hint={token}/>
    <ExcelSheetPicker arabic={ar} sheets={Array.from({length:8},(_,i)=>({sheetName:token+i,detectedColumns:[long]}))} selected={sheets} onChange={setSheets}/>
    <ExcelColumnReview arabic={ar} detectedColumns={[token,long]} fields={[{key:'name',en:long,ar:long,aliases:[]}]} mapping={{name:token}} extraSelected={extras} onExtraSelected={setExtras}/>
    <ExcelPreviewToolbar arabic={ar} excludedCount={0} readyCount={999999} selectedCount={0} onExclude={()=>{}} onRestore={()=>{}}/>
    <Modal open={modal} onClose={()=>setModal(false)} title={long} description={token} footer={<><Button fullWidth={false} onClick={()=>setModal(false)}>Close modal</Button><Button fullWidth={false} onClick={()=>setNested(true)}>Nested modal</Button></>}>
      <DateInput label="Modal date" defaultValue="2026-12-31"/><TimeInput label="Modal time" defaultValue="23:59"/><SelectInput label="Modal select">{opts}</SelectInput><EmployeeSearchSelect label="Modal employee" value={employee} onChange={setEmployee}/><p>{token.repeat(8)}</p>
    </Modal>
    <Modal open={nested} onClose={()=>setNested(false)} title="Nested dialog"><SelectInput label="Nested select">{opts}</SelectInput><Button onClick={()=>setNested(false)}>Close nested</Button></Modal>
    <ConfirmDialog open={confirm} title={long} message={token.repeat(6)} onCancel={()=>setConfirm(false)} onConfirm={()=>setConfirm(false)}/>
  </main>;
}
createRoot(document.getElementById('root')!).render(<BrowserRouter><LocalizationProvider><ToastProvider><AuthProvider><Bridge/>{lab?<Lab/>:<AppRoutes/>}</AuthProvider></ToastProvider></LocalizationProvider></BrowserRouter>);
