interface Tab {
  key: string
  label: string
  icon?: React.ComponentType<{ size?: number }>
}

interface TabContainerProps {
  tabs: Tab[]
  activeTab: string
  onTabChange: (key: string) => void
}

export default function TabContainer({ tabs, activeTab, onTabChange }: TabContainerProps) {
  return (
    <div className="flex border-b border-border">
      {tabs.map(({ key, label, icon: Icon }) => (
        <button
          key={key}
          onClick={() => onTabChange(key)}
          className={`flex items-center gap-2 px-4 py-2 text-sm font-medium border-b-2 transition-colors duration-150 ${
            activeTab === key
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          {Icon && <Icon size={16} />}
          {label}
        </button>
      ))}
    </div>
  )
}
