import { useState, useEffect } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { Search, SlidersHorizontal, X, MessageCircleQuestion } from 'lucide-react'
import AskPage from './AskPage'

const KNOWLEDGE_TYPES = ['Note', 'Document', 'Email', 'Image', 'Audio', 'Video', 'Code', 'Link', 'MeetingMinutes']

export default function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const mode = searchParams.get('mode') === 'ask' ? 'ask' : 'search'
  const queryParam = searchParams.get('q') || ''
  const [inputValue, setInputValue] = useState(queryParam)
  const [showFilters, setShowFilters] = useState(() => {
    // Show filters if any filter param is already set
    return !!(
      searchParams.get('vaultId') ||
      searchParams.get('type') ||
      searchParams.get('startDate') ||
      searchParams.get('endDate') ||
      searchParams.get('tags')
    )
  })

  const vaultId = searchParams.get('vaultId') || ''
  const typeFilter = searchParams.get('type') || ''
  const startDate = searchParams.get('startDate') || ''
  const endDate = searchParams.get('endDate') || ''
  const tagsFilter = searchParams.get('tags') || ''

  const hasActiveFilters = !!(vaultId || typeFilter || startDate || endDate || tagsFilter)

  const vaults = useQuery({
    queryKey: ['vaults', 'search-filters'],
    queryFn: () => api.listVaults(false),
    enabled: showFilters && mode === 'search',
  })

  useEffect(() => {
    if (mode !== 'search') return
    const timer = setTimeout(() => {
      const params = new URLSearchParams(searchParams)
      if (inputValue) {
        params.set('q', inputValue)
      } else {
        params.delete('q')
      }
      setSearchParams(params, { replace: true })
    }, 300)
    return () => clearTimeout(timer)
  }, [inputValue, searchParams, setSearchParams, mode])

  const updateFilter = (key: string, value: string) => {
    const params = new URLSearchParams(searchParams)
    if (value) {
      params.set(key, value)
    } else {
      params.delete(key)
    }
    setSearchParams(params, { replace: true })
  }

  const clearFilters = () => {
    const params = new URLSearchParams()
    if (queryParam) params.set('q', queryParam)
    setSearchParams(params, { replace: true })
  }

  const { data, isLoading, error } = useQuery({
    queryKey: ['search', queryParam, vaultId, typeFilter, startDate, endDate, tagsFilter],
    queryFn: () =>
      api.search(queryParam, {
        vaultId: vaultId || undefined,
        type: typeFilter || undefined,
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        tags: tagsFilter || undefined,
      }),
    enabled: queryParam.length > 0 && mode === 'search',
  })

  const handleModeToggle = (newMode: 'search' | 'ask') => {
    const params = new URLSearchParams()
    if (newMode === 'ask') {
      params.set('mode', 'ask')
    }
    setSearchParams(params, { replace: true })
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Search</h1>
        <div className="flex bg-muted rounded-lg p-0.5">
          <button
            onClick={() => handleModeToggle('search')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
              mode === 'search'
                ? 'bg-card text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <Search size={14} />
            Search
          </button>
          <button
            onClick={() => handleModeToggle('ask')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
              mode === 'ask'
                ? 'bg-card text-foreground shadow-sm'
                : 'text-muted-foreground hover:text-foreground'
            }`}
          >
            <MessageCircleQuestion size={14} />
            Ask
          </button>
        </div>
      </div>

      {mode === 'ask' ? (
        <AskPage />
      ) : (
        <>
          <div className="relative">
            <Search size={18} className="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
            <input
              type="text"
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              placeholder="Search knowledge..."
              autoFocus
              className="w-full pl-10 pr-12 py-2.5 border border-input rounded-md bg-card text-sm"
            />
            <button
              onClick={() => setShowFilters(!showFilters)}
              className={`absolute right-2 top-1/2 -translate-y-1/2 p-1.5 rounded transition-colors ${
                showFilters || hasActiveFilters
                  ? 'text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30'
                  : 'text-muted-foreground hover:text-foreground'
              }`}
              title={showFilters ? 'Hide filters' : 'Show filters'}
            >
              <SlidersHorizontal size={16} />
            </button>
          </div>

          {showFilters && (
            <div className="p-4 border border-border/60 rounded-xl bg-muted space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Filters</span>
                {hasActiveFilters && (
                  <button
                    onClick={clearFilters}
                    className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
                  >
                    <X size={12} /> Clear filters
                  </button>
                )}
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1">
                    Vault
                  </label>
                  <select
                    value={vaultId}
                    onChange={(e) => updateFilter('vaultId', e.target.value)}
                    className="w-full px-3 py-1.5 border border-input rounded-md bg-card text-sm"
                  >
                    <option value="">All vaults</option>
                    {vaults.data?.vaults.map((v) => (
                      <option key={v.id} value={v.id}>
                        {v.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1">
                    Type
                  </label>
                  <select
                    value={typeFilter}
                    onChange={(e) => updateFilter('type', e.target.value)}
                    className="w-full px-3 py-1.5 border border-input rounded-md bg-card text-sm"
                  >
                    <option value="">All types</option>
                    {KNOWLEDGE_TYPES.map((t) => (
                      <option key={t} value={t}>
                        {t}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1">
                    From
                  </label>
                  <input
                    type="date"
                    value={startDate}
                    onChange={(e) => updateFilter('startDate', e.target.value)}
                    className="w-full px-3 py-1.5 border border-input rounded-md bg-card text-sm"
                  />
                </div>

                <div>
                  <label className="block text-xs font-medium text-muted-foreground mb-1">
                    To
                  </label>
                  <input
                    type="date"
                    value={endDate}
                    onChange={(e) => updateFilter('endDate', e.target.value)}
                    className="w-full px-3 py-1.5 border border-input rounded-md bg-card text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-muted-foreground mb-1">
                  Tags (comma-separated)
                </label>
                <input
                  type="text"
                  value={tagsFilter}
                  onChange={(e) => updateFilter('tags', e.target.value)}
                  placeholder="e.g. finance, quarterly"
                  className="w-full px-3 py-1.5 border border-input rounded-md bg-card text-sm"
                />
              </div>
            </div>
          )}

          {error && (
            <p className="text-red-600 dark:text-red-400">
              {error instanceof Error ? error.message : 'Search failed'}
            </p>
          )}

          {isLoading && queryParam && (
            <div className="space-y-2">
              {Array.from({ length: 3 }).map((_, i) => (
                <div key={i} className="h-20 bg-muted rounded animate-pulse" />
              ))}
            </div>
          )}

          {data && (
            <div className="space-y-2">
              <p className="text-sm text-muted-foreground">
                {data.totalResults} result{data.totalResults !== 1 ? 's' : ''}
                {hasActiveFilters && ' (filtered)'}
              </p>
              {data.items.map((result) => (
                <Link
                  key={result.knowledgeId}
                  to={`/knowledge/${result.knowledgeId}`}
                  className="block p-4 bg-card border border-border/60 rounded-xl hover:shadow-md transition-all"
                >
                  <div className="flex items-center gap-2 mb-1">
                    <h3 className="font-medium">{result.title}</h3>
                    {result.knowledgeType && (
                      <span className="px-2 py-0.5 text-xs bg-muted rounded">
                        {result.knowledgeType}
                      </span>
                    )}
                    <span className="text-xs text-muted-foreground ml-auto">
                      {Math.round(result.score * 100)}%
                    </span>
                  </div>
                  <p className="text-sm text-muted-foreground line-clamp-2">
                    {result.summary || result.content}
                  </p>
                  <div className="flex flex-wrap gap-2 mt-2">
                    {result.vaultName && (
                      <span className="text-xs text-muted-foreground">
                        {result.vaultName}
                      </span>
                    )}
                    {result.tags.map((tag) => (
                      <span key={tag} className="px-1.5 py-0.5 text-xs bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded">
                        {tag}
                      </span>
                    ))}
                  </div>
                </Link>
              ))}
              {data.items.length === 0 && queryParam && (
                <p className="text-muted-foreground text-center py-8">
                  No results found for &quot;{queryParam}&quot;.
                  {hasActiveFilters && ' Try adjusting your filters.'}
                </p>
              )}
            </div>
          )}

          {!queryParam && !isLoading && (
            <p className="text-muted-foreground text-center py-8">
              Type a query to search your knowledge base.
            </p>
          )}
        </>
      )}
    </div>
  )
}
