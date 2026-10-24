import { Download, FileSpreadsheet, History, Upload } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Button } from '../../../components/common/Button';
import { Modal } from '../../../components/common/Modal';
import { useToast } from '../../../components/common/Toast';
import { ExcelDropzone } from '../../import/ExcelDropzone';
import { getApiErrorMessage } from '../../../services/apiClient';
import { legalService } from '../services/legalService';
import { useLegalText } from '../legalUi';
import type { LegalImportHistoryItem, LegalImportPreview, LegalImportResult } from '../types/legal';

interface Props {
  onClose: () => void;
  onImported: () => void;
  open: boolean;
}

const ERROR_COPY: Record<string, [string, string]> = {
  ENTITY_REQUIRED: ['الجهة / البنك مطلوب.', 'Entity / Bank is required.'],
  ENTITY_NOT_FOUND: ['الجهة / البنك غير معروفة.', 'Entity / Bank was not found.'],
  PORTFOLIO_REQUIRED: ['المحفظة مطلوبة.', 'Portfolio is required.'],
  PORTFOLIO_NOT_FOUND: ['المحفظة غير معروفة.', 'Portfolio was not found.'],
  PORTFOLIO_ORG_MISMATCH: ['المحفظة لا تتبع الجهة المحددة.', 'The portfolio does not belong to the selected entity.'],
  LEGAL_STAGE_REQUIRED: ['المرحلة القانونية مطلوبة.', 'Legal Stage is required.'],
  LEGAL_STAGE_UNKNOWN: ['المرحلة القانونية غير معروفة.', 'Legal Stage is not recognized.'],
  LEGAL_STAGE_TERMINAL: ['الحكم والتسوية والإرجاع تُسجل من خلال إجراءات الملف القانوني.', 'Judgment, settlement, and return must be recorded through the legal action workflow.'],
  NATIONAL_ID_INVALID: ['الرقم القومي غير صالح.', 'National ID is invalid.'],
  MOBILE_INVALID: ['رقم الهاتف غير صالح.', 'Mobile number is invalid.'],
  CUSTOMER_REQUIRED: ['أدخل كود العميل أو الرقم القومي أو مرجع حالة التحصيل.', 'Enter a customer code, National ID, or collection case reference.'],
  CUSTOMER_NOT_FOUND: ['لم يتم العثور على العميل في الجهة المحددة.', 'Customer was not found in the selected entity.'],
  CUSTOMER_CODE_MISMATCH: ['كود العميل لا يطابق حالة التحصيل.', 'Customer Code does not match the collection case.'],
  NATIONAL_ID_MISMATCH: ['الرقم القومي لا يطابق العميل أو حالة التحصيل.', 'National ID does not match the customer or collection case.'],
  CUSTOMER_ENTITY_MISMATCH: ['العميل يتبع جهة أخرى.', 'Customer belongs to another entity.'],
  CASE_NOT_FOUND: ['لم يتم العثور على حالة تحصيل مطابقة لهذا العميل والمحفظة.', 'No matching collection case was found for this customer and portfolio.'],
  CASE_MATCH_AMBIGUOUS: ['تطابق أكثر من حالة؛ أدخل رقم حالة التحصيل أو رقم الحساب.', 'More than one case matched; provide a Collection Case Number or Account Number.'],
  CASE_REFERENCE_REQUIRED: ['للعميل أكثر من حالة؛ أدخل رقم حالة التحصيل أو رقم الحساب.', 'The customer has multiple cases; provide a Collection Case Number or Account Number.'],
  CASE_REFERENCE_MISMATCH: ['رقم حالة التحصيل ورقم الحساب لا يشيران إلى الحالة نفسها.', 'Collection Case Number and Account Number do not identify the same case.'],
  DUPLICATE_CASE_NUMBER: ['رقم القضية القانونية موجود مسبقًا في ملف قانوني آخر.', 'Legal Case Number is already present on another legal file.'],
  CASE_PORTFOLIO_MISMATCH: ['حالة التحصيل لا تتبع المحفظة المحددة.', 'Collection case does not belong to the selected portfolio.'],
  CASE_ENTITY_MISMATCH: ['حالة التحصيل لا تتبع الجهة المحددة.', 'Collection case does not belong to the selected entity.'],
  CASE_CUSTOMER_MISMATCH: ['حالة التحصيل مرتبطة بعميل آخر.', 'Collection case is linked to a different customer.'],
  CASE_ARCHIVED: ['حالة التحصيل مؤرشفة ولا يمكن استيرادها.', 'Collection case is archived and cannot be imported.'],
  CASE_STATUS_NOT_ELIGIBLE: ['حالة التحصيل غير مؤهلة للتحويل إلى القانونية.', 'Collection case status is not eligible for legal referral.'],
  CASE_ALREADY_EXISTS: ['ملف قانوني موجود بالفعل لهذه الحالة.', 'A legal file already exists for this case.'],
  DUPLICATE: ['هذا السجل مكرر داخل الملف.', 'This row is duplicated within the file.'],
  DATE_INVALID: ['تاريخ الجلسة غير صالح؛ استخدم YYYY-MM-DD.', 'Session date is invalid; use YYYY-MM-DD.'],
  CUSTOMER_CODE_TOO_LONG: ['كود العميل يتجاوز الحد المسموح.', 'Customer Code exceeds the maximum length.'],
  CUSTOMER_NAME_TOO_LONG: ['اسم العميل يتجاوز الحد المسموح.', 'Customer Name exceeds the maximum length.'],
  COLLECTION_CASE_NUMBER_TOO_LONG: ['رقم حالة التحصيل يتجاوز الحد المسموح.', 'Collection Case Number exceeds the maximum length.'],
  ACCOUNT_NUMBER_TOO_LONG: ['رقم الحساب يتجاوز الحد المسموح.', 'Account Number exceeds the maximum length.'],
  LEGAL_CASE_NUMBER_TOO_LONG: ['رقم القضية القانونية يتجاوز الحد المسموح.', 'Legal Case Number exceeds the maximum length.'],
  COURT_TOO_LONG: ['اسم المحكمة يتجاوز الحد المسموح.', 'Court name exceeds the maximum length.'],
  LAWYER_TOO_LONG: ['اسم المحامي يتجاوز الحد المسموح.', 'Lawyer name exceeds the maximum length.'],
  NOTES_TOO_LONG: ['الملاحظات تتجاوز الحد المسموح.', 'Notes exceed the maximum length.'],
};

function StatusLabel({ value, text }: { value: string; text: (ar: string, en: string) => string }) {
  const labels: Record<string, [string, string]> = {
    VALID: ['صالح', 'Valid'],
    INVALID: ['غير صالح', 'Invalid'],
    DUPLICATE: ['مكرر', 'Duplicate'],
    CUSTOMER_NOT_FOUND: ['العميل غير موجود', 'Customer Not Found'],
    CASE_ALREADY_EXISTS: ['القضية موجودة بالفعل', 'Case Already Exists'],
    IMPORTED: ['تم الاستيراد', 'Imported'],
  };
  const [ar, en] = labels[value] ?? ['غير صالح', 'Invalid'];
  const tone = value === 'VALID' || value === 'IMPORTED'
    ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
    : value === 'DUPLICATE' || value === 'CASE_ALREADY_EXISTS'
      ? 'border-amber-200 bg-amber-50 text-amber-800'
      : 'border-red-200 bg-red-50 text-red-700';
  return <span className={`inline-flex rounded-full border px-2.5 py-1 text-xs font-semibold ${tone}`}>{text(ar, en)}</span>;
}

export function LegalCasesImportModal({ onClose, onImported, open }: Props) {
  const l = useLegalText();
  const toast = useToast();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<LegalImportPreview | null>(null);
  const [result, setResult] = useState<LegalImportResult | null>(null);
  const [history, setHistory] = useState<LegalImportHistoryItem[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [importing, setImporting] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState('');
  const localizedApiError = (reason: unknown, arabic: string, english: string) =>
    l.ar ? arabic : getApiErrorMessage(reason, english);

  useEffect(() => {
    if (!open) return;
    let active = true;
    setHistoryLoading(true);
    legalService.importHistory()
      .then((items) => { if (active) setHistory(items); })
      .catch((reason) => { if (active) toast.error(localizedApiError(reason, 'تعذر تحميل سجل الاستيراد.', 'Could not load import history.')); })
      .finally(() => { if (active) setHistoryLoading(false); });
    return () => { active = false; };
  }, [open]);

  async function createPreview(nextFile: File | null) {
    setFile(nextFile);
    setPreview(null);
    setResult(null);
    setError('');
    if (!nextFile) return;
    setPreviewing(true);
    try {
      setPreview(await legalService.previewImport(nextFile));
    } catch (reason) {
      const message = localizedApiError(reason, 'تعذر معاينة الملف. تحقق من القالب والبيانات.', 'Could not preview the file. Check the template and data.');
      setError(message);
      toast.error(message);
    } finally {
      setPreviewing(false);
    }
  }

  async function downloadTemplate() {
    setDownloading(true);
    try {
      await legalService.downloadImportTemplate();
    } catch (reason) {
      toast.error(localizedApiError(reason, 'تعذر تنزيل قالب Excel.', 'Could not download the Excel template.'));
    } finally {
      setDownloading(false);
    }
  }

  async function importValidRows() {
    if (!preview || importing || preview.validRows === 0) return;
    setImporting(true);
    setError('');
    try {
      const imported = await legalService.importCases(preview.fileName, preview.rows.map((row) => row.source));
      setResult(imported);
      onImported();
      toast.success(l.text(
        `تم استيراد ${imported.successfulRows} سجل قانوني.`,
        `Imported ${imported.successfulRows} legal record${imported.successfulRows === 1 ? '' : 's'}.`,
      ));
      legalService.importHistory()
        .then(setHistory)
        .catch((reason) => toast.error(localizedApiError(reason, 'تعذر تحديث سجل الاستيراد.', 'Could not refresh import history.')));
    } catch (reason) {
      const message = localizedApiError(reason, 'تعذر استيراد السجلات القانونية.', 'Could not import the legal records.');
      setError(message);
      toast.error(message);
    } finally {
      setImporting(false);
    }
  }

  const statusText = (code: string) => {
    const [ar, en] = ERROR_COPY[code] ?? ['تعذر التحقق من هذا السجل.', 'This row could not be validated.'];
    return l.text(ar, en);
  };

  return (
    <Modal
      open={open}
      onClose={onClose}
      size="full"
      title={l.text('رفع ملف قضايا', 'Upload Legal Cases File')}
      description={l.text(
        'عاين السجلات أولاً. لن يتم حفظ أي بيانات قبل تأكيد استيراد السجلات الصحيحة.',
        'Preview the rows first. No records are saved until you confirm the valid rows.',
      )}
      closeLabel={l.text('إغلاق', 'Close')}
      footer={(
        <>
          <Button fullWidth={false} disabled={previewing || importing} onClick={onClose} variant="outline">
            {l.text('إغلاق', 'Close')}
          </Button>
          {preview ? (
            <Button
              fullWidth={false}
              isLoading={importing}
              disabled={preview.validRows === 0 || previewing}
              leftIcon={<Upload />}
              onClick={() => void importValidRows()}
            >
              {l.text('استيراد السجلات الصحيحة', 'Import Valid Records')} ({l.number(preview.validRows)})
            </Button>
          ) : null}
        </>
      )}
    >
      <div className="space-y-6">
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-mis-border bg-slate-50 p-4">
          <div>
            <h3 className="font-bold text-mis-navy">{l.text('نموذج الاستيراد', 'Import template')}</h3>
            <p className="mt-1 text-sm text-slate-600">
              {l.text('يربط الملف بعملاء وحالات تحصيل موجودة فقط، ولا ينشئ سجلات مكررة.', 'The file links to existing customers and collection cases only; it does not create duplicate records.')}
            </p>
          </div>
          <Button fullWidth={false} isLoading={downloading} leftIcon={<Download />} onClick={() => void downloadTemplate()} variant="outline">
            {l.text('تحميل نموذج Excel', 'Download Excel Template')}
          </Button>
        </div>

        <ExcelDropzone
          arabic={l.ar}
          busy={previewing || importing}
          file={file}
          hint={l.text('يدعم XLSX وXLS وCSV بحد أقصى 50 ميجابايت.', 'Supports XLSX, XLS, and CSV up to 50 MB.')}
          label={l.text('ملف القضايا', 'Legal cases file')}
          onFile={(next) => void createPreview(next)}
        />

        {previewing ? (
          <div className="flex items-center gap-3 rounded-xl bg-mis-pale px-4 py-3 text-sm font-semibold text-mis-deep" role="status">
            <FileSpreadsheet className="h-5 w-5" />
            {l.text('جارٍ التحقق من السجلات ومطابقتها...', 'Validating rows and matching existing records...')}
          </div>
        ) : null}
        {error ? <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700" role="alert">{error}</div> : null}

        {preview ? (
          <section className="space-y-4" aria-label={l.text('معاينة الاستيراد', 'Import preview')}>
            <h3 className="text-lg font-bold text-mis-navy">{l.text('معاينة الاستيراد', 'Import Preview')}</h3>
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
              <Summary label={l.text('إجمالي الصفوف', 'Total Rows')} value={preview.totalRows} />
              <Summary label={l.text('الصفوف الصحيحة', 'Valid Rows')} value={preview.validRows} tone="success" />
              <Summary label={l.text('الصفوف غير الصحيحة', 'Invalid Rows')} value={preview.invalidRows} tone="danger" />
              <Summary label={l.text('العملاء المطابقون', 'Existing Customers Matched')} value={preview.existingCustomersMatched} />
              <Summary label={l.text('حالات التحصيل المطابقة', 'Existing Cases Matched')} value={preview.existingCasesMatched} />
              <Summary label={l.text('الصفوف المكررة', 'Duplicate Rows')} value={preview.duplicateRows} tone="warning" />
              <Summary label={l.text('ملفات قانونية جديدة', 'New Legal Cases')} value={preview.newLegalCases} tone="success" />
            </div>
            <div className="overflow-hidden rounded-2xl border border-mis-border">
              <div className="max-h-[26rem] overflow-auto">
                <table className="min-w-[78rem] w-full text-sm">
                  <thead className="sticky top-0 bg-slate-50 text-xs font-bold text-slate-500">
                    <tr>
                      <th className="px-3 py-3 text-start">{l.text('رقم الصف', 'Row')}</th>
                      <th className="px-3 py-3 text-start">{l.text('الحالة', 'Status')}</th>
                      <th className="px-3 py-3 text-start">{l.text('العميل', 'Customer')}</th>
                      <th className="px-3 py-3 text-start">{l.text('رقم حالة التحصيل', 'Collection Case')}</th>
                      <th className="px-3 py-3 text-start">{l.text('رقم القضية القانونية', 'Legal Case Number')}</th>
                      <th className="px-3 py-3 text-start">{l.text('الجهة / المحفظة', 'Entity / Portfolio')}</th>
                      <th className="px-3 py-3 text-start">{l.text('الأخطاء', 'Validation Errors')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-mis-border">
                    {preview.rows.map((row) => (
                      <tr key={row.source.rowNumber} className="align-top">
                        <td className="px-3 py-3 font-semibold" dir="ltr">{row.source.rowNumber}</td>
                        <td className="px-3 py-3"><StatusLabel text={l.text} value={result?.rows.find((item) => item.rowNumber === row.source.rowNumber)?.status ?? row.status} /></td>
                        <td className="px-3 py-3">
                          <p>{row.matchedCustomerName ?? row.source.customerName ?? '—'}</p>
                          {row.source.customerCode ? <p className="mt-1 text-xs text-slate-500" dir="ltr">{row.source.customerCode}</p> : null}
                        </td>
                        <td className="px-3 py-3" dir="ltr">{row.matchedCaseNumber ?? row.source.collectionCaseNumber ?? row.source.accountNumber ?? '—'}</td>
                        <td className="px-3 py-3" dir="ltr">{row.source.legalCaseNumber ?? '—'}</td>
                        <td className="px-3 py-3">{row.source.organization ?? '—'} / {row.source.portfolio ?? '—'}</td>
                        <td className="max-w-sm px-3 py-3 text-xs text-red-700">
                          {(result?.rows.find((item) => item.rowNumber === row.source.rowNumber)?.errors ?? row.errors).map((code) => (
                            <p key={code}>{statusText(code)}</p>
                          ))}
                          {result?.rows.find((item) => item.rowNumber === row.source.rowNumber)?.status === 'IMPORTED'
                            ? <span className="text-emerald-700">{l.text('تم إنشاء الملف القانوني وربطه بحالة التحصيل.', 'Legal file created and linked to the collection case.')}</span>
                            : null}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
            {result ? (
              <div className="rounded-xl border border-mis-border bg-slate-50 p-4 text-sm">
                <p className="font-bold text-mis-navy">
                  {l.text('نتيجة الاستيراد', 'Import Result')}: {l.number(result.successfulRows)} / {l.number(result.totalRows)} {l.text('تم استيرادها', 'imported')}
                </p>
                {result.failedRows ? <p className="mt-1 text-red-700">{l.text('السجلات التي لم تُستورد', 'Rows not imported')}: {l.number(result.failedRows)}</p> : null}
              </div>
            ) : null}
          </section>
        ) : null}

        <section className="space-y-3 border-t border-mis-border pt-5">
          <h3 className="flex items-center gap-2 font-bold text-mis-navy"><History className="h-4 w-4" />{l.text('سجل استيراد الملفات', 'File Import History')}</h3>
          {historyLoading ? <p className="text-sm text-slate-500">{l.text('جارٍ تحميل السجل...', 'Loading history...')}</p> : history.length === 0 ? (
            <p className="rounded-xl bg-slate-50 px-4 py-3 text-sm text-slate-600">{l.text('لا توجد عمليات استيراد سابقة.', 'No previous imports.')}</p>
          ) : (
            <div className="space-y-2">
              {history.map((item) => (
                <details key={item.id} className="rounded-xl border border-mis-border bg-white px-4 py-3">
                  <summary className="cursor-pointer list-none">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="font-semibold text-mis-navy" dir="ltr">{item.fileName}</span>
                      <span className="text-xs text-slate-500">
                        {l.dateTime(item.uploadedAt)} · {item.uploadedBy} · {l.text('ناجح', 'Successful')}: {l.number(item.successfulRows)} · {l.text('فاشل', 'Failed')}: {l.number(item.failedRows)}
                      </span>
                    </div>
                  </summary>
                  {item.errors.length ? (
                    <div className="mt-3 space-y-1 border-t border-mis-border pt-3 text-xs text-red-700">
                      {item.errors.slice(0, 50).map((entry) => (
                        <p key={entry.rowNumber} dir={l.ar ? 'rtl' : 'ltr'}>
                          {l.text('الصف', 'Row')} {l.number(entry.rowNumber)}: {entry.errors.map(statusText).join(' · ')}
                        </p>
                      ))}
                      {item.errors.length > 50 ? <p>{l.text('يتم عرض أول 50 خطأ.', 'Showing the first 50 errors.')}</p> : null}
                    </div>
                  ) : null}
                </details>
              ))}
            </div>
          )}
        </section>
      </div>
    </Modal>
  );
}

function Summary({ label, tone, value }: { label: string; tone?: 'success' | 'danger' | 'warning'; value: number }) {
  const color = tone === 'success' ? 'text-emerald-700' : tone === 'danger' ? 'text-red-700' : tone === 'warning' ? 'text-amber-700' : 'text-mis-navy';
  return (
    <div className="rounded-xl border border-mis-border bg-white px-4 py-3">
      <p className="text-xs font-medium text-slate-500">{label}</p>
      <p className={`mt-1 text-xl font-bold ${color}`} dir="ltr">{value.toLocaleString()}</p>
    </div>
  );
}
