import axios from 'axios';
import { CreateJobResponse, JobDetailResponse, JobStatusResponse } from '../types/job.types';

// Sử dụng proxy /api (hoặc BASE_URL từ biến môi trường)
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api';

const apiClient = axios.create({
  baseURL: API_BASE_URL,
});

export const jobsApi = {
  /**
   * Upload file PDF để tạo translation job mới
   */
  async createJob(file: File, targetLanguage = 'vi', sourceLanguage = 'auto'): Promise<CreateJobResponse> {
    const formData = new FormData();
    formData.append('File', file);
    formData.append('TargetLanguage', targetLanguage);
    formData.append('SourceLanguage', sourceLanguage);

    const response = await apiClient.post<CreateJobResponse>('/jobs', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });
    return response.data;
  },

  /**
   * Tạo Job PDF học thuật mẫu (có công thức LaTeX & ảnh) để kiểm thử Tuần 6
   */
  async createSampleAcademicJob(): Promise<CreateJobResponse> {
    const response = await apiClient.post<CreateJobResponse>('/jobs/create-sample-academic');
    return response.data;
  },

  /**
   * Lấy trạng thái của Job theo ID
   */
  async getJobById(id: string): Promise<JobDetailResponse> {
    const response = await apiClient.get<JobDetailResponse>(`/jobs/${id}`);
    return response.data;
  },

  /**
   * Lấy trạng thái tiến độ và lịch sử xử lý ngầm của Job (Background Job)
   */
  async getJobStatus(id: string): Promise<JobStatusResponse> {
    const response = await apiClient.get<JobStatusResponse>(`/jobs/${id}/status`);
    return response.data;
  },

  /**
   * Trích xuất các khối văn bản từ file PDF (Tuần 2)
   */
  async extractJobContent(id: string): Promise<any> {
    const response = await apiClient.post(`/jobs/${id}/extract`);
    return response.data;
  },

  /**
   * Lấy danh sách ContentBlocks của Job từ Database (kèm blockType, BoundingBox, TranslatedText)
   */
  async getJobBlocks(id: string): Promise<any> {
    const response = await apiClient.get(`/jobs/${id}/blocks`);
    return response.data;
  },

  /**
   * Dịch tài liệu theo batch từng trang bằng AI (Giai đoạn 3 & 4)
   */
  async translateJob(id: string, fromPage?: number, toPage?: number): Promise<any> {
    let url = `/jobs/${id}/translate`;
    const params = new URLSearchParams();
    if (fromPage !== undefined) params.append('fromPage', fromPage.toString());
    if (toPage !== undefined) params.append('toPage', toPage.toString());
    const queryString = params.toString();
    if (queryString) {
      url += `?${queryString}`;
    }
    const response = await apiClient.post(url);
    return response.data;
  },

  /**
   * Lấy URL mở xem trực tiếp PDF gốc trên trình duyệt
   */
  getOriginalPdfUrl(id: string): string {
    return `${API_BASE_URL}/jobs/${id}/original-pdf`;
  },

  /**
   * Lấy URL mở xem trực tiếp PDF tiếng Việt trên trình duyệt
   */
  getTranslatedPdfUrl(id: string): string {
    return `${API_BASE_URL}/jobs/${id}/translated-pdf`;
  },

  /**
   * Lấy URL tải file PDF tiếng Việt về máy tính
   */
  getDownloadUrl(id: string): string {
    return `${API_BASE_URL}/jobs/${id}/download`;
  },
};

export default apiClient;
