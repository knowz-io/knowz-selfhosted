import { useSearchParams } from 'react-router-dom'
import { Layers, Tag, Tags, Users } from 'lucide-react'
import PageHeader from '../components/ui/PageHeader'
import SurfaceCard from '../components/ui/SurfaceCard'
import TabContainer from '../components/TabContainer'
import TagsPage from './TagsPage'
import TopicsPage from './TopicsPage'
import EntitiesPage from './EntitiesPage'

const tabs = [
  { key: 'tags', label: 'Tags', icon: Tag },
  { key: 'topics', label: 'Topics', icon: Tags },
  { key: 'entities', label: 'Entities', icon: Users },
]

const tabCopy: Record<string, { title: string; description: string }> = {
  tags: {
    title: 'Labels and retrieval cues',
    description: 'Keep lightweight descriptors tidy so knowledge stays easy to filter, browse, and refine.',
  },
  topics: {
    title: 'Themes and grouped context',
    description: 'Review broader categories that connect related knowledge into a stronger navigable map.',
  },
  entities: {
    title: 'Named people, places, and events',
    description: 'Curate extracted entities so references stay clean, searchable, and easier to reason over.',
  },
}

export default function OrganizePage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const requestedTab = searchParams.get('tab') || 'tags'
  const activeTab = tabs.some((tab) => tab.key === requestedTab) ? requestedTab : 'tags'
  const activeCopy = tabCopy[activeTab]

  const handleTabChange = (key: string) => {
    setSearchParams(key === 'tags' ? {} : { tab: key }, { replace: true })
  }

  return (
    <div className="space-y-6">
      <PageHeader
        eyebrow="Structure"
        title="Organize knowledge"
        titleAs="h2"
        description="Keep the self-hosted workspace clean and legible by managing tags, topics, and entities from one place."
        meta={
          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <div className="sh-stat">
              <p className="sh-kicker">Tags</p>
              <p className="mt-2 text-sm font-semibold">Fast metadata cleanup</p>
              <p className="mt-2 text-xs text-muted-foreground">Rename or remove labels that drift over time.</p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">Topics</p>
              <p className="mt-2 text-sm font-semibold">Stronger thematic browse</p>
              <p className="mt-2 text-xs text-muted-foreground">Inspect the clusters that define how content hangs together.</p>
            </div>
            <div className="sh-stat">
              <p className="sh-kicker">Entities</p>
              <p className="mt-2 text-sm font-semibold">Cleaner extracted references</p>
              <p className="mt-2 text-xs text-muted-foreground">Adjust named things before they turn into noisy retrieval signals.</p>
            </div>
          </div>
        }
      />

      <TabContainer tabs={tabs} activeTab={activeTab} onTabChange={handleTabChange} />

      <SurfaceCard className="p-5">
        <div className="flex items-start gap-4">
          <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <Layers size={18} />
          </div>
          <div className="min-w-0">
            <p className="sh-kicker">{tabs.find((tab) => tab.key === activeTab)?.label}</p>
            <h3 className="mt-2 text-xl font-semibold tracking-tight">{activeCopy.title}</h3>
            <p className="mt-2 max-w-3xl text-sm leading-6 text-muted-foreground">
              {activeCopy.description}
            </p>
          </div>
        </div>
      </SurfaceCard>

      {activeTab === 'tags' && <TagsPage />}
      {activeTab === 'topics' && <TopicsPage />}
      {activeTab === 'entities' && <EntitiesPage />}
    </div>
  )
}
