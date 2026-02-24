import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  Settings,
  Save,
  Loader2,
  RefreshCw,
  Eye,
  EyeOff,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Clock,
  Activity,
  Wifi,
} from 'lucide-react'
import { api } from '../../lib/api-client'
import type {
  ConfigCategoryDto,
  ConfigEntryDto,
  ConfigEntryUpdateDto,
  ServiceHealthResult,
} from '../../lib/types'

const TAB_ORDER = [
  'ConnectionStrings',
  'KnowzPlatform',
  'AzureOpenAI',
  'AzureAISearch',
  'Storage',
  'SelfHosted',
  'Inbox',
  'SSO',
  'Logging',
  'AzureKeyVault',
]

export default function AdminSettingsPage() {
  const queryClient = useQueryClient()
  const [activeTab, setActiveTab] = useState(TAB_ORDER[0])
  const [restartBanner, setRestartBanner] = useState<string[] | null>(null)

  const categoriesQuery = useQuery({
    queryKey: ['admin', 'config'],
    queryFn: () => api.getConfigCategories(),
  })

  const statusQuery = useQuery({
    queryKey: ['admin', 'config', 'status'],
    queryFn: () => api.getConfigStatus(),
  })

  if (categoriesQuery.error) {
    return (
      <div className="text-center py-12">
        <p className="text-red-600 dark:text-red-400 mb-4">
          {categoriesQuery.error instanceof Error
            ? categoriesQuery.error.message
            : 'Failed to load configuration'}
        </p>
        <button
          onClick={() => categoriesQuery.refetch()}
          className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium transition-colors"
        >
          <RefreshCw size={16} /> Retry
        </button>
      </div>
    )
  }

  if (categoriesQuery.isLoading) {
    return (
      <div className="space-y-6">
        <h1 className="text-2xl font-bold">Settings</h1>
        <div className="space-y-4">
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="h-24 bg-muted rounded-lg animate-pulse"
            />
          ))}
        </div>
      </div>
    )
  }

  const categories = categoriesQuery.data ?? []
  const sortedCategories = TAB_ORDER.map((name) =>
    categories.find((c) => c.category === name),
  ).filter(Boolean) as ConfigCategoryDto[]

  const activeCategory = sortedCategories.find(
    (c) => c.category === activeTab,
  )

  const status = statusQuery.data
  const showRestartBanner =
    restartBanner !== null || status?.restartRequired

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Settings size={24} className="text-muted-foreground" />
          <h1 className="text-2xl font-bold">Settings</h1>
        </div>
        <button
          onClick={() => {
            categoriesQuery.refetch()
            statusQuery.refetch()
          }}
          disabled={categoriesQuery.isFetching}
          className="inline-flex items-center gap-2 px-3 py-1.5 border border-input rounded-md text-sm font-medium hover:bg-muted transition-colors disabled:opacity-50"
        >
          {categoriesQuery.isFetching ? (
            <Loader2 size={14} className="animate-spin" />
          ) : (
            <RefreshCw size={14} />
          )}
          Refresh
        </button>
      </div>

      {/* Deployment Status Card */}
      {status && (
        <div className="flex flex-wrap gap-4 p-4 bg-card border border-border/60 rounded-xl shadow-sm">
          <div className="flex items-center gap-2">
            <Activity size={14} className="text-muted-foreground" />
            <span className="text-sm text-muted-foreground">
              Mode:
            </span>
            <span className="text-sm font-medium">{status.mode}</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-sm text-muted-foreground">
              Version:
            </span>
            <span className="text-sm font-medium">{status.version}</span>
          </div>
          <div className="flex items-center gap-2">
            <Clock size={14} className="text-muted-foreground" />
            <span className="text-sm text-muted-foreground">
              Uptime:
            </span>
            <span className="text-sm font-medium">
              {formatUptime(status.startupTime)}
            </span>
          </div>
        </div>
      )}

      {/* Restart Required Banner */}
      {showRestartBanner && (
        <div className="flex items-start gap-3 p-4 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-lg">
          <AlertTriangle
            size={18}
            className="text-amber-600 dark:text-amber-400 mt-0.5 shrink-0"
          />
          <div>
            <p className="text-sm font-medium text-amber-800 dark:text-amber-300">
              Configuration changes require a restart to take effect.
            </p>
            {(restartBanner ?? status?.restartReasons ?? []).length > 0 && (
              <p className="text-xs text-amber-700 dark:text-amber-400 mt-1">
                Changed:{' '}
                {(restartBanner ?? status?.restartReasons ?? []).join(', ')}
              </p>
            )}
          </div>
        </div>
      )}

      {/* Tab Navigation */}
      <div className="border-b border-border/60">
        <nav className="-mb-px flex space-x-4 overflow-x-auto" aria-label="Tabs">
          {sortedCategories.map((cat) => (
            <button
              key={cat.category}
              onClick={() => setActiveTab(cat.category)}
              className={`whitespace-nowrap py-3 px-1 border-b-2 text-sm font-medium transition-colors ${
                activeTab === cat.category
                  ? 'border-foreground text-foreground'
                  : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border'
              }`}
            >
              {cat.displayName}
            </button>
          ))}
        </nav>
      </div>

      {/* Active Category Section */}
      {activeCategory && (
        <CategorySection
          category={activeCategory}
          onRestartRequired={(reasons) =>
            setRestartBanner((prev) => [
              ...(prev ?? []),
              ...reasons,
            ])
          }
          queryClient={queryClient}
        />
      )}
    </div>
  )
}

function CategorySection({
  category,
  onRestartRequired,
  queryClient,
}: {
  category: ConfigCategoryDto
  onRestartRequired: (reasons: string[]) => void
  queryClient: ReturnType<typeof useQueryClient>
}) {
  const [formValues, setFormValues] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {}
    for (const entry of category.entries) {
      initial[entry.key] = entry.value ?? ''
    }
    return initial
  })

  const [dirtyFields, setDirtyFields] = useState<Set<string>>(new Set())
  const [visibleSecrets, setVisibleSecrets] = useState<Set<string>>(new Set())
  const [healthResult, setHealthResult] = useState<ServiceHealthResult | null>(
    null,
  )
  const [saveSuccess, setSaveSuccess] = useState(false)

  const saveMutation = useMutation({
    mutationFn: async () => {
      const entries: ConfigEntryUpdateDto[] = category.entries.map((entry) => {
        if (dirtyFields.has(entry.key)) {
          return { key: entry.key, value: formValues[entry.key] || null }
        }
        // Unchanged secret: send sentinel
        if (entry.isSecret) {
          return { key: entry.key, value: '****' }
        }
        // Unchanged non-secret: send current value
        return { key: entry.key, value: formValues[entry.key] || null }
      })
      return api.updateConfigCategory(category.category, entries)
    },
    onSuccess: (result) => {
      setDirtyFields(new Set())
      setSaveSuccess(true)
      setTimeout(() => setSaveSuccess(false), 3000)
      queryClient.invalidateQueries({ queryKey: ['admin', 'config'] })
      if (result.restartRequired) {
        onRestartRequired([`${category.displayName} updated`])
      }
    },
  })

  const healthMutation = useMutation({
    mutationFn: () => api.testConfigHealth(category.category),
    onSuccess: (result) => setHealthResult(result),
  })

  const handleChange = (key: string, value: string) => {
    setFormValues((prev) => ({ ...prev, [key]: value }))
    setDirtyFields((prev) => new Set(prev).add(key))
    setSaveSuccess(false)
  }

  const toggleSecretVisibility = (key: string) => {
    setVisibleSecrets((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  const hasTestableConnection = [
    'ConnectionStrings',
    'KnowzPlatform',
    'AzureOpenAI',
    'AzureAISearch',
    'Storage',
  ].includes(category.category)

  return (
    <div className="bg-card border border-border/60 rounded-xl shadow-sm">
      {/* Section Header */}
      <div className="px-6 py-4 border-b border-border/60">
        <h2 className="text-lg font-semibold">{category.displayName}</h2>
        <p className="text-sm text-muted-foreground mt-1">
          {category.description}
        </p>
      </div>

      {/* Config Entries */}
      <div className="divide-y divide-border">
        {category.entries.map((entry) => (
          <ConfigEntryField
            key={entry.key}
            entry={entry}
            value={formValues[entry.key] ?? ''}
            isDirty={dirtyFields.has(entry.key)}
            isSecretVisible={visibleSecrets.has(entry.key)}
            onChange={(val) => handleChange(entry.key, val)}
            onToggleVisibility={() => toggleSecretVisibility(entry.key)}
          />
        ))}
      </div>

      {/* Action Bar */}
      <div className="flex items-center justify-between px-6 py-4 border-t border-border/60 bg-muted rounded-b-xl">
        <div className="flex items-center gap-3">
          <button
            onClick={() => saveMutation.mutate()}
            disabled={saveMutation.isPending || dirtyFields.size === 0}
            className="inline-flex items-center gap-2 px-4 py-2 bg-primary text-primary-foreground rounded-md text-sm font-medium hover:opacity-90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {saveMutation.isPending ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <Save size={14} />
            )}
            Save Changes
          </button>

          {saveSuccess && (
            <span className="inline-flex items-center gap-1 text-sm text-green-600 dark:text-green-400">
              <CheckCircle2 size={14} /> Saved
            </span>
          )}

          {saveMutation.isError && (
            <span className="text-sm text-red-600 dark:text-red-400">
              {saveMutation.error instanceof Error
                ? saveMutation.error.message
                : 'Save failed'}
            </span>
          )}
        </div>

        <div className="flex items-center gap-3">
          {hasTestableConnection && (
            <button
              onClick={() => healthMutation.mutate()}
              disabled={healthMutation.isPending}
              className="inline-flex items-center gap-2 px-3 py-2 border border-input rounded-md text-sm font-medium hover:bg-muted transition-colors disabled:opacity-50"
            >
              {healthMutation.isPending ? (
                <Loader2 size={14} className="animate-spin" />
              ) : (
                <Wifi size={14} />
              )}
              Test Connection
            </button>
          )}

          {healthResult && <HealthBadge result={healthResult} />}
        </div>
      </div>
    </div>
  )
}

function ConfigEntryField({
  entry,
  value,
  isDirty,
  isSecretVisible,
  onChange,
  onToggleVisibility,
}: {
  entry: ConfigEntryDto
  value: string
  isDirty: boolean
  isSecretVisible: boolean
  onChange: (val: string) => void
  onToggleVisibility: () => void
}) {
  return (
    <div className="px-6 py-4">
      <div className="flex items-start justify-between gap-4">
        <div className="flex-1 min-w-0">
          <label className="block text-sm font-medium text-foreground">
            {entry.key}
            {entry.source && <SourceBadge source={entry.source} />}
            {entry.requiresRestart && (
              <span className="ml-2 text-xs text-amber-600 dark:text-amber-400">
                Requires restart
              </span>
            )}
          </label>
          {entry.description && (
            <p className="text-xs text-muted-foreground mt-0.5">
              {entry.description}
            </p>
          )}
        </div>
      </div>

      <div className="mt-2 flex items-center gap-2">
        <div className="relative flex-1">
          <input
            type={entry.isSecret && !isSecretVisible ? 'password' : 'text'}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            placeholder={entry.isSet ? undefined : 'Not configured'}
            className={`w-full px-3 py-2 text-sm border rounded-md bg-card text-foreground focus:outline-none focus:ring-2 focus:ring-ring ${
              isDirty
                ? 'border-blue-400 dark:border-blue-500'
                : 'border-input'
            }`}
          />
        </div>
        {entry.isSecret && (
          <button
            type="button"
            onClick={onToggleVisibility}
            className="p-2 text-muted-foreground hover:text-foreground transition-colors"
            title={isSecretVisible ? 'Hide value' : 'Show value'}
          >
            {isSecretVisible ? <EyeOff size={16} /> : <Eye size={16} />}
          </button>
        )}
      </div>

      {entry.lastModifiedAt && (
        <p className="text-xs text-muted-foreground mt-1.5">
          Last modified: {new Date(entry.lastModifiedAt).toLocaleDateString()}{' '}
          {entry.lastModifiedBy ? `by ${entry.lastModifiedBy}` : ''}
        </p>
      )}
    </div>
  )
}

function SourceBadge({ source }: { source: string }) {
  const config: Record<string, { label: string; className: string }> = {
    database: {
      label: 'DB',
      className:
        'bg-green-50 dark:bg-green-950/30 text-green-700 dark:text-green-400',
    },
    keyvault: {
      label: 'Key Vault',
      className:
        'bg-blue-50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-400',
    },
    environment: {
      label: 'Env',
      className:
        'bg-muted text-muted-foreground',
    },
    appsettings: {
      label: 'Config',
      className:
        'bg-muted text-muted-foreground',
    },
  }

  const cfg = config[source]
  if (!cfg) return null

  return (
    <span
      className={`ml-2 inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium ${cfg.className}`}
    >
      {cfg.label}
    </span>
  )
}

function HealthBadge({ result }: { result: ServiceHealthResult }) {
  if (result.isHealthy) {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-green-50 dark:bg-green-950/30 text-green-700 dark:text-green-400">
        <CheckCircle2 size={12} />
        {result.status}
        {result.latencyMs != null && ` - ${result.latencyMs}ms`}
      </span>
    )
  }

  if (result.status === 'Not Configured') {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-amber-50 dark:bg-amber-950/30 text-amber-700 dark:text-amber-400">
        <AlertTriangle size={12} />
        Not Configured
      </span>
    )
  }

  return (
    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-400">
      <XCircle size={12} />
      {result.status}
    </span>
  )
}

function formatUptime(startupTime: string): string {
  const start = new Date(startupTime).getTime()
  const now = Date.now()
  const diff = now - start

  const hours = Math.floor(diff / (1000 * 60 * 60))
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60))

  if (hours > 0) {
    return `${hours}h ${minutes}m`
  }
  return `${minutes}m`
}
