import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { Users, MapPin, Calendar, Plus, Pencil, Trash2, X, Check } from 'lucide-react'
import type { EntityItem } from '../lib/types'

const entityTabs = [
  { type: 'person', label: 'Persons', icon: Users },
  { type: 'location', label: 'Locations', icon: MapPin },
  { type: 'event', label: 'Events', icon: Calendar },
] as const

type EntityType = (typeof entityTabs)[number]['type']

export default function EntitiesPage() {
  const queryClient = useQueryClient()
  const [activeTab, setActiveTab] = useState<EntityType>('person')
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [newName, setNewName] = useState('')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editName, setEditName] = useState('')
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['entities', activeTab, search],
    queryFn: () => api.findEntities(activeTab, search || undefined),
  })

  const createMutation = useMutation({
    mutationFn: (name: string) => api.createEntity(activeTab, name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['entities', activeTab] })
      setNewName('')
      setShowCreate(false)
      setError(null)
    },
    onError: (err: Error) => setError(err.message),
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, name }: { id: string; name: string }) =>
      api.updateEntity(id, activeTab, name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['entities', activeTab] })
      setEditingId(null)
      setError(null)
    },
    onError: (err: Error) => setError(err.message),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.deleteEntity(id, activeTab),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['entities', activeTab] })
      setDeleteConfirm(null)
      setError(null)
    },
    onError: (err: Error) => setError(err.message),
  })

  const handleTabChange = (tab: EntityType) => {
    setActiveTab(tab)
    setSearch('')
    setShowCreate(false)
    setEditingId(null)
    setDeleteConfirm(null)
    setError(null)
  }

  const handleCreate = () => {
    const trimmed = newName.trim()
    if (!trimmed) return
    createMutation.mutate(trimmed)
  }

  const handleUpdate = (id: string) => {
    const trimmed = editName.trim()
    if (!trimmed) return
    updateMutation.mutate({ id, name: trimmed })
  }

  const entities: EntityItem[] = data?.entities ?? []

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Entities</h1>
        <button
          onClick={() => { setShowCreate(true); setError(null) }}
          className="inline-flex items-center gap-2 px-3 py-1.5 text-sm bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md hover:opacity-80"
        >
          <Plus size={16} /> Add {activeTab.charAt(0).toUpperCase() + activeTab.slice(1)}
        </button>
      </div>

      {/* Tabs */}
      <div className="flex border-b border-gray-200 dark:border-gray-800">
        {entityTabs.map(({ type, label, icon: Icon }) => (
          <button
            key={type}
            onClick={() => handleTabChange(type)}
            className={`flex items-center gap-2 px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
              activeTab === type
                ? 'border-gray-900 dark:border-white text-gray-900 dark:text-white'
                : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
            }`}
          >
            <Icon size={16} />
            {label}
          </button>
        ))}
      </div>

      {error && (
        <div className="p-3 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg text-sm text-red-700 dark:text-red-400">
          {error}
        </div>
      )}

      {/* Create form */}
      {showCreate && (
        <div className="flex items-center gap-2 p-3 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <input
            type="text"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleCreate()}
            placeholder={`${activeTab.charAt(0).toUpperCase() + activeTab.slice(1)} name...`}
            className="flex-1 px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-1 focus:ring-gray-400"
            autoFocus
          />
          <button
            onClick={handleCreate}
            disabled={createMutation.isPending}
            className="p-1.5 text-green-600 hover:bg-green-50 dark:hover:bg-green-950/30 rounded"
          >
            <Check size={18} />
          </button>
          <button
            onClick={() => { setShowCreate(false); setNewName(''); setError(null) }}
            className="p-1.5 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 rounded"
          >
            <X size={18} />
          </button>
        </div>
      )}

      {/* Search */}
      <input
        type="text"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder={`Search ${activeTab}s...`}
        className="w-full px-3 py-2 text-sm border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 focus:outline-none focus:ring-1 focus:ring-gray-400"
      />

      {/* List */}
      {isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-12 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="space-y-1">
          {entities.map((entity) => {
            const ActiveIcon = entityTabs.find((t) => t.type === activeTab)?.icon ?? Users
            return (
              <div
                key={entity.id}
                className="flex items-center gap-3 p-3 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg"
              >
                <ActiveIcon size={16} className="text-gray-400 flex-shrink-0" />
                {editingId === entity.id ? (
                  <>
                    <input
                      type="text"
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') handleUpdate(entity.id)
                        if (e.key === 'Escape') setEditingId(null)
                      }}
                      className="flex-1 px-2 py-1 text-sm border border-gray-300 dark:border-gray-700 rounded bg-white dark:bg-gray-800 focus:outline-none focus:ring-1 focus:ring-gray-400"
                      autoFocus
                    />
                    <button
                      onClick={() => handleUpdate(entity.id)}
                      disabled={updateMutation.isPending}
                      className="p-1 text-green-600 hover:bg-green-50 dark:hover:bg-green-950/30 rounded"
                    >
                      <Check size={16} />
                    </button>
                    <button
                      onClick={() => setEditingId(null)}
                      className="p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 rounded"
                    >
                      <X size={16} />
                    </button>
                  </>
                ) : (
                  <>
                    <span className="flex-1 font-medium text-sm">{entity.name}</span>
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {new Date(entity.createdAt).toLocaleDateString()}
                    </span>
                    <button
                      onClick={() => {
                        setEditingId(entity.id)
                        setEditName(entity.name)
                        setError(null)
                      }}
                      className="p-1 text-gray-400 hover:text-gray-700 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-800 rounded"
                    >
                      <Pencil size={14} />
                    </button>
                    {deleteConfirm === entity.id ? (
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => deleteMutation.mutate(entity.id)}
                          disabled={deleteMutation.isPending}
                          className="px-2 py-0.5 text-xs text-red-600 border border-red-300 dark:border-red-800 rounded hover:bg-red-50 dark:hover:bg-red-950/30"
                        >
                          Delete
                        </button>
                        <button
                          onClick={() => setDeleteConfirm(null)}
                          className="p-1 text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 rounded"
                        >
                          <X size={14} />
                        </button>
                      </div>
                    ) : (
                      <button
                        onClick={() => setDeleteConfirm(entity.id)}
                        className="p-1 text-gray-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/30 rounded"
                      >
                        <Trash2 size={14} />
                      </button>
                    )}
                  </>
                )}
              </div>
            )
          })}
          {entities.length === 0 && (
            <p className="text-gray-500 dark:text-gray-400 text-center py-8">
              No {activeTab}s found.
            </p>
          )}
        </div>
      )}
    </div>
  )
}
