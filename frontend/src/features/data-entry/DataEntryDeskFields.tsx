import { SelectInput } from '../../components/forms/SelectInput';
import { useCollectionsLocalization } from '../collections/localization/collectionsTranslations';
import {
  PRIMARY_OPTIONS,
  deskHintKey,
  deskLabelKey,
  displayPrimary,
  secondaryOptions,
  type PrimaryClassification,
  type SubClassification,
} from '../collections/organizationClassification';
import { useDataEntryText } from './dataEntryUi';
import type { DataEntryOrganization, DataEntryPortfolio } from './types/dataEntry';

export function DataEntryDeskFields({
  organizations,
  portfolios,
  organizationId,
  primaryClassification,
  subClassification,
  onOrganizationChange,
  onPrimaryChange,
  onSubChange,
}: {
  organizations: DataEntryOrganization[];
  portfolios: DataEntryPortfolio[];
  organizationId: string;
  primaryClassification: string;
  subClassification: string;
  onOrganizationChange: (id: string) => void;
  onPrimaryChange: (value: string) => void;
  onSubChange: (value: string) => void;
}) {
  const d = useDataEntryText();
  const { ct } = useCollectionsLocalization();
  const primary = (PRIMARY_OPTIONS.includes(primaryClassification as PrimaryClassification) ? primaryClassification : '') as PrimaryClassification | '';
  const subs = primary ? secondaryOptions(primary) : [];
  const matched = portfolios.find((item) =>
    item.primaryClassification === primaryClassification && item.subClassification === subClassification);
  const orgLabel = (org: DataEntryOrganization) => {
    const name = (d.ar ? org.nameArabic : org.nameEnglish) || org.code;
    const kind = org.organizationType === 'CONSUMER_FINANCE'
      ? d.text('تقسيط', 'Installment')
      : d.text('بنك', 'Bank');
    return `${name} · ${kind}`;
  };

  return (
    <div className="grid gap-3 md:grid-cols-2">
      <SelectInput
        containerClassName="md:col-span-2"
        label={d.text('الجهة', 'Organization')}
        required
        value={organizationId}
        onChange={(event) => onOrganizationChange(event.target.value)}
      >
        <option value="">{d.text('اختر الجهة', 'Choose an organization')}</option>
        {organizations.map((org) => <option key={org.id} value={org.id}>{orgLabel(org)}</option>)}
      </SelectInput>
      <SelectInput
        hint={primary ? ct(deskHintKey(primary)) : d.text('نفس تصنيف محافظ التحصيل: نشط، إعدام، أو شركات.', 'Same collections desks: Active, Write-off, or Corporate.')}
        label={d.text('المحفظة الرئيسية', 'Primary book')}
        required
        value={primary}
        onChange={(event) => onPrimaryChange(event.target.value)}
      >
        <option value="">{d.text('اختر المحفظة', 'Choose a book')}</option>
        {PRIMARY_OPTIONS.map((item) => (
          <option key={item} value={item}>{displayPrimary(item)} · {ct(deskLabelKey(item))}</option>
        ))}
      </SelectInput>
      <SelectInput
        disabled={!primary}
        hint={primary && subClassification ? ct(deskHintKey(primary, subClassification as SubClassification)) : undefined}
        label={d.text('التصنيف الفرعي', 'Sub classification')}
        required
        value={subClassification}
        onChange={(event) => onSubChange(event.target.value)}
      >
        <option value="">{d.text('اختر التصنيف', 'Choose a classification')}</option>
        {subs.map((item) => (
          <option key={item} value={item}>{displayPrimary(item)} · {ct(deskLabelKey(item))}</option>
        ))}
      </SelectInput>
      {primary && subClassification ? (
        <p className="rounded-xl bg-slate-50 px-3 py-2 text-sm text-slate-600 md:col-span-2">
          {matched
            ? d.text(`المحفظة جاهزة: ${d.ar ? matched.nameArabic : matched.nameEnglish}`, `Desk ready: ${matched.nameEnglish}`)
            : d.text('المكتب ده هيتسجل تلقائي عند الإرسال لنفس تصنيف التحصيل.', 'This desk will be registered automatically on send, using the same collections classification.')}
        </p>
      ) : null}
    </div>
  );
}

export function DataEntryDeskLabel({ primary, sub }: { primary?: string | null; sub?: string | null }) {
  const { ct } = useCollectionsLocalization();
  if (!primary && !sub) return <>{'—'}</>;
  const parts = [primary, sub].filter((value): value is string => Boolean(value)).map((value) => `${displayPrimary(value)} · ${ct(deskLabelKey(value))}`);
  return <>{parts.join(' / ')}</>;
}
