import { apiClient, requestFormData } from '../../../services/apiClient';
import type {
  LegalCaseDetails,
  LegalCasePage,
  LegalDashboard,
  LegalImportHistoryItem,
  LegalImportPreview,
  LegalImportResult,
  LegalImportSourceRow,
  LegalOrganization,
  RecordLegalActionInput,
  SaveLegalFileInput,
} from '../types/legal';

export const legalService = {
  async dashboard() {
    return (await apiClient.get<LegalDashboard>('/legal/dashboard')).data;
  },

  async organizations() {
    return (await apiClient.get<LegalOrganization[]>('/legal/organizations')).data;
  },

  async cases(params: { search?: string; organizationId?: string; stage?: string; page?: number; pageSize?: number }) {
    return (await apiClient.get<LegalCasePage>('/legal/cases', {
      params: {
        search: params.search || undefined,
        organizationId: params.organizationId || undefined,
        stage: params.stage || undefined,
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 20,
      },
    })).data;
  },

  async case(collectionCaseId: string) {
    return (await apiClient.get<LegalCaseDetails>(`/legal/cases/${collectionCaseId}`)).data;
  },

  async saveFile(collectionCaseId: string, input: SaveLegalFileInput) {
    return (await apiClient.put<LegalCaseDetails>(`/legal/cases/${collectionCaseId}/file`, input)).data;
  },

  async recordAction(collectionCaseId: string, input: RecordLegalActionInput) {
    return (await apiClient.post<LegalCaseDetails>(`/legal/cases/${collectionCaseId}/actions`, input)).data;
  },

  async downloadImportTemplate() {
    const response = await apiClient.get<Blob>('/legal/imports/template', { responseType: 'blob' });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'Legal_Cases_Import_Template.xlsx';
    link.click();
    URL.revokeObjectURL(url);
  },

  async previewImport(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return requestFormData<LegalImportPreview>('/legal/imports/preview', formData);
  },

  async importCases(fileName: string, rows: LegalImportSourceRow[]) {
    return (await apiClient.post<LegalImportResult>('/legal/imports', { fileName, rows })).data;
  },

  async importHistory() {
    return (await apiClient.get<LegalImportHistoryItem[]>('/legal/imports/history')).data;
  },
};
