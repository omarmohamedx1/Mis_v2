import { getApiErrorMessage, getApiErrorStatus, isNetworkApiError } from '../../services/apiClient';
import { LegalClientResponseError } from './legalClientResponse';

export function legalClientError(reason: unknown, text: (ar: string, en: string) => string, saving = false): string {
  if (reason instanceof LegalClientResponseError) return text(
    'بيانات ملف العميل المستلمة غير متوافقة. حدّث الصفحة، وإذا استمرت المشكلة اطلب مراجعة توافق نسخة الواجهة مع الخادم.',
    'The client profile response is incompatible. Refresh the page; if this continues, ask for the frontend and API versions to be checked.');
  switch (getApiErrorStatus(reason)) {
    case 401: return text('انتهت جلسة الدخول. سجّل الدخول مرة أخرى ثم افتح ملف العميل.', 'Your session has expired. Sign in again, then reopen the client.');
    case 403: return text('ليس لديك صلاحية الوصول إلى ملف العميل أو تعديله.', 'You do not have permission to access or edit this client.');
    case 404: return text('ملف العميل غير موجود أو غير متاح في خدمة Legal الحالية. حدّث قائمة العملاء وحاول مرة أخرى.', 'The client profile was not found or is unavailable in the current Legal service. Refresh the client list and try again.');
    default:
      if ((getApiErrorStatus(reason) ?? 0) >= 500) return text(
        'حدث خطأ في الخادم أثناء معالجة ملف العميل. حاول مرة أخرى، وإذا استمر الخطأ تواصل مع مسؤول النظام.',
        'The server could not process the client profile. Try again; if this continues, contact your administrator.');
      if (isNetworkApiError(reason)) return text('تعذر الاتصال بالخادم. تحقق من الاتصال وحاول مرة أخرى.', 'Could not connect to the server. Check your connection and try again.');
      return getApiErrorMessage(reason, saving
        ? text('تعذر حفظ بيانات الملف.', 'Could not save the client file.')
        : text('تعذر تحميل بيانات ملف العميل.', 'Could not load the client profile.'));
  }
}
