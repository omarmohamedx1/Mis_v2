import React, { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { LocalizationProvider } from './src/context/LocalizationContext';
import { AuthProvider } from './src/context/AuthContext';
import { ToastProvider } from './src/components/common/Toast';
import { CollectionEntryForm } from './src/features/collections/components/CollectionEntryForm';
import { CollectionRecordActions } from './src/features/collections/components/CollectionRecordActions';
import { collectionsService } from './src/features/collections/services/collectionsService';

const state = window as any;
state.calls = []; state.saved = 0;
localStorage.setItem('mis.language', 'en');
sessionStorage.setItem('mis.auth', JSON.stringify({ accessToken: 'local-test', user: { id: 'owner', roles: ['Admin'], permissions: ['*'] } }));
for (const name of ['createActivity', 'createPromise', 'submitPayment', 'updateActivity', 'updatePromise', 'deleteActivity', 'deletePromise']) {
  (collectionsService as any)[name] = async (...args: unknown[]) => { state.calls.push({ name, args }); return { id: 'test' }; };
}
const mode = new URLSearchParams(location.search).get('mode') || 'payment';
function Fixture() {
  const [saving, setSaving] = useState(false);
  const onSaved = async () => { state.saved++; };
  if (mode === 'activity-actions') return <CollectionRecordActions caseId="test-case" context="Test customer · TEST-CASE" activity={{ id: 'activity-id', type: 'NOTE', notes: 'Original note', createdAt: new Date().toISOString(), createdBy: 'Test', createdById: 'owner' }} onSaved={onSaved} />;
  if (mode === 'promise-actions') return <CollectionRecordActions caseId="test-case" context="Test customer · TEST-CASE" promise={{ id: 'promise-id', caseId: 'test-case', caseNumber: 'TEST-CASE', customerName: 'Test customer', promisedAmount: 400, promiseDate: '2026-10-17', actualPaidAmount: 0, status: 'UPCOMING', channel: 'CALL', notes: 'Original promise', collectorName: 'Test', collectorId: 'owner', createdAt: new Date().toISOString() }} onSaved={onSaved} />;
  return <CollectionEntryForm type={mode as 'payment' | 'promise'} caseId="test-case" context="Test customer · TEST-CASE" saving={saving} setSaving={setSaving} onSaved={onSaved} />;
}
createRoot(document.getElementById('root')!).render(<LocalizationProvider><AuthProvider><ToastProvider><Fixture /></ToastProvider></AuthProvider></LocalizationProvider>);
