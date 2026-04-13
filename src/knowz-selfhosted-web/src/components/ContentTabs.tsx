import type { LucideIcon } from 'lucide-react'

export type TabId = 'summary' | 'original' | 'attachments' | 'history' | 'commit-history'

interface Tab {
  id: TabId
  label: string
  icon: LucideIcon
  count?: number
}

interface ContentTabsProps {
  activeTab: TabId
  onTabChange: (tab: TabId) => void
  tabs: Tab[]
}

export default function ContentTabs({ activeTab, onTabChange, tabs }: ContentTabsProps) {
  return (
    <div className="border-b border-border/60">
      <nav className="-mb-px flex space-x-1" aria-label="Content tabs">
        {tabs.map(({ id, label, icon: Icon, count }) => {
          const isActive = activeTab === id
          return (
            <button
              key={id}
              onClick={() => onTabChange(id)}
              className={`inline-flex items-center gap-1.5 whitespace-nowrap py-2.5 px-3 border-b-2 text-sm font-medium transition-colors ${
                isActive
                  ? 'border-primary text-primary font-semibold'
                  : 'border-transparent text-muted-foreground hover:text-foreground hover:border-border'
              }`}
            >
              <Icon size={14} />
              {label}
              {count !== undefined && count > 0 && (
                <span
                  className={`ml-0.5 px-1.5 py-0.5 text-[10px] font-medium rounded-full ${
                    isActive
                      ? 'bg-primary/10 text-primary'
                      : 'bg-muted text-muted-foreground'
                  }`}
                >
                  {count}
                </span>
              )}
            </button>
          )
        })}
      </nav>
    </div>
  )
}
