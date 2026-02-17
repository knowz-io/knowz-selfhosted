import { useState, useEffect } from 'react'
import { useSearchParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { Search, SlidersHorizontal, X } from 'lucide-react'

const KNOWLEDGE_TYPES = ['Note', 'Document', 'Email', 'Image', 'Audio', 'Video', 'Code', 'Link', 'MeetingMinutes']

export default function SearchPage() {
  const [searchParams, setSearchParams] = useSearchParams()
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
    enabled: showFilters,
  })

  useEffect(() => {
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
  }, [inputValue, searchParams, setSearchParams])

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
    enabled: queryParam.length > 0,
  })

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Search</h1>

      <div className="relative">
        <Search size={18} className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400" />
        <input
          type="text"
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          placeholder="Search knowledge..."
          autoFocus
          className="w-full pl-10 pr-12 py-2.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
        />
        <button
          onClick={() => setShowFilters(!showFilters)}
          className={`absolute right-2 top-1/2 -translate-y-1/2 p-1.5 rounded transition-colors ${
            showFilters || hasActiveFilters
              ? 'text-blue-600 dark:text-blue-400 bg-blue-50 dark:bg-blue-900/30'
              : 'text-gray-400 hover:text-gray-600 dark:hover:text-gray-300'
          }`}
          title={showFilters ? 'Hide filters' : 'Show filters'}
        >
          <SlidersHorizontal size={16} />
        </button>
      </div>

      {showFilters && (
        <div className="p-4 border border-gray-200 dark:border-gray-800 rounded-lg bg-gray-50 dark:bg-gray-900/50 space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-sm font-medium text-gray-700 dark:text-gray-300">Filters</span>
            {hasActiveFilters && (
              <button
                onClick={clearFilters}
                className="inline-flex items-center gap-1 text-xs text-gray-500 hover:text-gray-700 dark:hover:text-gray-300"
              >
                <X size={12} /> Clear filters
              </button>
            )}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            <div>
              <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">
                Vault
              </label>
              <select
                value={vaultId}
                onChange={(e) => updateFilter('vaultId', e.target.value)}
                className="w-full px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
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
              <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">
                Type
              </label>
              <select
                value={typeFilter}
                onChange={(e) => updateFilter('type', e.target.value)}
                className="w-full px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
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
              <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">
                From
              </label>
              <input
                type="date"
                value={startDate}
                onChange={(e) => updateFilter('startDate', e.target.value)}
                className="w-full px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
              />
            </div>

            <div>
              <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">
                To
              </label>
              <input
                type="date"
                value={endDate}
                onChange={(e) => updateFilter('endDate', e.target.value)}
                className="w-full px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
              />
            </div>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-500 dark:text-gray-400 mb-1">
              Tags (comma-separated)
            </label>
            <input
              type="text"
              value={tagsFilter}
              onChange={(e) => updateFilter('tags', e.target.value)}
              placeholder="e.g. finance, quarterly"
              className="w-full px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
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
            <div key={i} className="h-20 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
          ))}
        </div>
      )}

      {data && (
        <div className="space-y-2">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {data.totalResults} result{data.totalResults !== 1 ? 's' : ''}
            {hasActiveFilters && ' (filtered)'}
          </p>
          {data.items.map((result) => (
            <Link
              key={result.knowledgeId}
              to={`/knowledge/${result.knowledgeId}`}
              className="block p-4 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg hover:border-gray-400 dark:hover:border-gray-600 transition-colors"
            >
              <div className="flex items-center gap-2 mb-1">
                <h3 className="font-medium">{result.title}</h3>
                {result.knowledgeType && (
                  <span className="px-2 py-0.5 text-xs bg-gray-100 dark:bg-gray-800 rounded">
                    {result.knowledgeType}
                  </span>
                )}
                <span className="text-xs text-gray-400 ml-auto">
                  {Math.round(result.score * 100)}%
                </span>
              </div>
              <p className="text-sm text-gray-600 dark:text-gray-400 line-clamp-2">
                {result.summary || result.content}
              </p>
              <div className="flex flex-wrap gap-2 mt-2">
                {result.vaultName && (
                  <span className="text-xs text-gray-500 dark:text-gray-400">
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
            <p className="text-gray-500 dark:text-gray-400 text-center py-8">
              No results found for &quot;{queryParam}&quot;.
              {hasActiveFilters && ' Try adjusting your filters.'}
            </p>
          )}
        </div>
      )}

      {!queryParam && !isLoading && (
        <p className="text-gray-500 dark:text-gray-400 text-center py-8">
          Type a query to search your knowledge base.
        </p>
      )}
    </div>
  )
}
