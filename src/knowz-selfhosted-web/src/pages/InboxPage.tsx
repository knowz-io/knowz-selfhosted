import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import type { InboxItemDto, Vault } from '../lib/types'
import {
  Inbox,
  Plus,
  Search,
  Trash2,
  Pencil,
  ArrowRightLeft,
  Check,
  X,
  ChevronLeft,
  ChevronRight,
  Loader2,
} from 'lucide-react'

const TYPE_OPTIONS = ['All', 'Note', 'Link', 'File'] as const

export default function InboxPage() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [searchInput, setSearchInput] = useState('')
  const [typeFilter, setTypeFilter] = useState<string>('All')
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [quickCapture, setQuickCapture] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editBody, setEditBody] = useState('')
  const [showConvertDialog, setShowConvertDialog] = useState(false)
  const [convertIds, setConvertIds] = useState<string[]>([])
  const [convertVaultId, setConvertVaultId] = useState<string>('')
  const [convertTags, setConvertTags] = useState('')

  const queryParams: Record<string, string | undefined> = {
    page: String(page),
    pageSize: String(pageSize),
    search: search || undefined,
    type: typeFilter !== 'All' ? typeFilter : undefined,
  }

  const { data, isLoading } = useQuery({
    queryKey: ['inbox', page, search, typeFilter],
    queryFn: () => api.listInbox(queryParams),
  })

  const { data: vaultsData } = useQuery({
    queryKey: ['vaults'],
    queryFn: () => api.listVaults(false),
  })

  const createMutation = useMutation({
    mutationFn: (body: string) => api.createInboxItem(body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
      setQuickCapture('')
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, body }: { id: string; body: string }) => api.updateInboxItem(id, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
      setEditingId(null)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteInboxItem(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
      setSelectedIds((prev) => {
        const next = new Set(prev)
        next.delete(deleteMutation.variables!)
        return next
      })
    },
  })

  const convertMutation = useMutation({
    mutationFn: ({ id, vaultId, tags }: { id: string; vaultId?: string; tags?: string[] }) =>
      api.convertInboxItem(id, { vaultId, tags }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
    },
  })

  const batchConvertMutation = useMutation({
    mutationFn: (data: { ids: string[]; vaultId?: string; tags?: string[] }) =>
      api.batchConvertInbox(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['inbox'] })
      setSelectedIds(new Set())
      setShowConvertDialog(false)
    },
  })

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault()
    setSearch(searchInput)
    setPage(1)
  }

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  const toggleSelectAll = () => {
    if (!data) return
    if (selectedIds.size === data.items.length) {
      setSelectedIds(new Set())
    } else {
      setSelectedIds(new Set(data.items.map((i) => i.id)))
    }
  }

  const startEdit = (item: InboxItemDto) => {
    setEditingId(item.id)
    setEditBody(item.body)
  }

  const saveEdit = () => {
    if (editingId && editBody.trim()) {
      updateMutation.mutate({ id: editingId, body: editBody.trim() })
    }
  }

  const cancelEdit = () => {
    setEditingId(null)
    setEditBody('')
  }

  const openConvertDialog = (ids: string[]) => {
    setConvertIds(ids)
    setConvertVaultId('')
    setConvertTags('')
    setShowConvertDialog(true)
  }

  const handleConvert = () => {
    const tags = convertTags
      .split(',')
      .map((t) => t.trim())
      .filter(Boolean)
    if (convertIds.length === 1) {
      convertMutation.mutate(
        {
          id: convertIds[0],
          vaultId: convertVaultId || undefined,
          tags: tags.length > 0 ? tags : undefined,
        },
        {
          onSuccess: () => {
            setShowConvertDialog(false)
            setSelectedIds((prev) => {
              const next = new Set(prev)
              next.delete(convertIds[0])
              return next
            })
          },
        },
      )
    } else {
      batchConvertMutation.mutate({
        ids: convertIds,
        vaultId: convertVaultId || undefined,
        tags: tags.length > 0 ? tags : undefined,
      })
    }
  }

  const preview = (body: string) => (body.length > 80 ? body.slice(0, 80) + '...' : body)

  const relativeTime = (dateStr: string) => {
    const date = new Date(dateStr)
    const now = new Date()
    const diff = now.getTime() - date.getTime()
    const minutes = Math.floor(diff / 60000)
    if (minutes < 1) return 'just now'
    if (minutes < 60) return `${minutes}m ago`
    const hours = Math.floor(minutes / 60)
    if (hours < 24) return `${hours}h ago`
    const days = Math.floor(hours / 24)
    if (days < 30) return `${days}d ago`
    return date.toLocaleDateString()
  }

  const typeBadgeClass = (type: string) => {
    switch (type) {
      case 'Note':
        return 'bg-blue-100 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400'
      case 'Link':
        return 'bg-green-100 dark:bg-green-950/40 text-green-700 dark:text-green-400'
      case 'File':
        return 'bg-purple-100 dark:bg-purple-950/40 text-purple-700 dark:text-purple-400'
      default:
        return 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-400'
    }
  }

  const vaults: Vault[] = vaultsData?.vaults ?? []

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Inbox size={24} />
        <h1 className="text-2xl font-bold">Inbox</h1>
      </div>

      {/* Quick Capture */}
      <div className="flex gap-2 items-end">
        <textarea
          rows={2}
          value={quickCapture}
          onChange={(e) => setQuickCapture(e.target.value)}
          onKeyDown={(e) => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
              e.preventDefault()
              if (quickCapture.trim()) {
                createMutation.mutate(quickCapture.trim())
              }
            }
          }}
          placeholder="Quick capture: type a note... (Ctrl+Enter to save)"
          className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
        />
        <button
          type="button"
          onClick={() => {
            if (quickCapture.trim()) {
              createMutation.mutate(quickCapture.trim())
            }
          }}
          disabled={!quickCapture.trim() || createMutation.isPending}
          className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed self-end"
        >
          <Plus size={16} />
          Add
        </button>
      </div>

      {/* Search & Filter Bar */}
      <div className="flex gap-2 items-center">
        <form onSubmit={handleSearch} className="flex-1 flex gap-2">
          <div className="relative flex-1">
            <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder="Search inbox..."
              className="w-full pl-10 pr-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <button
            type="submit"
            className="px-4 py-2 bg-gray-200 dark:bg-gray-700 text-gray-700 dark:text-gray-300 rounded-md hover:bg-gray-300 dark:hover:bg-gray-600"
          >
            Search
          </button>
        </form>
        <select
          value={typeFilter}
          onChange={(e) => {
            setTypeFilter(e.target.value)
            setPage(1)
          }}
          className="px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          {TYPE_OPTIONS.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </div>

      {/* Batch Action Bar */}
      {selectedIds.size > 0 && (
        <div className="flex items-center gap-3 px-4 py-2 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-md">
          <span className="text-sm font-medium text-blue-700 dark:text-blue-400">
            {selectedIds.size} selected
          </span>
          <button
            onClick={() => openConvertDialog(Array.from(selectedIds))}
            className="flex items-center gap-1 px-3 py-1 text-sm bg-blue-600 text-white rounded hover:bg-blue-700"
          >
            <ArrowRightLeft size={14} />
            Convert Selected
          </button>
          <button
            onClick={() => {
              selectedIds.forEach((id) => deleteMutation.mutate(id))
              setSelectedIds(new Set())
            }}
            className="flex items-center gap-1 px-3 py-1 text-sm bg-red-600 text-white rounded hover:bg-red-700"
          >
            <Trash2 size={14} />
            Delete Selected
          </button>
          <button
            onClick={() => setSelectedIds(new Set())}
            className="text-sm text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
          >
            Clear
          </button>
        </div>
      )}

      {/* Items List */}
      {isLoading ? (
        <div className="flex items-center justify-center py-12">
          <Loader2 size={24} className="animate-spin text-gray-400" />
        </div>
      ) : data && data.items.length > 0 ? (
        <div className="border border-gray-200 dark:border-gray-800 rounded-md divide-y divide-gray-200 dark:divide-gray-800">
          {/* Header */}
          <div className="flex items-center gap-3 px-4 py-2 bg-gray-50 dark:bg-gray-900 text-sm font-medium text-gray-500 dark:text-gray-400">
            <input
              type="checkbox"
              checked={selectedIds.size === data.items.length && data.items.length > 0}
              onChange={toggleSelectAll}
              className="rounded"
            />
            <span className="flex-1">Content</span>
            <span className="w-16 text-center">Type</span>
            <span className="w-24 text-right">Time</span>
            <span className="w-28 text-right">Actions</span>
          </div>

          {data.items.map((item) => (
            <div
              key={item.id}
              className="flex items-center gap-3 px-4 py-3 hover:bg-gray-50 dark:hover:bg-gray-800/50"
            >
              <input
                type="checkbox"
                checked={selectedIds.has(item.id)}
                onChange={() => toggleSelect(item.id)}
                className="rounded"
              />
              <div className="flex-1 min-w-0">
                {editingId === item.id ? (
                  <div className="flex items-center gap-2">
                    <input
                      type="text"
                      value={editBody}
                      onChange={(e) => setEditBody(e.target.value)}
                      className="flex-1 px-2 py-1 border border-gray-300 dark:border-gray-700 rounded bg-white dark:bg-gray-800 text-sm"
                      autoFocus
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') saveEdit()
                        if (e.key === 'Escape') cancelEdit()
                      }}
                    />
                    <button
                      onClick={saveEdit}
                      className="p-1 text-green-600 hover:text-green-700"
                      title="Save"
                    >
                      <Check size={16} />
                    </button>
                    <button
                      onClick={cancelEdit}
                      className="p-1 text-gray-400 hover:text-gray-600"
                      title="Cancel"
                    >
                      <X size={16} />
                    </button>
                  </div>
                ) : (
                  <p className="text-sm text-gray-900 dark:text-white truncate">{preview(item.body)}</p>
                )}
              </div>
              <span className="w-16 text-center">
                <span
                  className={`inline-flex px-2 py-0.5 rounded text-xs font-medium ${typeBadgeClass(item.type)}`}
                >
                  {item.type}
                </span>
              </span>
              <span className="w-24 text-right text-xs text-gray-500 dark:text-gray-400">
                {relativeTime(item.createdAt)}
              </span>
              <div className="w-28 flex items-center justify-end gap-1">
                <button
                  onClick={() => startEdit(item)}
                  className="p-1.5 text-gray-400 hover:text-blue-600 rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                  title="Edit"
                >
                  <Pencil size={14} />
                </button>
                <button
                  onClick={() => openConvertDialog([item.id])}
                  className="p-1.5 text-gray-400 hover:text-green-600 rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                  title="Convert to Knowledge"
                >
                  <ArrowRightLeft size={14} />
                </button>
                <button
                  onClick={() => deleteMutation.mutate(item.id)}
                  className="p-1.5 text-gray-400 hover:text-red-600 rounded hover:bg-gray-100 dark:hover:bg-gray-700"
                  title="Delete"
                >
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="text-center py-12 text-gray-500 dark:text-gray-400">
          <Inbox size={48} className="mx-auto mb-4 opacity-50" />
          <p className="text-lg font-medium">No inbox items</p>
          <p className="text-sm mt-1">Use the quick capture above to add your first item.</p>
        </div>
      )}

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Showing {(page - 1) * pageSize + 1}-{Math.min(page * pageSize, data.totalItems)} of{' '}
            {data.totalItems}
          </p>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              className="p-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <ChevronLeft size={16} />
            </button>
            <span className="text-sm text-gray-700 dark:text-gray-300">
              Page {page} of {data.totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
              disabled={page >= data.totalPages}
              className="p-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <ChevronRight size={16} />
            </button>
          </div>
        </div>
      )}

      {/* Convert Dialog */}
      {showConvertDialog && (
        <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center">
          <div className="bg-white dark:bg-gray-900 rounded-lg shadow-xl p-6 w-full max-w-md mx-4">
            <h2 className="text-lg font-semibold mb-4">Convert to Knowledge</h2>
            <p className="text-sm text-gray-500 dark:text-gray-400 mb-4">
              Converting {convertIds.length} item{convertIds.length > 1 ? 's' : ''} to knowledge.
            </p>

            <div className="space-y-3">
              <div>
                <label className="block text-sm font-medium mb-1">
                  Vault (optional)
                </label>
                <select
                  value={convertVaultId}
                  onChange={(e) => setConvertVaultId(e.target.value)}
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800"
                >
                  <option value="">No vault</option>
                  {vaults.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-sm font-medium mb-1">
                  Tags (optional, comma-separated)
                </label>
                <input
                  type="text"
                  value={convertTags}
                  onChange={(e) => setConvertTags(e.target.value)}
                  placeholder="tag1, tag2"
                  className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800"
                />
              </div>
            </div>

            <div className="flex justify-end gap-2 mt-6">
              <button
                onClick={() => setShowConvertDialog(false)}
                className="px-4 py-2 text-sm text-gray-600 dark:text-gray-400 hover:text-gray-800 dark:hover:text-gray-200"
              >
                Cancel
              </button>
              <button
                onClick={handleConvert}
                disabled={convertMutation.isPending || batchConvertMutation.isPending}
                className="px-4 py-2 text-sm bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
              >
                {convertMutation.isPending || batchConvertMutation.isPending
                  ? 'Converting...'
                  : 'Convert'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
