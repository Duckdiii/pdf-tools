import React, { useState, useEffect } from 'react';
import { jobsApi } from './services/api';
import { CreateJobResponse, JobStatusResponse, JobStatus } from './types/job.types';
import { 
  FileUp, 
  FileText, 
  CheckCircle2, 
  AlertCircle, 
  RefreshCw, 
  Languages, 
  Sparkles, 
  Download, 
  ExternalLink,
  Clock,
  Activity,
  Layers,
  ChevronDown,
  ChevronUp
} from 'lucide-react';

const STEPS: { key: JobStatus; label: string }[] = [
  { key: 'Pending', label: '1. Hàng đợi' },
  { key: 'Extracting', label: '2. Trích xuất Text' },
  { key: 'Translating', label: '3. Dịch thuật AI' },
  { key: 'Rebuilding', label: '4. Tái tạo PDF' },
  { key: 'Completed', label: '5. Hoàn tất' },
];

function getStepIndex(status: JobStatus): number {
  switch (status) {
    case 'Pending': return 0;
    case 'Extracting': return 1;
    case 'Translating': return 2;
    case 'Rebuilding': return 3;
    case 'Completed': return 4;
    case 'Failed': return -1;
    default: return 0;
  }
}

export function App() {
  const [file, setFile] = useState<File | null>(null);
  const [sourceLanguage, setSourceLanguage] = useState('auto');
  const [targetLanguage, setTargetLanguage] = useState('vi');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentJob, setCurrentJob] = useState<CreateJobResponse | null>(null);
  const [jobStatus, setJobStatus] = useState<JobStatusResponse | null>(null);
  const [isPolling, setIsPolling] = useState(false);
  const [extractedBlocks, setExtractedBlocks] = useState<any[]>([]);
  const [showBlocks, setShowBlocks] = useState(false);
  const [showHistory, setShowHistory] = useState(true);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      const selectedFile = e.target.files[0];
      if (!selectedFile.name.toLowerCase().endsWith('.pdf')) {
        setError('Chỉ chấp nhận file định dạng .pdf!');
        setFile(null);
        return;
      }
      setFile(selectedFile);
      setError(null);
    }
  };

  const handleUpload = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) {
      setError('Vui lòng chọn một file PDF!');
      return;
    }

    try {
      setIsLoading(true);
      setError(null);
      setJobStatus(null);
      setExtractedBlocks([]);
      
      const res = await jobsApi.createJob(file, targetLanguage, sourceLanguage);
      setCurrentJob(res);
      setIsPolling(true);

      // Tra cứu trạng thái lần đầu ngay lập tức
      const initialStatus = await jobsApi.getJobStatus(res.jobId);
      setJobStatus(initialStatus);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi tải lên file.');
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
          // Tự động tải danh sách blocks để preview khi xong
          if (res.status === 'Completed') {
            try {
              const blocksData = await jobsApi.extractJobContent(currentJob.jobId);
              if (isMounted) setExtractedBlocks(blocksData.blocks || []);
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

  const handleManualRefresh = async () => {
    if (!currentJob) return;
    try {
      const res = await jobsApi.getJobStatus(currentJob.jobId);
      setJobStatus(res);
      if (res.status === 'Completed') {
        const blocksData = await jobsApi.extractJobContent(currentJob.jobId);
        setExtractedBlocks(blocksData.blocks || []);
      }
    } catch (err: any) {
      setError('Không thể cập nhật trạng thái.');
    }
  };

  const currentStepIdx = jobStatus ? getStepIndex(jobStatus.status) : -1;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
      <header style={{ textAlign: 'center' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.5rem', color: '#38bdf8' }}>PDF Translator Tools</h1>
        <p style={{ color: '#94a3b8' }}>
          Hệ thống dịch thuật tài liệu PDF tự động hoá với <span style={{ color: '#a78bfa', fontWeight: 600 }}>Hangfire Background Job</span>
        </p>
      </header>

      {/* Upload Form Card */}
      <div className="card">
        <form onSubmit={handleUpload} style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          <label className="upload-zone" htmlFor="pdf-input">
            <input
              id="pdf-input"
              type="file"
              accept=".pdf"
              onChange={handleFileChange}
              style={{ display: 'none' }}
            />
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.75rem' }}>
              <FileUp size={48} color="#38bdf8" />
              {file ? (
                <div>
                  <p style={{ fontWeight: 600, color: '#f8fafc', fontSize: '1.1rem' }}>{file.name}</p>
                  <p style={{ color: '#64748b', fontSize: '0.875rem' }}>
                    {(file.size / (1024 * 1024)).toFixed(2)} MB
                  </p>
                </div>
              ) : (
                <div>
                  <p style={{ fontWeight: 500, color: '#e2e8f0' }}>Bấm để chọn file PDF hoặc kéo thả vào đây</p>
                  <p style={{ color: '#64748b', fontSize: '0.875rem' }}>Hỗ trợ file .pdf mọi kích thước</p>
                </div>
              )}
            </div>
          </label>

          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
            <div>
              <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', color: '#94a3b8' }}>
                <Languages size={16} style={{ display: 'inline', marginRight: '4px', verticalAlign: 'middle' }} />
                Ngôn ngữ gốc:
              </label>
              <select
                value={sourceLanguage}
                onChange={(e) => setSourceLanguage(e.target.value)}
                style={{
                  width: '100%',
                  padding: '0.6rem',
                  borderRadius: '6px',
                  background: '#0f172a',
                  color: '#fff',
                  border: '1px solid #334155',
                }}
              >
                <option value="auto">Tự động phát hiện</option>
                <option value="en">Tiếng Anh</option>
                <option value="vi">Tiếng Việt</option>
                <option value="zh">Tiếng Trung</option>
                <option value="ja">Tiếng Nhật</option>
                <option value="ko">Tiếng Hàn</option>
              </select>
            </div>

            <div>
              <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', color: '#94a3b8' }}>
                <Languages size={16} style={{ display: 'inline', marginRight: '4px', verticalAlign: 'middle' }} />
                Ngôn ngữ đích:
              </label>
              <select
                value={targetLanguage}
                onChange={(e) => setTargetLanguage(e.target.value)}
                style={{
                  width: '100%',
                  padding: '0.6rem',
                  borderRadius: '6px',
                  background: '#0f172a',
                  color: '#fff',
                  border: '1px solid #334155',
                }}
              >
                <option value="vi">Tiếng Việt</option>
                <option value="en">Tiếng Anh</option>
                <option value="zh">Tiếng Trung</option>
                <option value="ja">Tiếng Nhật</option>
                <option value="ko">Tiếng Hàn</option>
              </select>
            </div>
          </div>

          {error && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#f87171', background: '#450a0a', padding: '0.75rem', borderRadius: '6px' }}>
              <AlertCircle size={20} />
              <span>{error}</span>
            </div>
          )}

          <button
            type="submit"
            disabled={!file || isLoading}
            style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem', padding: '0.8rem' }}
          >
            {isLoading ? <RefreshCw size={20} className="spin" /> : <Sparkles size={20} />}
            {isLoading ? 'Đang khởi tạo job...' : 'Tải lên & Dịch Tự Động (Background Job)'}
          </button>
        </form>
      </div>

      {/* Background Job Progress & Status Section */}
      {currentJob && jobStatus && (
        <div className="card" style={{ borderLeft: '4px solid #38bdf8' }}>
          {/* Header Bar */}
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.25rem', flexWrap: 'wrap', gap: '0.75rem' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <Activity size={24} color="#38bdf8" />
              <div>
                <h2 style={{ fontSize: '1.25rem', color: '#38bdf8', margin: 0 }}>
                  Tiến độ Xử lý Ngầm (Hangfire Job)
                </h2>
                <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
                  {jobStatus.fileName} &bull; ID: {jobStatus.jobId}
                </span>
              </div>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
              <a
                href="http://localhost:5210/hangfire"
                target="_blank"
                rel="noopener noreferrer"
                style={{
                  background: '#334155',
                  color: '#38bdf8',
                  padding: '0.4rem 0.8rem',
                  borderRadius: '6px',
                  textDecoration: 'none',
                  fontSize: '0.85rem',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '0.4rem',
                }}
              >
                <ExternalLink size={14} />
                Hangfire Dashboard
              </a>

              <button
                onClick={handleManualRefresh}
                style={{ background: '#1e293b', border: '1px solid #475569', padding: '0.4rem 0.8rem', fontSize: '0.85rem', display: 'inline-flex', alignItems: 'center', gap: '0.4rem' }}
              >
                <RefreshCw size={14} className={isPolling ? 'spin' : ''} />
                {isPolling ? 'Đang cập nhật...' : 'Làm mới'}
              </button>
            </div>
          </div>

          {/* Progress Bar & Percentage */}
          <div style={{ background: '#0f172a', padding: '1.25rem', borderRadius: '8px', marginBottom: '1.5rem', border: '1px solid #334155' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '0.5rem' }}>
              <span style={{ fontWeight: 600, color: '#f8fafc', fontSize: '1rem' }}>
                {jobStatus.currentStep || 'Đang xử lý...'}
              </span>
              <span style={{ fontWeight: 700, color: '#38bdf8', fontSize: '1.25rem' }}>
                {jobStatus.progressPercent}%
              </span>
            </div>

            <div className="progress-container">
              <div 
                className="progress-fill" 
                style={{ 
                  width: `${jobStatus.progressPercent}%`,
                  backgroundColor: jobStatus.status === 'Failed' ? '#ef4444' : undefined 
                }} 
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '0.75rem', fontSize: '0.85rem', color: '#94a3b8' }}>
              <span>Trạng thái: <strong className={`status-badge status-${jobStatus.status.toLowerCase()}`}>{jobStatus.status}</strong></span>
              {jobStatus.totalBlocks > 0 && (
                <span>Đã dịch: <strong>{jobStatus.translatedBlocks} / {jobStatus.totalBlocks}</strong> khối văn bản</span>
              )}
            </div>
          </div>

          {/* Step Pipeline Visualization */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 1fr))', gap: '0.5rem', marginBottom: '1.5rem' }}>
            {STEPS.map((step, idx) => {
              const isPast = currentStepIdx > idx || jobStatus.status === 'Completed';
              const isCurrent = currentStepIdx === idx && jobStatus.status !== 'Completed';
              const isFail = jobStatus.status === 'Failed' && currentStepIdx === idx;

              let bg = '#0f172a';
              let borderColor = '#334155';
              let textColor = '#64748b';

              if (isPast) {
                bg = '#064e3b';
                borderColor = '#10b981';
                textColor = '#34d399';
              } else if (isCurrent) {
                bg = '#1e3a8a';
                borderColor = '#38bdf8';
                textColor = '#bae6fd';
              } else if (isFail) {
                bg = '#450a0a';
                borderColor = '#ef4444';
                textColor = '#f87171';
              }

              return (
                <div
                  key={step.key}
                  style={{
                    backgroundColor: bg,
                    border: `1px solid ${borderColor}`,
                    padding: '0.6rem 0.5rem',
                    borderRadius: '6px',
                    textAlign: 'center',
                    fontSize: '0.8rem',
                    fontWeight: isCurrent || isPast ? 600 : 400,
                    color: textColor,
                    transition: 'all 0.3s ease',
                  }}
                >
                  {isCurrent && <RefreshCw size={12} className="spin" style={{ display: 'inline', marginRight: '4px' }} />}
                  {isPast && <CheckCircle2 size={12} style={{ display: 'inline', marginRight: '4px' }} />}
                  {step.label}
                </div>
              );
            })}
          </div>

          {/* Error Message if Failed */}
          {jobStatus.status === 'Failed' && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#f87171', background: '#450a0a', padding: '1rem', borderRadius: '8px', marginBottom: '1.5rem' }}>
              <AlertCircle size={24} />
              <div>
                <p style={{ fontWeight: 600, margin: 0 }}>Xử lý ngầm thất bại</p>
                <p style={{ fontSize: '0.875rem', margin: 0 }}>{jobStatus.errorMessage || 'Vui lòng kiểm tra lại log hệ thống.'}</p>
              </div>
            </div>
          )}

          {/* Output Actions when Completed */}
          {jobStatus.status === 'Completed' && (
            <div style={{ background: '#022c22', border: '1px solid #059669', padding: '1.25rem', borderRadius: '8px', marginBottom: '1.5rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#34d399', marginBottom: '1rem' }}>
                <CheckCircle2 size={24} color="#34d399" />
                <h3 style={{ margin: 0, fontSize: '1.1rem' }}>Tài liệu đã được dịch và tái tạo thành công!</h3>
              </div>

              <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap' }}>
                <a
                  href={jobsApi.getTranslatedPdfUrl(jobStatus.jobId)}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{
                    backgroundColor: '#0284c7',
                    color: 'white',
                    padding: '0.7rem 1.4rem',
                    borderRadius: '8px',
                    textDecoration: 'none',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                    fontWeight: 600,
                    fontSize: '0.95rem'
                  }}
                >
                  <ExternalLink size={18} />
                  Xem PDF Tiếng Việt Trực Tiếp
                </a>

                <a
                  href={jobsApi.getDownloadUrl(jobStatus.jobId)}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{
                    backgroundColor: '#059669',
                    color: 'white',
                    padding: '0.7rem 1.4rem',
                    borderRadius: '8px',
                    textDecoration: 'none',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                    fontWeight: 600,
                    fontSize: '0.95rem'
                  }}
                >
                  <Download size={18} />
                  Tải File PDF Tiếng Việt (.pdf)
                </a>

                <a
                  href={`http://localhost:5210/api/jobs/${jobStatus.jobId}/debug-pdf`}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{
                    backgroundColor: '#334155',
                    color: '#e2e8f0',
                    padding: '0.7rem 1.2rem',
                    borderRadius: '8px',
                    textDecoration: 'none',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                    fontWeight: 500,
                    fontSize: '0.95rem'
                  }}
                >
                  <FileText size={18} />
                  Xem PDF Debug (Khung đỏ)
                </a>
              </div>
            </div>
          )}

          {/* Job Status History Timeline */}
          {jobStatus.history && jobStatus.history.length > 0 && (
            <div style={{ marginTop: '1rem' }}>
              <div 
                onClick={() => setShowHistory(!showHistory)}
                style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', cursor: 'pointer', padding: '0.5rem 0', color: '#94a3b8' }}
              >
                <span style={{ fontSize: '0.95rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#e2e8f0' }}>
                  <Clock size={16} color="#38bdf8" />
                  Lịch sử chuyển đổi trạng thái ({jobStatus.history.length} sự kiện):
                </span>
                {showHistory ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </div>

              {showHistory && (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', marginTop: '0.5rem' }}>
                  {jobStatus.history.map((h) => (
                    <div
                      key={h.id}
                      style={{
                        background: '#0f172a',
                        border: '1px solid #1e293b',
                        padding: '0.6rem 0.9rem',
                        borderRadius: '6px',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        fontSize: '0.85rem',
                        gap: '0.5rem'
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <span style={{ color: '#38bdf8', fontWeight: 600 }}>
                          {h.fromStatus ? `${h.fromStatus} ➔ ` : 'Bắt đầu ➔ '}
                          <span style={{ color: '#34d399' }}>{h.toStatus}</span>
                        </span>
                        <span style={{ color: '#cbd5e1' }}>&bull; {h.message}</span>
                      </div>
                      <span style={{ color: '#64748b', fontSize: '0.75rem', whiteSpace: 'nowrap' }}>
                        {new Date(h.changedAt).toLocaleTimeString('vi-VN')}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Extracted & Translated Blocks Preview Toggle */}
          {extractedBlocks.length > 0 && (
            <div style={{ marginTop: '1.5rem' }}>
              <div 
                onClick={() => setShowBlocks(!showBlocks)}
                style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', cursor: 'pointer', padding: '0.5rem 0', color: '#94a3b8' }}
              >
                <span style={{ fontSize: '0.95rem', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#e2e8f0' }}>
                  <Layers size={16} color="#38bdf8" />
                  Xem chi tiết các khối văn bản đã trích xuất & dịch ({extractedBlocks.length} blocks):
                </span>
                {showBlocks ? <ChevronUp size={18} /> : <ChevronDown size={18} />}
              </div>

              {showBlocks && (
                <div style={{ maxHeight: '350px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '0.5rem', marginTop: '0.5rem' }}>
                  {extractedBlocks.map((b: any, idx: number) => (
                    <div
                      key={idx}
                      style={{
                        background: '#0f172a',
                        padding: '0.75rem 1rem',
                        borderRadius: '6px',
                        border: '1px solid #334155',
                        fontSize: '0.9rem'
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', color: '#38bdf8', marginBottom: '0.25rem', fontSize: '0.8rem' }}>
                        <span><strong>Trang {b.pageIndex}</strong> | Khối #{b.orderIndex} ({b.blockType})</span>
                        <span>Font: <strong>{b.boundingBox?.fontName}</strong> ({b.boundingBox?.fontSize}pt)</span>
                      </div>
                      <p style={{ color: '#f1f5f9', fontWeight: 500, margin: '0.25rem 0' }}>"{b.text || b.originalText}"</p>
                      {b.translatedText && (
                        <p style={{ color: '#34d399', fontWeight: 500, margin: '0.25rem 0', borderLeft: '3px solid #10b981', paddingLeft: '0.5rem' }}>
                          Dịch: "{b.translatedText}"
                        </p>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default App;
