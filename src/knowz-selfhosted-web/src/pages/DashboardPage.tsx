import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { BookOpen, Archive, Calendar, RefreshCw, ArrowRight } from 'lucide-react'
import { useFormatters } from '../hooks/useFormatters'
import PageHeader from '../components/ui/PageHeader'
import SurfaceCard from '../components/ui/SurfaceCard'

export default function DashboardPage() {
  const fmt = useFormatters()
  const stats = useQuery({ queryKey: ['stats'], queryFn: () => api.getStats() })
  const vaults = useQuery({
    queryKey: ['vaults', 'dashboard'],
    queryFn: () => api.listVaults(true),
  })

  const error = stats.error || vaults.error
  if (error) {
    return (
      <div className="space-y-6">
        <PageHeader
          eyebrow="Overview"
          title="Workspace pulse"
          titleAs="h2"
          description="See what is growing, what is organized, and what still needs attention."
        />
        <SurfaceCard className="p-8 text-center">
          <p className="mb-4 text-red-600 dark:text-red-400">
            {error instanceof Error ? error.message : 'Failed to load dashboard'}
          </p>
          <button
            onClick={() => { stats.refetch(); vaults.refetch() }}
            className="inline-flex items-center gap-2 rounded-2xl bg-primary px-4 py-2.5 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20 transition-all duration-200 hover:brightness-110"
          >
            <RefreshCw size={16} /> Retry
          </button>
        </SurfaceCard>
      </div>
    )
  }

  const isLoading = stats.isLoading || vaults.isLoading

  if (isLoading) {
    return (
      <div className="space-y-6">
        <PageHeader
          eyebrow="Overview"
          title="Workspace pulse"
          titleAs="h2"
          description="See what is growing, what is organized, and what still needs attention."
        />
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="sh-stat h-28 animate-pulse" />
          ))}
        </div>
        <div className="grid grid-cols-1 gap-5 xl:grid-cols-[1.2fr_0.8fr]">
          <div className="sh-surface h-64 animate-pulse" />
          <div className="sh-surface h-64 animate-pulse" />
        </div>
      </div>
    )
  }

  const s = stats.data
  const v = vaults.data?.vaults ?? []

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Overview"
        title="Workspace pulse"
        titleAs="h2"
        description="See what is growing, what is organized, and what still needs attention across your self-hosted knowledge base."
        actions={
          <>
            <Link
              to="/knowledge"
              className="inline-flex items-center gap-2 rounded-2xl border border-border/70 bg-card/80 px-4 py-2.5 text-sm font-medium shadow-sm transition-colors hover:bg-card"
            >
              Browse knowledge
            </Link>
            <Link
              to="/vaults"
              className="inline-flex items-center gap-2 rounded-2xl bg-primary px-4 py-2.5 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20 transition-all duration-200 hover:brightness-110"
            >
              Manage vaults
            </Link>
          </>
        }
        meta={
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <div className="sh-stat">
              <p className="sh-kicker">Knowledge</p>
              <p className="mt-2 text-3xl font-semibold tracking-tight">{s?.totalKnowledgeItems ?? 0}</p>
              <p className="mt-2 text-xs text-muted-foreground">Indexed and ready to explore.</p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">Vaults</p>
              <p className="mt-2 text-3xl font-semibold tracking-tight">{v.length}</p>
              <p className="mt-2 text-xs text-muted-foreground">Collections shaping the workspace.</p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">Date Range</p>
              <p className="mt-2 text-sm font-semibold leading-6">
                {s?.dateRange
                  ? `${fmt.date(s.dateRange.earliest)} to ${fmt.date(s.dateRange.latest)}`
                  : 'No dated activity yet'}
              </p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">Coverage</p>
              <p className="mt-2 text-sm font-semibold leading-6">
                {s?.byType?.length ?? 0} active knowledge type{(s?.byType?.length ?? 0) === 1 ? '' : 's'}
              </p>
              <p className="mt-2 text-xs text-muted-foreground">A quick read on content diversity.</p>
            </div>
          </div>
        }
      />

      <div className="grid grid-cols-1 gap-5 xl:grid-cols-[1.15fr_0.85fr]">
        <SurfaceCard className="p-6">
          <div className="mb-5 flex items-center justify-between gap-3">
            <div>
              <p className="sh-kicker">By Type</p>
              <h3 className="mt-2 text-xl font-semibold tracking-tight">Content distribution</h3>
            </div>
            <span className="rounded-full border border-border/70 bg-background/80 px-3 py-1 text-xs text-muted-foreground">
              {s?.byType?.length ?? 0} buckets
            </span>
          </div>

          {s?.byType && s.byType.length > 0 ? (
            <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
              {s.byType.map(({ type, count }) => (
                <div
                  key={type}
                  className="rounded-[22px] border border-border/60 bg-background/70 px-4 py-4"
                >
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                    {type}
                  </p>
                  <p className="mt-2 text-2xl font-semibold tracking-tight">{count}</p>
                </div>
              ))}
            </div>
          ) : (
            <div className="rounded-[22px] border border-dashed border-border/70 bg-background/50 px-5 py-10 text-center text-sm text-muted-foreground">
              No type breakdown available yet.
            </div>
          )}
        </SurfaceCard>

        <SurfaceCard className="p-6">
          <div className="mb-5 flex items-center justify-between gap-3">
            <div>
              <p className="sh-kicker">Collections</p>
              <h3 className="mt-2 text-xl font-semibold tracking-tight">Active vaults</h3>
            </div>
            <Link
              to="/vaults"
              className="inline-flex items-center gap-1 text-sm font-medium text-primary transition-colors hover:text-primary/80"
            >
              View all
              <ArrowRight size={14} />
            </Link>
          </div>

          {v.length > 0 ? (
            <div className="space-y-3">
              {v.slice(0, 5).map((vault) => (
                <Link
                  key={vault.id}
                  to={`/knowledge?vaultId=${vault.id}`}
                  className="flex items-start gap-3 rounded-[22px] border border-border/60 bg-background/70 px-4 py-4 transition-all duration-200 hover:-translate-y-0.5 hover:bg-card"
                >
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
                    <Archive size={18} />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-center gap-2">
                      <p className="truncate font-semibold">{vault.name}</p>
                      <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-medium text-muted-foreground">
                        {vault.knowledgeCount ?? 0} items
                      </span>
                    </div>
                    {vault.description && (
                      <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">
                        {vault.description}
                      </p>
                    )}
                  </div>
                </Link>
              ))}
            </div>
          ) : (
            <div className="rounded-[22px] border border-dashed border-border/70 bg-background/50 px-5 py-10 text-center text-sm text-muted-foreground">
              No vaults created yet.
            </div>
          )}
        </SurfaceCard>
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <SurfaceCard className="p-5">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-blue-500/10 text-blue-600 dark:bg-blue-500/15 dark:text-blue-400">
              <BookOpen size={18} />
            </div>
            <div>
              <p className="text-sm font-semibold">Knowledge momentum</p>
              <p className="text-xs text-muted-foreground">Keep the library fresh and reviewed.</p>
            </div>
          </div>
        </SurfaceCard>
        <SurfaceCard className="p-5">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-violet-500/10 text-violet-600 dark:bg-violet-500/15 dark:text-violet-400">
              <Archive size={18} />
            </div>
            <div>
              <p className="text-sm font-semibold">Vault structure</p>
              <p className="text-xs text-muted-foreground">Collections stay readable when naming is deliberate.</p>
            </div>
          </div>
        </SurfaceCard>
        <SurfaceCard className="p-5">
          <div className="flex items-center gap-3">
            <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-amber-500/10 text-amber-600 dark:bg-amber-500/15 dark:text-amber-400">
              <Calendar size={18} />
            </div>
            <div>
              <p className="text-sm font-semibold">Temporal context</p>
              <p className="text-xs text-muted-foreground">Recent content is easier to trust when dates are visible.</p>
            </div>
          </div>
        </SurfaceCard>
      </div>
    </div>
  )
}
