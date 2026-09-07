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
  statusUrl?: string;
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

export interface JobStatusHistoryItem {
  id: string;
  fromStatus: string | null;
  toStatus: string;
  changedAt: string;
  message: string;
}

export interface JobStatusResponse {
  jobId: string;
  fileName: string;
  status: JobStatus;
  progressPercent: number;
  currentStep: string;
  totalBlocks: number;
  translatedBlocks: number;
  errorMessage?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  history: JobStatusHistoryItem[];
}
