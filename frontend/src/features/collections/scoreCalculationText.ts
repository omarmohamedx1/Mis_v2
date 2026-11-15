import type { CollectionScoreDashboard } from './types/score';

const reasons: Record<string, [string, string]> = {
  MOVEMENT_OVERRIDE_UNAPPROVED: ['تصحيح حركة BKT يحتاج سببًا واعتمادًا صريحًا ضمن قواعد الفترة.', 'A BKT movement correction requires a reason and explicit approval in the period rules.'],
  SIGNED_DUES_REQUIRE_APPROVAL: ['المصدر يحتوي على مستحقات سالبة؛ يلزم اعتماد التعامل معها في قواعد الفترة.', 'Source dues include negative amounts; the period rules must explicitly approve signed dues.'],
  ACT_NOT_ENABLED: ['الحساب غير متاح: لم يتم تفعيل مصادر التقييم المعتمدة بعد.', 'Calculation unavailable: approved scoring sources are not enabled yet.'],
  SOURCE_MISSING: ['لا توجد بيانات افتتاحية وتحصيلات معتمدة لهذه المحفظة والشهر.', 'No approved opening and collection snapshot exists for this portfolio and month.'],
  RULES_MISSING: ['مستهدفات وأوزان الفترة غير متاحة.', 'Period targets and weights are missing.'],
  RULES_UNAPPROVED: ['قواعد الفترة غير معتمدة.', 'Period rules are not approved.'],
  SOURCE_INCOMPLETE: ['بيانات بداية الشهر أو التحصيلات غير مكتملة أو غير معتمدة.', 'Opening or collection sources are incomplete or unreconciled.'],
  SCOPE_MISMATCH: ['بيانات المصدر لا تطابق الجهة والمحفظة والشهر والعملة المختارة.', 'Source organization, portfolio, month or currency does not match.'],
  NEGATIVE_STAB_TARGET_REQUIRES_APPROVAL: ['مجموع المستهدفات يتجاوز 100%؛ يلزم اعتماد صريح لقاعدة المستقر السالب.', 'Targets exceed 100%; a negative stability target requires explicit approval.'],
  MODEL_NOT_APPROVED: ['لا يوجد نموذج حساب معتمد لهذا النوع من المحافظ؛ لم يتم افتراض قواعد ACT.', 'No approved calculation model exists for this portfolio type; ACT rules are not assumed.'],
  WEIGHTS_INVALID: ['الأوزان أو المستهدفات غير صالحة.', 'Weights or targets are invalid.'],
  RULES_INCOMPLETE: ['قواعد حركة BKT غير مكتملة.', 'BKT movement rules are incomplete.'],
  RULE_VERSION_UNSUPPORTED: ['إصدار قواعد الحساب غير مدعوم.', 'The calculation rule version is unsupported.'],
  OPENING_INVALID: ['الأرصدة أو شرائح المستحقات الافتتاحية ناقصة أو غير صالحة.', 'Opening balances or dues bands are missing or invalid.'],
  PAYMENT_INVALID: ['توجد تحصيلات خارج الفترة أو بدون ربط صحيح بالحالات.', 'Collection facts have an invalid date or case association.'],
  DUPLICATE_SOURCE: ['توجد حالات أو تحصيلات مكررة بالمصدر.', 'Source cases or payments are duplicated.'],
  CUTOFF_INVALID: ['تاريخ قطع بيانات الفترة غير صالح.', 'The source cutoff is invalid.'],
  SOURCE_INVALID: ['صيغة بيانات مصدر التقييم غير صالحة.', 'The scoring source format is invalid.'],
  NUMERIC_RANGE: ['الأرقام خارج نطاق الحساب المدعوم.', 'Values exceed the supported calculation range.'],
};

export function scoreCalculationText(data: CollectionScoreDashboard, arabic: boolean): string {
  const index = arabic ? 0 : 1;
  if (!data.calculationStatus || data.calculationStatus === 'MANUAL') return arabic
    ? 'تقييم يدوي محفوظ؛ لا يتم استبداله تلقائيًا. الملخص المحسوب غير متاح لهذا السجل.'
    : 'Preserved manual evaluation; never replaced automatically. A calculated summary is unavailable for this record.';
  const scope = arabic
    ? 'المستحق = مستحقات BKT6 الافتتاحية؛ المدفوع = تحصيل ACT شامل BKT6. سكور الجدول يختلف عن معادلة الإجمالي. الوزن والسكور لا ينطبقان على STAB، وسكور MORE لا يوزع على صفوف BKT6.'
    : 'Due = opening BKT6 dues; Paid = ACT collections including BKT6. Table scores differ from the overall formula. STAB has no weight/score; MORE is not allocated to BKT6 movement rows.';
  if (data.calculationStatus === 'CALCULATED') return (arabic ? 'محسوب من مصادر الفترة المعتمدة. ' : 'Calculated from approved period sources. ') + scope;
  const issues = [...new Set((data.calculationIssues ?? []).map(code => {
    if (code.endsWith('_DENOMINATOR_ZERO')) return arabic ? 'تعذر حساب بعض النسب لعدم وجود رصيد أو مستحقات أو نسبة حركة تصلح للقسمة.' : 'Some ratios are unavailable because their balance, dues or movement denominator is zero.';
    return reasons[code]?.[index] ?? (arabic ? 'بيانات الحساب غير متاحة؛ راجع إعدادات الفترة.' : 'Calculation data is unavailable; review period setup.');
  }))].join(' ');
  return issues + (data.calculationStatus === 'PARTIAL' ? ` ${scope}` : '');
}
