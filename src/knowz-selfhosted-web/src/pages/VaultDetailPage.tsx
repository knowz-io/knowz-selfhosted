import { useState } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { ArrowLeft, BookOpen, Pencil, Trash2, X } from 'lucide-react'

export default function VaultDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [showEdit, setShowEdit] = useState(false)
  const [editName, setEditName] = useState('')
  const [editDescription, setEditDescription] = useState('')
  const [editError, setEditError] = useState('')

  const [showDelete, setShowDelete] = useState(false)

  const { data: vault, isLoading: vaultLoading } = useQuery({
    queryKey: ['vault', id],
    queryFn: () => api.getVault(id!),
    enabled: !!id,
  })

  const { data: contents, isLoading: contentsLoading, error } = useQuery({
    queryKey: ['vault-contents', id],
    queryFn: () => api.getVaultContents(id!),
    enabled: !!id,
  })

  const updateMutation = useMutation({
    mutationFn: () =>
      api.updateVault(id!, {
        name: editName.trim() || undefined,
        description: editDescription.trim(),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vault', id] })
      queryClient.invalidateQueries({ queryKey: ['vaults'] })
      setShowEdit(false)
      setEditError('')
    },
    onError: (err) => {
      setEditError(err instanceof Error ? err.message : 'Failed to update vault')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: () => api.deleteVault(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] })
      navigate('/vaults')
    },
  })

  const openEditModal = () => {
    setEditName(vault?.name ?? '')
    setEditDescription(vault?.description ?? '')
    setEditError('')
    setShowEdit(true)
  }

  const handleUpdate = (e: React.FormEvent) => {
    e.preventDefault()
    if (!editName.trim()) return
    setEditError('')
    updateMutation.mutate()
  }

  const isLoading = vaultLoading || contentsLoading

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="h-14 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
        ))}
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-4">
        <Link to="/vaults" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-900 dark:hover:text-white">
          <ArrowLeft size={16} /> Back to Vaults
        </Link>
        <p className="text-red-600 dark:text-red-400">
          {error instanceof Error ? error.message : 'Failed to load vault contents'}
        </p>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <Link
        to="/vaults"
        className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-900 dark:hover:text-white"
      >
        <ArrowLeft size={16} /> Back to Vaults
      </Link>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{vault?.name ?? 'Vault Contents'}</h1>
          {vault?.description && (
            <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">{vault.description}</p>
          )}
        </div>
        <div className="flex items-center gap-2">
          {vault?.vaultType && (
            <span className="px-2 py-0.5 text-xs bg-gray-100 dark:bg-gray-800 rounded">
              {vault.vaultType}
            </span>
          )}
          <span className="text-sm text-gray-500 dark:text-gray-400">
            {contents?.totalItems ?? 0} items
          </span>
          <button
            onClick={openEditModal}
            className="p-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 transition-colors"
            title="Edit vault"
          >
            <Pencil size={16} />
          </button>
          {!vault?.isDefault && (
            <button
              onClick={() => setShowDelete(true)}
              className="p-2 rounded hover:bg-red-50 dark:hover:bg-red-950/30 text-gray-500 hover:text-red-600 dark:hover:text-red-400 transition-colors"
              title="Delete vault"
            >
              <Trash2 size={16} />
            </button>
          )}
        </div>
      </div>

      {/* Edit Modal */}
      {showEdit && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-gray-900 rounded-lg shadow-xl w-full max-w-md">
            <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-800">
              <h2 className="text-lg font-semibold">Edit Vault</h2>
              <button
                onClick={() => setShowEdit(false)}
                className="p-1 rounded hover:bg-gray-100 dark:hover:bg-gray-800"
              >
                <X size={20} />
              </button>
            </div>
            <form onSubmit={handleUpdate} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">Name *</label>
                <input
                  type="text"
                  value={editName}
                  onChange={(e) => setEditName(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 text-sm focus:outline-none focus:ring-2 focus:ring-gray-900 dark:focus:ring-white"
                  autoFocus
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">Description</label>
                <textarea
                  value={editDescription}
                  onChange={(e) => setEditDescription(e.target.value)}
                  rows={3}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 text-sm focus:outline-none focus:ring-2 focus:ring-gray-900 dark:focus:ring-white resize-none"
                />
              </div>
              {editError && (
                <p className="text-sm text-red-600 dark:text-red-400">{editError}</p>
              )}
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setShowEdit(false)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!editName.trim() || updateMutation.isPending}
                  className="px-4 py-2 text-sm font-medium bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md hover:opacity-90 disabled:opacity-50 transition-opacity"
                >
                  {updateMutation.isPending ? 'Saving...' : 'Save'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {showDelete && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-gray-900 rounded-lg shadow-xl w-full max-w-sm">
            <div className="p-6 space-y-4">
              <h2 className="text-lg font-semibold">Delete Vault</h2>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                Are you sure you want to delete <strong>{vault?.name}</strong>? This action cannot be undone.
                Knowledge items in this vault will not be deleted.
              </p>
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setShowDelete(false)}
                  className="px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md"
                >
                  Cancel
                </button>
                <button
                  onClick={() => deleteMutation.mutate()}
                  disabled={deleteMutation.isPending}
                  className="px-4 py-2 text-sm font-medium bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 transition-colors"
                >
                  {deleteMutation.isPending ? 'Deleting...' : 'Delete'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="space-y-2">
        {contents?.items.map((item) => (
          <Link
            key={item.id}
            to={`/knowledge/${item.id}`}
            className="flex items-center gap-3 p-3 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg hover:border-gray-400 dark:hover:border-gray-600 transition-colors"
          >
            <BookOpen size={16} className="text-gray-400 flex-shrink-0" />
            <div className="min-w-0 flex-1">
              <p className="font-medium truncate">{item.title}</p>
              {item.summary && (
                <p className="text-sm text-gray-500 dark:text-gray-400 truncate">{item.summary}</p>
              )}
            </div>
            <span className="px-2 py-0.5 text-xs bg-gray-100 dark:bg-gray-800 rounded flex-shrink-0">
              {item.type}
            </span>
            <span className="text-xs text-gray-500 dark:text-gray-400 flex-shrink-0">
              {new Date(item.createdAt).toLocaleDateString()}
            </span>
          </Link>
        ))}
        {contents?.items.length === 0 && (
          <p className="text-gray-500 dark:text-gray-400 text-center py-8">
            This vault is empty.
          </p>
        )}
      </div>
    </div>
  )
}
