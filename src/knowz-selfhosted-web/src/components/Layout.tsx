import { useState, useRef, useEffect, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Menu, LogOut, ChevronDown, User, Building2 } from 'lucide-react'
import Sidebar from './Sidebar'
import { useAuth } from '../lib/auth'
import { UserRole } from '../lib/types'

const roleLabels: Record<number, string> = {
  [UserRole.SuperAdmin]: 'SuperAdmin',
  [UserRole.Admin]: 'Admin',
  [UserRole.User]: 'User',
}

interface LayoutProps {
  children: ReactNode
}

interface PageMeta {
  section: string
  title: string
  description: string
}

const pageRegistry: Array<{ match: (pathname: string) => boolean; meta: PageMeta }> = [
  {
    match: (pathname) => pathname === '/' || pathname === '/dashboard',
    meta: {
      section: 'Overview',
      title: 'Dashboard',
      description: 'Track the shape of your self-hosted workspace at a glance.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/knowledge'),
    meta: {
      section: 'Library',
      title: 'Knowledge',
      description: 'Browse, refine, and manage your self-hosted knowledge base.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/search') || pathname.startsWith('/ask'),
    meta: {
      section: 'Discover',
      title: 'Search',
      description: 'Search, filter, and ask questions across your self-hosted workspace.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/chat'),
    meta: {
      section: 'Workspace',
      title: 'Chat',
      description: 'Talk directly to your vaults with traceable, self-hosted context.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/vaults'),
    meta: {
      section: 'Library',
      title: 'Vaults',
      description: 'Organize collections and shape how knowledge is grouped.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/settings') || pathname.startsWith('/account'),
    meta: {
      section: 'Control',
      title: 'Settings',
      description: 'Configure connections, account settings, and self-hosted capabilities.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/files'),
    meta: {
      section: 'Assets',
      title: 'Files',
      description: 'Inspect uploaded files and manage attachment-heavy knowledge workflows.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/inbox'),
    meta: {
      section: 'Capture',
      title: 'Inbox',
      description: 'Review staged content before it becomes durable knowledge.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/organize'),
    meta: {
      section: 'Structure',
      title: 'Organize',
      description: 'Navigate tags, topics, and entities with a cleaner self-hosted frame.',
    },
  },
  {
    match: (pathname) => pathname.startsWith('/admin'),
    meta: {
      section: 'Administration',
      title: 'Admin',
      description: 'Manage tenants, users, audit history, and self-hosted operations.',
    },
  },
]

function getPageMeta(pathname: string): PageMeta {
  return pageRegistry.find((entry) => entry.match(pathname))?.meta ?? {
    section: 'Workspace',
    title: 'Knowz',
    description: 'Operate your self-hosted knowledge workspace with a cleaner shell.',
  }
}

export default function Layout({ children }: LayoutProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const { user, isAuthenticated, logout } = useAuth()
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)
  const location = useLocation()
  const pageMeta = getPageMeta(location.pathname)

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        setUserMenuOpen(false)
      }
    }
    if (userMenuOpen) {
      document.addEventListener('mousedown', handleClickOutside)
    }
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [userMenuOpen])

  return (
    <div className="flex min-h-screen overflow-hidden bg-background text-foreground">
      <Sidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <div className="relative flex min-w-0 flex-1 flex-col overflow-hidden">
        <div className="pointer-events-none absolute inset-x-0 top-0 h-72 sh-page-glow" />
        <header className="relative z-10 border-b border-border/60 bg-background/82 backdrop-blur-xl">
          <div className="mx-auto flex h-20 max-w-7xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-8">
            <div className="flex min-w-0 flex-1 items-center gap-3">
              <button
                onClick={() => setSidebarOpen(true)}
                className="rounded-2xl border border-border/70 bg-card/80 p-2 text-muted-foreground shadow-sm transition-colors hover:bg-muted lg:hidden"
                aria-label="Open menu"
              >
                <Menu size={20} />
              </button>
              <Link
                to="/knowledge"
                className="inline-flex items-center gap-2 rounded-2xl border border-border/70 bg-card/70 px-3 py-2 text-sm font-semibold shadow-sm transition-colors hover:bg-card lg:hidden"
              >
                <span className="flex h-7 w-7 items-center justify-center rounded-xl bg-primary text-primary-foreground">
                  K
                </span>
                Knowz
              </Link>
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <p className="sh-kicker">{pageMeta.section}</p>
                  {user?.tenantName && (
                    <span className="inline-flex items-center gap-1 rounded-full border border-border/70 bg-card/70 px-2.5 py-1 text-[10px] font-medium text-muted-foreground">
                      <Building2 size={11} />
                      {user.tenantName}
                    </span>
                  )}
                </div>
                <div className="mt-1 min-w-0">
                  <h1 className="truncate text-xl font-semibold tracking-tight sm:text-2xl">
                    {pageMeta.title}
                  </h1>
                  <p className="hidden max-w-2xl truncate text-sm text-muted-foreground sm:block">
                    {pageMeta.description}
                  </p>
                </div>
              </div>
            </div>

            {isAuthenticated && user && (
              <div className="relative shrink-0" ref={menuRef}>
                <button
                  onClick={() => setUserMenuOpen(!userMenuOpen)}
                  className="flex items-center gap-2 rounded-2xl border border-border/70 bg-card/80 px-3 py-2 shadow-sm transition-colors hover:bg-card"
                >
                  <div className="flex h-9 w-9 items-center justify-center rounded-2xl bg-primary/10">
                    <User size={15} className="text-primary" />
                  </div>
                  <div className="hidden text-left sm:block">
                    <p className="max-w-40 truncate text-sm font-medium">
                      {user.displayName || user.username}
                    </p>
                    <p className="text-[11px] text-muted-foreground">
                      {roleLabels[user.role] ?? 'User'}
                    </p>
                  </div>
                  <ChevronDown size={14} className="text-muted-foreground" />
                </button>

                {userMenuOpen && (
                  <div className="absolute right-0 top-full mt-2 w-64 rounded-3xl border border-border/70 bg-card/95 py-1 shadow-elevated backdrop-blur-xl animate-scale-in">
                    <div className="border-b border-border/60 px-4 py-3">
                      <p className="truncate text-sm font-medium">
                        {user.displayName || user.username}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {roleLabels[user.role] ?? 'User'}
                      </p>
                      {user.tenantName && (
                        <p className="truncate text-xs text-muted-foreground">
                          {user.tenantName}
                        </p>
                      )}
                    </div>
                    <button
                      onClick={() => {
                        setUserMenuOpen(false)
                        logout()
                      }}
                      className="flex w-full items-center gap-2 px-4 py-2.5 text-sm transition-colors hover:bg-muted"
                    >
                      <LogOut size={14} />
                      Sign out
                    </button>
                  </div>
                )}
              </div>
            )}
          </div>
        </header>
        <main className="relative z-10 flex-1 overflow-y-auto">
          <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
            <div className="animate-fade-in">{children}</div>
          </div>
        </main>
      </div>
    </div>
  )
}
