import React, { useState } from 'react';
import { jobsApi } from './services/api';
import { CreateJobResponse, JobDetailResponse } from './types/job.types';
import { FileUp, FileText, CheckCircle2, AlertCircle, RefreshCw, Languages } from 'lucide-react';

export function App() {
  const [file, setFile] = useState<File | null>(null);
  const [sourceLanguage, setSourceLanguage] = useState('auto');
  const [targetLanguage, setTargetLanguage] = useState('vi');
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [currentJob, setCurrentJob] = useState<CreateJobResponse | null>(null);
  const [jobDetail, setJobDetail] = useState<JobDetailResponse | null>(null);
  const [extractedBlocks, setExtractedBlocks] = useState<any[]>([]);
  const [isExtracting, setIsExtracting] = useState(false);

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
      const res = await jobsApi.createJob(file, targetLanguage, sourceLanguage);
      setCurrentJob(res);
      // Lấy chi tiết ngay sau khi tạo
      const detail = await jobsApi.getJobById(res.jobId);
      setJobDetail(detail);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Có lỗi xảy ra khi tải lên file.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefreshStatus = async () => {
    if (!currentJob) return;
    try {
      setIsLoading(true);
      const detail = await jobsApi.getJobById(currentJob.jobId);
      setJobDetail(detail);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Không thể cập nhật trạng thái.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleExtract = async () => {
    if (!currentJob) return;
    try {
      setIsExtracting(true);
      setError(null);
      const data = await jobsApi.extractJobContent(currentJob.jobId);
      setExtractedBlocks(data.blocks || []);
      const detail = await jobsApi.getJobById(currentJob.jobId);
      setJobDetail(detail);
    } catch (err: any) {
      setError(err.response?.data?.message || 'Lỗi khi trích xuất PDF.');
    } finally {
      setIsExtracting(false);
    }
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '2rem' }}>
      <header style={{ textAlign: 'center' }}>
        <h1 style={{ fontSize: '2.5rem', marginBottom: '0.5rem', color: '#38bdf8' }}>PDF Translator Tools</h1>
        <p style={{ color: '#94a3b8' }}>Nền tảng dịch và xử lý tài liệu PDF thông minh</p>
      </header>

      <div className="card">
        <form onSubmit={handleUpload} style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          {/* File Upload Zone */}
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
                  <p style={{ color: '#64748b', fontSize: '0.875rem' }}>Hỗ trợ file .pdf</p>
                </div>
              )}
            </div>
          </label>

          {/* Languages Options */}
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
            {isLoading ? <RefreshCw size={20} className="spin" /> : <FileText size={20} />}
            {isLoading ? 'Đang xử lý tải lên...' : 'Bắt đầu Dịch PDF'}
          </button>
        </form>
      </div>

      {/* Result Section */}
      {currentJob && (
        <div className="card" style={{ borderLeft: '4px solid #38bdf8' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
            <h2 style={{ fontSize: '1.25rem', display: 'flex', alignItems: 'center', gap: '0.5rem', color: '#38bdf8' }}>
              <CheckCircle2 size={24} color="#38bdf8" />
              Thông tin Job dịch
            </h2>
            <button
              onClick={handleRefreshStatus}
              disabled={isLoading}
              style={{ background: '#334155', padding: '0.4rem 0.8rem', fontSize: '0.875rem' }}
            >
              Làm mới trạng thái
            </button>
          </div>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', background: '#0f172a', padding: '1rem', borderRadius: '8px' }}>
            <div>
              <p style={{ color: '#64748b', fontSize: '0.875rem' }}>Job ID</p>
              <p style={{ fontFamily: 'monospace', color: '#e2e8f0', wordBreak: 'break-all' }}>{currentJob.jobId}</p>
            </div>
            <div>
              <p style={{ color: '#64748b', fontSize: '0.875rem' }}>File Name</p>
              <p style={{ fontWeight: 500 }}>{currentJob.fileName}</p>
            </div>
            <div>
              <p style={{ color: '#64748b', fontSize: '0.875rem' }}>Trạng thái</p>
              <span className={`status-badge status-${(jobDetail?.status || currentJob.status).toLowerCase()}`}>
                {jobDetail?.status || currentJob.status}
              </span>
            </div>
            <div>
              <p style={{ color: '#64748b', fontSize: '0.875rem' }}>Cặp ngôn ngữ</p>
              <p>{currentJob.sourceLanguage} &rarr; {currentJob.targetLanguage}</p>
            </div>
          </div>

          {/* Action: Tuần 2 Extract PDF Content & Debug PDF */}
          <div style={{ marginTop: '1.5rem', display: 'flex', gap: '1rem', alignItems: 'center', flexWrap: 'wrap' }}>
            <button
              onClick={handleExtract}
              disabled={isExtracting}
              style={{
                backgroundColor: '#10b981',
                padding: '0.6rem 1.2rem',
                display: 'flex',
                alignItems: 'center',
                gap: '0.5rem',
                fontWeight: 600
              }}
            >
              {isExtracting ? <RefreshCw size={18} className="spin" /> : <FileText size={18} />}
              {isExtracting ? 'Đang trích xuất với iText7...' : 'Trích xuất Text Block (iText7)'}
            </button>

            <a
              href={`http://localhost:5210/api/jobs/${currentJob.jobId}/debug-pdf`}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                backgroundColor: '#ef4444',
                color: 'white',
                padding: '0.6rem 1.2rem',
                borderRadius: '8px',
                textDecoration: 'none',
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.5rem',
                fontWeight: 600,
                fontSize: '1em'
              }}
            >
              <FileText size={18} />
              Xem PDF Debug (Khung đỏ)
            </a>

            <span style={{ color: '#94a3b8', fontSize: '0.875rem' }}>
              {extractedBlocks.length > 0 ? `Đã bóc tách thành công ${extractedBlocks.length} khối văn bản!` : 'Bấm để bóc tách text kèm tọa độ và font.'}
            </span>
          </div>

          {/* Render List of Extracted Blocks */}
          {extractedBlocks.length > 0 && (
            <div style={{ marginTop: '1.5rem', display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
              <h3 style={{ fontSize: '1.1rem', color: '#f8fafc' }}>
                Danh sách Text Blocks đã trích xuất:
              </h3>
              <div style={{ maxHeight: '350px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
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
                    <p style={{ color: '#f1f5f9', fontWeight: 500, margin: '0.25rem 0' }}>"{b.text}"</p>
                    <div style={{ color: '#64748b', fontSize: '0.75rem', fontFamily: 'monospace' }}>
                      BoundingBox: X={b.boundingBox?.x}, Y={b.boundingBox?.y}, W={b.boundingBox?.width}, H={b.boundingBox?.height}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

export default App;
