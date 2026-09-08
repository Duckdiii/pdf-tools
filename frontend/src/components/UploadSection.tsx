import React, { useState } from 'react';
import { 
  FileUp, 
  FileText, 
  Sparkles, 
  RefreshCw, 
  AlertCircle, 
  CheckCircle2, 
  Sigma, 
  Image as ImageIcon,
  Clock, 
  ChevronDown, 
  ChevronUp, 
  Layers,
  ArrowRight
} from 'lucide-react';
import { JobStatus, JobStatusResponse } from '../types/job.types';

interface UploadSectionProps {
  onUpload: (file: File, targetLanguage: string, sourceLanguage: string) => Promise<void>;
  onCreateSampleAcademic: () => Promise<void>;
  isLoading: boolean;
  error: string | null;
  jobStatus: JobStatusResponse | null;
  extractedBlocks: any[];
  onOpenViewer: () => void;
}

const STEPS: { key: JobStatus; label: string }[] = [
  { key: 'Pending', label: '1. Hàng đợi' },
  { key: 'Extracting', label: '2. Bóc tách & Phân loại' },
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

export const UploadSection: React.FC<UploadSectionProps> = ({
  onUpload,
  onCreateSampleAcademic,
  isLoading,
  error,
  jobStatus,
  extractedBlocks,
  onOpenViewer
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [sourceLanguage, setSourceLanguage] = useState('auto');
  const [targetLanguage, setTargetLanguage] = useState('vi');
  const [isDragOver, setIsDragOver] = useState(false);
  const [showHistory, setShowHistory] = useState(false);
  const [filterBlockType, setFilterBlockType] = useState<string>('ALL');

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      const selected = e.target.files[0];
      if (selected.name.toLowerCase().endsWith('.pdf')) {
        setFile(selected);
      }
    }
  };

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    if (e.dataTransfer.files && e.dataTransfer.files[0]) {
      const dropped = e.dataTransfer.files[0];
      if (dropped.name.toLowerCase().endsWith('.pdf')) {
        setFile(dropped);
      }
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!file) return;
    onUpload(file, targetLanguage, sourceLanguage);
  };

  const currentStepIdx = jobStatus ? getStepIndex(jobStatus.status) : -1;

  const textBlocksCount = extractedBlocks.filter((b: any) => b.blockType === 'TEXT').length;
  const formulaBlocksCount = extractedBlocks.filter((b: any) => b.blockType === 'FORMULA_TEXT').length;
  const imageBlocksCount = extractedBlocks.filter((b: any) => b.blockType === 'IMAGE').length;

  const filteredBlocks = extractedBlocks.filter((b: any) => {
    if (filterBlockType === 'ALL') return true;
    return b.blockType === filterBlockType;
  });

  return (
    <div className="max-w-5xl mx-auto px-4 py-8 space-y-8">
      {/* Hero Header */}
      <div className="text-center space-y-3">
        <h2 className="text-3xl md:text-4xl font-extrabold text-transparent bg-clip-text bg-gradient-to-r from-sky-400 via-indigo-300 to-purple-400">
          Dịch Thuật PDF Giữ Nguyên Layout Chuyên Nghiệp
        </h2>
        <p className="text-slate-400 text-sm md:text-base max-w-2xl mx-auto">
          Tự động nhận diện văn bản, bảo toàn <strong className="text-purple-400">công thức toán LaTeX</strong> và <strong className="text-emerald-400">hình ảnh raster/vector</strong> với tiến trình xử lý ngầm Hangfire.
        </p>
      </div>

      {/* Grid Form & Quick Test */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Upload Form (Left Column: 7 cols) */}
        <div className="lg:col-span-7 bg-slate-900/80 backdrop-blur border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Drag & Drop Zone */}
            <div
              onDragOver={(e) => { e.preventDefault(); setIsDragOver(true); }}
              onDragLeave={() => setIsDragOver(false)}
              onDrop={handleDrop}
              className={`border-2 border-dashed rounded-xl p-6 text-center cursor-pointer transition-all ${
                isDragOver
                  ? 'border-sky-400 bg-sky-500/10'
                  : file
                  ? 'border-emerald-500/60 bg-emerald-500/5'
                  : 'border-slate-700 bg-slate-800/40 hover:border-slate-600 hover:bg-slate-800/80'
              }`}
            >
              <label htmlFor="file-input" className="cursor-pointer block">
                <input
                  id="file-input"
                  type="file"
                  accept=".pdf"
                  onChange={handleFileChange}
                  className="hidden"
                />
                <div className="flex flex-col items-center gap-3">
                  <div className={`w-14 h-14 rounded-2xl flex items-center justify-center transition-colors ${
                    file ? 'bg-emerald-500/20 text-emerald-400' : 'bg-sky-500/10 text-sky-400'
                  }`}>
                    {file ? <FileText className="w-7 h-7" /> : <FileUp className="w-7 h-7" />}
                  </div>
                  {file ? (
                    <div>
                      <p className="text-slate-100 font-semibold text-base truncate max-w-sm">{file.name}</p>
                      <p className="text-slate-400 text-xs mt-0.5">
                        {(file.size / (1024 * 1024)).toFixed(2)} MB &bull; Bấm để thay đổi file
                      </p>
                    </div>
                  ) : (
                    <div>
                      <p className="text-slate-200 font-medium text-sm">
                        Kéo thả file PDF vào đây, hoặc <span className="text-sky-400 underline">chọn file</span>
                      </p>
                      <p className="text-slate-500 text-xs mt-1">Hỗ trợ tài liệu học thuật, báo cáo, bài báo nghiên cứu (.pdf)</p>
                    </div>
                  )}
                </div>
              </label>
            </div>

            {/* Language Selectors */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                  Ngôn ngữ nguồn
                </label>
                <select
                  value={sourceLanguage}
                  onChange={(e) => setSourceLanguage(e.target.value)}
                  className="w-full bg-slate-800 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-slate-200 focus:outline-none focus:border-sky-500 transition"
                >
                  <option value="auto">Tự động phát hiện</option>
                  <option value="en">Tiếng Anh (English)</option>
                  <option value="vi">Tiếng Việt</option>
                  <option value="zh">Tiếng Trung (中文)</option>
                  <option value="ja">Tiếng Nhật (日本語)</option>
                  <option value="ko">Tiếng Hàn (한국어)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
                  Ngôn ngữ dịch
                </label>
                <select
                  value={targetLanguage}
                  onChange={(e) => setTargetLanguage(e.target.value)}
                  className="w-full bg-slate-800 border border-slate-700 rounded-xl px-3 py-2.5 text-sm text-slate-200 focus:outline-none focus:border-sky-500 transition"
                >
                  <option value="vi">Tiếng Việt</option>
                  <option value="en">Tiếng Anh</option>
                  <option value="zh">Tiếng Trung</option>
                  <option value="ja">Tiếng Nhật</option>
                  <option value="ko">Tiếng Hàn</option>
                </select>
              </div>
            </div>

            {/* Error Message */}
            {error && (
              <div className="flex items-center gap-2 p-3.5 rounded-xl bg-red-950/50 border border-red-800/80 text-red-300 text-sm">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {/* Upload Button */}
            <button
              type="submit"
              disabled={!file || isLoading}
              className="w-full py-3 px-4 rounded-xl font-semibold text-sm bg-gradient-to-r from-sky-600 to-indigo-600 hover:from-sky-500 hover:to-indigo-500 text-white shadow-lg shadow-sky-600/25 transition flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed active:scale-[0.99]"
            >
              {isLoading ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  <span>Đang khởi tạo Job...</span>
                </>
              ) : (
                <>
                  <Sparkles className="w-4 h-4" />
                  <span>Tải lên & Dịch Tự Động (Hangfire Job)</span>
                </>
              )}
            </button>
          </form>
        </div>

        {/* Quick Academic Test Card (Right Column: 5 cols) */}
        <div className="lg:col-span-5 bg-gradient-to-b from-indigo-950/40 via-purple-950/20 to-slate-900 border border-indigo-900/40 rounded-2xl p-6 shadow-xl flex flex-col justify-between space-y-6">
          <div className="space-y-3">
            <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-purple-500/10 border border-purple-500/20 text-purple-300 text-xs font-semibold">
              <Sigma className="w-3.5 h-3.5" />
              <span>Kiểm thử Tuần 6</span>
            </div>
            <h3 className="text-lg font-bold text-white">
              Tài Liệu Học Thuật Mẫu
            </h3>
            <p className="text-slate-400 text-xs leading-relaxed">
              Tạo ngay một bài báo nghiên cứu khoa học mẫu (*Deep Neural Network Optimization*) chứa 2 công thức display LaTeX ($L(\theta)$, $g_t$), 1 hình ảnh XObject hàm loss và các đoạn văn bản lý thuyết để kiểm nghiệm:
            </p>
            <ul className="text-xs space-y-2 text-slate-300">
              <li className="flex items-center gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-purple-400" />
                <span>Heuristic phân loại công thức toán học</span>
              </li>
              <li className="flex items-center gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400" />
                <span>Bắt toán tử `Do` bảo toàn hình ảnh XObject</span>
              </li>
              <li className="flex items-center gap-2">
                <span className="w-1.5 h-1.5 rounded-full bg-sky-400" />
                <span>Dịch chuẩn xác các đoạn văn bản xung quanh</span>
              </li>
            </ul>
          </div>

          <button
            type="button"
            onClick={onCreateSampleAcademic}
            disabled={isLoading}
            className="w-full py-3 px-4 rounded-xl font-semibold text-xs md:text-sm bg-gradient-to-r from-purple-600 to-indigo-600 hover:from-purple-500 hover:to-indigo-500 text-white shadow-lg shadow-purple-600/30 transition flex items-center justify-center gap-2 disabled:opacity-50 active:scale-[0.99]"
          >
            <Sigma className="w-4 h-4" />
            <span>Thử Nghiệm PDF Học Thuật (1-Click)</span>
          </button>
        </div>
      </div>

      {/* Progress & Results Section (When Job exists) */}
      {jobStatus && (
        <div className="bg-slate-900/90 border border-slate-800 rounded-2xl p-6 shadow-2xl space-y-6">
          {/* Progress Header */}
          <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-800 pb-5">
            <div className="space-y-1">
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold uppercase tracking-wider text-slate-500">Tiến trình xử lý</span>
                <span className={`px-2 py-0.5 rounded-full text-xs font-bold ${
                  jobStatus.status === 'Completed' ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20' :
                  jobStatus.status === 'Failed' ? 'bg-red-500/10 text-red-400 border border-red-500/20' :
                  'bg-sky-500/10 text-sky-400 border border-sky-500/20'
                }`}>
                  {jobStatus.status}
                </span>
              </div>
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                {jobStatus.fileName}
              </h3>
              <p className="text-xs text-slate-400">{jobStatus.currentStep}</p>
            </div>

            {/* If completed, show CTA to open side-by-side viewer */}
            {jobStatus.status === 'Completed' && (
              <button
                onClick={onOpenViewer}
                className="px-5 py-2.5 rounded-xl font-bold text-sm bg-gradient-to-r from-sky-500 to-indigo-600 hover:from-sky-400 hover:to-indigo-500 text-white shadow-lg shadow-sky-500/25 flex items-center gap-2 transition active:scale-95"
              >
                <span>Mở Trình Xem Đối Chiếu Song Song</span>
                <ArrowRight className="w-4 h-4" />
              </button>
            )}
          </div>

          {/* Progress Bar */}
          <div className="space-y-2">
            <div className="flex justify-between text-xs font-semibold">
              <span className="text-slate-400">Tiến độ tổng thể</span>
              <span className="text-sky-400">{jobStatus.progressPercent}%</span>
            </div>
            <div className="w-full h-3 bg-slate-800 rounded-full overflow-hidden p-0.5 border border-slate-700/50">
              <div
                className={`h-full rounded-full transition-all duration-500 ${
                  jobStatus.status === 'Failed'
                    ? 'bg-red-500'
                    : 'bg-gradient-to-r from-sky-500 via-indigo-500 to-emerald-400'
                }`}
                style={{ width: `${jobStatus.progressPercent}%` }}
              />
            </div>
          </div>

          {/* 5-Step Pipeline Badges */}
          <div className="grid grid-cols-2 sm:grid-cols-5 gap-2">
            {STEPS.map((step, idx) => {
              const isPast = currentStepIdx > idx || jobStatus.status === 'Completed';
              const isCurrent = currentStepIdx === idx && jobStatus.status !== 'Completed';
              const isFailed = jobStatus.status === 'Failed' && currentStepIdx === idx;

              return (
                <div
                  key={step.key}
                  className={`p-3 rounded-xl border text-center text-xs font-semibold transition-all ${
                    isPast
                      ? 'bg-emerald-950/30 border-emerald-600/40 text-emerald-300'
                      : isCurrent
                      ? 'bg-sky-950/40 border-sky-500/60 text-sky-200 shadow-md shadow-sky-950'
                      : isFailed
                      ? 'bg-red-950/40 border-red-600/50 text-red-300'
                      : 'bg-slate-800/40 border-slate-800 text-slate-500'
                  }`}
                >
                  <div className="flex items-center justify-center gap-1.5">
                    {isCurrent && <RefreshCw className="w-3.5 h-3.5 animate-spin text-sky-400" />}
                    {isPast && <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />}
                    <span className="truncate">{step.label}</span>
                  </div>
                </div>
              );
            })}
          </div>

          {/* History Accordion */}
          {jobStatus.history && jobStatus.history.length > 0 && (
            <div className="border border-slate-800/80 rounded-xl overflow-hidden">
              <button
                type="button"
                onClick={() => setShowHistory(!showHistory)}
                className="w-full px-4 py-2.5 bg-slate-800/30 hover:bg-slate-800/50 flex items-center justify-between text-xs font-semibold text-slate-300 transition"
              >
                <span className="flex items-center gap-2">
                  <Clock className="w-3.5 h-3.5 text-sky-400" />
                  Lịch sử chuyển đổi trạng thái ({jobStatus.history.length} sự kiện)
                </span>
                {showHistory ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
              </button>

              {showHistory && (
                <div className="p-3 space-y-1.5 bg-slate-950/40 divide-y divide-slate-800/40 max-h-48 overflow-y-auto text-xs">
                  {jobStatus.history.map((h) => (
                    <div key={h.id} className="pt-2 first:pt-0 flex items-center justify-between text-slate-300">
                      <div className="flex items-center gap-2">
                        <span className="font-semibold text-sky-400">
                          {h.fromStatus ? `${h.fromStatus} ➔ ` : 'Bắt đầu ➔ '}
                          <span className="text-emerald-400">{h.toStatus}</span>
                        </span>
                        <span className="text-slate-400">&bull; {h.message}</span>
                      </div>
                      <span className="text-slate-500 text-[10px]">
                        {new Date(h.changedAt).toLocaleTimeString('vi-VN')}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Extracted Blocks Classification Preview */}
          {extractedBlocks.length > 0 && (
            <div className="border border-slate-800/80 rounded-xl overflow-hidden space-y-3 p-4 bg-slate-950/30">
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div className="flex items-center gap-2">
                  <Layers className="w-4 h-4 text-sky-400" />
                  <span className="text-xs font-bold text-slate-200">
                    Bóc tách & Phân loại ({extractedBlocks.length} khối):
                  </span>
                </div>

                {/* Filter Tabs */}
                <div className="flex items-center gap-1.5 text-xs">
                  <button
                    onClick={() => setFilterBlockType('ALL')}
                    className={`px-2.5 py-1 rounded-lg font-medium transition ${
                      filterBlockType === 'ALL' ? 'bg-slate-700 text-white' : 'text-slate-400 hover:text-slate-200'
                    }`}
                  >
                    Tất cả ({extractedBlocks.length})
                  </button>
                  <button
                    onClick={() => setFilterBlockType('TEXT')}
                    className={`px-2.5 py-1 rounded-lg font-medium transition ${
                      filterBlockType === 'TEXT' ? 'bg-sky-600 text-white' : 'text-sky-400 hover:bg-sky-950/40'
                    }`}
                  >
                    Văn bản ({textBlocksCount})
                  </button>
                  <button
                    onClick={() => setFilterBlockType('FORMULA_TEXT')}
                    className={`px-2.5 py-1 rounded-lg font-medium transition ${
                      filterBlockType === 'FORMULA_TEXT' ? 'bg-purple-600 text-white' : 'text-purple-400 hover:bg-purple-950/40'
                    }`}
                  >
                    Công thức ({formulaBlocksCount})
                  </button>
                  <button
                    onClick={() => setFilterBlockType('IMAGE')}
                    className={`px-2.5 py-1 rounded-lg font-medium transition ${
                      filterBlockType === 'IMAGE' ? 'bg-emerald-600 text-white' : 'text-emerald-400 hover:bg-emerald-950/40'
                    }`}
                  >
                    Hình ảnh ({imageBlocksCount})
                  </button>
                </div>
              </div>

              {/* Block List */}
              <div className="space-y-2 max-h-72 overflow-y-auto pr-1">
                {filteredBlocks.map((b: any, idx: number) => {
                  const isImage = b.blockType === 'IMAGE';
                  const isFormula = b.blockType === 'FORMULA_TEXT';

                  return (
                    <div
                      key={idx}
                      className={`p-3 rounded-xl border text-xs transition ${
                        isFormula
                          ? 'bg-purple-950/20 border-purple-800/40'
                          : isImage
                          ? 'bg-emerald-950/20 border-emerald-800/40'
                          : 'bg-slate-900 border-slate-800'
                      }`}
                    >
                      <div className="flex items-center justify-between mb-1.5">
                        <div className="flex items-center gap-2">
                          <span className="text-slate-400 font-mono">Trang {b.pageIndex} &bull; #{b.orderIndex}</span>
                          {isImage && (
                            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 font-bold border border-emerald-500/20">
                              <ImageIcon className="w-3 h-3" /> IMAGE (Bảo toàn)
                            </span>
                          )}
                          {isFormula && (
                            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-purple-500/10 text-purple-400 font-bold border border-purple-500/20">
                              <Sigma className="w-3 h-3" /> FORMULA (Bảo toàn)
                            </span>
                          )}
                          {!isImage && !isFormula && (
                            <span className="px-2 py-0.5 rounded-full bg-sky-500/10 text-sky-400 font-bold border border-sky-500/20">
                              TEXT (Dịch AI)
                            </span>
                          )}
                        </div>

                        {b.boundingBox && (
                          <span className="text-slate-500 font-mono text-[11px]">
                            {Math.round(b.boundingBox.width)}×{Math.round(b.boundingBox.height)}pt
                          </span>
                        )}
                      </div>

                      {isImage ? (
                        <p className="text-emerald-400/90 italic">
                          [Hình ảnh XObject raster: Bảo toàn nguyên trạng 100% trong PDF kết quả]
                        </p>
                      ) : (
                        <p className={`font-medium ${isFormula ? 'text-purple-200 font-mono' : 'text-slate-200'}`}>
                          "{b.originalText || b.text}"
                        </p>
                      )}

                      {!isImage && !isFormula && b.translatedText && (
                        <div className="mt-1.5 pt-1.5 border-t border-slate-800 text-emerald-400 font-medium">
                          Dịch: "{b.translatedText}"
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
