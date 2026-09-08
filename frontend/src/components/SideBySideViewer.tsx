import React, { useState, useRef } from 'react';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { Document, Page, pdfjs } from 'react-pdf';
import { 
  ZoomIn, 
  ZoomOut, 
  RotateCcw, 
  ChevronLeft, 
  ChevronRight, 
  Eye, 
  EyeOff, 
  GripVertical,
  FileText,
  AlertCircle,
  RefreshCw
} from 'lucide-react';
import { PdfPageOverlay, ContentBlockItem } from './PdfPageOverlay';
import { BlockInspector } from './BlockInspector';

// Configure PDF.js worker via Vite URL bundler
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

interface SideBySideViewerProps {
  originalPdfUrl: string;
  translatedPdfUrl: string;
  blocks: ContentBlockItem[];
  jobFileName?: string;
}

export const SideBySideViewer: React.FC<SideBySideViewerProps> = ({
  originalPdfUrl,
  translatedPdfUrl,
  blocks,
  jobFileName
}) => {
  const [numPages, setNumPages] = useState<number>(1);
  const [currentPage, setCurrentPage] = useState<number>(1);
  const [scale, setScale] = useState<number>(1.1);
  const [showOutlines, setShowOutlines] = useState<boolean>(true);
  const [hoveredBlockIndex, setHoveredBlockIndex] = useState<number | null>(null);
  const [selectedBlock, setSelectedBlock] = useState<ContentBlockItem | null>(null);

  // Lưu kích thước thật của trang PDF (pt) để tính toán toạ độ BBox
  const [pageSize, setPageSize] = useState<{ width: number; height: number }>({ width: 595.32, height: 841.92 });

  // Refs cho cuộn đồng bộ
  const leftScrollRef = useRef<HTMLDivElement>(null);
  const rightScrollRef = useRef<HTMLDivElement>(null);
  const isSyncingLeftRef = useRef(false);
  const isSyncingRightRef = useRef(false);

  const handleLeftScroll = () => {
    if (isSyncingLeftRef.current) {
      isSyncingLeftRef.current = false;
      return;
    }
    if (leftScrollRef.current && rightScrollRef.current) {
      isSyncingRightRef.current = true;
      rightScrollRef.current.scrollTop = leftScrollRef.current.scrollTop;
      rightScrollRef.current.scrollLeft = leftScrollRef.current.scrollLeft;
    }
  };

  const handleRightScroll = () => {
    if (isSyncingRightRef.current) {
      isSyncingRightRef.current = false;
      return;
    }
    if (leftScrollRef.current && rightScrollRef.current) {
      isSyncingLeftRef.current = true;
      leftScrollRef.current.scrollTop = rightScrollRef.current.scrollTop;
      leftScrollRef.current.scrollLeft = rightScrollRef.current.scrollLeft;
    }
  };

  const handleDocumentLoadSuccess = ({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
    if (currentPage > numPages) setCurrentPage(1);
  };

  const handlePageLoadSuccess = (page: any) => {
    if (page.originalWidth && page.originalHeight) {
      setPageSize({
        width: page.originalWidth,
        height: page.originalHeight
      });
    }
  };

  const handleZoomIn = () => setScale((prev) => Math.min(prev + 0.15, 2.5));
  const handleZoomOut = () => setScale((prev) => Math.max(prev - 0.15, 0.6));
  const handleResetZoom = () => setScale(1.1);

  const handlePrevPage = () => setCurrentPage((prev) => Math.max(prev - 1, 1));
  const handleNextPage = () => setCurrentPage((prev) => Math.min(prev + 1, numPages));

  return (
    <div className="flex flex-col h-[calc(100vh-4rem)] w-full bg-slate-950 overflow-hidden">
      {/* Top Toolbar */}
      <div className="h-12 bg-slate-900 border-b border-slate-800 px-4 flex items-center justify-between shrink-0 z-20">
        {/* Page Navigation */}
        <div className="flex items-center gap-2">
          <button
            onClick={handlePrevPage}
            disabled={currentPage <= 1}
            className="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 disabled:opacity-30 disabled:hover:bg-transparent transition"
            title="Trang trước"
          >
            <ChevronLeft className="w-4 h-4" />
          </button>

          <span className="text-xs font-semibold text-slate-300 font-mono">
            Trang {currentPage} / {numPages || 1}
          </span>

          <button
            onClick={handleNextPage}
            disabled={currentPage >= numPages}
            className="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 disabled:opacity-30 disabled:hover:bg-transparent transition"
            title="Trang sau"
          >
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>

        {/* Center: Toolbar Actions */}
        <div className="flex items-center gap-3">
          {/* Zoom controls */}
          <div className="flex items-center gap-1 bg-slate-800/80 p-0.5 rounded-lg border border-slate-700/60">
            <button
              onClick={handleZoomOut}
              className="p-1 rounded text-slate-400 hover:text-white hover:bg-slate-700 transition"
              title="Thu nhỏ"
            >
              <ZoomOut className="w-3.5 h-3.5" />
            </button>
            <span className="text-xs font-mono font-medium text-slate-300 px-1.5 min-w-[3.5rem] text-center">
              {Math.round(scale * 100)}%
            </span>
            <button
              onClick={handleZoomIn}
              className="p-1 rounded text-slate-400 hover:text-white hover:bg-slate-700 transition"
              title="Phóng to"
            >
              <ZoomIn className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={handleResetZoom}
              className="p-1 rounded text-slate-400 hover:text-white hover:bg-slate-700 transition"
              title="Đặt lại 100%"
            >
              <RotateCcw className="w-3 h-3" />
            </button>
          </div>

          {/* Toggle Bounding Box outlines */}
          <button
            onClick={() => setShowOutlines(!showOutlines)}
            className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium border transition ${
              showOutlines
                ? 'bg-sky-600/20 border-sky-500/40 text-sky-300'
                : 'bg-slate-800 border-slate-700 text-slate-400 hover:text-slate-200'
            }`}
          >
            {showOutlines ? <Eye className="w-3.5 h-3.5 text-sky-400" /> : <EyeOff className="w-3.5 h-3.5" />}
            <span className="hidden sm:inline">Khung Bounding Box</span>
          </button>
        </div>

        {/* Legend / Color Tags */}
        <div className="hidden lg:flex items-center gap-3 text-xs font-semibold">
          <span className="flex items-center gap-1 text-sky-400">
            <span className="w-2 h-2 rounded-full bg-sky-400" /> Text
          </span>
          <span className="flex items-center gap-1 text-purple-400">
            <span className="w-2 h-2 rounded-full bg-purple-400" /> Formula (Bảo toàn)
          </span>
          <span className="flex items-center gap-1 text-emerald-400">
            <span className="w-2 h-2 rounded-full bg-emerald-400" /> Image (Bảo toàn)
          </span>
        </div>
      </div>

      {/* Main Split Panels */}
      <div className="flex-1 overflow-hidden relative">
        <Group orientation="horizontal" className="h-full">
          {/* Left Panel: Original PDF */}
          <Panel defaultSize={50} minSize={25} className="flex flex-col h-full bg-slate-900/40">
            <div className="h-8 bg-slate-900/90 border-b border-slate-800 px-4 flex items-center justify-between text-xs font-bold text-slate-300 shrink-0">
              <span className="flex items-center gap-1.5 text-sky-400">
                <FileText className="w-3.5 h-3.5" />
                PDF GỐC (ENGLISH)
              </span>
              <span className="text-slate-500 text-[11px] font-mono truncate max-w-[200px]">
                {jobFileName || 'Original Document'}
              </span>
            </div>

            <div
              ref={leftScrollRef}
              onScroll={handleLeftScroll}
              className="flex-1 overflow-auto p-4 flex justify-center items-start"
            >
              <div className="relative shadow-2xl rounded-sm">
                <Document
                  file={originalPdfUrl}
                  onLoadSuccess={handleDocumentLoadSuccess}
                  loading={
                    <div className="p-12 flex flex-col items-center gap-3 text-slate-400">
                      <RefreshCw className="w-6 h-6 animate-spin text-sky-500" />
                      <span className="text-xs">Đang tải PDF gốc...</span>
                    </div>
                  }
                  error={
                    <div className="p-8 flex items-center gap-2 text-red-400 bg-red-950/20 border border-red-800/40 rounded-xl text-xs">
                      <AlertCircle className="w-4 h-4" />
                      <span>Không thể tải file PDF gốc từ máy chủ.</span>
                    </div>
                  }
                >
                  <Page
                    pageNumber={currentPage}
                    scale={scale}
                    renderTextLayer={false}
                    renderAnnotationLayer={false}
                    onLoadSuccess={handlePageLoadSuccess}
                  />
                </Document>

                {/* Overlay Bounding Box */}
                <PdfPageOverlay
                  pageIndex={currentPage}
                  pageWidth={pageSize.width}
                  pageHeight={pageSize.height}
                  scale={scale}
                  blocks={blocks}
                  hoveredBlockIndex={hoveredBlockIndex}
                  selectedBlockIndex={selectedBlock ? selectedBlock.orderIndex : null}
                  onHoverBlock={setHoveredBlockIndex}
                  onSelectBlock={setSelectedBlock}
                  showOutlines={showOutlines}
                />
              </div>
            </div>
          </Panel>

          {/* Resizable Divider Handle */}
          <Separator className="w-2 bg-slate-800 hover:bg-sky-600 transition-colors flex items-center justify-center cursor-col-resize group shadow-lg">
            <GripVertical className="w-3 h-3 text-slate-500 group-hover:text-white transition-colors" />
          </Separator>

          {/* Right Panel: Translated PDF */}
          <Panel defaultSize={50} minSize={25} className="flex flex-col h-full bg-slate-900/40">
            <div className="h-8 bg-slate-900/90 border-b border-slate-800 px-4 flex items-center justify-between text-xs font-bold text-slate-300 shrink-0">
              <span className="flex items-center gap-1.5 text-emerald-400">
                <FileText className="w-3.5 h-3.5" />
                PDF TIẾNG VIỆT (TRANSLATED)
              </span>
              <span className="text-emerald-500/80 text-[11px] font-mono">
                Rebuilt Layout Active
              </span>
            </div>

            <div
              ref={rightScrollRef}
              onScroll={handleRightScroll}
              className="flex-1 overflow-auto p-4 flex justify-center items-start"
            >
              <div className="relative shadow-2xl rounded-sm">
                <Document
                  file={translatedPdfUrl}
                  loading={
                    <div className="p-12 flex flex-col items-center gap-3 text-slate-400">
                      <RefreshCw className="w-6 h-6 animate-spin text-emerald-500" />
                      <span className="text-xs">Đang tải PDF tiếng Việt...</span>
                    </div>
                  }
                  error={
                    <div className="p-8 flex items-center gap-2 text-red-400 bg-red-950/20 border border-red-800/40 rounded-xl text-xs">
                      <AlertCircle className="w-4 h-4" />
                      <span>Không thể tải file PDF đã dịch từ máy chủ.</span>
                    </div>
                  }
                >
                  <Page
                    pageNumber={currentPage}
                    scale={scale}
                    renderTextLayer={false}
                    renderAnnotationLayer={false}
                  />
                </Document>

                {/* Overlay Bounding Box */}
                <PdfPageOverlay
                  pageIndex={currentPage}
                  pageWidth={pageSize.width}
                  pageHeight={pageSize.height}
                  scale={scale}
                  blocks={blocks}
                  hoveredBlockIndex={hoveredBlockIndex}
                  selectedBlockIndex={selectedBlock ? selectedBlock.orderIndex : null}
                  onHoverBlock={setHoveredBlockIndex}
                  onSelectBlock={setSelectedBlock}
                  showOutlines={showOutlines}
                />
              </div>
            </div>
          </Panel>
        </Group>
      </div>

      {/* Block Inspector Drawer (When a block is selected) */}
      <BlockInspector
        block={selectedBlock}
        onClose={() => setSelectedBlock(null)}
      />
    </div>
  );
};
