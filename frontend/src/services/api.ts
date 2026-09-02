import axios from 'axios';
import { CreateJobResponse, JobDetailResponse } from '../types/job.types';

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
   * Lấy trạng thái của Job theo ID
   */
  async getJobById(id: string): Promise<JobDetailResponse> {
    const response = await apiClient.get<JobDetailResponse>(`/jobs/${id}`);
    return response.data;
  },
};

export default apiClient;
