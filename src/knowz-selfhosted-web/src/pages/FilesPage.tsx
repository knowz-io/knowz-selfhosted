import { useState, useRef, useCallback } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '../lib/api-client'
import type { FileMetadataDto } from '../lib/types'
import {
  FileText,
  Upload,
  Search,
  X,
  Trash2,
  Download,
  ChevronLeft,
  ChevronRight,
  ChevronDown,
  ChevronUp,
  Loader2,
  BookOpen,
  Archive,
  Eye,
  Mic,
  FileSearch,
  Cpu,
  AlertCircle,
} from 'lucide-react'
import { formatFileSize, parseAsUtc, formatDate } from '../lib/format-utils'
import PageHeader from '../components/ui/PageHeader'
import SurfaceCard from '../components/ui/SurfaceCard'

function relativeTime(dateStr: string): string {
  // parseAsUtc handles naive selfhosted timestamps that lack a Z suffix.
  const date = parseAsUtc(dateStr)
  const now = new Date()
  const diff = Math.max(0, now.getTime() - date.getTime())
  const minutes = Math.floor(diff / 60000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`
  return formatDate(date)
}

function contentTypeBadgeClass(contentType?: string): string {
  if (!contentType) return 'bg-muted text-muted-foreground'
  if (contentType.startsWith('image/'))
    return 'bg-purple-100 dark:bg-purple-950/40 text-purple-700 dark:text-purple-400'
  if (contentType.startsWith('video/'))
    return 'bg-pink-100 dark:bg-pink-950/40 text-pink-700 dark:text-pink-400'
  if (contentType.startsWith('audio/'))
    return 'bg-yellow-100 dark:bg-yellow-950/40 text-yellow-700 dark:text-yellow-400'
  if (contentType === 'application/pdf')
    return 'bg-red-100 dark:bg-red-950/40 text-red-700 dark:text-red-400'
  if (contentType.startsWith('text/'))
    return 'bg-blue-100 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400'
  return 'bg-green-100 dark:bg-green-950/40 text-green-700 dark:text-green-400'
}

function contentTypeLabel(contentType?: string): string {
  if (!contentType) return 'Unknown'
  const parts = contentType.split('/')
  return parts[parts.length - 1].toUpperCase()
}

const EXTRACTION_STATUS_LABELS: Record<number, string> = {
  0: 'Not Started',
  1: 'Processing',
  2: 'Completed',
  3: 'Failed',
}

function extractionStatusBadgeClass(status?: number): string {
  switch (status) {
    case 2: return 'bg-green-100 dark:bg-green-950/40 text-green-700 dark:text-green-400'
    case 1: return 'bg-yellow-100 dark:bg-yellow-950/40 text-yellow-700 dark:text-yellow-400'
    case 3: return 'bg-red-100 dark:bg-red-950/40 text-red-700 dark:text-red-400'
    default: return 'bg-muted text-muted-foreground'
  }
}

const TRUNCATE_LENGTH = 200

function TruncatedText({ text, label, icon }: { text: string; label: string; icon: React.ReactNode }) {
  const [expanded, setExpanded] = useState(false)
  const needsTruncation = text.length > TRUNCATE_LENGTH

  return (
    <div>
      <div className="flex items-center gap-1.5 mb-1 text-xs font-medium text-muted-foreground">
        {icon}
        {label}
      </div>
      <p className="text-sm text-foreground whitespace-pre-wrap break-words">
        {needsTruncation && !expanded ? text.slice(0, TRUNCATE_LENGTH) + '...' : text}
      </p>
      {needsTruncation && (
        <button
          onClick={(e) => { e.stopPropagation(); setExpanded(!expanded) }}
          className="text-xs text-blue-600 dark:text-blue-400 hover:underline mt-1"
        >
          {expanded ? 'Show less' : 'Show more'}
        </button>
      )}
    </div>
  )
}

function FileDetailPanel({ file }: { file: FileMetadataDto }) {
  const hasAiDetails = file.extractedText || file.transcriptionText || file.visionDescription
  const hasAssociations = file.knowledgeId || file.vaultId
  const extractionStatus = file.textExtractionStatus ?? 0
  const hasNoAIData = !hasAiDetails && !file.visionTagsJson && !file.visionObjectsJson && !file.visionExtractedText

  return (
    <div className="px-4 py-4 bg-muted/50">
      {/* Status badges row */}
      <div className="flex flex-wrap items-center gap-2 mb-4">
        {/* Extraction status badge */}
        <span
          data-testid="extraction-status-badge"
          className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium ${extractionStatusBadgeClass(extractionStatus)}`}
        >
          <FileSearch size={10} />
          Extraction: {EXTRACTION_STATUS_LABELS[extractionStatus] ?? 'Unknown'}
        </span>

        {/* Provider badge */}
        {file.attachmentAIProvider && (
          <span
            data-testid="provider-badge"
            className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium bg-purple-100 dark:bg-purple-950/40 text-purple-700 dark:text-purple-400"
          >
            <Cpu size={10} />
            {file.attachmentAIProvider}
          </span>
        )}
      </div>

      {hasNoAIData && !hasAssociations ? (
        <div className="text-sm text-muted-foreground flex items-center gap-2" data-testid="no-ai-analysis-message">
          <AlertCircle size={14} className="shrink-0" />
          <span>
            No AI analysis available.
            {file.attachmentAIProvider === 'NoOp' && ' AI analysis is not configured for this deployment.'}
          </span>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Left: Associations */}
          <div>
            <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-3">
              Associations
            </h4>
            {hasAssociations ? (
              <div className="space-y-2">
                {file.knowledgeId && (
                  <div className="flex items-center gap-2 text-sm">
                    <BookOpen size={14} className="text-blue-500 shrink-0" />
                    <span className="text-muted-foreground">Knowledge:</span>
                    <Link
                      to={`/knowledge/${file.knowledgeId}`}
                      onClick={(e) => e.stopPropagation()}
                      className="text-blue-600 dark:text-blue-400 hover:underline truncate"
                    >
                      {file.knowledgeTitle || file.knowledgeId}
                    </Link>
                  </div>
                )}
                {file.vaultId && (
                  <div className="flex items-center gap-2 text-sm">
                    <Archive size={14} className="text-green-500 shrink-0" />
                    <span className="text-muted-foreground">Vault:</span>
                    <Link
                      to={`/vaults/${file.vaultId}`}
                      onClick={(e) => e.stopPropagation()}
                      className="text-green-600 dark:text-green-400 hover:underline truncate"
                    >
                      {file.vaultName || file.vaultId}
                    </Link>
                  </div>
                )}
              </div>
            ) : (
              <p className="text-sm text-muted-foreground">Not attached to any knowledge item.</p>
            )}
          </div>

          {/* Right: AI Details */}
          {hasAiDetails && (
            <div>
              <h4 className="text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-3">
                AI Details
              </h4>
              <div className="space-y-3">
                {file.extractedText && (
                  <TruncatedText
                    text={file.extractedText}
                    label="Extracted Text"
                    icon={<FileSearch size={12} />}
                  />
                )}
                {file.transcriptionText && (
                  <TruncatedText
                    text={file.transcriptionText}
                    label="Transcription"
                    icon={<Mic size={12} />}
                  />
                )}
                {file.visionDescription && (
                  <TruncatedText
                    text={file.visionDescription}
                    label="Vision Description"
                    icon={<Eye size={12} />}
                  />
                )}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

export default function FilesPage() {
  const queryClient = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [contentTypeFilter, setContentTypeFilter] = useState('')
  const [isDragging, setIsDragging] = useState(false)
  const [expandedFileId, setExpandedFileId] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['files', page, pageSize, search, contentTypeFilter],
    queryFn: () => api.listFiles(page, pageSize, search || undefined, contentTypeFilter || undefined),
  })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => api.uploadFile(file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['files'] })
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteFile(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['files'] })
    },
  })

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault()
    setSearch(searchInput)
    setPage(1)
  }

  const handleUploadClick = () => {
    fileInputRef.current?.click()
  }

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (files) {
      Array.from(files).forEach((file) => uploadMutation.mutate(file))
    }
    // Reset input so same file can be uploaded again
    if (fileInputRef.current) {
      fileInputRef.current.value = ''
    }
  }

  const handleDownload = async (id: string, fileName: string) => {
    try {
      const blob = await api.downloadFile(id)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch {
      // Error handling is minimal -- could add toast notifications
    }
  }

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(true)
  }, [])

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    e.stopPropagation()
    setIsDragging(false)
  }, [])

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault()
      e.stopPropagation()
      setIsDragging(false)
      const files = e.dataTransfer.files
      if (files) {
        Array.from(files).forEach((file) => uploadMutation.mutate(file))
      }
    },
    [uploadMutation],
  )

  const toggleExpand = (fileId: string) => {
    setExpandedFileId((prev) => (prev === fileId ? null : fileId))
  }

  const CONTENT_TYPE_OPTIONS = ['All', 'application/pdf', 'image/', 'text/', 'video/', 'audio/']
  const files = data?.items ?? []
  const linkedCount = files.filter((file) => Boolean(file.knowledgeId)).length
  const aiReadyCount = files.filter((file) =>
    Boolean(file.extractedText || file.transcriptionText || file.visionDescription || (file.textExtractionStatus ?? 0) === 2),
  ).length
  const hasActiveFilters = Boolean(search || searchInput || contentTypeFilter)
  const clearFilters = () => {
    setSearch('')
    setSearchInput('')
    setContentTypeFilter('')
    setPage(1)
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Assets"
        title="Files"
        titleAs="h2"
        description="Manage uploaded files, inspect AI extraction state, and keep attachment-heavy workflows tidy."
        actions={
          <button
            type="button"
            onClick={handleUploadClick}
            className="inline-flex items-center gap-2 rounded-2xl bg-primary px-4 py-2.5 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20 transition-all duration-200 hover:brightness-110"
          >
            <Upload size={16} />
            Upload
          </button>
        }
        meta={
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div className="sh-stat">
              <p className="sh-kicker">Library</p>
              <p className="mt-2 text-sm font-semibold">{`${data?.totalItems ?? 0} total files`}</p>
              <p className="mt-2 text-xs text-muted-foreground">Uploaded across the self-hosted workspace.</p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">Knowledge</p>
              <p className="mt-2 text-sm font-semibold">{`${linkedCount} linked to knowledge`}</p>
              <p className="mt-2 text-xs text-muted-foreground">Files already anchored to a knowledge item.</p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">AI State</p>
              <p className="mt-2 text-sm font-semibold">{`${aiReadyCount} ready for AI`}</p>
              <p className="mt-2 text-xs text-muted-foreground">Showing extraction or structured AI detail.</p>
            </div>
          </div>
        }
      />

      {/* Drag and Drop Upload Area */}
      <SurfaceCard
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        className={`border-2 border-dashed p-8 text-center transition-colors ${
          isDragging
            ? 'border-blue-500 bg-blue-50 dark:bg-blue-950/20'
            : 'border-input hover:bg-card'
        }`}
      >
        <Upload size={32} className="mx-auto mb-2 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">
          Drag and drop files here, or{' '}
          <button
            type="button"
            onClick={handleUploadClick}
            className="text-blue-600 dark:text-blue-400 hover:underline font-medium"
          >
            browse
          </button>
        </p>
        <input
          ref={fileInputRef}
          type="file"
          multiple
          onChange={handleFileChange}
          className="hidden"
        />
        {uploadMutation.isPending && (
          <div className="mt-2 flex items-center justify-center gap-2 text-sm text-muted-foreground">
            <Loader2 size={16} className="animate-spin" />
            Uploading...
          </div>
        )}
        {uploadMutation.error && (
          <p className="mt-2 text-sm text-red-600 dark:text-red-400">
            {uploadMutation.error instanceof Error ? uploadMutation.error.message : 'Upload failed'}
          </p>
        )}
      </SurfaceCard>

      {/* Search & Filter Bar */}
      <div className="sh-toolbar flex flex-wrap gap-2 items-center p-3">
        <form onSubmit={handleSearch} className="flex-1 flex gap-2">
          <div className="relative flex-1">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Search files..."
              className="w-full pl-10 pr-3 py-2 border border-input rounded-md bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          <button
            type="submit"
            className="rounded-2xl bg-muted px-4 py-2.5 text-sm font-medium text-foreground transition-colors hover:bg-muted/80"
          >
            Search
          </button>
        </form>
        <select
          value={contentTypeFilter}
          onChange={(e) => {
            setContentTypeFilter(e.target.value)
            setPage(1)
          }}
          className="rounded-2xl border border-input bg-card px-3 py-2.5 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-ring"
        >
          {CONTENT_TYPE_OPTIONS.map((t) => (
            <option key={t} value={t === 'All' ? '' : t}>
              {t === 'All' ? 'All Types' : t}
            </option>
          ))}
        </select>
        {hasActiveFilters && (
          <button
            type="button"
            onClick={clearFilters}
            className="inline-flex items-center gap-2 rounded-2xl border border-border/70 bg-card/80 px-4 py-2.5 text-sm font-medium transition-colors hover:bg-card"
          >
            <X size={14} />
            Clear
          </button>
        )}
      </div>

      {/* File List */}
      {isLoading ? (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <div key={i} className="sh-surface h-32 animate-pulse" />
          ))}
        </div>
      ) : data && data.items.length > 0 ? (
        <SurfaceCard className="overflow-hidden">
          {/* Header */}
          <div className="flex items-center gap-3 border-b border-border/60 bg-muted/60 px-4 py-3 text-sm font-medium text-muted-foreground">
            <span className="w-6 shrink-0" />
            <span className="flex-1 min-w-0">Name</span>
            <span className="w-24 text-center shrink-0">Type</span>
            <span className="w-20 text-right shrink-0">Size</span>
            <span className="w-36 truncate shrink-0">Knowledge</span>
            <span className="w-28 truncate shrink-0">Vault</span>
            <span className="w-24 text-right shrink-0">Uploaded</span>
            <span className="w-20 text-right shrink-0">Actions</span>
          </div>

          {data.items.map((file) => (
            <div key={file.id}>
              <div
                onClick={() => toggleExpand(file.id)}
                className="flex cursor-pointer select-none items-center gap-3 px-4 py-3 transition-colors hover:bg-muted/60"
              >
                <span className="w-6 text-muted-foreground">
                  {expandedFileId === file.id ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
                </span>
                <div className="flex-1 min-w-0">
                  <p className="text-sm text-foreground truncate">{file.fileName}</p>
                </div>
                <span className="w-24 text-center shrink-0 overflow-hidden">
                  <span
                    className={`inline-flex px-2 py-0.5 rounded text-xs font-medium max-w-full truncate ${contentTypeBadgeClass(file.contentType)}`}
                    title={file.contentType || 'Unknown'}
                  >
                    {contentTypeLabel(file.contentType)}
                  </span>
                </span>
                <span className="w-20 text-right text-sm text-muted-foreground shrink-0">
                  {formatFileSize(file.sizeBytes)}
                </span>
                <span className="w-36 truncate text-sm shrink-0">
                  {file.knowledgeId ? (
                    <Link
                      to={`/knowledge/${file.knowledgeId}`}
                      onClick={(e) => e.stopPropagation()}
                      className="text-blue-600 dark:text-blue-400 hover:underline"
                    >
                      {file.knowledgeTitle || 'Untitled'}
                    </Link>
                  ) : (
                    <span className="text-muted-foreground">--</span>
                  )}
                </span>
                <span className="w-28 truncate text-sm shrink-0">
                  {file.vaultId ? (
                    <Link
                      to={`/vaults/${file.vaultId}`}
                      onClick={(e) => e.stopPropagation()}
                      className="text-green-600 dark:text-green-400 hover:underline"
                    >
                      {file.vaultName || 'Unnamed'}
                    </Link>
                  ) : (
                    <span className="text-muted-foreground">--</span>
                  )}
                </span>
                <span className="w-24 text-right text-xs text-muted-foreground shrink-0">
                  {relativeTime(file.createdAt)}
                </span>
                <div className="w-20 flex items-center justify-end gap-1 shrink-0">
                  <button
                    onClick={(e) => { e.stopPropagation(); handleDownload(file.id, file.fileName) }}
                    className="p-1.5 text-muted-foreground hover:text-blue-600 rounded hover:bg-muted"
                    title="Download"
                  >
                    <Download size={14} />
                  </button>
                  <button
                    onClick={(e) => { e.stopPropagation(); deleteMutation.mutate(file.id) }}
                    className="p-1.5 text-muted-foreground hover:text-red-600 rounded hover:bg-muted"
                    title="Delete"
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>
              {expandedFileId === file.id && <FileDetailPanel file={file} />}
            </div>
          ))}
        </SurfaceCard>
      ) : (
        <SurfaceCard className="p-12 text-center text-muted-foreground">
          <FileText size={48} className="mx-auto mb-4 opacity-50" />
          <p className="text-lg font-medium">No files</p>
          <p className="text-sm mt-1">Upload your first file using the area above.</p>
        </SurfaceCard>
      )}

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="sh-toolbar flex items-center justify-between p-3">
          <p className="text-sm text-muted-foreground">
            Showing {(page - 1) * pageSize + 1}-{Math.min(page * pageSize, data.totalItems)} of{' '}
            {data.totalItems}
          </p>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="p-2 rounded hover:bg-muted disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronLeft size={16} />
            </button>
            <span className="text-sm text-foreground">
              Page {page} of {data.totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
              disabled={page >= data.totalPages}
              className="p-2 rounded hover:bg-muted disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronRight size={16} />
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
