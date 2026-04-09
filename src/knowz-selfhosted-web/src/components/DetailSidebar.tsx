import { Link } from 'react-router-dom'
import { Sparkles, Tag, Info, Paperclip, Calendar } from 'lucide-react'
import SidebarCard from './SidebarCard'

interface DetailSidebarProps {
  briefSummary?: string
  tags: string[]
  type: string
  vaults: { id: string; name: string; isPrimary: boolean }[]
  source?: string
  createdAt: string
  updatedAt: string
  isIndexed: boolean
  indexedAt?: string
  attachmentCount: number
}

export default function DetailSidebar({
  briefSummary,
  tags,
  type,
  vaults,
  source,
  createdAt,
  updatedAt,
  isIndexed,
  indexedAt,
  attachmentCount,
}: DetailSidebarProps) {
  return (
    <div className="space-y-3">
      {/* Brief Summary */}
      {briefSummary && (
        <SidebarCard
          title="AI Summary"
          icon={<Sparkles size={12} />}
          defaultOpen
        >
          <p className="text-[11px] text-muted-foreground leading-relaxed">
            {briefSummary}
          </p>
        </SidebarCard>
      )}

      {/* Tags */}
      <SidebarCard
        title="Tags"
        icon={<Tag size={12} />}
        count={tags.length}
        defaultOpen
      >
        {tags.length > 0 ? (
          <div className="flex flex-wrap gap-1.5">
            {tags.map((tag) => (
              <span
                key={tag}
                className="px-2 py-0.5 text-[10px] font-medium bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300 rounded-full"
              >
                {tag}
              </span>
            ))}
          </div>
        ) : (
          <p className="text-[10px] text-muted-foreground">No tags</p>
        )}
      </SidebarCard>

      {/* Metadata */}
      <SidebarCard
        title="Metadata"
        icon={<Info size={12} />}
        defaultOpen
      >
        <dl className="space-y-2 text-[11px]">
          <div>
            <dt className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Type</dt>
            <dd className="mt-0.5">
              <span className="px-2 py-0.5 bg-muted rounded text-[10px] font-medium">{type}</span>
            </dd>
          </div>

          {vaults.length > 0 && (
            <div>
              <dt className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Vault</dt>
              <dd className="mt-0.5 flex flex-wrap gap-1">
                {vaults.map((v) => (
                  <Link
                    key={v.id}
                    to={`/vaults/${v.id}`}
                    className="text-blue-600 dark:text-blue-400 hover:underline text-[11px]"
                  >
                    {v.name}
                  </Link>
                ))}
              </dd>
            </div>
          )}

          {source && (
            <div>
              <dt className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Source</dt>
              <dd className="mt-0.5 text-muted-foreground truncate" title={source}>{source}</dd>
            </div>
          )}

          <div>
            <dt className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Created</dt>
            <dd className="mt-0.5 flex items-center gap-1 text-muted-foreground">
              <Calendar size={10} />
              {new Date(createdAt).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })}
            </dd>
          </div>

          <div>
            <dt className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Updated</dt>
            <dd className="mt-0.5 flex items-center gap-1 text-muted-foreground">
              <Calendar size={10} />
              {new Date(updatedAt).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })}
            </dd>
          </div>

          <div>
            <dt className="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Enrichment</dt>
            <dd className="mt-0.5">
              {isIndexed ? (
                <span className="text-green-600 dark:text-green-400 text-[11px]">
                  Indexed
                  {indexedAt && (
                    <span className="text-muted-foreground ml-1">
                      {new Date(indexedAt).toLocaleDateString()}
                    </span>
                  )}
                </span>
              ) : (
                <span className="text-muted-foreground text-[11px]">Pending</span>
              )}
            </dd>
          </div>
        </dl>
      </SidebarCard>

      {/* Attachments count */}
      <SidebarCard
        title="Attachments"
        icon={<Paperclip size={12} />}
        count={attachmentCount}
        defaultOpen={false}
      >
        <p className="text-[10px] text-muted-foreground">
          {attachmentCount > 0
            ? `${attachmentCount} file${attachmentCount === 1 ? '' : 's'} attached. View in the Attachments tab.`
            : 'No files attached.'}
        </p>
      </SidebarCard>
    </div>
  )
}
