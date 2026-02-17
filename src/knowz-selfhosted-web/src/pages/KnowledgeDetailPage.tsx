import { useState, useRef } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { ArrowLeft, Pencil, Trash2, Save, X, Paperclip, Download, Upload, Loader2, RefreshCw } from 'lucide-react'
import { formatMarkdown } from '../lib/format-markdown'
import CommentSection from '../components/CommentSection'
import type { FileMetadataDto } from '../lib/types'

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const k = 1024
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  const size = bytes / Math.pow(k, i)
  return `${size.toFixed(i > 0 ? 1 : 0)} ${units[i]}`
}

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

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
        <div className="h-64 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-4">
        <Link to="/knowledge" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-900 dark:hover:text-white">
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
        className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-900 dark:hover:text-white"
      >
        <ArrowLeft size={16} /> Back to Knowledge
      </Link>

      {editing ? (
        <div className="space-y-4">
          <input
            type="text"
            value={editTitle}
            onChange={(e) => setEditTitle(e.target.value)}
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-lg font-semibold"
          />
          <div className="border border-gray-300 dark:border-gray-700 rounded-md overflow-hidden">
            <div className="flex border-b border-gray-300 dark:border-gray-700 bg-gray-50 dark:bg-gray-800">
              <button
                type="button"
                onClick={() => setEditTab('write')}
                className={`px-4 py-2 text-sm font-medium transition-colors ${
                  editTab === 'write'
                    ? 'text-gray-900 dark:text-white bg-white dark:bg-gray-900 border-b-2 border-gray-900 dark:border-white'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                }`}
              >
                Write
              </button>
              <button
                type="button"
                onClick={() => setEditTab('preview')}
                className={`px-4 py-2 text-sm font-medium transition-colors ${
                  editTab === 'preview'
                    ? 'text-gray-900 dark:text-white bg-white dark:bg-gray-900 border-b-2 border-gray-900 dark:border-white'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
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
                className="w-full px-3 py-2 bg-white dark:bg-gray-900 text-sm font-mono border-0 focus:ring-0 focus:outline-none"
              />
            ) : (
              <div className="px-3 py-2 bg-white dark:bg-gray-900 min-h-[384px]">
                {editContent.trim() ? (
                  <div
                    className="prose prose-sm dark:prose-invert max-w-none"
                    dangerouslySetInnerHTML={{ __html: formatMarkdown(editContent) }}
                  />
                ) : (
                  <p className="text-gray-400 dark:text-gray-500 text-sm italic">
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
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
          />
          <input
            type="text"
            value={editTags}
            onChange={(e) => setEditTags(e.target.value)}
            placeholder="Tags (comma-separated)"
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
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
              className="inline-flex items-center gap-2 px-4 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium disabled:opacity-50"
            >
              <Save size={16} /> {updateMut.isPending ? 'Saving...' : 'Save'}
            </button>
            <button
              onClick={() => setEditing(false)}
              className="inline-flex items-center gap-2 px-4 py-2 border border-gray-300 dark:border-gray-700 rounded-md text-sm font-medium"
            >
              <X size={16} /> Cancel
            </button>
          </div>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="flex items-start justify-between gap-4">
            <h1 className="text-2xl font-bold">{data.title}</h1>
            <div className="flex gap-2 flex-shrink-0">
              <button
                onClick={startEditing}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md text-sm"
              >
                <Pencil size={14} /> Edit
              </button>
              <button
                onClick={() => { setReprocessMsg(null); reprocessMut.mutate() }}
                disabled={reprocessMut.isPending}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md text-sm disabled:opacity-50"
              >
                <RefreshCw size={14} className={reprocessMut.isPending ? 'animate-spin' : ''} /> {reprocessMut.isPending ? 'Reprocessing...' : 'Reprocess'}
              </button>
              <button
                onClick={() => setShowDeleteConfirm(true)}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 border border-red-300 dark:border-red-700 text-red-600 dark:text-red-400 rounded-md text-sm"
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

          <div className="flex flex-wrap gap-2 text-sm">
            <span className="px-2 py-0.5 bg-gray-100 dark:bg-gray-800 rounded">
              {data.type}
            </span>
            {data.tags.map((tag) => (
              <span key={tag} className="px-2 py-0.5 bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded">
                {tag}
              </span>
            ))}
          </div>

          {data.summary && (
            <p className="text-gray-600 dark:text-gray-400 italic">{data.summary}</p>
          )}

          <div className="whitespace-pre-wrap text-sm bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg p-4">
            {data.content}
          </div>

          <div className="text-sm text-gray-500 dark:text-gray-400 space-y-1">
            {data.source && <p>Source: {data.source}</p>}
            {data.filePath && <p>File: {data.filePath}</p>}
            {data.vaults.length > 0 && (
              <p>
                Vaults:{' '}
                {data.vaults.map((v) => (
                  <Link
                    key={v.id}
                    to={`/vaults/${v.id}`}
                    className="text-blue-600 dark:text-blue-400 hover:underline mr-2"
                  >
                    {v.name}
                  </Link>
                ))}
              </p>
            )}
            {data.topic && (
              <p>Topic: {data.topic.name}</p>
            )}
            <p>
              Created: {new Date(data.createdAt).toLocaleString()} | Updated: {new Date(data.updatedAt).toLocaleString()}
            </p>
            <p>
              Enrichment:{' '}
              {data.isIndexed ? (
                <>
                  <span className="text-green-600 dark:text-green-400">Indexed</span>
                  {data.indexedAt && ` on ${new Date(data.indexedAt).toLocaleString()}`}
                </>
              ) : (
                <span className="text-gray-500">Pending</span>
              )}
            </p>
          </div>

          {/* Attachments Section */}
          <div className="border border-gray-200 dark:border-gray-800 rounded-lg p-4 space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Paperclip size={16} className="text-gray-500" />
                <h2 className="text-sm font-semibold">Attachments</h2>
              </div>
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => attachFileInputRef.current?.click()}
                  className="inline-flex items-center gap-1 px-2 py-1 text-xs bg-blue-600 text-white rounded hover:bg-blue-700"
                >
                  <Upload size={12} />
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
                  className="inline-flex items-center gap-1 px-2 py-1 text-xs border border-gray-300 dark:border-gray-700 rounded hover:bg-gray-100 dark:hover:bg-gray-800"
                >
                  <Paperclip size={12} />
                  Attach File
                </button>
              </div>
            </div>

            {uploadAndAttachMut.isPending && (
              <div className="flex items-center gap-2 text-xs text-gray-500">
                <Loader2 size={14} className="animate-spin" />
                Uploading and attaching...
              </div>
            )}

            {attachmentsLoading ? (
              <div className="flex items-center justify-center py-4">
                <Loader2 size={16} className="animate-spin text-gray-400" />
              </div>
            ) : attachments && attachments.length > 0 ? (
              <div className="space-y-2">
                {attachments.map((file: FileMetadataDto) => (
                  <div
                    key={file.id}
                    className="flex items-center justify-between py-1.5 px-2 rounded hover:bg-gray-50 dark:hover:bg-gray-800/50"
                  >
                    <div className="flex items-center gap-2 min-w-0">
                      <Paperclip size={14} className="text-gray-400 flex-shrink-0" />
                      <span className="text-sm truncate">{file.fileName}</span>
                      <span className="text-xs text-gray-400 flex-shrink-0">
                        {formatFileSize(file.sizeBytes)}
                      </span>
                    </div>
                    <div className="flex items-center gap-1 flex-shrink-0">
                      <button
                        onClick={() => handleDownloadAttachment(file.id, file.fileName)}
                        className="p-1 text-gray-400 hover:text-blue-600 rounded"
                        title="Download"
                      >
                        <Download size={14} />
                      </button>
                      <button
                        onClick={() => detachMut.mutate(file.id)}
                        className="p-1 text-gray-400 hover:text-red-600 rounded"
                        title="Detach"
                      >
                        <X size={14} />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p className="text-xs text-gray-400 py-2">No attachments yet.</p>
            )}

            {/* Attach from existing files picker */}
            {showAttachPicker && (
              <div className="border border-gray-200 dark:border-gray-700 rounded p-3 space-y-2">
                <p className="text-xs font-medium text-gray-600 dark:text-gray-400">
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
                          className="w-full flex items-center gap-2 px-2 py-1.5 text-left text-sm rounded hover:bg-gray-100 dark:hover:bg-gray-800 disabled:opacity-50"
                        >
                          <Paperclip size={12} className="text-gray-400" />
                          <span className="truncate">{file.fileName}</span>
                          <span className="text-xs text-gray-400 ml-auto">
                            {formatFileSize(file.sizeBytes)}
                          </span>
                        </button>
                      ))}
                  </div>
                ) : (
                  <p className="text-xs text-gray-400">No files available.</p>
                )}
                <button
                  type="button"
                  onClick={() => setShowAttachPicker(false)}
                  className="text-xs text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
                >
                  Close
                </button>
              </div>
            )}
          </div>

          {/* Contributions Section */}
          <CommentSection knowledgeId={id!} />
        </div>
      )}

      {showDeleteConfirm && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-gray-900 rounded-lg p-6 max-w-sm w-full space-y-4">
            <h2 className="text-lg font-semibold">Delete Knowledge Item?</h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">
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
                className="px-4 py-2 border border-gray-300 dark:border-gray-700 rounded-md text-sm"
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
    </div>
  )
}
