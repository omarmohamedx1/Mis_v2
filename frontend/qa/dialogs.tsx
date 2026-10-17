import { createRoot } from 'react-dom/client';
import { useState } from 'react';
import { LocalizationProvider } from '../src/context/LocalizationContext';
import { useActionDialog } from '../src/components/common/useActionDialog';
import { Button } from '../src/components/common/Button';
import '../src/styles.css';
localStorage.setItem('mis.language',new URLSearchParams(location.search).get('lang')??'en');
function Lab(){const {prompt,confirm,dialog}=useActionDialog();const [value,setValue]=useState('pending');return <main className="space-y-4 p-4"><Button onClick={async()=>setValue(JSON.stringify(await prompt('Reason / سبب الإجراء '.repeat(12))))}>Prompt</Button><Button onClick={async()=>setValue(JSON.stringify(await confirm('Confirm / تأكيد البيانات '.repeat(12))))}>Confirm</Button><output id="result">{value}</output>{dialog}</main>;}
createRoot(document.getElementById('root')!).render(<LocalizationProvider><Lab/></LocalizationProvider>);
