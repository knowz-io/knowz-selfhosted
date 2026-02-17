import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { BookOpen, Archive, Calendar, RefreshCw } from 'lucide-react'

export default function DashboardPage() {
  const stats = useQuery({ queryKey: ['stats'], queryFn: () => api.getStats() })
  const vaults = useQuery({
    queryKey: ['vaults', 'dashboard'],
    queryFn: () => api.listVaults(true),
  })

  const error = stats.error || vaults.error
  if (error) {
    return (
      <div className="text-center py-12">
        <p className="text-red-600 dark:text-red-400 mb-4">
          {error instanceof Error ? error.message : 'Failed to load dashboard'}
        </p>
        <button
          onClick={() => { stats.refetch(); vaults.refetch() }}
          className="inline-flex items-center gap-2 px-4 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium"
        >
          <RefreshCw size={16} /> Retry
        </button>
      </div>
    )
  }

  const isLoading = stats.isLoading || vaults.isLoading

  if (isLoading) {
    return (
      <div className="space-y-6">
        <h1 className="text-2xl font-bold">Dashboard</h1>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-28 bg-gray-100 dark:bg-gray-800 rounded-lg animate-pulse" />
          ))}
        </div>
      </div>
    )
  }

  const s = stats.data
  const v = vaults.data?.vaults ?? []

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Dashboard</h1>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
        <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center gap-3 mb-2 text-gray-500 dark:text-gray-400">
            <BookOpen size={18} />
            <span className="text-sm font-medium">Knowledge Items</span>
          </div>
          <p className="text-3xl font-bold">{s?.totalKnowledgeItems ?? 0}</p>
        </div>

        <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center gap-3 mb-2 text-gray-500 dark:text-gray-400">
            <Archive size={18} />
            <span className="text-sm font-medium">Vaults</span>
          </div>
          <p className="text-3xl font-bold">{v.length}</p>
        </div>

        {s?.dateRange && (
          <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
            <div className="flex items-center gap-3 mb-2 text-gray-500 dark:text-gray-400">
              <Calendar size={18} />
              <span className="text-sm font-medium">Date Range</span>
            </div>
            <p className="text-sm">
              {new Date(s.dateRange.earliest).toLocaleDateString()} &mdash;{' '}
              {new Date(s.dateRange.latest).toLocaleDateString()}
            </p>
          </div>
        )}
      </div>

      {s?.byType && s.byType.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold mb-3">By Type</h2>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
            {s.byType.map(({ type, count }) => (
              <div
                key={type}
                className="p-3 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg"
              >
                <p className="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wide">{type}</p>
                <p className="text-xl font-bold mt-1">{count}</p>
              </div>
            ))}
          </div>
        </div>
      )}

      {v.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold mb-3">Vaults</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
            {v.map((vault) => (
              <div
                key={vault.id}
                className="p-4 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg"
              >
                <p className="font-medium">{vault.name}</p>
                {vault.description && (
                  <p className="text-sm text-gray-500 dark:text-gray-400 mt-1 line-clamp-2">
                    {vault.description}
                  </p>
                )}
                <p className="text-sm text-gray-500 dark:text-gray-400 mt-2">
                  {vault.knowledgeCount ?? 0} items
                </p>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
