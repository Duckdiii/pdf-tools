import React from 'react';
import { X, Sigma, Image as ImageIcon, FileText, Check, Copy, Info } from 'lucide-react';
import { ContentBlockItem } from './PdfPageOverlay';

interface BlockInspectorProps {
  block: ContentBlockItem | null;
  onClose: () => void;
}

export const BlockInspector: React.FC<BlockInspectorProps> = ({ block, onClose }) => {
  const [copiedOriginal, setCopiedOriginal] = React.useState(false);
  const [copiedTranslated, setCopiedTranslated] = React.useState(false);

  if (!block) return null;

  const isImage = block.blockType === 'IMAGE';
  const isFormula = block.blockType === 'FORMULA_TEXT';

  const handleCopy = (text: string, isOriginal: boolean) => {
    navigator.clipboard.writeText(text);
    if (isOriginal) {
      setCopiedOriginal(true);
      setTimeout(() => setCopiedOriginal(false), 2000);
    } else {
      setCopiedTranslated(true);
      setTimeout(() => setCopiedTranslated(false), 2000);
    }
  };

  return (
    <div className="bg-slate-900 border-t border-slate-700/80 shadow-2xl p-4 md:p-5 z-40 transition-all animate-in slide-in-from-bottom-5">
      <div className="max-w-7xl mx-auto space-y-3">
        {/* Header bar */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <span className="text-xs font-mono font-bold text-slate-400 bg-slate-800 px-2.5 py-1 rounded-lg border border-slate-700">
              Trang {block.pageIndex} &bull; Khối #{block.orderIndex}
            </span>

            {isImage && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-500/10 text-emerald-400 text-xs font-bold border border-emerald-500/20">
                <ImageIcon className="w-3.5 h-3.5" /> HÌNH ẢNH XOBJECT (Bảo toàn 100%)
              </span>
            )}
            {isFormula && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-purple-500/10 text-purple-400 text-xs font-bold border border-purple-500/20">
                <Sigma className="w-3.5 h-3.5" /> CÔNG THỨC TOÁN HỌC (Bảo toàn)
              </span>
            )}
            {!isImage && !isFormula && (
              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-sky-500/10 text-sky-400 text-xs font-bold border border-sky-500/20">
                <FileText className="w-3.5 h-3.5" /> VĂN BẢN (Dịch AI)
              </span>
            )}

            {block.boundingBox && (
              <span className="hidden sm:inline-block text-xs font-mono text-slate-400">
                Kích thước: {Math.round(block.boundingBox.width)} × {Math.round(block.boundingBox.height)} pt
                {block.boundingBox.fontName && ` | Font: ${block.boundingBox.fontName}`}
              </span>
            )}
          </div>

          <button
            onClick={onClose}
            className="p-1 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
            title="Đóng bảng chi tiết"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Comparison Content */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs md:text-sm">
          {/* Original Text */}
          <div className="bg-slate-950/60 rounded-xl p-3.5 border border-slate-800 space-y-1.5 relative group">
            <div className="flex items-center justify-between text-slate-400 text-xs font-semibold">
              <span>BẢN GỐC (TIẾNG ANH):</span>
              {block.originalText && (
                <button
                  onClick={() => handleCopy(block.originalText || block.text || '', true)}
                  className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-slate-300 transition"
                >
                  {copiedOriginal ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  {copiedOriginal ? 'Đã chép' : 'Sao chép'}
                </button>
              )}
            </div>
            {isImage ? (
              <p className="text-emerald-400 italic">
                [Khối hình ảnh raster: Bắt bởi toán tử PDF Do / XObject]
              </p>
            ) : (
              <p className={`font-medium ${isFormula ? 'font-mono text-purple-300' : 'text-slate-200'}`}>
                {block.originalText || block.text || '(Trống)'}
              </p>
            )}
          </div>

          {/* Translated Text */}
          <div className="bg-slate-950/60 rounded-xl p-3.5 border border-slate-800 space-y-1.5 relative group">
            <div className="flex items-center justify-between text-emerald-400 text-xs font-semibold">
              <span>BẢN DỊCH (TIẾNG VIỆT):</span>
              {block.translatedText && (
                <button
                  onClick={() => handleCopy(block.translatedText || '', false)}
                  className="flex items-center gap-1 text-[11px] text-slate-500 hover:text-emerald-300 transition"
                >
                  {copiedTranslated ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                  {copiedTranslated ? 'Đã chép' : 'Sao chép'}
                </button>
              )}
            </div>
            {isImage ? (
              <p className="text-emerald-400 italic">
                [Hình ảnh được giữ nguyên 100% pixel trong file PDF kết quả]
              </p>
            ) : isFormula ? (
              <p className="font-mono text-purple-300 font-medium">
                {block.translatedText || block.originalText || '(Trống)'}
              </p>
            ) : (
              <p className="text-emerald-300 font-medium">
                {block.translatedText || '(Đang chờ dịch...)'}
              </p>
            )}
          </div>
        </div>

        {/* Note info */}
        <div className="flex items-center gap-2 text-[11px] text-slate-400 bg-slate-800/40 px-3 py-1.5 rounded-lg border border-slate-700/50">
          <Info className="w-3.5 h-3.5 text-sky-400 shrink-0" />
          {isFormula && (
            <span>Hệ thống phân loại công thức toán học dựa trên mật độ Unicode Math, font Symbol/LaTeX và đánh số (1), (2). Công thức được giữ nguyên để bảo đảm tính chuẩn xác khoa học.</span>
          )}
          {isImage && (
            <span>Hệ thống phát hiện hình ảnh XObject (toán tử Do), ghi nhận tọa độ CTM và bỏ qua bước ghi đè để bảo toàn đồ họa sắc nét ban đầu.</span>
          )}
          {!isImage && !isFormula && (
            <span>Văn bản được dịch bằng Gemini AI, sau đó tự động tính toán co giãn kích cỡ font (auto-shrink) để vừa vặn trong Bounding Box gốc mà không làm vỡ bố cục.</span>
          )}
        </div>
      </div>
    </div>
  );
};
