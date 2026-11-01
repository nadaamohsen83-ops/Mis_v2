import { apiClient, requestFormData } from '../../../services/apiClient';
import type { AttendanceImportSheet } from '../types/attendance';

export interface ExcuseImportMapping {
  sheetName: string | null;
  headerRow: number;
  firstDataRow: number;
  dateFormat: string | null;
  columns: Record<string, string>;
  sheetNames?: string[];
}

export interface ExcuseImportUpload {
  id: string;
  fileName: string;
  sheets: AttendanceImportSheet[];
}

export interface ExcuseImportRow {
  row: number;
  employeeNumber: string;
  employeeName: string;
  excuseType: string;
  date: string | null;
  fromTime: string | null;
  toTime: string | null;
  status: 'Ready' | 'Error';
  errors: string[];
}

export interface ExcuseImportPreview {
  id: string;
  previewId: string;
  rows: ExcuseImportRow[];
}

export interface ExcuseImportResult {
  imported: number;
  skipped: number;
  failed: number;
}

const base = '/hr/excuses/imports';

export const excuseImportService = {
  async downloadTemplate() {
    const response = await apiClient.get<Blob>(`${base}/template`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Excuses_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },
  upload(file: File) {
    const form = new FormData();
    form.append('file', file);
    return requestFormData<ExcuseImportUpload>(base, form);
  },
  async preview(id: string, mapping: ExcuseImportMapping) {
    return (await apiClient.post<ExcuseImportPreview>(`${base}/${id}/preview`, mapping)).data;
  },
  async confirm(id: string, previewId: string) {
    return (await apiClient.post<ExcuseImportResult>(`${base}/${id}/confirm`, { previewId })).data;
  },
};
