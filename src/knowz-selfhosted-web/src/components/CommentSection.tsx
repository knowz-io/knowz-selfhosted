import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { MessageSquare, Paperclip, Loader2 } from 'lucide-react'
import type { Comment } from '../lib/types'

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

  return (
    <div className={isReply ? 'ml-8 border-l-2 border-border pl-4' : ''}>
      <div className="py-3">
        <div className="flex items-center gap-2 mb-1">
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
          <p className="text-sm">{comment.body}</p>
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

  const [newBody, setNewBody] = useState('')
  const [replyingTo, setReplyingTo] = useState<string | null>(null)
  const [replyBody, setReplyBody] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editBody, setEditBody] = useState('')
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null)

  const { data: comments, isLoading } = useQuery({
    queryKey: ['comments', knowledgeId],
    queryFn: () => api.listComments(knowledgeId),
  })

  const addMut = useMutation({
    mutationFn: (data: { body: string; parentCommentId?: string }) =>
      api.addComment(knowledgeId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['comments', knowledgeId] })
      setNewBody('')
      setReplyingTo(null)
      setReplyBody('')
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
          placeholder="Add a contribution..."
          className="w-full px-3 py-2 text-sm border border-input rounded-lg bg-card focus:outline-none focus:ring-1 focus:ring-ring transition-colors"
          rows={3}
        />
        <button
          onClick={handleAddComment}
          disabled={!newBody.trim() || addMut.isPending}
          className="px-4 py-1.5 text-sm bg-primary text-primary-foreground rounded-lg font-medium disabled:opacity-50 hover:opacity-90 transition-opacity"
        >
          {addMut.isPending ? 'Adding...' : 'Add Contribution'}
        </button>
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
        <div className="divide-y divide-border">
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
