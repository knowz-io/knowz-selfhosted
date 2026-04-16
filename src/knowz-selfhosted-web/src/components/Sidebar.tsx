import { useState, useEffect } from 'react'
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
  Plus,
  PanelLeftClose,
  PanelLeftOpen,
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
  // SH_HidePlatformSync: experimental feature hidden from nav. Route still registered in App.tsx.
  // { path: '/admin/platform-sync', label: 'Platform Sync', icon: Cloud },
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

const COLLAPSED_STORAGE_KEY = 'selfhosted-sidebar-collapsed'

export default function Sidebar({ open, onClose }: SidebarProps) {
  const { user, isAuthenticated, logout, activeTenantId, setActiveTenantId } = useAuth()
  const isSuperAdmin = user?.role === UserRole.SuperAdmin
  const isAdmin = user?.role === UserRole.Admin
  const showAdmin = isSuperAdmin || isAdmin
  const queryClient = useQueryClient()
  const { theme, toggle: toggleTheme } = useTheme()

  const [collapsed, setCollapsed] = useState<boolean>(() => {
    if (typeof window === 'undefined') return false
    return window.localStorage.getItem(COLLAPSED_STORAGE_KEY) === 'true'
  })

  useEffect(() => {
    if (typeof window === 'undefined') return
    window.localStorage.setItem(COLLAPSED_STORAGE_KEY, String(collapsed))
  }, [collapsed])

  // Track desktop viewport (>= lg breakpoint = 1024px) to apply collapse only on desktop.
  // On mobile, the sidebar is always shown expanded when the overlay is open.
  const [isDesktop, setIsDesktop] = useState<boolean>(() => {
    if (typeof window === 'undefined') return true
    return window.matchMedia('(min-width: 1024px)').matches
  })

  useEffect(() => {
    if (typeof window === 'undefined') return
    const mq = window.matchMedia('(min-width: 1024px)')
    const handler = (e: MediaQueryListEvent) => setIsDesktop(e.matches)
    mq.addEventListener('change', handler)
    return () => mq.removeEventListener('change', handler)
  }, [])

  const toggleCollapsed = () => setCollapsed((prev) => !prev)

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

  // Collapsed state only applies on desktop (lg+). On mobile, sidebar uses overlay open/close
  // and labels are always shown when the overlay is open.
  const isCollapsed = isDesktop && collapsed
  const showLabels = !isCollapsed

  const navLinkClass = ({ isActive }: { isActive: boolean }) =>
    `group flex items-center ${isCollapsed ? 'justify-center' : 'gap-3'} rounded-2xl ${
      isCollapsed ? 'px-0 py-2.5' : 'px-3 py-2.5'
    } text-sm font-medium transition-all duration-200 ${
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
          fixed top-0 left-0 z-40 flex h-screen ${isCollapsed ? 'w-16' : 'w-72'} flex-col border-r border-border/40 dark:border-white/10 bg-sidebar text-sidebar-foreground shadow-elevated
          transform transition-all duration-200
          lg:translate-x-0 lg:sticky lg:top-0 lg:h-screen lg:z-30
          ${open ? 'translate-x-0' : '-translate-x-full'}
        `}
      >
        <div className={`border-b border-border/40 dark:border-white/10 bg-gradient-to-br from-white/8 via-white/4 to-transparent ${isCollapsed ? 'px-2 py-4' : 'px-5 py-5'}`}>
          <div className={`flex items-center ${isCollapsed ? 'justify-center' : 'justify-between'} gap-2`}>
            <NavLink to="/knowledge" className={`flex items-center ${isCollapsed ? '' : 'gap-3'}`} onClick={onClose} title="Knowz">
              <div className="flex h-10 w-10 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-lg shadow-black/20">
                <BookOpen size={18} />
              </div>
              {showLabels && <p className="text-lg font-semibold tracking-tight">Knowz</p>}
            </NavLink>
            {/* Desktop collapse toggle */}
            <button
              onClick={toggleCollapsed}
              className={`hidden lg:inline-flex rounded-2xl p-2 text-sidebar-foreground/70 transition-colors hover:bg-sidebar-muted/60 dark:hover:bg-white/8 hover:text-sidebar-foreground ${
                isCollapsed ? 'absolute right-[-12px] top-6 z-50 border border-border/40 bg-sidebar dark:border-white/10 shadow-md' : ''
              }`}
              aria-label={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              title={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
            >
              {isCollapsed ? <PanelLeftOpen size={16} /> : <PanelLeftClose size={16} />}
            </button>
            {/* Mobile close button */}
            <button
              onClick={onClose}
              className="rounded-2xl p-2 text-sidebar-foreground/70 transition-colors hover:bg-sidebar-muted/60 dark:hover:bg-white/8 hover:text-sidebar-foreground lg:hidden"
              aria-label="Close sidebar"
            >
              <X size={18} />
            </button>
          </div>

          {showLabels && (
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
          )}
          {!showLabels && (
            <div className="mt-4 flex justify-center">
              <Link
                to="/knowledge/new"
                onClick={onClose}
                className="inline-flex h-10 w-10 items-center justify-center rounded-2xl bg-sidebar-accent text-slate-950 shadow-lg shadow-black/20 transition-transform hover:-translate-y-0.5"
                title="New Knowledge"
              >
                <Plus size={16} />
              </Link>
            </div>
          )}
        </div>

        {showLabels && <TenantSwitcher />}

        {showLabels && isSuperAdmin && tenants && tenants.length > 0 && (
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

        <nav className={`flex-1 overflow-y-auto ${isCollapsed ? 'px-2 py-3' : 'px-3 py-4'}`}>
          {navSections.map((section, si) => (
            <div key={si} className="mb-4">
              {showLabels && section.label && (
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
                  title={isCollapsed ? label : undefined}
                >
                  <Icon size={18} />
                  {showLabels && label}
                </NavLink>
              ))}
            </div>
          ))}

          {showLabels && (
            <div className="px-3 pb-1 pt-1">
              <span className="text-[10px] font-semibold uppercase tracking-[0.18em] text-sidebar-foreground/45">
                Settings
              </span>
            </div>
          )}
          <NavLink
            to={settingsItem.path}
            onClick={onClose}
            className={navLinkClass}
            title={isCollapsed ? settingsItem.label : undefined}
          >
            <settingsItem.icon size={18} />
            {showLabels && settingsItem.label}
          </NavLink>

          {showAdmin && (() => {
            const superAdminOnlyPaths = new Set(['/admin/tenants', '/admin/sso', '/admin/settings'])
            const visibleAdminItems = isSuperAdmin
              ? adminItems
              : adminItems.filter(item => !superAdminOnlyPaths.has(item.path))
            return (
              <>
                {showLabels && (
                  <div className="px-3 pb-1 pt-4">
                    <div className="flex items-center gap-2 text-[10px] font-semibold uppercase tracking-[0.18em] text-sidebar-foreground/45">
                      <Shield size={12} />
                      Administration
                    </div>
                  </div>
                )}
                {visibleAdminItems.map(({ path, label, icon: Icon }) => (
                  <NavLink
                    key={path}
                    to={path}
                    end={path === '/admin'}
                    onClick={onClose}
                    className={navLinkClass}
                    title={isCollapsed ? label : undefined}
                  >
                    <Icon size={18} />
                    {showLabels && label}
                  </NavLink>
                ))}
              </>
            )
          })()}
        </nav>

        {isAuthenticated && user && (
          <div className={`border-t border-border/40 dark:border-white/10 bg-gradient-to-t from-black/20 to-transparent ${isCollapsed ? 'p-2' : 'p-4'}`}>
            {showLabels ? (
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
            ) : (
              <div className="flex flex-col items-center gap-2">
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
            )}
          </div>
        )}
      </aside>
    </>
  )
}
