import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '../lib/api-client'
import { Archive, RefreshCw, Plus, X } from 'lucide-react'

export default function VaultListPage() {
  const queryClient = useQueryClient()
  const [showCreate, setShowCreate] = useState(false)
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [vaultType, setVaultType] = useState('')
  const [createError, setCreateError] = useState('')

  const { data, isLoading, error, refetch } = useQuery({
    queryKey: ['vaults', 'list'],
    queryFn: () => api.listVaults(true),
  })

  const createMutation = useMutation({
    mutationFn: () =>
      api.createVault({
        name: name.trim(),
        description: description.trim() || undefined,
        vaultType: vaultType || undefined,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['vaults'] })
      setShowCreate(false)
      setName('')
      setDescription('')
      setVaultType('')
      setCreateError('')
    },
    onError: (err) => {
      setCreateError(err instanceof Error ? err.message : 'Failed to create vault')
    },
  })

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault()
    if (!name.trim()) return
    setCreateError('')
    createMutation.mutate()
  }

  if (error) {
    return (
      <div className="space-y-4">
        <h1 className="text-2xl font-bold">Vaults</h1>
        <p className="text-red-600 dark:text-red-400">
          {error instanceof Error ? error.message : 'Failed to load vaults'}
        </p>
        <button
          onClick={() => refetch()}
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium transition-colors"
        >
          <RefreshCw size={16} /> Retry
        </button>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Vaults</h1>
        <button
          onClick={() => setShowCreate(true)}
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium hover:opacity-90 transition-opacity"
        >
          <Plus size={16} /> Create Vault
        </button>
      </div>

      {/* Create Vault Modal */}
      {showCreate && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-card rounded-xl shadow-xl w-full max-w-md">
            <div className="flex items-center justify-between px-6 py-4 border-b border-border/60">
              <h2 className="text-lg font-semibold">Create Vault</h2>
              <button
                onClick={() => {
                  setShowCreate(false)
                  setCreateError('')
                }}
                className="p-1 rounded hover:bg-muted transition-colors"
              >
                <X size={20} />
              </button>
            </div>
            <form onSubmit={handleCreate} className="p-6 space-y-4">
              <div>
                <label className="block text-sm font-medium mb-1">Name *</label>
                <input
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="My Vault"
                  className="w-full px-3 py-2 border border-input rounded-md bg-card text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                  autoFocus
                  required
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">Description</label>
                <textarea
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Optional description..."
                  rows={3}
                  className="w-full px-3 py-2 border border-input rounded-md bg-card text-sm focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                />
              </div>
              <div>
                <label className="block text-sm font-medium mb-1">Type</label>
                <select
                  value={vaultType}
                  onChange={(e) => setVaultType(e.target.value)}
                  className="w-full px-3 py-2 border border-input rounded-md bg-card text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                >
                  <option value="">General Knowledge</option>
                  <option value="Business">Business</option>
                  <option value="Product">Product</option>
                  <option value="CodeBase">CodeBase</option>
                  <option value="DailyDiary">Daily Diary</option>
                  <option value="QuestionAnswer">Question & Answer</option>
                  <option value="PersonBound">Person Bound</option>
                  <option value="LocationBound">Location Bound</option>
                </select>
              </div>
              {createError && (
                <p className="text-sm text-red-600 dark:text-red-400">{createError}</p>
              )}
              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => {
                    setShowCreate(false)
                    setCreateError('')
                  }}
                  className="px-4 py-2 text-sm font-medium text-muted-foreground hover:bg-muted rounded-md transition-colors"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!name.trim() || createMutation.isPending}
                  className="px-4 py-2 text-sm font-medium bg-primary text-primary-foreground rounded-md hover:opacity-90 disabled:opacity-50 transition-opacity"
                >
                  {createMutation.isPending ? 'Creating...' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-32 bg-muted rounded-xl animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {data?.vaults.map((vault) => (
            <Link
              key={vault.id}
              to={`/knowledge?vaultId=${vault.id}`}
              className="block p-5 bg-card border border-border/60 rounded-xl hover:shadow-md transition-all"
            >
              <div className="flex items-center gap-3 mb-2">
                <Archive size={18} className="text-muted-foreground" />
                <h3 className="font-semibold">{vault.name}</h3>
                {vault.isDefault && (
                  <span className="px-1.5 py-0.5 text-xs bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded">
                    Default
                  </span>
                )}
              </div>
              {vault.description && (
                <p className="text-sm text-muted-foreground line-clamp-2 mb-2">
                  {vault.description}
                </p>
              )}
              <p className="text-sm text-muted-foreground">
                {vault.knowledgeCount ?? 0} items
              </p>
              {vault.vaultType && (
                <span className="inline-block mt-2 px-2 py-0.5 text-xs bg-muted rounded">
                  {vault.vaultType}
                </span>
              )}
            </Link>
          ))}
          {data?.vaults.length === 0 && (
            <p className="text-muted-foreground col-span-full text-center py-8">
              No vaults found. Create one to get started.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
