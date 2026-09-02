export type JobStatus =
  | 'Pending'
  | 'Extracting'
  | 'Translating'
  | 'Rebuilding'
  | 'Completed'
  | 'Failed';

export interface CreateJobResponse {
  jobId: string;
  fileName: string;
  sourceLanguage: string;
  targetLanguage: string;
  status: JobStatus;
  createdAt: string;
  message: string;
}

export interface JobDetailResponse {
  id: string;
  originalFileName: string;
  sourceLanguage: string;
  targetLanguage: string;
  status: JobStatus;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  totalBlocks: number;
}
