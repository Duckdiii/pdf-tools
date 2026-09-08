import React from 'react';

export interface BoundingBox {
  x: number;
  y: number;
  width: number;
  height: number;
  fontSize?: number;
  fontName?: string;
}

export interface ContentBlockItem {
  id?: string;
  pageIndex: number;
  orderIndex: number;
  blockType: string;
  originalText?: string;
  text?: string;
  translatedText?: string;
  translatedContent?: string;
  boundingBox?: BoundingBox;
}

interface PdfPageOverlayProps {
  pageIndex: number;
  pageWidth: number;
  pageHeight: number;
  scale: number;
  blocks: ContentBlockItem[];
  hoveredBlockIndex: number | null;
  selectedBlockIndex: number | null;
  onHoverBlock: (index: number | null) => void;
  onSelectBlock: (block: ContentBlockItem) => void;
  showOutlines: boolean;
}

export const PdfPageOverlay: React.FC<PdfPageOverlayProps> = ({
  pageIndex,
  pageHeight,
  scale,
  blocks,
  hoveredBlockIndex,
  selectedBlockIndex,
  onHoverBlock,
  onSelectBlock,
  showOutlines,
}) => {
  if (!showOutlines || !pageHeight) return null;

  const pageBlocks = blocks.filter((b) => b.pageIndex === pageIndex && b.boundingBox);

  return (
    <div 
      className="absolute inset-0 pointer-events-none z-10"
      style={{
        width: '100%',
        height: '100%',
      }}
    >
      {pageBlocks.map((block) => {
        const bbox = block.boundingBox!;
        // iText7: origin (0,0) is bottom-left
        // HTML: origin (0,0) is top-left
        const top = (pageHeight - (bbox.y + bbox.height)) * scale;
        const left = bbox.x * scale;
        const width = bbox.width * scale;
        const height = bbox.height * scale;

        const isHovered = hoveredBlockIndex === block.orderIndex;
        const isSelected = selectedBlockIndex === block.orderIndex;
        const isImage = block.blockType === 'IMAGE';
        const isFormula = block.blockType === 'FORMULA_TEXT';

        let borderColor = 'border-sky-400/80';
        let bgColor = 'bg-sky-500/10 hover:bg-sky-500/25';
        let labelColor = 'bg-sky-600 text-white';
        let labelText = 'TEXT';

        if (isFormula) {
          borderColor = 'border-purple-400/90';
          bgColor = 'bg-purple-500/15 hover:bg-purple-500/30';
          labelColor = 'bg-purple-600 text-white';
          labelText = 'FORMULA';
        } else if (isImage) {
          borderColor = 'border-emerald-400/90';
          bgColor = 'bg-emerald-500/15 hover:bg-emerald-500/30';
          labelColor = 'bg-emerald-600 text-white';
          labelText = 'IMAGE';
        }

        return (
          <div
            key={block.orderIndex}
            onClick={(e) => {
              e.stopPropagation();
              onSelectBlock(block);
            }}
            onMouseEnter={() => onHoverBlock(block.orderIndex)}
            onMouseLeave={() => onHoverBlock(null)}
            className={`absolute border cursor-pointer pointer-events-auto transition-all duration-150 rounded-sm group ${borderColor} ${bgColor} ${
              isSelected ? 'ring-2 ring-yellow-400 ring-offset-1 ring-offset-slate-900 border-yellow-400 bg-yellow-500/20 z-20 shadow-lg' : ''
            } ${isHovered ? 'scale-[1.01] shadow-md z-20' : ''}`}
            style={{
              top: `${top}px`,
              left: `${left}px`,
              width: `${width}px`,
              height: `${height}px`,
            }}
            title={`Khối #${block.orderIndex} [${block.blockType}]: Bấm để xem chi tiết`}
          >
            {/* Tag Badge on hover or select */}
            {(isHovered || isSelected) && (
              <span
                className={`absolute -top-5 left-0 px-1.5 py-0.5 text-[10px] font-bold rounded shadow-md tracking-wider flex items-center gap-1 uppercase z-30 whitespace-nowrap ${labelColor}`}
              >
                #{block.orderIndex} {labelText}
              </span>
            )}
          </div>
        );
      })}
    </div>
  );
};
