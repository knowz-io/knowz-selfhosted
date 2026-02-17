import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useSearchParams, useNavigate, Link } from 'react-router-dom'
import { api } from '../lib/api-client'
import { Plus, ChevronLeft, ChevronRight, Trash2, FolderInput, X, Loader2, RefreshCw } from 'lucide-react'

export default function KnowledgeListPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const page = Number(searchParams.get('page') || '1')
  const pageSize = 20
  const type = searchParams.get('type') || ''
  const title = searchParams.get('title') || ''
  const sort = searchParams.get('sort') || 'created'
  const sortDir = searchParams.get('sortDir') || 'desc'

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [showDeleteConfirm, setShowDeleteConfirm] = useState(false)
  const [showMoveToVault, setShowMoveToVault] = useState(false)
  const [batchError, setBatchError] = useState('')

  const { data, isLoading, error } = useQuery({
    queryKey: ['knowledge', page, type, title, sort, sortDir],
    queryFn: () =>
      api.listKnowledge({
        page: String(page),
        pageSize: String(pageSize),
        type: type || undefined,
        title: title || undefined,
        sort,
        sortDir,
      }),
  })

  const vaults = useQuery({
    queryKey: ['vaults', 'bulk-move'],
    queryFn: () => api.listVaults(false),
    enabled: showMoveToVault,
  })

  const batchDeleteMut = useMutation({
    mutationFn: async (ids: string[]) => {
      const results = await Promise.allSettled(ids.map((id) => api.deleteKnowledge(id)))
      const failed = results.filter((r) => r.status === 'rejected').length
      if (failed > 0) throw new Error(`${failed} of ${ids.length} deletes failed`)
    },
    onSuccess: () => {
      setSelectedIds(new Set())
      setShowDeleteConfirm(false)
      setBatchError('')
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
    onError: (err) => {
      setBatchError(err instanceof Error ? err.message : 'Batch delete failed')
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
  })

  const batchReprocessMut = useMutation({
    mutationFn: async (ids: string[]) => {
      const results = await Promise.allSettled(ids.map((id) => api.reprocessKnowledge(id)))
      const failed = results.filter((r) => r.status === 'rejected').length
      if (failed > 0) throw new Error(`${failed} of ${ids.length} reprocesses failed`)
    },
    onSuccess: () => {
      setSelectedIds(new Set())
      setBatchError('')
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
    onError: (err) => {
      setBatchError(err instanceof Error ? err.message : 'Batch reprocess failed')
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
  })

  const batchMoveMut = useMutation({
    mutationFn: async ({ ids, vaultId }: { ids: string[]; vaultId: string }) => {
      const results = await Promise.allSettled(
        ids.map((id) => api.updateKnowledge(id, { vaultId })),
      )
      const failed = results.filter((r) => r.status === 'rejected').length
      if (failed > 0) throw new Error(`${failed} of ${ids.length} moves failed`)
    },
    onSuccess: () => {
      setSelectedIds(new Set())
      setShowMoveToVault(false)
      setBatchError('')
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
    onError: (err) => {
      setBatchError(err instanceof Error ? err.message : 'Batch move failed')
      queryClient.invalidateQueries({ queryKey: ['knowledge'] })
    },
  })

  const isBatchOperating = batchDeleteMut.isPending || batchMoveMut.isPending || batchReprocessMut.isPending

  const updateParam = (key: string, value: string) => {
    const params = new URLSearchParams(searchParams)
    if (value) {
      params.set(key, value)
    } else {
      params.delete(key)
    }
    if (key !== 'page') params.set('page', '1')
    setSearchParams(params)
  }

  const toggleSort = (field: string) => {
    if (sort === field) {
      updateParam('sortDir', sortDir === 'asc' ? 'desc' : 'asc')
    } else {
      const params = new URLSearchParams(searchParams)
      params.set('sort', field)
      params.set('sortDir', 'desc')
      params.set('page', '1')
      setSearchParams(params)
    }
  }

  const sortIndicator = (field: string) => {
    if (sort !== field) return ''
    return sortDir === 'asc' ? ' \u2191' : ' \u2193'
  }

  const currentItems = data?.items || []
  const allSelected = currentItems.length > 0 && currentItems.every((item) => selectedIds.has(item.id))

  const toggleSelectAll = () => {
    if (allSelected) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(currentItems.map((item) => item.id)))
    }
  }

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Knowledge</h1>
        <Link
          to="/knowledge/new"
          className="inline-flex items-center gap-2 px-4 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium"
        >
          <Plus size={16} /> New
        </Link>
      </div>

      <div className="flex flex-wrap gap-3">
        <input
          type="text"
          placeholder="Filter by title..."
          value={title}
          onChange={(e) => updateParam('title', e.target.value)}
          className="px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
        />
        <select
          value={type}
          onChange={(e) => updateParam('type', e.target.value)}
          className="px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
        >
          <option value="">All types</option>
          {['Note', 'Document', 'Email', 'Image', 'Audio', 'Video', 'Code', 'Link'].map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
      </div>

      {error && (
        <p className="text-red-600 dark:text-red-400">
          {error instanceof Error ? error.message : 'Failed to load'}
        </p>
      )}

      {isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-14 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
          ))}
        </div>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-gray-200 dark:border-gray-800 text-gray-500 dark:text-gray-400">
                  <th className="py-2 px-3 w-10">
                    <input
                      type="checkbox"
                      checked={allSelected}
                      onChange={toggleSelectAll}
                      disabled={currentItems.length === 0}
                      className="rounded border-gray-300 dark:border-gray-600"
                    />
                  </th>
                  <th
                    className="py-2 px-3 font-medium cursor-pointer select-none"
                    onClick={() => toggleSort('title')}
                  >
                    Title{sortIndicator('title')}
                  </th>
                  <th className="py-2 px-3 font-medium w-24">Type</th>
                  <th className="py-2 px-3 font-medium w-24">Status</th>
                  <th
                    className="py-2 px-3 font-medium cursor-pointer select-none w-36"
                    onClick={() => toggleSort('created')}
                  >
                    Created{sortIndicator('created')}
                  </th>
                  <th
                    className="py-2 px-3 font-medium cursor-pointer select-none w-36"
                    onClick={() => toggleSort('updated')}
                  >
                    Updated{sortIndicator('updated')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {currentItems.map((item) => (
                  <tr
                    key={item.id}
                    className={`border-b border-gray-100 dark:border-gray-800/50 hover:bg-gray-50 dark:hover:bg-gray-900 cursor-pointer ${
                      selectedIds.has(item.id) ? 'bg-blue-50/50 dark:bg-blue-900/10' : ''
                    }`}
                  >
                    <td className="py-2.5 px-3" onClick={(e) => e.stopPropagation()}>
                      <input
                        type="checkbox"
                        checked={selectedIds.has(item.id)}
                        onChange={() => toggleSelect(item.id)}
                        className="rounded border-gray-300 dark:border-gray-600"
                      />
                    </td>
                    <td className="py-2.5 px-3" onClick={() => navigate(`/knowledge/${item.id}`)}>
                      <p className="font-medium truncate max-w-md">{item.title}</p>
                      {item.summary && (
                        <p className="text-xs text-gray-500 dark:text-gray-400 truncate max-w-md">
                          {item.summary}
                        </p>
                      )}
                    </td>
                    <td className="py-2.5 px-3" onClick={() => navigate(`/knowledge/${item.id}`)}>
                      <span className="inline-block px-2 py-0.5 text-xs bg-gray-100 dark:bg-gray-800 rounded">
                        {item.type}
                      </span>
                    </td>
                    <td className="py-2.5 px-3" onClick={() => navigate(`/knowledge/${item.id}`)}>
                      <span className="inline-flex items-center gap-1.5 text-xs">
                        <span className={`inline-block w-2 h-2 rounded-full ${
                          item.isIndexed ? 'bg-green-500' : 'bg-gray-400'
                        }`} />
                        {item.isIndexed ? 'Indexed' : 'Pending'}
                      </span>
                    </td>
                    <td className="py-2.5 px-3 text-gray-500 dark:text-gray-400" onClick={() => navigate(`/knowledge/${item.id}`)}>
                      {new Date(item.createdAt).toLocaleDateString()}
                    </td>
                    <td className="py-2.5 px-3 text-gray-500 dark:text-gray-400" onClick={() => navigate(`/knowledge/${item.id}`)}>
                      {new Date(item.updatedAt).toLocaleDateString()}
                    </td>
                  </tr>
                ))}
                {currentItems.length === 0 && (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-gray-500 dark:text-gray-400">
                      No knowledge items found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-between pt-2">
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Page {data.page} of {data.totalPages} ({data.totalItems} items)
              </p>
              <div className="flex gap-2">
                <button
                  disabled={page <= 1}
                  onClick={() => updateParam('page', String(page - 1))}
                  className="p-1.5 border border-gray-300 dark:border-gray-700 rounded disabled:opacity-30"
                >
                  <ChevronLeft size={16} />
                </button>
                <button
                  disabled={page >= data.totalPages}
                  onClick={() => updateParam('page', String(page + 1))}
                  className="p-1.5 border border-gray-300 dark:border-gray-700 rounded disabled:opacity-30"
                >
                  <ChevronRight size={16} />
                </button>
              </div>
            </div>
          )}
        </>
      )}

      {/* Floating action bar for bulk operations */}
      {selectedIds.size > 0 && (
        <div className="fixed bottom-6 left-1/2 -translate-x-1/2 z-40">
          <div className="flex items-center gap-3 px-5 py-3 bg-gray-900 dark:bg-gray-100 text-white dark:text-gray-900 rounded-lg shadow-xl">
            <span className="text-sm font-medium whitespace-nowrap">
              {selectedIds.size} item{selectedIds.size !== 1 ? 's' : ''} selected
            </span>
            <div className="w-px h-5 bg-gray-700 dark:bg-gray-300" />
            <button
              onClick={() => setShowMoveToVault(true)}
              disabled={isBatchOperating}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm bg-gray-800 dark:bg-gray-200 hover:bg-gray-700 dark:hover:bg-gray-300 rounded disabled:opacity-50 transition-colors"
            >
              <FolderInput size={14} /> Move to Vault
            </button>
            <button
              onClick={() => { setBatchError(''); batchReprocessMut.mutate(Array.from(selectedIds)) }}
              disabled={isBatchOperating}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm bg-gray-800 dark:bg-gray-200 hover:bg-gray-700 dark:hover:bg-gray-300 rounded disabled:opacity-50 transition-colors"
            >
              <RefreshCw size={14} className={batchReprocessMut.isPending ? 'animate-spin' : ''} /> {batchReprocessMut.isPending ? 'Reprocessing...' : 'Reprocess'}
            </button>
            <button
              onClick={() => { setBatchError(''); setShowDeleteConfirm(true) }}
              disabled={isBatchOperating}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm bg-red-600 hover:bg-red-700 text-white rounded disabled:opacity-50 transition-colors"
            >
              <Trash2 size={14} /> Delete
            </button>
            <button
              onClick={() => setSelectedIds(new Set())}
              disabled={isBatchOperating}
              className="inline-flex items-center gap-1 px-2 py-1.5 text-sm hover:bg-gray-800 dark:hover:bg-gray-200 rounded transition-colors"
              title="Deselect all"
            >
              <X size={14} />
            </button>
          </div>
        </div>
      )}

      {/* Batch delete confirmation modal */}
      {showDeleteConfirm && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-gray-900 rounded-lg p-6 max-w-sm w-full space-y-4">
            <h2 className="text-lg font-semibold">Delete {selectedIds.size} item{selectedIds.size !== 1 ? 's' : ''}?</h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              Are you sure you want to delete {selectedIds.size} knowledge item{selectedIds.size !== 1 ? 's' : ''}? This action cannot be undone.
            </p>
            {batchError && (
              <p className="text-red-600 dark:text-red-400 text-sm">{batchError}</p>
            )}
            <div className="flex gap-2 justify-end">
              <button
                onClick={() => { setShowDeleteConfirm(false); setBatchError('') }}
                disabled={batchDeleteMut.isPending}
                className="px-4 py-2 border border-gray-300 dark:border-gray-700 rounded-md text-sm"
              >
                Cancel
              </button>
              <button
                onClick={() => batchDeleteMut.mutate(Array.from(selectedIds))}
                disabled={batchDeleteMut.isPending}
                className="inline-flex items-center gap-2 px-4 py-2 bg-red-600 text-white rounded-md text-sm font-medium disabled:opacity-50"
              >
                {batchDeleteMut.isPending ? (
                  <>
                    <Loader2 size={14} className="animate-spin" /> Deleting...
                  </>
                ) : (
                  'Delete'
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Move to vault modal */}
      {showMoveToVault && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-gray-900 rounded-lg p-6 max-w-sm w-full space-y-4">
            <h2 className="text-lg font-semibold">
              Move {selectedIds.size} item{selectedIds.size !== 1 ? 's' : ''} to vault
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              Select a vault to move the selected items to.
            </p>
            {batchError && (
              <p className="text-red-600 dark:text-red-400 text-sm">{batchError}</p>
            )}
            <div className="space-y-2">
              {vaults.isLoading ? (
                <div className="h-10 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
              ) : (
                vaults.data?.vaults.map((vault) => (
                  <button
                    key={vault.id}
                    onClick={() => batchMoveMut.mutate({ ids: Array.from(selectedIds), vaultId: vault.id })}
                    disabled={batchMoveMut.isPending}
                    className="w-full text-left px-4 py-2.5 border border-gray-200 dark:border-gray-700 rounded-md hover:bg-gray-50 dark:hover:bg-gray-800 text-sm transition-colors disabled:opacity-50"
                  >
                    <span className="font-medium">{vault.name}</span>
                    {vault.description && (
                      <span className="text-gray-500 dark:text-gray-400 ml-2">
                        {vault.description}
                      </span>
                    )}
                  </button>
                ))
              )}
            </div>
            {batchMoveMut.isPending && (
              <div className="flex items-center gap-2 text-sm text-gray-500">
                <Loader2 size={14} className="animate-spin" /> Moving items...
              </div>
            )}
            <div className="flex justify-end">
              <button
                onClick={() => { setShowMoveToVault(false); setBatchError('') }}
                disabled={batchMoveMut.isPending}
                className="px-4 py-2 border border-gray-300 dark:border-gray-700 rounded-md text-sm"
              >
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
