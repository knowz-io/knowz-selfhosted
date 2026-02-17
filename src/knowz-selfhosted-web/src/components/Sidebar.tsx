import { NavLink } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  LayoutDashboard,
  BookOpen,
  Archive,
  Search,
  MessageCircleQuestion,
  MessagesSquare,
  Inbox,
  FileText,
  Tags,
  Tag,
  Contact,
  UserCircle,
  Settings,
  Key,
  Database,
  Plug,
  X,
  Shield,
  Building2,
  Users,
  LogOut,
  ArrowLeftRight,
} from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api } from '../lib/api-client'
import { UserRole } from '../lib/types'

const navItems = [
  { path: '/', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/knowledge', label: 'Knowledge', icon: BookOpen },
  { path: '/vaults', label: 'Vaults', icon: Archive },
  { path: '/search', label: 'Search', icon: Search },
  { path: '/ask', label: 'Ask', icon: MessageCircleQuestion },
  { path: '/chat', label: 'Chat', icon: MessagesSquare },
  { path: '/inbox', label: 'Inbox', icon: Inbox },
  { path: '/files', label: 'Files', icon: FileText },
  { path: '/topics', label: 'Topics', icon: Tags },
  { path: '/tags', label: 'Tags', icon: Tag },
  { path: '/entities', label: 'Entities', icon: Contact },
  { path: '/account', label: 'Account', icon: UserCircle },
  { path: '/settings', label: 'Settings', icon: Settings },
  { path: '/api-keys', label: 'API Keys', icon: Key },
  { path: '/data', label: 'Data', icon: Database },
  { path: '/mcp-setup', label: 'MCP Setup', icon: Plug },
]

const adminItems = [
  { path: '/admin', label: 'Dashboard', icon: LayoutDashboard },
  { path: '/admin/tenants', label: 'Tenants', icon: Building2 },
  { path: '/admin/users', label: 'Users', icon: Users },
  { path: '/admin/sso', label: 'SSO', icon: Shield },
  { path: '/admin/settings', label: 'Settings', icon: Settings },
]

const roleLabels: Record<number, string> = {
  [UserRole.SuperAdmin]: 'SuperAdmin',
  [UserRole.Admin]: 'Admin',
  [UserRole.User]: 'User',
}

const roleBadgeStyles: Record<number, string> = {
  [UserRole.SuperAdmin]: 'bg-purple-100 dark:bg-purple-950/40 text-purple-700 dark:text-purple-400',
  [UserRole.Admin]: 'bg-blue-100 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400',
  [UserRole.User]: 'bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400',
}

interface SidebarProps {
  open: boolean
  onClose: () => void
}

export default function Sidebar({ open, onClose }: SidebarProps) {
  const { user, isAuthenticated, logout, activeTenantId, setActiveTenantId } = useAuth()
  const isSuperAdmin = user?.role === UserRole.SuperAdmin
  const queryClient = useQueryClient()

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
          fixed top-0 left-0 z-40 h-full w-60 bg-gray-50 dark:bg-gray-900
          border-r border-gray-200 dark:border-gray-800
          transform transition-transform duration-200
          lg:translate-x-0 lg:static lg:z-auto
          ${open ? 'translate-x-0' : '-translate-x-full'}
          flex flex-col
        `}
      >
        <div className="flex items-center justify-between h-14 px-4 border-b border-gray-200 dark:border-gray-800">
          <span className="text-lg font-semibold">Knowz</span>
          <button
            onClick={onClose}
            className="lg:hidden p-1 rounded hover:bg-gray-200 dark:hover:bg-gray-700"
            aria-label="Close sidebar"
          >
            <X size={20} />
          </button>
        </div>

        {/* SuperAdmin Tenant Selector */}
        {isSuperAdmin && tenants && tenants.length > 1 && (
          <div className="px-3 py-2 border-b border-gray-200 dark:border-gray-800">
            <div className="flex items-center gap-1.5 mb-1">
              <ArrowLeftRight size={12} className="text-purple-500" />
              <span className="text-[10px] font-semibold uppercase tracking-wider text-purple-600 dark:text-purple-400">
                Tenant Context
              </span>
            </div>
            <select
              value={activeTenantId ?? ''}
              onChange={handleTenantChange}
              className="w-full px-2 py-1.5 text-xs border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-800 text-gray-900 dark:text-white focus:outline-none focus:ring-1 focus:ring-purple-500"
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
          {navItems.map(({ path, label, icon: Icon }) => (
            <NavLink
              key={path}
              to={path}
              end={path === '/'}
              onClick={onClose}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-gray-200 dark:bg-gray-800 text-gray-900 dark:text-white'
                    : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-900 dark:hover:text-white'
                }`
              }
            >
              <Icon size={18} />
              {label}
            </NavLink>
          ))}

          {/* Admin Section */}
          {isSuperAdmin && (
            <>
              <div className="pt-4 pb-1 px-3">
                <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500">
                  <Shield size={12} />
                  Administration
                </div>
              </div>
              {adminItems.map(({ path, label, icon: Icon }) => (
                <NavLink
                  key={path}
                  to={path}
                  end={path === '/admin'}
                  onClick={onClose}
                  className={({ isActive }) =>
                    `flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                      isActive
                        ? 'bg-gray-200 dark:bg-gray-800 text-gray-900 dark:text-white'
                        : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800 hover:text-gray-900 dark:hover:text-white'
                    }`
                  }
                >
                  <Icon size={18} />
                  {label}
                </NavLink>
              ))}
            </>
          )}
        </nav>

        {/* User Info & Logout */}
        {isAuthenticated && user && (
          <div className="border-t border-gray-200 dark:border-gray-800 p-3">
            <div className="flex items-center gap-3 px-2 py-2">
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
                onClick={logout}
                className="p-1.5 rounded hover:bg-gray-200 dark:hover:bg-gray-700 text-gray-500 hover:text-gray-700 dark:hover:text-gray-300 transition-colors"
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
