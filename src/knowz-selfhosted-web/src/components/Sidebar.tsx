import { NavLink } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  LayoutDashboard,
  BookOpen,
  Archive,
  Search,
  MessagesSquare,
  Inbox,
  FileText,
  Layers,
  Settings,
  X,
  Shield,
  Building2,
  Users,
  LogOut,
  ArrowLeftRight,
  Wrench,
  Sun,
  Moon,
  ClipboardList,
  Cloud,
} from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api } from '../lib/api-client'
import { UserRole } from '../lib/types'
import { useTheme } from '../lib/theme'
import TenantSwitcher from './TenantSwitcher'

interface NavSection {
  label?: string
  items: { path: string; label: string; icon: React.ComponentType<{ size?: number }> }[]
}

const navSections: NavSection[] = [
  {
    items: [
      { path: '/', label: 'Dashboard', icon: LayoutDashboard },
      { path: '/chat', label: 'Chat', icon: MessagesSquare },
    ],
  },
  {
    label: 'Knowledge',
    items: [
      { path: '/knowledge', label: 'Knowledge', icon: BookOpen },
      { path: '/vaults', label: 'Vaults', icon: Archive },
      { path: '/files', label: 'Files', icon: FileText },
      { path: '/inbox', label: 'Inbox', icon: Inbox },
    ],
  },
  {
    label: 'Discover',
    items: [
      { path: '/search', label: 'Search', icon: Search },
      { path: '/organize', label: 'Organize', icon: Layers },
    ],
  },
]

const settingsItem = { path: '/settings', label: 'Settings', icon: Settings }

const adminItems = [
  { path: '/admin', label: 'Overview', icon: LayoutDashboard },
  { path: '/admin/tenants', label: 'Tenants', icon: Building2 },
  { path: '/admin/users', label: 'Users', icon: Users },
  { path: '/admin/audit-logs', label: 'Audit Logs', icon: ClipboardList },
  { path: '/admin/platform-sync', label: 'Platform Sync', icon: Cloud },
  { path: '/admin/sso', label: 'SSO', icon: Shield },
  { path: '/admin/settings', label: 'Configuration', icon: Wrench },
]

const roleLabels: Record<number, string> = {
  [UserRole.SuperAdmin]: 'SuperAdmin',
  [UserRole.Admin]: 'Admin',
  [UserRole.User]: 'User',
}

const roleBadgeStyles: Record<number, string> = {
  [UserRole.SuperAdmin]: 'bg-purple-100 dark:bg-purple-950/40 text-purple-700 dark:text-purple-400',
  [UserRole.Admin]: 'bg-blue-100 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400',
  [UserRole.User]: 'bg-muted text-muted-foreground',
}

interface SidebarProps {
  open: boolean
  onClose: () => void
}

export default function Sidebar({ open, onClose }: SidebarProps) {
  const { user, isAuthenticated, logout, activeTenantId, setActiveTenantId } = useAuth()
  const isSuperAdmin = user?.role === UserRole.SuperAdmin
  const isAdmin = user?.role === UserRole.Admin
  const showAdmin = isSuperAdmin || isAdmin
  const queryClient = useQueryClient()
  const { theme, toggle: toggleTheme } = useTheme()

  const { data: tenants } = useQuery({
    queryKey: ['admin', 'tenants-sidebar'],
    queryFn: () => api.listTenants(),
    enabled: isSuperAdmin,
    staleTime: 60_000,
  })

  const activeTenantName = activeTenantId
    ? tenants?.find((t) => t.id === activeTenantId)?.name ?? 'Loading...'
    : null

  const handleTenantChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const value = e.target.value || null
    setActiveTenantId(value)
    // Invalidate all queries so they refetch with the new tenant context
    queryClient.invalidateQueries()
  }

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium transition-all duration-200 ${
      isActive
        ? 'bg-primary/10 text-primary shadow-sm ring-1 ring-primary/10'
        : 'text-muted-foreground hover:bg-muted/80 hover:text-foreground hover:translate-x-0.5'
    }`

  return (
    <>
      {open && (
        <div
          className="fixed inset-0 bg-black/50 z-30 lg:hidden"
          onClick={onClose}
        />
      )}
      <aside
        className={`
          fixed top-0 left-0 z-40 h-full w-60 bg-card shadow-lg
          border-r border-border/50
          transform transition-transform duration-200
          lg:translate-x-0 lg:static lg:z-auto
          ${open ? 'translate-x-0' : '-translate-x-full'}
          flex flex-col
        `}
      >
        <div className="flex items-center justify-between h-14 px-4 border-b border-border/50 bg-gradient-to-r from-primary/5 to-transparent">
          <NavLink to="/knowledge" className="flex items-center gap-2.5">
            <div className="flex items-center justify-center w-8 h-8 bg-primary rounded-lg shadow-sm">
              <BookOpen className="text-primary-foreground" size={16} />
            </div>
            <span className="text-lg font-bold tracking-tight">Knowz</span>
          </NavLink>
          <button
            onClick={onClose}
            className="lg:hidden p-1.5 rounded-lg hover:bg-muted transition-colors"
            aria-label="Close sidebar"
          >
            <X size={18} />
          </button>
        </div>

        {/* Multi-tenant User Switcher */}
        <TenantSwitcher />

        {/* SuperAdmin Tenant Selector */}
        {isSuperAdmin && tenants && tenants.length > 0 && (
          <div className="px-3 py-2 border-b">
            <div className="flex items-center gap-1.5 mb-1">
              <ArrowLeftRight size={12} className="text-purple-500" />
              <span className="text-[10px] font-semibold uppercase tracking-wider text-purple-600 dark:text-purple-400">
                Tenant Context
              </span>
            </div>
            <select
              value={activeTenantId ?? ''}
              onChange={handleTenantChange}
              className="w-full px-2 py-1.5 text-xs border border-input rounded-md bg-card focus:outline-none focus:ring-1 focus:ring-ring"
            >
              <option value="">My Tenant ({user?.tenantName ?? 'Default'})</option>
              {tenants.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} {t.id === user?.tenantId ? '(yours)' : ''}
                </option>
              ))}
            </select>
            {activeTenantName && (
              <p className="mt-1 text-[10px] text-purple-600 dark:text-purple-400">
                Viewing: {activeTenantName}
              </p>
            )}
          </div>
        )}

        <nav className="flex-1 overflow-y-auto p-2 space-y-1">
          {navSections.map((section, si) => (
            <div key={si}>
              {section.label && (
                <div className="pt-4 pb-1 px-3">
                  <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                    {section.label}
                  </span>
                </div>
              )}
              {section.items.map(({ path, label, icon: Icon }) => (
                <NavLink
                  key={path}
                  to={path}
                  end={path === '/'}
                  onClick={onClose}
                  className={navLinkClass}
                >
                  <Icon size={18} />
                  {label}
                </NavLink>
              ))}
            </div>
          ))}

          {/* Settings - positioned before admin */}
          <div className="pt-4 pb-1 px-3">
            <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
              Settings
            </span>
          </div>
          <NavLink
            to={settingsItem.path}
            onClick={onClose}
            className={navLinkClass}
          >
            <settingsItem.icon size={18} />
            {settingsItem.label}
          </NavLink>

          {/* Admin Section */}
          {showAdmin && (() => {
            const superAdminOnlyPaths = new Set(['/admin/tenants', '/admin/sso', '/admin/settings'])
            const visibleAdminItems = isSuperAdmin
              ? adminItems
              : adminItems.filter(item => !superAdminOnlyPaths.has(item.path))
            return (
              <>
                <div className="pt-4 pb-1 px-3">
                  <div className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
                    <Shield size={12} />
                    Administration
                  </div>
                </div>
                {visibleAdminItems.map(({ path, label, icon: Icon }) => (
                  <NavLink
                    key={path}
                    to={path}
                    end={path === '/admin'}
                    onClick={onClose}
                    className={navLinkClass}
                  >
                    <Icon size={18} />
                    {label}
                  </NavLink>
                ))}
              </>
            )
          })()}
        </nav>

        {/* User Info & Logout */}
        {isAuthenticated && user && (
          <div className="border-t border-border/50 p-3 bg-gradient-to-t from-muted/30 to-transparent">
            <div className="flex items-center gap-3 px-2 py-2">
              <div className="flex items-center justify-center w-8 h-8 rounded-full bg-primary/10 text-primary text-xs font-bold shrink-0">
                {(user.displayName || user.username || '?').charAt(0).toUpperCase()}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-sm font-medium truncate">
                  {user.displayName || user.username}
                </p>
                <span
                  className={`inline-flex px-1.5 py-0 rounded text-[10px] font-medium ${
                    roleBadgeStyles[user.role] ?? roleBadgeStyles[UserRole.User]
                  }`}
                >
                  {roleLabels[user.role] ?? 'User'}
                </span>
              </div>
              <button
                onClick={toggleTheme}
                className="p-1.5 rounded-lg hover:bg-muted text-muted-foreground hover:text-foreground transition-all duration-200 hover:scale-105"
                title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
              >
                {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
              </button>
              <button
                onClick={logout}
                className="p-1.5 rounded-lg hover:bg-muted text-muted-foreground hover:text-foreground transition-all duration-200 hover:scale-105"
                title="Sign out"
              >
                <LogOut size={16} />
              </button>
            </div>
          </div>
        )}
      </aside>
    </>
  )
}
