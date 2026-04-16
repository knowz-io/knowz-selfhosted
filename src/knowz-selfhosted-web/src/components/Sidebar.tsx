import { Link, NavLink } from 'react-router-dom'
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
  Plus,
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
    queryClient.invalidateQueries()
  }

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `group flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-all duration-200 ${
      isActive
        ? 'bg-sidebar-muted/60 dark:bg-white/12 text-sidebar-foreground shadow-sm ring-1 ring-border/30 dark:ring-white/10'
        : 'text-sidebar-foreground/72 hover:bg-sidebar-muted/40 dark:hover:bg-white/6 hover:text-sidebar-foreground'
    }`

  return (
    <>
      {open && (
        <div
          className="fixed inset-0 z-30 bg-black/50 lg:hidden"
          onClick={onClose}
        />
      )}
      <aside
        className={`
          fixed top-0 left-0 z-40 flex h-screen w-72 flex-col border-r border-border/40 dark:border-white/10 bg-sidebar text-sidebar-foreground shadow-elevated
          transform transition-transform duration-200
          lg:translate-x-0 lg:sticky lg:top-0 lg:h-screen lg:z-30
          ${open ? 'translate-x-0' : '-translate-x-full'}
        `}
      >
        <div className="border-b border-border/40 dark:border-white/10 bg-gradient-to-br from-white/8 via-white/4 to-transparent px-5 py-5">
          <div className="flex items-start justify-between gap-3">
            <NavLink to="/knowledge" className="flex items-center gap-3" onClick={onClose}>
              <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-lg shadow-black/20">
                <BookOpen size={18} />
              </div>
              <p className="text-lg font-semibold tracking-tight">Knowz</p>
            </NavLink>
            <button
              onClick={onClose}
              className="rounded-2xl p-2 text-sidebar-foreground/70 transition-colors hover:bg-sidebar-muted/60 dark:hover:bg-white/8 hover:text-sidebar-foreground lg:hidden"
              aria-label="Close sidebar"
            >
              <X size={18} />
            </button>
          </div>

          <div className="mt-4">
            <Link
              to="/knowledge/new"
              onClick={onClose}
              className="inline-flex w-full items-center justify-center gap-2 rounded-2xl bg-sidebar-accent px-4 py-3 text-sm font-semibold text-slate-950 shadow-lg shadow-black/20 transition-transform hover:-translate-y-0.5"
            >
              <Plus size={15} />
              New Knowledge
            </Link>
          </div>
        </div>

        <TenantSwitcher />

        {isSuperAdmin && tenants && tenants.length > 0 && (
          <div className="border-b border-border/40 dark:border-white/10 px-4 py-3">
            <div className="mb-1 flex items-center gap-1.5">
              <ArrowLeftRight size={12} className="text-purple-500" />
              <span className="text-[10px] font-semibold uppercase tracking-wider text-purple-300">
                Tenant Context
              </span>
            </div>
            <select
              value={activeTenantId ?? ''}
              onChange={handleTenantChange}
              className="w-full rounded-xl border border-border/30 dark:border-white/10 bg-sidebar-muted/50 dark:bg-white/6 px-3 py-2 text-xs text-sidebar-foreground focus:outline-none focus:ring-1 focus:ring-sidebar-accent"
            >
              <option value="">My Tenant ({user?.tenantName ?? 'Default'})</option>
              {tenants.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} {t.id === user?.tenantId ? '(yours)' : ''}
                </option>
              ))}
            </select>
            {activeTenantName && (
              <p className="mt-1 text-[10px] text-purple-300">
                Viewing: {activeTenantName}
              </p>
            )}
          </div>
        )}

        <nav className="flex-1 overflow-y-auto px-3 py-4">
          {navSections.map((section, si) => (
            <div key={si} className="mb-4">
              {section.label && (
                <div className="px-3 pb-1 pt-1">
                  <span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-sidebar-foreground/45">
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

          <div className="px-3 pb-1 pt-1">
            <span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-sidebar-foreground/45">
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

          {showAdmin && (() => {
            const superAdminOnlyPaths = new Set(['/admin/tenants', '/admin/sso', '/admin/settings'])
            const visibleAdminItems = isSuperAdmin
              ? adminItems
              : adminItems.filter(item => !superAdminOnlyPaths.has(item.path))
            return (
              <>
                <div className="px-3 pb-1 pt-4">
                  <div className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.18em] text-sidebar-foreground/45">
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

        {isAuthenticated && user && (
          <div className="border-t border-border/40 dark:border-white/10 bg-gradient-to-t from-black/20 to-transparent p-4">
            <div className="flex items-center gap-3 rounded-2xl border border-border/30 dark:border-white/8 bg-sidebar-muted/40 dark:bg-white/5 px-3 py-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-sidebar-muted/60 dark:bg-white/10 text-sm font-bold text-sidebar-foreground">
                {(user.displayName || user.username || '?').charAt(0).toUpperCase()}
              </div>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-sidebar-foreground">
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
                className="rounded-2xl p-2 text-sidebar-foreground/70 transition-colors hover:bg-sidebar-muted/60 dark:hover:bg-white/10 hover:text-sidebar-foreground"
                title={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
              >
                {theme === 'dark' ? <Sun size={16} /> : <Moon size={16} />}
              </button>
              <button
                onClick={logout}
                className="rounded-2xl p-2 text-sidebar-foreground/70 transition-colors hover:bg-sidebar-muted/60 dark:hover:bg-white/10 hover:text-sidebar-foreground"
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
