import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '../lib/api-client'
import { Tags, ArrowLeft, BookOpen } from 'lucide-react'

export default function TopicsPage() {
  const [selectedTopicId, setSelectedTopicId] = useState<string | null>(null)

  const { data, isLoading, error } = useQuery({
    queryKey: ['topics'],
    queryFn: () => api.listTopics(),
  })

  const topicDetail = useQuery({
    queryKey: ['topic', selectedTopicId],
    queryFn: () => api.getTopicDetails(selectedTopicId!),
    enabled: !!selectedTopicId,
  })

  if (selectedTopicId) {
    return (
      <div className="space-y-4">
        <button
          onClick={() => setSelectedTopicId(null)}
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft size={16} /> Back to Topics
        </button>

        {topicDetail.isLoading ? (
          <div className="space-y-2">
            <div className="h-8 w-48 bg-muted rounded animate-pulse" />
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-14 bg-muted rounded animate-pulse" />
            ))}
          </div>
        ) : topicDetail.error ? (
          <p className="text-red-600 dark:text-red-400">
            {topicDetail.error instanceof Error ? topicDetail.error.message : 'Failed to load topic'}
          </p>
        ) : topicDetail.data ? (
          <>
            <h1 className="text-2xl font-bold">{topicDetail.data.name}</h1>
            {topicDetail.data.description && (
              <p className="text-muted-foreground">{topicDetail.data.description}</p>
            )}
            <div className="space-y-2">
              {topicDetail.data.knowledgeItems.map((item) => (
                <Link
                  key={item.id}
                  to={`/knowledge/${item.id}`}
                  className="flex items-center gap-3 p-3 bg-card border border-border/60 rounded-lg hover:border-border transition-colors"
                >
                  <BookOpen size={16} className="text-muted-foreground flex-shrink-0" />
                  <div className="min-w-0 flex-1">
                    <p className="font-medium truncate">{item.title}</p>
                    {item.summary && (
                      <p className="text-sm text-muted-foreground truncate">
                        {item.summary}
                      </p>
                    )}
                  </div>
                  <span className="px-2 py-0.5 text-xs bg-muted rounded flex-shrink-0">
                    {item.type}
                  </span>
                </Link>
              ))}
              {topicDetail.data.knowledgeItems.length === 0 && (
                <p className="text-muted-foreground text-center py-8">
                  No knowledge items in this topic.
                </p>
              )}
            </div>
          </>
        ) : null}
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {error && (
        <p className="text-red-600 dark:text-red-400">
          {error instanceof Error ? error.message : 'Failed to load topics'}
        </p>
      )}

      {isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-16 bg-muted rounded animate-pulse" />
          ))}
        </div>
      ) : (
        <>
          {data && (
            <p className="text-sm text-muted-foreground">
              {data.totalCount} topic{data.totalCount !== 1 ? 's' : ''}
            </p>
          )}
          <div className="space-y-2">
            {data?.topics.map((topic) => (
              <button
                key={topic.id}
                onClick={() => setSelectedTopicId(topic.id)}
                className="w-full text-left flex items-center gap-3 p-4 bg-card border border-border/60 rounded-lg hover:border-border transition-colors"
              >
                <Tags size={16} className="text-muted-foreground flex-shrink-0" />
                <div className="min-w-0 flex-1">
                  <p className="font-medium">{topic.name}</p>
                  {topic.description && (
                    <p className="text-sm text-muted-foreground truncate">
                      {topic.description}
                    </p>
                  )}
                </div>
                <span className="text-sm text-muted-foreground flex-shrink-0">
                  {topic.knowledgeCount} items
                </span>
              </button>
            ))}
            {data?.topics.length === 0 && (
              <p className="text-muted-foreground text-center py-8">
                No topics found.
              </p>
            )}
          </div>
        </>
      )}
    </div>
  )
}
