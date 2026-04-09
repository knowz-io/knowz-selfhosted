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
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium"
        >
          <RefreshCw size={16} /> Retry
        </button>
      </div>
    )
  }

  const isLoading = stats.isLoading || vaults.isLoading

  if (isLoading) {
    return (
      <div className="space-y-8">
        <div>
          <div className="h-7 w-32 bg-muted rounded-lg animate-pulse" />
          <div className="h-4 w-56 bg-muted/60 rounded-lg animate-pulse mt-2" />
        </div>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-28 bg-card border border-border/40 rounded-2xl animate-pulse" />
          ))}
        </div>
      </div>
    )
  }

  const s = stats.data
  const v = vaults.data?.vaults ?? []

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Dashboard</h1>
        <p className="text-sm text-muted-foreground mt-1">Overview of your knowledge base</p>
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-5">
        <div className="group p-5 bg-card border border-border/40 rounded-2xl shadow-card hover:shadow-card-hover transition-all duration-300 hover:-translate-y-0.5">
          <div className="flex items-center gap-3 mb-3">
            <div className="p-2.5 bg-blue-500/10 dark:bg-blue-500/15 rounded-xl">
              <BookOpen size={18} className="text-blue-600 dark:text-blue-400" />
            </div>
            <span className="text-sm font-medium text-muted-foreground">Knowledge Items</span>
          </div>
          <p className="text-3xl font-bold tracking-tight">{s?.totalKnowledgeItems ?? 0}</p>
        </div>

        <div className="group p-5 bg-card border border-border/40 rounded-2xl shadow-card hover:shadow-card-hover transition-all duration-300 hover:-translate-y-0.5">
          <div className="flex items-center gap-3 mb-3">
            <div className="p-2.5 bg-violet-500/10 dark:bg-violet-500/15 rounded-xl">
              <Archive size={18} className="text-violet-600 dark:text-violet-400" />
            </div>
            <span className="text-sm font-medium text-muted-foreground">Vaults</span>
          </div>
          <p className="text-3xl font-bold tracking-tight">{v.length}</p>
        </div>

        {s?.dateRange && (
          <div className="group p-5 bg-card border border-border/40 rounded-2xl shadow-card hover:shadow-card-hover transition-all duration-300 hover:-translate-y-0.5">
            <div className="flex items-center gap-3 mb-3">
              <div className="p-2.5 bg-amber-500/10 dark:bg-amber-500/15 rounded-xl">
                <Calendar size={18} className="text-amber-600 dark:text-amber-400" />
              </div>
              <span className="text-sm font-medium text-muted-foreground">Date Range</span>
            </div>
            <p className="text-sm font-medium">
              {new Date(s.dateRange.earliest).toLocaleDateString()} &mdash;{' '}
              {new Date(s.dateRange.latest).toLocaleDateString()}
            </p>
          </div>
        )}
      </div>

      {s?.byType && s.byType.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold mb-4">By Type</h2>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
            {s.byType.map(({ type, count }) => (
              <div
                key={type}
                className="group p-4 bg-card border border-border/40 rounded-xl shadow-card hover:shadow-card-hover transition-all duration-300 hover:-translate-y-0.5"
              >
                <p className="text-[11px] text-muted-foreground uppercase tracking-wider font-medium">{type}</p>
                <p className="text-xl font-bold mt-1.5 tracking-tight">{count}</p>
              </div>
            ))}
          </div>
        </div>
      )}

      {v.length > 0 && (
        <div>
          <h2 className="text-lg font-semibold mb-4">Vaults</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {v.map((vault) => (
              <div
                key={vault.id}
                className="group p-5 bg-card border border-border/40 rounded-2xl shadow-card hover:shadow-card-hover transition-all duration-300 hover:-translate-y-0.5"
              >
                <div className="flex items-center gap-2.5 mb-2">
                  <div className="p-1.5 bg-primary/8 rounded-lg">
                    <Archive size={14} className="text-primary" />
                  </div>
                  <p className="font-semibold">{vault.name}</p>
                </div>
                {vault.description && (
                  <p className="text-sm text-muted-foreground mt-1 line-clamp-2">
                    {vault.description}
                  </p>
                )}
                <div className="flex items-center gap-1.5 mt-3 pt-3 border-t border-border/40">
                  <span className="text-xs font-medium text-muted-foreground">
                    {vault.knowledgeCount ?? 0} items
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
