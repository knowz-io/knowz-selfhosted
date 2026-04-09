import { useState, useRef, useMemo } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../lib/api-client'
import {
  ArrowLeft, Pencil, Trash2, Save, X, Paperclip, Download, Upload,
  Loader2, RefreshCw, History, RotateCcw, ChevronDown, ChevronRight,
  Sparkles, FileText, PanelRightClose, PanelRightOpen, Eye,
} from 'lucide-react'
import CommentSection from '../components/CommentSection'
import MarkdownContent from '../components/MarkdownContent'
import ContentTabs from '../components/ContentTabs'
import DetailSidebar from '../components/DetailSidebar'
import EnrichmentBanner from '../components/EnrichmentBanner'
import AttachmentViewer from '../components/AttachmentViewer'
import type { TabId } from '../components/ContentTabs'
import type { FileMetadataDto, KnowledgeVersion } from '../lib/types'
import { formatFileSize } from '../lib/format-utils'

export default function KnowledgeDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [editing, setEditing] = useState(false)
  const [editTitle, setEditTitle] = useState('')
  const [editContent, setEditContent] = useState('')
  const [editTags, setEditTags] = useState('')
  const [editSource, setEditSource] = useState('')
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [reprocessMsg, setReprocessMsg] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [editTab, setEditTab] = useState<'write' | 'preview'>('write')
  const [showAttachPicker, setShowAttachPicker] = useState(false)
  const [activeTab, setActiveTab] = useState<TabId>('summary')
  const [sidebarOpen, setSidebarOpen] = useState(true)
  const [expandedVersion, setExpandedVersion] = useState<number | null>(null)
  const [showRestoreConfirm, setShowRestoreConfirm] = useState<number | null>(null)
  const [viewingAttachment, setViewingAttachment] = useState<FileMetadataDto | null>(null)
  const attachFileInputRef = useRef<HTMLInputElement>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['knowledge', id],
    queryFn: () => api.getKnowledge(id!),
    enabled: !!id,
  })

  const { data: attachments, isLoading: attachmentsLoading } = useQuery({
    queryKey: ['knowledge-attachments', id],
    queryFn: () => api.getKnowledgeAttachments(id!),
    enabled: !!id,
  })

  const { data: availableFiles } = useQuery({
    queryKey: ['files-for-attach'],
    queryFn: () => api.listFiles(1, 50),
    enabled: showAttachPicker,
  })

  const { data: versions, isLoading: versionsLoading, error: versionsError } = useQuery({
    queryKey: ['knowledge-versions', id],
    queryFn: () => api.getVersionHistory(id!),
    enabled: !!id && activeTab === 'history',
  })

  // Enrichment status polling — resilient to 404 if endpoint not yet deployed
  const { data: enrichmentStatus } = useQuery({
    queryKey: ['enrichment-status', id],
    queryFn: async () => {
      try {
        return await api.getEnrichmentStatus(id!)
      } catch (err) {
        if (err instanceof ApiError && err.status === 404) {
          return null
        }
        throw err
      }
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status === 'pending' || status === 'processing' ? 3000 : false
    },
    enabled: !!id,
    retry: false,
  })

  // When enrichment completes, reload knowledge data to get fresh AI summary
  const enrichmentStatusValue = enrichmentStatus?.status
  const prevEnrichmentRef = useRef<string | undefined>(undefined)
  if (
    prevEnrichmentRef.current &&
    (prevEnrichmentRef.current === 'pending' || prevEnrichmentRef.current === 'processing') &&
    enrichmentStatusValue === 'completed'
  ) {
    queryClient.invalidateQueries({ queryKey: ['knowledge', id] })
  }
  prevEnrichmentRef.current = enrichmentStatusValue ?? undefined

  const restoreMut = useMutation({
    mutationFn: (versionNumber: number) => api.restoreVersion(id!, versionNumber),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge', id] })
      queryClient.invalidateQueries({ queryKey: ['knowledge-versions', id] })
      setShowRestoreConfirm(null)
    },
  })

  const updateMut = useMutation({
    mutationFn: () =>
      api.updateKnowledge(id!, {
        title: editTitle,
        content: editContent,
        source: editSource || undefined,
        tags: editTags
          .split(',')
          .map((t) => t.trim())
          .filter(Boolean),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge', id] })
      setEditing(false)
    },
  })

  const reprocessMut = useMutation({
    mutationFn: () => api.reprocessKnowledge(id!),
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['knowledge', id] })
      setReprocessMsg(
        data.reprocessed
          ? { type: 'success', text: 'Reprocessing complete. Item has been re-indexed and queued for enrichment.' }
          : { type: 'error', text: 'Reprocessing failed. Check that AI services are configured.' }
      )
    },
    onError: (err) => {
      setReprocessMsg({ type: 'error', text: err instanceof Error ? err.message : 'Reprocess failed' })
    },
  })

  const deleteMut = useMutation({
    mutationFn: () => api.deleteKnowledge(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
      navigate('/knowledge')
    },
  })

  const attachMut = useMutation({
    mutationFn: (fileRecordId: string) => api.attachFileToKnowledge(id!, fileRecordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-attachments', id] })
      setShowAttachPicker(false)
    },
  })

  const detachMut = useMutation({
    mutationFn: (fileRecordId: string) => api.detachFileFromKnowledge(id!, fileRecordId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-attachments', id] })
    },
  })

  const uploadAndAttachMut = useMutation({
    mutationFn: async (file: File) => {
      const result = await api.uploadFile(file)
      await api.attachFileToKnowledge(id!, result.fileRecordId)
      return result
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['knowledge-attachments', id] })
      queryClient.invalidateQueries({ queryKey: ['files'] })
    },
  })

  const handleAttachUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (files) {
      Array.from(files).forEach((file) => uploadAndAttachMut.mutate(file))
    }
    if (attachFileInputRef.current) {
      attachFileInputRef.current.value = ''
    }
  }

  const handleDownloadAttachment = async (fileId: string, fileName: string) => {
    try {
      const blob = await api.downloadFile(fileId)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch {
      // Minimal error handling
    }
  }

  const startEditing = () => {
    if (!data) return
    setEditTitle(data.title)
    setEditContent(data.content)
    setEditTags(data.tags.join(', '))
    setEditSource(data.source || '')
    setEditTab('write')
    setEditing(true)
  }

  const attachmentCount = attachments?.length ?? 0

  const contentTabs = useMemo(
    () => [
      { id: 'summary' as TabId, label: 'AI Summary', icon: Sparkles },
      { id: 'original' as TabId, label: 'Original', icon: FileText },
      { id: 'attachments' as TabId, label: 'Attachments', icon: Paperclip, count: attachmentCount },
      { id: 'history' as TabId, label: 'History', icon: History },
    ],
    [attachmentCount],
  )

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-muted rounded animate-pulse" />
        <div className="h-64 bg-muted rounded animate-pulse" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-4">
        <Link to="/knowledge" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors">
          <ArrowLeft size={16} /> Back to Knowledge
        </Link>
        <p className="text-red-600 dark:text-red-400">
          {error instanceof Error ? error.message : 'Failed to load item'}
        </p>
      </div>
    )
  }

  if (!data) return null

  return (
    <div className="space-y-4">
      <Link
        to="/knowledge"
        className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <ArrowLeft size={16} /> Back to Knowledge
      </Link>

      {editing ? (
        /* --- EDIT MODE (unchanged) --- */
        <div className="space-y-4">
          <input
            type="text"
            value={editTitle}
            onChange={(e) => setEditTitle(e.target.value)}
            className="w-full px-3 py-2 border border-input rounded-md bg-card text-lg font-semibold"
          />
          <div className="border border-input rounded-md overflow-hidden">
            <div className="flex border-b border-input bg-muted">
              <button
                type="button"
                onClick={() => setEditTab('write')}
                className={`px-4 py-2 text-sm font-medium transition-colors ${
                  editTab === 'write'
                    ? 'text-foreground bg-card border-b-2 border-primary'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                Write
              </button>
              <button
                type="button"
                onClick={() => setEditTab('preview')}
                className={`px-4 py-2 text-sm font-medium transition-colors ${
                  editTab === 'preview'
                    ? 'text-foreground bg-card border-b-2 border-primary'
                    : 'text-muted-foreground hover:text-foreground'
                }`}
              >
                Preview
              </button>
            </div>
            {editTab === 'write' ? (
              <textarea
                value={editContent}
                onChange={(e) => setEditContent(e.target.value)}
                rows={16}
                className="w-full px-3 py-2 bg-card text-sm font-mono border-0 focus:ring-0 focus:outline-none"
              />
            ) : (
              <div className="px-3 py-2 bg-card min-h-[384px]">
                {editContent.trim() ? (
                  <MarkdownContent content={editContent} />
                ) : (
                  <p className="text-muted-foreground text-sm italic">
                    Nothing to preview
                  </p>
                )}
              </div>
            )}
          </div>
          <input
            type="text"
            value={editSource}
            onChange={(e) => setEditSource(e.target.value)}
            placeholder="Source (optional)"
            className="w-full px-3 py-2 border border-input rounded-md bg-card text-sm"
          />
          <input
            type="text"
            value={editTags}
            onChange={(e) => setEditTags(e.target.value)}
            placeholder="Tags (comma-separated)"
            className="w-full px-3 py-2 border border-input rounded-md bg-card text-sm"
          />
          {updateMut.error && (
            <p className="text-red-600 dark:text-red-400 text-sm">
              {updateMut.error instanceof Error ? updateMut.error.message : 'Update failed'}
            </p>
          )}
          <div className="flex gap-2">
            <button
              onClick={() => updateMut.mutate()}
              disabled={updateMut.isPending}
              className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium disabled:opacity-50 transition-colors"
            >
              <Save size={16} /> {updateMut.isPending ? 'Saving...' : 'Save'}
            </button>
            <button
              onClick={() => setEditing(false)}
              className="inline-flex items-center gap-2 px-4 py-2 border border-input rounded-md text-sm font-medium transition-colors"
            >
              <X size={16} /> Cancel
            </button>
          </div>
        </div>
      ) : (
        /* --- VIEW MODE: Two-column layout --- */
        <div className="space-y-4">
          {/* Header: Title + Actions */}
          <div className="flex items-start justify-between gap-4">
            <h1 className="text-2xl font-bold">{data.title}</h1>
            <div className="flex gap-2 flex-shrink-0">
              <button
                onClick={startEditing}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-input rounded-md text-sm hover:bg-muted transition-colors"
              >
                <Pencil size={14} /> Edit
              </button>
              <button
                onClick={() => { setReprocessMsg(null); reprocessMut.mutate() }}
                disabled={reprocessMut.isPending}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-input rounded-md text-sm disabled:opacity-50 hover:bg-muted transition-colors"
              >
                <RefreshCw size={14} className={reprocessMut.isPending ? 'animate-spin' : ''} />
                {reprocessMut.isPending ? 'Reprocessing...' : 'Reprocess'}
              </button>
              <button
                onClick={() => setSidebarOpen(!sidebarOpen)}
                className="hidden lg:inline-flex items-center gap-1.5 px-2 py-1.5 border border-input rounded-md text-sm hover:bg-muted transition-colors"
                title={sidebarOpen ? 'Hide sidebar' : 'Show sidebar'}
              >
                {sidebarOpen ? <PanelRightClose size={14} /> : <PanelRightOpen size={14} />}
              </button>
              <button
                onClick={() => setShowDeleteConfirm(true)}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-red-300 dark:border-red-700 text-red-600 dark:text-red-400 rounded-md text-sm hover:bg-red-50 dark:hover:bg-red-950/30 transition-colors"
              >
                <Trash2 size={14} /> Delete
              </button>
            </div>
          </div>

          {reprocessMsg && (
            <p className={`text-sm ${reprocessMsg.type === 'success' ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'}`}>
              {reprocessMsg.text}
            </p>
          )}

          <EnrichmentBanner status={enrichmentStatus?.status ?? null} />

          {/* Two-column grid */}
          <div className={`grid gap-6 ${sidebarOpen ? 'grid-cols-1 lg:grid-cols-[1fr_300px]' : 'grid-cols-1'}`}>
            {/* Left column: Content with tabs */}
            <div className="min-w-0 space-y-4">
              <ContentTabs
                activeTab={activeTab}
                onTabChange={setActiveTab}
                tabs={contentTabs}
              />

              <div className="animate-fade-in">
                {activeTab === 'summary' && (
                  <SummaryTabContent
                    summary={data.summary}
                    reprocessMut={reprocessMut}
                  />
                )}

                {activeTab === 'original' && (
                  <div className="bg-card border border-border/60 rounded-xl p-5">
                    <MarkdownContent content={data.content} />
                  </div>
                )}

                {activeTab === 'attachments' && (
                  <AttachmentsTabContent
                    attachments={attachments}
                    attachmentsLoading={attachmentsLoading}
                    attachFileInputRef={attachFileInputRef}
                    handleAttachUpload={handleAttachUpload}
                    handleDownloadAttachment={handleDownloadAttachment}
                    uploadAndAttachMut={uploadAndAttachMut}
                    detachMut={detachMut}
                    attachMut={attachMut}
                    showAttachPicker={showAttachPicker}
                    setShowAttachPicker={setShowAttachPicker}
                    availableFiles={availableFiles}
                    onViewAttachment={setViewingAttachment}
                  />
                )}

                {activeTab === 'history' && (
                  <VersionHistoryPanel
                    versions={versions}
                    isLoading={versionsLoading}
                    error={versionsError}
                    expandedVersion={expandedVersion}
                    onToggleExpand={(vn) => setExpandedVersion(expandedVersion === vn ? null : vn)}
                    showRestoreConfirm={showRestoreConfirm}
                    onShowRestoreConfirm={setShowRestoreConfirm}
                    restoreMut={restoreMut}
                  />
                )}
              </div>

              {/* Contributions Section - always visible below tabs */}
              <CommentSection knowledgeId={id!} />
            </div>

            {/* Right column: Sidebar */}
            {sidebarOpen && (
              <aside className="animate-fade-in">
                <DetailSidebar
                  briefSummary={data.briefSummary}
                  tags={data.tags}
                  type={data.type}
                  vaults={data.vaults}
                  source={data.source}
                  createdAt={data.createdAt}
                  updatedAt={data.updatedAt}
                  isIndexed={data.isIndexed}
                  indexedAt={data.indexedAt}
                  attachmentCount={attachmentCount}
                />
              </aside>
            )}
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {showDeleteConfirm && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-card rounded-xl p-6 max-w-sm w-full space-y-4 shadow-sm">
            <h2 className="text-lg font-semibold">Delete Knowledge Item?</h2>
            <p className="text-sm text-muted-foreground">
              This will permanently delete &quot;{data.title}&quot;. This action cannot be undone.
            </p>
            {deleteMut.error && (
              <p className="text-red-600 dark:text-red-400 text-sm">
                {deleteMut.error instanceof Error ? deleteMut.error.message : 'Delete failed'}
              </p>
            )}
            <div className="flex gap-2 justify-end">
              <button
                onClick={() => setShowDeleteConfirm(false)}
                className="px-4 py-2 border border-input rounded-md text-sm transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={() => deleteMut.mutate()}
                disabled={deleteMut.isPending}
                className="px-4 py-2 bg-red-600 text-white rounded-md text-sm font-medium disabled:opacity-50"
              >
                {deleteMut.isPending ? 'Deleting...' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Attachment Viewer Modal */}
      {viewingAttachment && (
        <AttachmentViewer
          file={viewingAttachment}
          onClose={() => setViewingAttachment(null)}
          onDownload={handleDownloadAttachment}
        />
      )}
    </div>
  )
}

// --- Summary Tab ---

function SummaryTabContent({
  summary,
  reprocessMut,
}: {
  summary?: string
  reprocessMut: { mutate: () => void; isPending: boolean }
}) {
  if (!summary) {
    return (
      <div className="text-center py-12 space-y-3">
        <Sparkles size={32} className="mx-auto text-muted-foreground" />
        <p className="text-muted-foreground text-sm">No AI summary available yet.</p>
        <button
          onClick={() => reprocessMut.mutate()}
          disabled={reprocessMut.isPending}
          className="inline-flex items-center gap-1.5 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium disabled:opacity-50 hover:opacity-90 transition-opacity"
        >
          <RefreshCw size={14} className={reprocessMut.isPending ? 'animate-spin' : ''} />
          {reprocessMut.isPending ? 'Processing...' : 'Generate Summary'}
        </button>
      </div>
    )
  }

  return (
    <div className="bg-card border border-border/60 rounded-xl p-5">
      <MarkdownContent content={summary} />
    </div>
  )
}

// --- Attachments Tab ---

function AttachmentsTabContent({
  attachments,
  attachmentsLoading,
  attachFileInputRef,
  handleAttachUpload,
  handleDownloadAttachment,
  uploadAndAttachMut,
  detachMut,
  attachMut,
  showAttachPicker,
  setShowAttachPicker,
  availableFiles,
  onViewAttachment,
}: {
  attachments: FileMetadataDto[] | undefined
  attachmentsLoading: boolean
  attachFileInputRef: React.RefObject<HTMLInputElement | null>
  handleAttachUpload: (e: React.ChangeEvent<HTMLInputElement>) => void
  handleDownloadAttachment: (fileId: string, fileName: string) => void
  uploadAndAttachMut: { isPending: boolean }
  detachMut: { mutate: (id: string) => void }
  attachMut: { mutate: (id: string) => void; isPending: boolean }
  showAttachPicker: boolean
  setShowAttachPicker: (v: boolean) => void
  availableFiles: { items: FileMetadataDto[] } | undefined
  onViewAttachment: (file: FileMetadataDto) => void
}) {
  return (
    <div className="space-y-4">
      {/* Upload actions */}
      <div className="flex gap-2">
        <button
          type="button"
          onClick={() => attachFileInputRef.current?.click()}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm bg-primary text-primary-foreground rounded-md hover:opacity-90 transition-opacity"
        >
          <Upload size={14} />
          Upload & Attach
        </button>
        <input
          ref={attachFileInputRef}
          type="file"
          multiple
          onChange={handleAttachUpload}
          className="hidden"
        />
        <button
          type="button"
          onClick={() => setShowAttachPicker(!showAttachPicker)}
          className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm border border-input rounded-md hover:bg-muted transition-colors"
        >
          <Paperclip size={14} />
          Attach Existing
        </button>
      </div>

      {uploadAndAttachMut.isPending && (
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <Loader2 size={14} className="animate-spin" />
          Uploading and attaching...
        </div>
      )}

      {/* Attach from existing files picker */}
      {showAttachPicker && (
        <div className="border border-border/60 rounded-lg p-3 space-y-2 bg-card">
          <p className="text-xs font-medium text-muted-foreground">
            Select an existing file to attach:
          </p>
          {availableFiles && availableFiles.items.length > 0 ? (
            <div className="max-h-40 overflow-y-auto space-y-1">
              {availableFiles.items
                .filter(
                  (f) => !attachments?.some((a: FileMetadataDto) => a.id === f.id),
                )
                .map((file) => (
                  <button
                    key={file.id}
                    onClick={() => attachMut.mutate(file.id)}
                    disabled={attachMut.isPending}
                    className="w-full flex items-center gap-2 px-2 py-1.5 text-left text-sm rounded hover:bg-muted disabled:opacity-50 transition-colors"
                  >
                    <Paperclip size={12} className="text-muted-foreground" />
                    <span className="truncate">{file.fileName}</span>
                    <span className="text-xs text-muted-foreground ml-auto">
                      {formatFileSize(file.sizeBytes)}
                    </span>
                  </button>
                ))}
            </div>
          ) : (
            <p className="text-xs text-muted-foreground">No files available.</p>
          )}
          <button
            type="button"
            onClick={() => setShowAttachPicker(false)}
            className="text-xs text-muted-foreground hover:text-foreground transition-colors"
          >
            Close
          </button>
        </div>
      )}

      {/* File list */}
      {attachmentsLoading ? (
        <div className="flex items-center justify-center py-8">
          <Loader2 size={16} className="animate-spin text-muted-foreground" />
        </div>
      ) : attachments && attachments.length > 0 ? (
        <div className="border border-border/60 rounded-lg divide-y divide-border/40">
          {attachments.map((file: FileMetadataDto) => (
            <div
              key={file.id}
              className="flex items-center justify-between py-2.5 px-3 hover:bg-muted/30 transition-colors"
            >
              <div className="flex items-center gap-2.5 min-w-0">
                <Paperclip size={14} className="text-muted-foreground flex-shrink-0" />
                <div className="min-w-0">
                  <span className="text-sm truncate block">{file.fileName}</span>
                  <span className="text-[10px] text-muted-foreground">
                    {formatFileSize(file.sizeBytes)}
                    {file.contentType && ` \u00b7 ${file.contentType}`}
                  </span>
                </div>
              </div>
              <div className="flex items-center gap-1 flex-shrink-0">
                <button
                  onClick={() => onViewAttachment(file)}
                  className="p-1.5 text-muted-foreground hover:text-purple-600 rounded hover:bg-muted transition-colors"
                  title="View"
                >
                  <Eye size={14} />
                </button>
                <button
                  onClick={() => handleDownloadAttachment(file.id, file.fileName)}
                  className="p-1.5 text-muted-foreground hover:text-blue-600 rounded hover:bg-muted transition-colors"
                  title="Download"
                >
                  <Download size={14} />
                </button>
                <button
                  onClick={() => detachMut.mutate(file.id)}
                  className="p-1.5 text-muted-foreground hover:text-red-600 rounded hover:bg-muted transition-colors"
                  title="Detach"
                >
                  <X size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="text-center py-8">
          <Paperclip size={32} className="mx-auto text-muted-foreground mb-3" />
          <p className="text-muted-foreground text-sm">No attachments yet.</p>
          <p className="text-xs text-muted-foreground mt-1">
            Upload a file or attach an existing one.
          </p>
        </div>
      )}
    </div>
  )
}

// --- Version History Panel ---

interface DiffLine {
  type: 'added' | 'removed' | 'unchanged'
  text: string
}

function computeSimpleDiff(oldText: string, newText: string): DiffLine[] {
  const oldLines = oldText.split('\n')
  const newLines = newText.split('\n')
  const result: DiffLine[] = []

  let oi = 0
  let ni = 0

  while (oi < oldLines.length || ni < newLines.length) {
    if (oi >= oldLines.length) {
      result.push({ type: 'added', text: newLines[ni] })
      ni++
    } else if (ni >= newLines.length) {
      result.push({ type: 'removed', text: oldLines[oi] })
      oi++
    } else if (oldLines[oi] === newLines[ni]) {
      result.push({ type: 'unchanged', text: oldLines[oi] })
      oi++
      ni++
    } else {
      let foundInNew = -1
      let foundInOld = -1
      const maxLen = Math.max(oldLines.length, newLines.length)
      const lookAhead = Math.min(5, maxLen)

      for (let k = 1; k <= lookAhead && ni + k < newLines.length; k++) {
        if (oldLines[oi] === newLines[ni + k]) {
          foundInNew = ni + k
          break
        }
      }
      for (let k = 1; k <= lookAhead && oi + k < oldLines.length; k++) {
        if (oldLines[oi + k] === newLines[ni]) {
          foundInOld = oi + k
          break
        }
      }

      if (foundInNew >= 0 && (foundInOld < 0 || (foundInNew - ni) <= (foundInOld - oi))) {
        while (ni < foundInNew) {
          result.push({ type: 'added', text: newLines[ni] })
          ni++
        }
      } else if (foundInOld >= 0) {
        while (oi < foundInOld) {
          result.push({ type: 'removed', text: oldLines[oi] })
          oi++
        }
      } else {
        result.push({ type: 'removed', text: oldLines[oi] })
        result.push({ type: 'added', text: newLines[ni] })
        oi++
        ni++
      }
    }
  }

  return result
}

function VersionHistoryPanel({
  versions,
  isLoading,
  error,
  expandedVersion,
  onToggleExpand,
  showRestoreConfirm,
  onShowRestoreConfirm,
  restoreMut,
}: {
  versions: KnowledgeVersion[] | undefined
  isLoading: boolean
  error: Error | null
  expandedVersion: number | null
  onToggleExpand: (vn: number) => void
  showRestoreConfirm: number | null
  onShowRestoreConfirm: (vn: number | null) => void
  restoreMut: { mutate: (vn: number) => void; isPending: boolean; error: Error | null }
}) {
  const [diffVersionNum, setDiffVersionNum] = useState<number | null>(null)

  if (isLoading) {
    return (
      <div className="space-y-3">
        {[1, 2, 3].map((i) => (
          <div key={i} className="h-16 bg-muted rounded-lg animate-pulse" />
        ))}
      </div>
    )
  }

  if (error) {
    const is404 = error instanceof ApiError && error.status === 404
    return (
      <div className="text-center py-8">
        <History size={32} className="mx-auto text-muted-foreground mb-3" />
        <p className="text-muted-foreground">
          {is404
            ? 'Version history is not available for this item.'
            : error.message || 'Failed to load version history.'}
        </p>
      </div>
    )
  }

  if (!versions || versions.length === 0) {
    return (
      <div className="text-center py-8">
        <History size={32} className="mx-auto text-muted-foreground mb-3" />
        <p className="text-muted-foreground">No version history available yet.</p>
        <p className="text-sm text-muted-foreground mt-1">
          Versions are created each time the item is edited.
        </p>
      </div>
    )
  }

  const sortedVersions = [...versions].sort((a, b) => b.versionNumber - a.versionNumber)

  return (
    <div className="space-y-3">
      {sortedVersions.map((version, idx) => {
        const isExpanded = expandedVersion === version.versionNumber
        const previousVersion = idx < sortedVersions.length - 1 ? sortedVersions[idx + 1] : null
        const showDiff = diffVersionNum === version.versionNumber

        return (
          <div
            key={version.id}
            className="border border-border/60 rounded-xl shadow-sm overflow-hidden"
          >
            <button
              onClick={() => onToggleExpand(version.versionNumber)}
              className="w-full flex items-center justify-between px-4 py-3 hover:bg-muted/30 transition-colors text-left"
            >
              <div className="flex items-center gap-3 min-w-0">
                {isExpanded ? (
                  <ChevronDown size={16} className="text-muted-foreground flex-shrink-0" />
                ) : (
                  <ChevronRight size={16} className="text-muted-foreground flex-shrink-0" />
                )}
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium">
                      Version {version.versionNumber}
                    </span>
                    {idx === 0 && (
                      <span className="px-1.5 py-0.5 text-[10px] font-medium bg-green-50 dark:bg-green-950/30 text-green-700 dark:text-green-400 rounded">
                        Latest
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {new Date(version.createdAt).toLocaleString()}
                    {version.changeDescription && ` - ${version.changeDescription}`}
                  </p>
                </div>
              </div>
              {idx > 0 && (
                <button
                  onClick={(e) => {
                    e.stopPropagation()
                    onShowRestoreConfirm(version.versionNumber)
                  }}
                  className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-input rounded hover:bg-muted transition-colors flex-shrink-0"
                  title="Restore this version"
                >
                  <RotateCcw size={12} /> Restore
                </button>
              )}
            </button>

            {isExpanded && (
              <div className="border-t border-border/60 px-4 py-3 space-y-3">
                <div className="flex items-center gap-2">
                  <h4 className="text-sm font-medium text-muted-foreground">Title:</h4>
                  <span className="text-sm">{version.title}</span>
                </div>

                {previousVersion && (
                  <button
                    onClick={() => setDiffVersionNum(showDiff ? null : version.versionNumber)}
                    className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-input rounded hover:bg-muted transition-colors"
                  >
                    {showDiff ? 'Hide Changes' : 'Show Changes'}
                  </button>
                )}

                {showDiff && previousVersion ? (
                  <DiffView
                    oldContent={previousVersion.content}
                    newContent={version.content}
                  />
                ) : (
                  <div className="whitespace-pre-wrap text-sm bg-muted border border-border/60 rounded-lg p-3 max-h-80 overflow-y-auto font-mono">
                    {version.content}
                  </div>
                )}
              </div>
            )}
          </div>
        )
      })}

      {/* Restore Confirmation Modal */}
      {showRestoreConfirm !== null && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-card rounded-xl p-6 max-w-sm w-full space-y-4 shadow-sm">
            <h2 className="text-lg font-semibold">Restore Version {showRestoreConfirm}?</h2>
            <p className="text-sm text-muted-foreground">
              This will replace the current content with the content from version {showRestoreConfirm}. A new version will be created with the current content.
            </p>
            {restoreMut.error && (
              <p className="text-red-600 dark:text-red-400 text-sm">
                {restoreMut.error instanceof Error ? restoreMut.error.message : 'Restore failed'}
              </p>
            )}
            <div className="flex gap-2 justify-end">
              <button
                onClick={() => onShowRestoreConfirm(null)}
                className="px-4 py-2 border border-input rounded-md text-sm transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={() => restoreMut.mutate(showRestoreConfirm)}
                disabled={restoreMut.isPending}
                className="px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium disabled:opacity-50"
              >
                {restoreMut.isPending ? 'Restoring...' : 'Restore'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

function DiffView({ oldContent, newContent }: { oldContent: string; newContent: string }) {
  const diffLines = useMemo(() => computeSimpleDiff(oldContent, newContent), [oldContent, newContent])

  return (
    <div className="border border-border/60 rounded-lg overflow-hidden max-h-80 overflow-y-auto">
      <div className="font-mono text-xs">
        {diffLines.map((line, i) => (
          <div
            key={i}
            className={`px-3 py-0.5 whitespace-pre-wrap ${
              line.type === 'added'
                ? 'bg-green-50 dark:bg-green-950/30 text-green-800 dark:text-green-300'
                : line.type === 'removed'
                  ? 'bg-red-50 dark:bg-red-950/30 text-red-800 dark:text-red-300'
                  : 'text-muted-foreground'
            }`}
          >
            <span className="select-none mr-2 text-muted-foreground">
              {line.type === 'added' ? '+' : line.type === 'removed' ? '-' : ' '}
            </span>
            {line.text}
          </div>
        ))}
      </div>
    </div>
  )
}
