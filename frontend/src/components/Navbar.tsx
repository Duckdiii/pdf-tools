import React from 'react';
import { 
  Languages, 
  Download, 
  ExternalLink, 
  Columns2, 
  LayoutDashboard,
  CheckCircle2,
  AlertCircle,
  RefreshCw
} from 'lucide-react';
import { JobStatusResponse } from '../types/job.types';

interface NavbarProps {
  viewMode: 'dashboard' | 'viewer';
  setViewMode: (mode: 'dashboard' | 'viewer') => void;
  jobStatus: JobStatusResponse | null;
  downloadUrl?: string;
  hasTranslatedPdf: boolean;
}

export const Navbar: React.FC<NavbarProps> = ({
  viewMode,
  setViewMode,
  jobStatus,
  downloadUrl,
  hasTranslatedPdf
}) => {
  return (
    <header className="h-16 bg-slate-900/90 backdrop-blur border-b border-slate-800 px-4 md:px-6 flex items-center justify-between sticky top-0 z-50">
      {/* Brand & Document Name */}
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-sky-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-sky-500/20">
          <Languages className="w-5 h-5 text-white" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <h1 className="text-base md:text-lg font-bold text-white tracking-tight">
              PDF Translator Tools
            </h1>
            <span className="hidden sm:inline-flex px-2 py-0.5 text-xs font-semibold rounded-full bg-sky-500/10 text-sky-400 border border-sky-500/20">
              Pro Dual Viewer
            </span>
          </div>
          {jobStatus && (
            <p className="text-xs text-slate-400 truncate max-w-xs md:max-w-md">
              {jobStatus.fileName}
            </p>
          )}
        </div>
      </div>

      {/* Mode Switcher Tabs */}
      <div className="flex items-center gap-2 bg-slate-800/80 p-1 rounded-xl border border-slate-700/60">
        <button
          onClick={() => setViewMode('dashboard')}
          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs md:text-sm font-medium transition-all ${
            viewMode === 'dashboard'
              ? 'bg-sky-600 text-white shadow-md'
              : 'text-slate-400 hover:text-slate-200 hover:bg-slate-700/50'
          }`}
        >
          <LayoutDashboard className="w-4 h-4" />
          <span>Bảng điều khiển</span>
        </button>

        <button
          onClick={() => setViewMode('viewer')}
          disabled={!hasTranslatedPdf && !jobStatus}
          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs md:text-sm font-medium transition-all ${
            viewMode === 'viewer'
              ? 'bg-gradient-to-r from-sky-600 to-indigo-600 text-white shadow-md'
              : hasTranslatedPdf || jobStatus
              ? 'text-slate-400 hover:text-slate-200 hover:bg-slate-700/50'
              : 'text-slate-600 cursor-not-allowed'
          }`}
        >
          <Columns2 className="w-4 h-4" />
          <span>Xem Đối Chiếu</span>
          {hasTranslatedPdf && (
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-ping ml-0.5" />
          )}
        </button>
      </div>

      {/* Actions (Download & Hangfire) */}
      <div className="flex items-center gap-2.5">
        <a
          href="http://localhost:5210/hangfire"
          target="_blank"
          rel="noopener noreferrer"
          className="hidden lg:flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
        >
          <ExternalLink className="w-3.5 h-3.5 text-sky-400" />
          <span>Hangfire Jobs</span>
        </a>

        {jobStatus && (
          <div className="hidden sm:flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium bg-slate-800/80 border border-slate-700">
            {jobStatus.status === 'Completed' && <CheckCircle2 className="w-3.5 h-3.5 text-emerald-400" />}
            {jobStatus.status === 'Failed' && <AlertCircle className="w-3.5 h-3.5 text-red-400" />}
            {jobStatus.status !== 'Completed' && jobStatus.status !== 'Failed' && (
              <RefreshCw className="w-3.5 h-3.5 text-sky-400 animate-spin" />
            )}
            <span className="text-slate-300">{jobStatus.status}</span>
            {jobStatus.status !== 'Completed' && (
              <span className="text-sky-400 font-bold">{jobStatus.progressPercent}%</span>
            )}
          </div>
        )}

        {hasTranslatedPdf && downloadUrl && (
          <a
            href={downloadUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="flex items-center gap-1.5 px-3.5 py-1.5 rounded-lg text-xs md:text-sm font-semibold bg-emerald-600 hover:bg-emerald-500 text-white shadow-lg shadow-emerald-600/20 transition active:scale-95"
          >
            <Download className="w-4 h-4" />
            <span className="hidden md:inline">Tải PDF Dịch</span>
            <span className="md:hidden">Tải PDF</span>
          </a>
        )}
      </div>
    </header>
  );
};
