import { apiClient, requestFormData } from '../../../services/apiClient';

export interface VisitImportMapping {
  sheetName: string | null;
  headerRow: number;
  firstDataRow: number;
  columns: Record<string, string>;
  sheetNames?: string[];
}

export interface VisitImportSheet {
  sheetName: string | null;
  suggestedHeaderRowNumber: number;
  detectedColumns: string[];
}

export interface VisitImportUpload {
  id: string;
  fileName: string;
  sheets: VisitImportSheet[];
}

export interface VisitImportRow {
  row: number;
  caseNumber: string;
  customerName: string | null;
  collectorName: string | null;
  visitDate: string | null;
  visitTime: string | null;
  address: string | null;
  status: 'Ready' | 'Error';
  errors: string[];
}

export interface VisitImportPreview {
  id: string;
  previewId: string;
  rows: VisitImportRow[];
}

export interface VisitImportResult {
  imported: number;
  skipped: number;
  failed: number;
}

export const visitImportService = {
  async downloadTemplate(bankId: string) {
    const response = await apiClient.get<Blob>(`/banks/${bankId}/visits/imports/template`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Visits_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },
  upload(bankId: string, file: File) {
    const form = new FormData();
    form.append('file', file);
    return requestFormData<VisitImportUpload>(`/banks/${bankId}/visits/imports`, form);
  },
  async preview(bankId: string, id: string, mapping: VisitImportMapping) {
    return (await apiClient.post<VisitImportPreview>(`/banks/${bankId}/visits/imports/${id}/preview`, mapping)).data;
  },
  async confirm(bankId: string, id: string, previewId: string) {
    return (await apiClient.post<VisitImportResult>(`/banks/${bankId}/visits/imports/${id}/confirm`, { previewId })).data;
  },
};
