import { useState } from 'react'
import { useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { ArrowLeft } from 'lucide-react'
import { formatMarkdown } from '../lib/format-markdown'

const KNOWLEDGE_TYPES = ['Note', 'Document', 'Email', 'Image', 'Audio', 'Video', 'Code', 'Link']

export default function KnowledgeCreatePage() {
  const navigate = useNavigate()

  const [title, setTitle] = useState('')
  const [content, setContent] = useState('')
  const [type, setType] = useState('Note')
  const [vaultId, setVaultId] = useState('')
  const [tags, setTags] = useState('')
  const [source, setSource] = useState('')
  const [validationError, setValidationError] = useState('')
  const [activeTab, setActiveTab] = useState<'write' | 'preview'>('write')

  const vaults = useQuery({
    queryKey: ['vaults', 'create'],
    queryFn: () => api.listVaults(false),
  })

  const createMut = useMutation({
    mutationFn: () =>
      api.createKnowledge({
        title: title || undefined,
        content,
        type,
        vaultId: vaultId || undefined,
        tags: tags
          .split(',')
          .map((t) => t.trim())
          .filter(Boolean),
        source: source || undefined,
      }),
    onSuccess: (data) => {
      navigate(`/knowledge/${data.id}`)
    },
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setValidationError('')
    if (!content.trim()) {
      setValidationError('Content is required.')
      return
    }
    createMut.mutate()
  }

  return (
    <div className="space-y-4">
      <Link
        to="/knowledge"
        className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-900 dark:hover:text-white"
      >
        <ArrowLeft size={16} /> Back to Knowledge
      </Link>

      <h1 className="text-2xl font-bold">Create Knowledge</h1>

      <form onSubmit={handleSubmit} className="space-y-4 max-w-2xl">
        <div>
          <label className="block text-sm font-medium mb-1">Title</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="Optional - auto-generated from content if empty"
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
          />
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">
            Content <span className="text-red-500">*</span>
          </label>
          <div className="border border-gray-300 dark:border-gray-700 rounded-md overflow-hidden">
            <div className="flex border-b border-gray-300 dark:border-gray-700 bg-gray-50 dark:bg-gray-800">
              <button
                type="button"
                onClick={() => setActiveTab('write')}
                className={`px-4 py-2 text-sm font-medium transition-colors ${
                  activeTab === 'write'
                    ? 'text-gray-900 dark:text-white bg-white dark:bg-gray-900 border-b-2 border-gray-900 dark:border-white'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                }`}
              >
                Write
              </button>
              <button
                type="button"
                onClick={() => setActiveTab('preview')}
                className={`px-4 py-2 text-sm font-medium transition-colors ${
                  activeTab === 'preview'
                    ? 'text-gray-900 dark:text-white bg-white dark:bg-gray-900 border-b-2 border-gray-900 dark:border-white'
                    : 'text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300'
                }`}
              >
                Preview
              </button>
            </div>
            {activeTab === 'write' ? (
              <textarea
                value={content}
                onChange={(e) => setContent(e.target.value)}
                rows={12}
                placeholder="Enter knowledge content... (supports Markdown)"
                className="w-full px-3 py-2 bg-white dark:bg-gray-900 text-sm font-mono border-0 focus:ring-0 focus:outline-none"
              />
            ) : (
              <div className="px-3 py-2 bg-white dark:bg-gray-900 min-h-[288px]">
                {content.trim() ? (
                  <div
                    className="prose prose-sm dark:prose-invert max-w-none"
                    dangerouslySetInnerHTML={{ __html: formatMarkdown(content) }}
                  />
                ) : (
                  <p className="text-gray-400 dark:text-gray-500 text-sm italic">
                    Nothing to preview
                  </p>
                )}
              </div>
            )}
          </div>
          {validationError && (
            <p className="text-red-600 dark:text-red-400 text-sm mt-1">{validationError}</p>
          )}
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium mb-1">Type</label>
            <select
              value={type}
              onChange={(e) => setType(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
            >
              {KNOWLEDGE_TYPES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-sm font-medium mb-1">Vault</label>
            <select
              value={vaultId}
              onChange={(e) => setVaultId(e.target.value)}
              className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
            >
              <option value="">Default vault</option>
              {vaults.data?.vaults.map((v) => (
                <option key={v.id} value={v.id}>{v.name}</option>
              ))}
            </select>
          </div>
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">Tags</label>
          <input
            type="text"
            value={tags}
            onChange={(e) => setTags(e.target.value)}
            placeholder="Comma-separated tags"
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
          />
        </div>

        <div>
          <label className="block text-sm font-medium mb-1">Source</label>
          <input
            type="text"
            value={source}
            onChange={(e) => setSource(e.target.value)}
            placeholder="URL or reference (optional)"
            className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
          />
        </div>

        {createMut.error && (
          <p className="text-red-600 dark:text-red-400 text-sm">
            {createMut.error instanceof Error ? createMut.error.message : 'Failed to create'}
          </p>
        )}

        <button
          type="submit"
          disabled={createMut.isPending}
          className="px-6 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium disabled:opacity-50"
        >
          {createMut.isPending ? 'Creating...' : 'Create'}
        </button>
      </form>
    </div>
  )
}
