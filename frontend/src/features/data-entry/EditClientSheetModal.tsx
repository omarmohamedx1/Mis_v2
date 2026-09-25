import { useState, type FormEvent } from 'react';
import { Button } from '../../components/common/Button';
import { Modal } from '../../components/common/Modal';
import { TextAreaInput } from '../../components/forms/TextAreaInput';
import { TextInput } from '../../components/forms/TextInput';
import { getApiErrorMessage } from '../../services/apiClient';
import { useDataEntryText } from './dataEntryUi';
import { dataEntryService } from './services/dataEntryService';

export type ClientSheetValues = {
  id: string;
  customerName: string;
  nationalId?: string | null;
  phones?: string | null;
  address?: string | null;
  feedback?: string | null;
  data?: string | null;
  fields?: Record<string, string> | null;
};

const reserved = new Set(['id', 'name', 'tell', 'tel', 'telephone', 'mobile', 'phone', 'all address', 'address', 'feedback', 'data', 'notes']);

export function EditClientSheetModal({ client, onClose, onSaved }: { client: ClientSheetValues; onClose: () => void; onSaved: () => void }) {
  const d = useDataEntryText();
  const extras = Object.entries(client.fields ?? {}).filter(([key]) => !reserved.has(key.trim().toLowerCase()));
  const [customerName, setCustomerName] = useState(client.customerName);
  const [nationalId, setNationalId] = useState(client.nationalId ?? '');
  const [phones, setPhones] = useState(client.phones ?? '');
  const [address, setAddress] = useState(client.address ?? '');
  const [feedback, setFeedback] = useState(client.feedback ?? '');
  const [data, setData] = useState(client.data ?? '');
  const [fields, setFields] = useState<Record<string, string>>(Object.fromEntries(extras));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!customerName.trim() || saving) return;
    setSaving(true);
    setError('');
    try {
      await dataEntryService.updateClient(client.id, {
        customerName: customerName.trim(),
        nationalId: nationalId.trim() || null,
        phones: phones.trim() || null,
        address: address.trim() || null,
        feedback: feedback.trim() || null,
        data: data.trim() || null,
        fields,
      });
      onSaved();
    } catch (reason) {
      setError(getApiErrorMessage(reason, d.text('تعذر حفظ التعديل', 'Could not save the changes')));
      setSaving(false);
    }
  }

  return (
    <Modal
      closeOnBackdrop={!saving}
      closeOnEscape={!saving}
      hideCloseButton={saving}
      onClose={onClose}
      open
      size="lg"
      title={d.text('تعديل بيانات العميل', 'Edit client data')}
      footer={
        <>
          <Button disabled={saving} fullWidth={false} size="md" type="button" variant="outline" onClick={onClose}>{d.text('إلغاء', 'Cancel')}</Button>
          <Button disabled={saving || !customerName.trim()} form="edit-client-sheet" fullWidth={false} isLoading={saving} size="md" type="submit">{d.text('حفظ', 'Save')}</Button>
        </>
      }
    >
      <form className="grid gap-4 sm:grid-cols-2" id="edit-client-sheet" onSubmit={(event) => void submit(event)}>
        {error ? <div className="rounded-xl bg-red-50 p-3 text-sm text-red-700 sm:col-span-2">{error}</div> : null}
        <TextInput containerClassName="sm:col-span-2" label={d.text('الاسم', 'Name')} required value={customerName} onChange={(event) => setCustomerName(event.target.value)} />
        <TextInput label={d.text('الرقم القومي', 'National ID')} value={nationalId} onChange={(event) => setNationalId(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('التليفونات كما في الملف', 'Phones as in the file')} rows={3} value={phones} onChange={(event) => setPhones(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('العنوان', 'Address')} rows={2} value={address} onChange={(event) => setAddress(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('فيدباك', 'Feedback')} rows={2} value={feedback} onChange={(event) => setFeedback(event.target.value)} />
        <TextAreaInput containerClassName="sm:col-span-2" label={d.text('بيانات', 'Data')} rows={2} value={data} onChange={(event) => setData(event.target.value)} />
        {Object.entries(fields).map(([key, value]) => (
          <TextAreaInput
            containerClassName="sm:col-span-2"
            key={key}
            label={key}
            rows={2}
            value={value}
            onChange={(event) => setFields((current) => ({ ...current, [key]: event.target.value }))}
          />
        ))}
      </form>
    </Modal>
  );
}
