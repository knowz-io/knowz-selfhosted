import { useEffect, useRef, useState } from 'react'
import { X, Download, FileText, Image, File, Loader2 } from 'lucide-react'
import { formatFileSize } from '../lib/format-utils'
import { api } from '../lib/api-client'
import type { FileMetadataDto } from '../lib/types'

interface AttachmentViewerProps {
  file: FileMetadataDto
  onClose: () => void
  onDownload?: (fileId: string, fileName: string) => void
}

function isImageType(contentType?: string): boolean {
  return !!contentType && contentType.startsWith('image/')
}

function hasExtractedContent(file: FileMetadataDto): boolean {
  return !!(file.extractedText || file.transcriptionText)
}

export default function AttachmentViewer({ file, onClose, onDownload }: AttachmentViewerProps) {
  const [imageBlobUrl, setImageBlobUrl] = useState<string | null>(null)
  const [imageLoading, setImageLoading] = useState(false)
  const blobUrlRef = useRef<string | null>(null)

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose()
      }
    }
    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [onClose])

  // Fetch image as blob and create object URL for authenticated preview
  useEffect(() => {
    if (!isImageType(file.contentType)) return

    let cancelled = false
    setImageLoading(true)

    api.downloadFile(file.id)
      .then((blob) => {
        if (!cancelled) {
          const url = URL.createObjectURL(blob)
          blobUrlRef.current = url
          setImageBlobUrl(url)
        }
      })
      .catch(() => {
        // Silently fail — image preview will not show
      })
      .finally(() => {
        if (!cancelled) setImageLoading(false)
      })

    return () => {
      cancelled = true
      if (blobUrlRef.current) {
        URL.revokeObjectURL(blobUrlRef.current)
        blobUrlRef.current = null
      }
    }
  }, [file.id, file.contentType])

  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose()
    }
  }

  return (
    <div
      data-testid="attachment-viewer-backdrop"
      className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4"
      onClick={handleBackdropClick}
    >
      <div className="bg-card rounded-xl shadow-lg max-w-2xl w-full max-h-[80vh] flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border/60">
          <div className="flex items-center gap-3 min-w-0">
            {isImageType(file.contentType) ? (
              <Image size={18} className="text-blue-500 flex-shrink-0" />
            ) : hasExtractedContent(file) ? (
              <FileText size={18} className="text-purple-500 flex-shrink-0" />
            ) : (
              <File size={18} className="text-muted-foreground flex-shrink-0" />
            )}
            <div className="min-w-0">
              <h2 className="text-sm font-semibold truncate">{file.fileName}</h2>
              <div className="flex items-center gap-2 text-[11px] text-muted-foreground">
                <span>{formatFileSize(file.sizeBytes)}</span>
                {file.contentType && (
                  <>
                    <span className="text-border">|</span>
                    <span>{file.contentType}</span>
                  </>
                )}
              </div>
            </div>
          </div>
          <button
            onClick={onClose}
            aria-label="Close"
            className="p-1.5 text-muted-foreground hover:text-foreground rounded hover:bg-muted transition-colors flex-shrink-0"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-5 space-y-4">
          {/* Image preview */}
          {isImageType(file.contentType) && (
            <div className="rounded-lg overflow-hidden bg-muted/30 flex items-center justify-center min-h-[120px]">
              {imageLoading ? (
                <div className="flex items-center gap-2 py-8 text-muted-foreground">
                  <Loader2 size={18} className="animate-spin" />
                  <span className="text-sm">Loading preview...</span>
                </div>
              ) : imageBlobUrl ? (
                <img
                  src={imageBlobUrl}
                  alt={file.fileName}
                  className="max-w-full max-h-96 object-contain"
                />
              ) : (
                <div className="py-8 text-sm text-muted-foreground">
                  Preview unavailable
                </div>
              )}
            </div>
          )}

          {/* Vision description for images */}
          {file.visionDescription && (
            <div>
              <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                AI Description
              </h3>
              <p className="text-sm text-foreground/80 leading-relaxed">
                {file.visionDescription}
              </p>
            </div>
          )}

          {/* Extracted text */}
          {file.extractedText && (
            <div>
              <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                Extracted Content
              </h3>
              <div className="bg-muted/30 border border-border/60 rounded-lg p-4 max-h-80 overflow-y-auto">
                <pre className="text-sm whitespace-pre-wrap font-mono text-foreground/80">
                  {file.extractedText}
                </pre>
              </div>
            </div>
          )}

          {/* Transcription text */}
          {file.transcriptionText && (
            <div>
              <h3 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-2">
                Transcription
              </h3>
              <div className="bg-muted/30 border border-border/60 rounded-lg p-4 max-h-80 overflow-y-auto">
                <pre className="text-sm whitespace-pre-wrap font-mono text-foreground/80">
                  {file.transcriptionText}
                </pre>
              </div>
            </div>
          )}

          {/* If nothing to show inline, show file info + download */}
          {!isImageType(file.contentType) && !file.extractedText && !file.transcriptionText && !file.visionDescription && (
            <div className="text-center py-8 space-y-3">
              <File size={40} className="mx-auto text-muted-foreground" />
              <p className="text-sm text-muted-foreground">
                No preview available for this file type.
              </p>
            </div>
          )}

          {/* Download button */}
          <div className="pt-2">
            <button
              onClick={() => onDownload?.(file.id, file.fileName)}
              className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium hover:opacity-90 transition-opacity"
            >
              <Download size={14} />
              Download
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
