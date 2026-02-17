import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useMutation } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import type { AskResponse } from '../lib/types'
import { Send, Loader2 } from 'lucide-react'

export default function AskPage() {
  const [question, setQuestion] = useState('')
  const [result, setResult] = useState<AskResponse | null>(null)

  const askMut = useMutation({
    mutationFn: (q: string) => api.ask({ question: q }),
    onSuccess: (data) => setResult(data),
  })

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (!question.trim()) return
    askMut.mutate(question)
  }

  return (
    <div className="space-y-4 max-w-3xl">
      <h1 className="text-2xl font-bold">Ask</h1>

      <form onSubmit={handleSubmit} className="space-y-3">
        <textarea
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Ask a question about your knowledge..."
          rows={3}
          className="w-full px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm"
        />
        <button
          type="submit"
          disabled={askMut.isPending || !question.trim()}
          className="inline-flex items-center gap-2 px-5 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium disabled:opacity-50"
        >
          {askMut.isPending ? <Loader2 size={16} className="animate-spin" /> : <Send size={16} />}
          {askMut.isPending ? 'Thinking...' : 'Ask'}
        </button>
      </form>

      {askMut.error && (
        <p className="text-red-600 dark:text-red-400 text-sm">
          {askMut.error instanceof Error ? askMut.error.message : 'Failed to get answer'}
        </p>
      )}

      {result && (
        <div className="space-y-4 pt-2">
          <div className="bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg p-5">
            <div className="flex items-center justify-between mb-3">
              <h2 className="text-sm font-medium text-gray-500 dark:text-gray-400">Answer</h2>
              <span className="text-xs px-2 py-0.5 bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-300 rounded">
                {Math.round(result.confidence * 100)}% confidence
              </span>
            </div>
            <div className="whitespace-pre-wrap text-sm">{result.answer}</div>
          </div>

          {result.sources.length > 0 && (
            <div>
              <h3 className="text-sm font-medium text-gray-500 dark:text-gray-400 mb-2">
                Sources ({result.sources.length})
              </h3>
              <div className="flex flex-wrap gap-2">
                {result.sources.map((s) => (
                  <Link
                    key={s.knowledgeId}
                    to={`/knowledge/${s.knowledgeId}`}
                    className="text-sm text-blue-600 dark:text-blue-400 hover:underline"
                  >
                    {s.knowledgeId.slice(0, 8)}...
                  </Link>
                ))}
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
