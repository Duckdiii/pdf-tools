import { useState, useEffect } from 'react';
import { jobsApi } from './services/api';
import { CreateJobResponse, JobStatusResponse } from './types/job.types';
import { Navbar } from './components/Navbar';
import { UploadSection } from './components/UploadSection';
import { SideBySideViewer } from './components/SideBySideViewer';

export function App() {
  const [viewMode, setViewMode] = useState<'dashboard' | 'viewer'>('dashboard');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentJob, setCurrentJob] = useState<CreateJobResponse | null>(null);
  const [jobStatus, setJobStatus] = useState<JobStatusResponse | null>(null);
  const [isPolling, setIsPolling] = useState(false);
  const [extractedBlocks, setExtractedBlocks] = useState<any[]>([]);

  // Xử lý upload file thông thường
  const handleUpload = async (file: File, targetLanguage: string, sourceLanguage: string) => {
    try {
      setIsLoading(true);
      setError(null);
      setJobStatus(null);
      setExtractedBlocks([]);
      setViewMode('dashboard');

      const res = await jobsApi.createJob(file, targetLanguage, sourceLanguage);
      setCurrentJob(res);
      setIsPolling(true);

      const initialStatus = await jobsApi.getJobStatus(res.jobId);
      setJobStatus(initialStatus);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi tải lên file.');
      setIsPolling(false);
    } finally {
      setIsLoading(false);
    }
  };

  // Xử lý tạo Job PDF học thuật mẫu (có công thức LaTeX & ảnh)
  const handleCreateSampleAcademic = async () => {
    try {
      setIsLoading(true);
      setError(null);
      setJobStatus(null);
      setExtractedBlocks([]);
      setViewMode('dashboard');

      const res = await jobsApi.createSampleAcademicJob();
      setCurrentJob(res);
      setIsPolling(true);

      const initialStatus = await jobsApi.getJobStatus(res.jobId);
      setJobStatus(initialStatus);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi tạo file học thuật mẫu.');
      setIsPolling(false);
    } finally {
      setIsLoading(false);
    }
  };

  // Polling tự động mỗi 2 giây khi job đang chạy ngầm
  useEffect(() => {
    if (!currentJob || !isPolling) return;

    let isMounted = true;
    const interval = setInterval(async () => {
      try {
        const res = await jobsApi.getJobStatus(currentJob.jobId);
        if (!isMounted) return;
        setJobStatus(res);

        // Khi job hoàn thành hoặc thất bại -> dừng polling
        if (res.status === 'Completed' || res.status === 'Failed') {
          setIsPolling(false);
          // Tự động tải danh sách blocks từ Database để preview khi xong
          if (res.status === 'Completed') {
            try {
              const blocksData = await jobsApi.getJobBlocks(currentJob.jobId);
              if (isMounted) {
                setExtractedBlocks(blocksData.blocks || []);
                // Tự động chuyển sang chế độ Xem Đối Chiếu Song Song khi hoàn tất
                setViewMode('viewer');
              }
            } catch (err) {
              console.warn('Không thể load blocks preview:', err);
            }
          }
        }
      } catch (err: any) {
        console.error('Polling status error:', err);
      }
    }, 2000);

    return () => {
      isMounted = false;
      clearInterval(interval);
    };
  }, [currentJob, isPolling]);

  const hasTranslatedPdf = jobStatus?.status === 'Completed';
  const currentJobId = jobStatus?.jobId || currentJob?.jobId;

  return (
    <div className="min-h-screen w-full flex flex-col bg-slate-950 text-slate-100">
      {/* Navbar Header */}
      <Navbar
        viewMode={viewMode}
        setViewMode={setViewMode}
        jobStatus={jobStatus}
        hasTranslatedPdf={hasTranslatedPdf}
        downloadUrl={currentJobId ? jobsApi.getDownloadUrl(currentJobId) : undefined}
      />

      {/* Main Content Area */}
      <main className="flex-1 w-full overflow-y-auto">
        {viewMode === 'dashboard' ? (
          <UploadSection
            onUpload={handleUpload}
            onCreateSampleAcademic={handleCreateSampleAcademic}
            isLoading={isLoading}
            error={error}
            jobStatus={jobStatus}
            extractedBlocks={extractedBlocks}
            onOpenViewer={() => setViewMode('viewer')}
          />
        ) : (
          currentJobId && (
            <SideBySideViewer
              originalPdfUrl={jobsApi.getOriginalPdfUrl(currentJobId)}
              translatedPdfUrl={jobsApi.getTranslatedPdfUrl(currentJobId)}
              blocks={extractedBlocks}
              jobFileName={jobStatus?.fileName}
            />
          )
        )}
      </main>
    </div>
  );
}

export default App;
