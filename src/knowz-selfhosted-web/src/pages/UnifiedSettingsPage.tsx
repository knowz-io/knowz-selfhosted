import { useSearchParams } from 'react-router-dom'
import { Settings, UserCircle, Key, Database, Plug } from 'lucide-react'
import TabContainer from '../components/TabContainer'
import SettingsPage from './SettingsPage'
import AccountPage from './AccountPage'
import ApiKeysPage from './ApiKeysPage'
import DataPortabilityPage from './DataPortabilityPage'
import McpSetupPage from './McpSetupPage'

const tabs = [
  { key: 'connection', label: 'Connection', icon: Settings },
  { key: 'account', label: 'Account', icon: UserCircle },
  { key: 'api-keys', label: 'API Keys', icon: Key },
  { key: 'data', label: 'Data', icon: Database },
  { key: 'mcp', label: 'MCP Setup', icon: Plug },
]

const tabComponents: Record<string, React.ComponentType> = {
  connection: SettingsPage,
  account: AccountPage,
  'api-keys': ApiKeysPage,
  data: DataPortabilityPage,
  mcp: McpSetupPage,
}

export default function UnifiedSettingsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const activeTab = searchParams.get('tab') || 'connection'

  const handleTabChange = (key: string) => {
    setSearchParams(key === 'connection' ? {} : { tab: key }, { replace: true })
  }

  const ActiveComponent = tabComponents[activeTab] ?? SettingsPage

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-bold">Settings</h1>
      <TabContainer tabs={tabs} activeTab={activeTab} onTabChange={handleTabChange} />
      <ActiveComponent />
    </div>
  )
}
