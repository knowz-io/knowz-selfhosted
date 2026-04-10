import { useState, useRef, useEffect, type KeyboardEvent } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { MessageSquare, Paperclip, Loader2, X, Download, Image } from 'lucide-react'
import MarkdownContent from './MarkdownContent'
import { formatFileSize } from '../lib/format-utils'
import type { Comment, FileMetadataDto } from '../lib/types'

interface CommentSectionProps {
  knowledgeId: string
}

function timeAgo(dateStr: string): string {
  const now = Date.now()
  const then = new Date(dateStr).getTime()
  const seconds = Math.floor((now - then) / 1000)
  if (seconds < 60) return 'just now'
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`
  return new Date(dateStr).toLocaleDateString()
}

function getInitials(name: string): string {
  const parts = name.trim().split(/\s+/)
  if (parts.length === 1) return parts[0][0].toUpperCase()
  return (parts[0][0] + parts[1][0]).toUpperCase()
}

interface PendingFile {
  file: File
  fileRecordId: string | null
  uploading: boolean
}

function isImageContentType(contentType?: string): boolean {
  return !!contentType && contentType.startsWith('image/')
}

function CommentAttachmentChip({ file }: { file: FileMetadataDto }) {
  const [thumbUrl, setThumbUrl] = useState<string | null>(null)
  const [thumbLoading, setThumbLoading] = useState(false)

  useEffect(() => {
    if (!isImageContentType(file.contentType)) return
    let cancelled = false
    setThumbLoading(true)
    api.downloadFile(file.id)
      .then((blob) => {
        if (!cancelled) {
          setThumbUrl(URL.createObjectURL(blob))
        }
      })
      .catch(() => {})
      .finally(() => { if (!cancelled) setThumbLoading(false) })
    return () => {
      cancelled = true
      if (thumbUrl) URL.revokeObjectURL(thumbUrl)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [file.id, file.contentType])

  const handleDownload = async () => {
    try {
      const blob = await api.downloadFile(file.id)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = file.fileName
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch {
      // silent
    }
  }

  return (
    <div className="inline-flex flex-col items-start gap-1 p-1.5 bg-muted/60 rounded-md border border-border/40">
      {isImageContentType(file.contentType) && (
        <div className="rounded overflow-hidden bg-muted/30 flex items-center justify-center" style={{ maxHeight: 100 }}>
          {thumbLoading ? (
            <div className="flex items-center justify-center w-20 h-16">
              <Loader2 size={14} className="animate-spin text-muted-foreground" />
            </div>
          ) : thumbUrl ? (
            <img src={thumbUrl} alt={file.fileName} className="max-h-[100px] max-w-[160px] object-contain" />
          ) : null}
        </div>
      )}
      <div className="inline-flex items-center gap-1.5">
        {isImageContentType(file.contentType) ? (
          <Image size={12} className="text-blue-500 flex-shrink-0" />
        ) : (
          <Paperclip size={12} className="text-muted-foreground flex-shrink-0" />
        )}
        <span className="text-xs truncate max-w-[120px]" title={file.fileName}>{file.fileName}</span>
        <span className="text-[10px] text-muted-foreground">{formatFileSize(file.sizeBytes)}</span>
        <button
          onClick={handleDownload}
          className="p-0.5 rounded hover:bg-background transition-colors text-muted-foreground hover:text-blue-600"
          title="Download"
        >
          <Download size={12} />
        </button>
      </div>
    </div>
  )
}

function CommentAttachments({ commentId, attachmentCount }: { commentId: string; attachmentCount: number }) {
  const { data: attachments, isLoading } = useQuery({
    queryKey: ['comment-attachments', commentId],
    queryFn: () => api.getCommentAttachments(commentId),
    enabled: attachmentCount > 0,
  })

  if (attachmentCount === 0) return null
  if (isLoading) {
    return (
      <div className="flex items-center gap-1.5 mt-1.5">
        <Loader2 size={12} className="animate-spin text-muted-foreground" />
        <span className="text-[10px] text-muted-foreground">Loading attachments...</span>
      </div>
    )
  }
  if (!attachments || attachments.length === 0) return null

  return (
    <div className="flex flex-wrap gap-1.5 mt-1.5">
      {attachments.map((file) => (
        <CommentAttachmentChip key={file.id} file={file} />
      ))}
    </div>
  )
}

function CommentItem({
  comment,
  knowledgeId,
  isReply,
  replyingTo,
  setReplyingTo,
  editingId,
  setEditingId,
  editBody,
  setEditBody,
  deleteConfirmId,
  setDeleteConfirmId,
  onReplySubmit,
  onEditSave,
  onDeleteConfirm,
  replyBody,
  setReplyBody,
  isMutating,
}: {
  comment: Comment
  knowledgeId: string
  isReply: boolean
  replyingTo: string | null
  setReplyingTo: (id: string | null) => void
  editingId: string | null
  setEditingId: (id: string | null) => void
  editBody: string
  setEditBody: (body: string) => void
  deleteConfirmId: string | null
  setDeleteConfirmId: (id: string | null) => void
  onReplySubmit: () => void
  onEditSave: () => void
  onDeleteConfirm: () => void
  replyBody: string
  setReplyBody: (body: string) => void
  isMutating: boolean
}) {
  const isEditing = editingId === comment.id
  const isDeleting = deleteConfirmId === comment.id
  const isReplying = replyingTo === comment.id

  const handleReplyKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault()
      onReplySubmit()
    }
  }

  return (
    <div className={isReply ? 'ml-8 border-l-2 border-border pl-4' : ''}>
      <div
        data-testid="comment-card"
        className="p-3 rounded-lg border border-border hover:border-primary/30 transition-colors"
      >
        <div className="flex items-center gap-2 mb-1">
          <div className="w-8 h-8 rounded-full bg-gradient-to-br from-primary/20 to-primary/10 flex items-center justify-center text-xs font-medium text-primary shrink-0">
            {getInitials(comment.authorName)}
          </div>
          <span className="text-sm font-medium">{comment.authorName}</span>
          <span className="text-xs text-muted-foreground">{timeAgo(comment.createdAt)}</span>
          {comment.sentiment && (
            <span className="text-xs px-1.5 py-0.5 bg-muted rounded">
              {comment.sentiment}
            </span>
          )}
          {comment.attachmentCount > 0 && (
            <span className="inline-flex items-center gap-0.5 text-xs text-muted-foreground">
              <Paperclip size={10} />
              <span>{comment.attachmentCount}</span>
            </span>
          )}
        </div>

        {isEditing ? (
          <div className="space-y-2">
            <textarea
              value={editBody}
              onChange={(e) => setEditBody(e.target.value)}
              className="w-full px-3 py-2 text-sm border border-input rounded-lg bg-card focus:outline-none focus:ring-1 focus:ring-ring transition-colors"
              rows={3}
            />
            <div className="flex gap-2">
              <button
                onClick={onEditSave}
                disabled={!editBody.trim() || isMutating}
                className="px-3 py-1 text-xs bg-primary text-primary-foreground rounded-lg disabled:opacity-50"
                aria-label="Save"
              >
                Save
              </button>
              <button
                onClick={() => setEditingId(null)}
                className="px-3 py-1 text-xs border border-input rounded-lg hover:bg-muted transition-colors"
                aria-label="Cancel edit"
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <>
            <MarkdownContent content={comment.body} compact />
            <CommentAttachments commentId={comment.id} attachmentCount={comment.attachmentCount} />
          </>
        )}

        {isDeleting && (
          <div className="mt-2 p-2 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg text-sm">
            <p className="text-red-700 dark:text-red-300">Are you sure you want to delete this contribution?</p>
            <div className="flex gap-2 mt-2">
              <button
                onClick={onDeleteConfirm}
                disabled={isMutating}
                className="px-3 py-1 text-xs bg-red-600 text-white rounded-lg disabled:opacity-50"
                aria-label="Confirm"
              >
                Confirm
              </button>
              <button
                onClick={() => setDeleteConfirmId(null)}
                className="px-3 py-1 text-xs border border-input rounded-lg hover:bg-muted transition-colors"
                aria-label="Cancel"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {!isEditing && !isDeleting && (
          <div className="flex gap-3 mt-1">
            {!isReply && (
              <button
                onClick={() => {
                  setReplyingTo(isReplying ? null : comment.id)
                  setReplyBody('')
                }}
                className="text-xs text-muted-foreground hover:text-foreground transition-colors"
                aria-label="Reply"
              >
                Reply
              </button>
            )}
            <button
              onClick={() => {
                setEditingId(comment.id)
                setEditBody(comment.body)
              }}
              className="text-xs text-muted-foreground hover:text-foreground transition-colors"
              aria-label="Edit"
            >
              Edit
            </button>
            <button
              onClick={() => setDeleteConfirmId(comment.id)}
              className="text-xs text-muted-foreground hover:text-red-500 transition-colors"
              aria-label="Delete"
            >
              Delete
            </button>
          </div>
        )}

        {isReplying && (
          <div className="mt-2 space-y-2">
            <textarea
              value={replyBody}
              onChange={(e) => setReplyBody(e.target.value)}
              onKeyDown={handleReplyKeyDown}
              placeholder="Write a reply..."
              className="w-full px-3 py-2 text-sm border border-input rounded-lg bg-card focus:outline-none focus:ring-1 focus:ring-ring transition-colors"
              rows={2}
            />
            <div className="flex gap-2">
              <button
                onClick={onReplySubmit}
                disabled={!replyBody.trim() || isMutating}
                className="px-3 py-1 text-xs bg-primary text-primary-foreground rounded-lg disabled:opacity-50"
                aria-label="Submit"
              >
                Submit
              </button>
              <button
                onClick={() => setReplyingTo(null)}
                className="px-3 py-1 text-xs border border-input rounded-lg hover:bg-muted transition-colors"
              >
                Cancel
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Render replies */}
      {comment.replies && comment.replies.length > 0 && (
        <div>
          {comment.replies.map((reply) => (
            <CommentItem
              key={reply.id}
              comment={reply}
              knowledgeId={knowledgeId}
              isReply={true}
              replyingTo={replyingTo}
              setReplyingTo={setReplyingTo}
              editingId={editingId}
              setEditingId={setEditingId}
              editBody={editBody}
              setEditBody={setEditBody}
              deleteConfirmId={deleteConfirmId}
              setDeleteConfirmId={setDeleteConfirmId}
              onReplySubmit={onReplySubmit}
              onEditSave={onEditSave}
              onDeleteConfirm={onDeleteConfirm}
              replyBody={replyBody}
              setReplyBody={setReplyBody}
              isMutating={isMutating}
            />
          ))}
        </div>
      )}
    </div>
  )
}

export default function CommentSection({ knowledgeId }: CommentSectionProps) {
  const queryClient = useQueryClient()
  const fileInputRef = useRef<HTMLInputElement>(null)

  const [newBody, setNewBody] = useState('')
  const [replyingTo, setReplyingTo] = useState<string | null>(null)
  const [replyBody, setReplyBody] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editBody, setEditBody] = useState('')
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null)
  const [pendingFiles, setPendingFiles] = useState<PendingFile[]>([])

  const { data: comments, isLoading } = useQuery({
    queryKey: ['comments', knowledgeId],
    queryFn: () => api.listComments(knowledgeId),
  })

  const addMut = useMutation({
    mutationFn: async (data: { body: string; parentCommentId?: string }) => {
      const comment = await api.addComment(knowledgeId, data)

      // Attach any pending files — only for top-level comments (not replies)
      if (!data.parentCommentId) {
        const uploadedFiles = pendingFiles.filter((pf) => pf.fileRecordId)
        for (const pf of uploadedFiles) {
          await api.attachFileToComment(comment.id, pf.fileRecordId!)
        }
      }

      return comment
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comments', knowledgeId] })
      queryClient.invalidateQueries({ queryKey: ['knowledge', knowledgeId] })
      queryClient.invalidateQueries({ queryKey: ['enrichment-status', knowledgeId] })
      setNewBody('')
      setReplyingTo(null)
      setReplyBody('')
      setPendingFiles([])
    },
  })

  const updateMut = useMutation({
    mutationFn: (data: { commentId: string; body: string }) =>
      api.updateComment(data.commentId, { body: data.body }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comments', knowledgeId] })
      setEditingId(null)
      setEditBody('')
    },
  })

  const deleteMut = useMutation({
    mutationFn: (commentId: string) => api.deleteComment(commentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comments', knowledgeId] })
      queryClient.invalidateQueries({ queryKey: ['knowledge', knowledgeId] })
      queryClient.invalidateQueries({ queryKey: ['enrichment-status', knowledgeId] })
      setDeleteConfirmId(null)
    },
  })

  const isMutating = addMut.isPending || updateMut.isPending || deleteMut.isPending

  const handleAddComment = () => {
    if (!newBody.trim()) return
    addMut.mutate({ body: newBody })
  }

  const handleReplySubmit = () => {
    if (!replyBody.trim() || !replyingTo) return
    addMut.mutate({ body: replyBody, parentCommentId: replyingTo })
  }

  const handleEditSave = () => {
    if (!editBody.trim() || !editingId) return
    updateMut.mutate({ commentId: editingId, body: editBody })
  }

  const handleDeleteConfirm = () => {
    if (!deleteConfirmId) return
    deleteMut.mutate(deleteConfirmId)
  }

  const handleNewCommentKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault()
      handleAddComment()
    }
  }

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files
    if (!files || files.length === 0) return

    for (const file of Array.from(files)) {
      const pending: PendingFile = { file, fileRecordId: null, uploading: true }
      setPendingFiles((prev) => [...prev, pending])

      try {
        const result = await api.uploadFile(file)
        setPendingFiles((prev) =>
          prev.map((pf) =>
            pf.file === file ? { ...pf, fileRecordId: result.fileRecordId, uploading: false } : pf,
          ),
        )
      } catch {
        // Remove failed upload from pending
        setPendingFiles((prev) => prev.filter((pf) => pf.file !== file))
      }
    }

    // Reset the input so the same file can be re-selected
    if (fileInputRef.current) fileInputRef.current.value = ''
  }

  const handleRemoveFile = (file: File) => {
    setPendingFiles((prev) => prev.filter((pf) => pf.file !== file))
  }

  return (
    <div className="bg-card border border-border/60 rounded-xl p-4 space-y-3 shadow-sm">
      <div className="flex items-center gap-2">
        <MessageSquare size={16} className="text-muted-foreground" />
        <h2 className="text-sm font-semibold">Contributions</h2>
        {comments && comments.length > 0 && (
          <span className="text-xs text-muted-foreground">({comments.length})</span>
        )}
      </div>

      {/* Add contribution form */}
      <div className="space-y-2">
        <textarea
          value={newBody}
          onChange={(e) => setNewBody(e.target.value)}
          onKeyDown={handleNewCommentKeyDown}
          placeholder="Add a contribution..."
          className="w-full px-3 py-2 text-sm border border-input rounded-lg bg-card focus:outline-none focus:ring-1 focus:ring-ring transition-colors"
          rows={3}
        />

        {/* File preview chips */}
        {pendingFiles.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {pendingFiles.map((pf, i) => (
              <div
                key={i}
                className="inline-flex items-center gap-1.5 px-2 py-1 bg-muted rounded-md text-xs"
              >
                <Paperclip size={12} className="text-muted-foreground" />
                <span>{pf.file.name}</span>
                <span className="text-muted-foreground">{formatFileSize(pf.file.size)}</span>
                {pf.uploading && <Loader2 size={12} className="animate-spin text-muted-foreground" />}
                <button
                  onClick={() => handleRemoveFile(pf.file)}
                  className="p-0.5 rounded hover:bg-background transition-colors"
                  aria-label="Remove file"
                >
                  <X size={12} />
                </button>
              </div>
            ))}
          </div>
        )}

        {/* Form action bar */}
        <div className="flex items-center gap-2">
          <input
            ref={fileInputRef}
            type="file"
            className="hidden"
            onChange={handleFileSelect}
          />
          <button
            onClick={() => fileInputRef.current?.click()}
            className="p-1.5 rounded hover:bg-muted transition-colors"
            aria-label="Attach file"
          >
            <Paperclip size={16} className="text-muted-foreground" />
          </button>
          <div className="flex-grow" />
          <button
            onClick={handleAddComment}
            disabled={!newBody.trim() || addMut.isPending}
            className="px-4 py-1.5 text-sm bg-primary text-primary-foreground rounded-lg font-medium disabled:opacity-50 hover:opacity-90 transition-opacity"
          >
            {addMut.isPending ? 'Adding...' : 'Add Contribution'}
          </button>
        </div>
      </div>

      {addMut.error && (
        <p className="text-xs text-red-600 dark:text-red-400">
          {addMut.error instanceof Error ? addMut.error.message : 'Failed to add contribution'}
        </p>
      )}

      {/* Comments list */}
      {isLoading ? (
        <div className="flex items-center justify-center py-4">
          <Loader2 size={16} className="animate-spin text-muted-foreground" />
        </div>
      ) : comments && comments.length > 0 ? (
        <div className="space-y-2">
          {comments.map((comment) => (
            <CommentItem
              key={comment.id}
              comment={comment}
              knowledgeId={knowledgeId}
              isReply={false}
              replyingTo={replyingTo}
              setReplyingTo={setReplyingTo}
              editingId={editingId}
              setEditingId={setEditingId}
              editBody={editBody}
              setEditBody={setEditBody}
              deleteConfirmId={deleteConfirmId}
              setDeleteConfirmId={setDeleteConfirmId}
              onReplySubmit={handleReplySubmit}
              onEditSave={handleEditSave}
              onDeleteConfirm={handleDeleteConfirm}
              replyBody={replyBody}
              setReplyBody={setReplyBody}
              isMutating={isMutating}
            />
          ))}
        </div>
      ) : (
        <p className="text-xs text-muted-foreground py-2">No contributions yet. Be the first to add one.</p>
      )}
    </div>
  )
}
