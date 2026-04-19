import { useEffect, useRef, useState } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeftRight,
  Brain,
  Building2,
  Menu,
  Plus,
  X,
} from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api } from '../lib/api-client'
import { UserRole } from '../lib/types'
import TenantSwitcher from './TenantSwitcher'
import UserMenu from './UserMenu'
import {
  filterNavItems,
  isActivePath,
  primaryNav,
  type NavItem,
} from './nav-config'
import { getPageMeta } from './page-meta-config'

function defaultSlug(path: string): string {
  if (path === '/') return 'root'
  return path.replace(/^\//, '').replace(/\//g, '-')
}

function testIdFor(item: NavItem): string {
  return `nav-link-${item.testId ?? defaultSlug(item.path)}`
}

function topNavClass(active: boolean): string {
  return [
    'group inline-flex min-w-[56px] flex-col items-center justify-center gap-0.5 h-auto py-1.5 px-2.5 rounded-lg transition-all',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
    active
      ? 'bg-primary/10 text-primary ring-1 ring-primary/20'
      : 'text-muted-foreground hover:bg-muted hover:text-foreground',
  ].join(' ')
}

function mobileNavClass(active: boolean): string {
  return [
    'flex items-center gap-3 rounded-2xl px-3 py-2.5 text-sm font-medium transition-colors',
    active
      ? 'bg-primary/10 text-primary ring-1 ring-primary/20'
      : 'text-muted-foreground hover:bg-muted hover:text-foreground',
  ].join(' ')
}

export default function Header() {
  const location = useLocation()
  const pathname = location.pathname
  const { user, isAuthenticated, activeTenantId, setActiveTenantId } = useAuth()
  const isSuperAdmin = user?.role === UserRole.SuperAdmin
  const queryClient = useQueryClient()
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const drawerRef = useRef<HTMLDivElement>(null)
  const hamburgerRef = useRef<HTMLButtonElement>(null)

  const visibleNavItems: NavItem[] = filterNavItems(primaryNav, user ?? null)
  const pageMeta = getPageMeta(pathname)

  const { data: tenants } = useQuery({
    queryKey: ['admin', 'tenants-header'],
    queryFn: () => api.listTenants(),
    enabled: isSuperAdmin,
    staleTime: 60_000,
  })

  const activeTenantName = activeTenantId
    ? tenants?.find((t) => t.id === activeTenantId)?.name ?? 'Loading...'
    : null

  // Close mobile drawer on route change.
  useEffect(() => {
    setMobileMenuOpen(false)
  }, [pathname])

  // Escape + outside-click close for mobile drawer.
  useEffect(() => {
    if (!mobileMenuOpen) return
    function handleKey(event: KeyboardEvent) {
      if (event.key === 'Escape') setMobileMenuOpen(false)
    }
    function handleClickOutside(event: MouseEvent) {
      const target = event.target as Node
      if (
        drawerRef.current &&
        !drawerRef.current.contains(target) &&
        hamburgerRef.current &&
        !hamburgerRef.current.contains(target)
      ) {
        setMobileMenuOpen(false)
      }
    }
    document.addEventListener('keydown', handleKey)
    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('keydown', handleKey)
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [mobileMenuOpen])

  const handleTenantChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    const value = event.target.value || null
    setActiveTenantId(value)
    queryClient.invalidateQueries()
  }

  return (
    <header
      data-testid="sh-header"
      role="banner"
      className="sticky top-0 z-40 overflow-x-hidden border-b border-border/60 bg-background/82 backdrop-blur-xl"
    >
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {/* Row 1: nav bar — two-column layout mirrors platform (logo+nav left, actions right) */}
        <div className="flex h-20 items-center justify-between gap-4">
          {/* Left column: logo group + primary nav */}
          <div className="flex min-w-0 items-center gap-3 lg:gap-8">
            {/* Logo group (hamburger visible <lg only; viewing pill when SuperAdmin cross-tenant) */}
            <div className="flex items-center gap-3">
              <button
                ref={hamburgerRef}
                type="button"
                data-testid="sh-mobile-hamburger"
                aria-label={mobileMenuOpen ? 'Close menu' : 'Open menu'}
                aria-expanded={mobileMenuOpen}
                aria-controls="sh-mobile-drawer"
                onClick={() => setMobileMenuOpen((prev) => !prev)}
                className="inline-flex items-center justify-center rounded-2xl border border-border/70 bg-card/80 p-2 text-muted-foreground shadow-sm transition-colors hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 lg:hidden"
              >
                {mobileMenuOpen ? <X size={20} /> : <Menu size={20} />}
              </button>
              <Link
                to="/"
                data-testid="sh-logo-link"
                className="group inline-flex items-center gap-2 shrink-0"
              >
                <div className="relative">
                  <div className="absolute inset-0 rounded-xl border-2 border-primary/50 animate-ping opacity-40" />
                  <div className="absolute inset-0 rounded-xl border border-primary/40 animate-pulse" />
                  <div className="relative p-1.5 sm:p-2 rounded-lg sm:rounded-xl bg-primary shadow-md">
                    <Brain className="h-4 w-4 sm:h-5 sm:w-5 text-primary-foreground" data-testid="sh-logo-brain-icon" />
                  </div>
                </div>
                <span
                  className="text-2xl sm:text-3xl font-normal tracking-wide text-foreground"
                  style={{
                    fontFamily: "'Pacifico', 'Comic Sans MS', cursive, sans-serif",
                    WebkitFontSmoothing: 'antialiased',
                    MozOsxFontSmoothing: 'grayscale',
                  }}
                >
                  knowz
                </span>
              </Link>
              {isSuperAdmin && activeTenantId && activeTenantName && (
                <span
                  data-testid="sh-superadmin-viewing-pill"
                  className="inline-flex items-center gap-1 rounded-full border border-purple-500/40 bg-purple-50 px-2.5 py-1 text-[10px] font-medium text-purple-700 dark:bg-purple-950/30 dark:text-purple-300"
                >
                  <Building2 size={11} />
                  Viewing: {activeTenantName}
                </span>
              )}
            </div>

            {/* Primary nav (hidden <lg — mobile drawer handles <lg) */}
            <nav
              aria-label="Primary"
              data-testid="sh-nav-primary"
              className="hidden min-w-0 lg:flex"
            >
              <ul className="flex flex-wrap items-center gap-1">
                {visibleNavItems.map((item) => {
                  const Icon = item.icon
                  const active = isActivePath(item, pathname)
                  const testId = testIdFor(item)
                  return (
                    <li key={item.path}>
                      <NavLink
                        to={item.path}
                        end={item.end}
                        data-testid={testId}
                        className={() => topNavClass(active)}
                        aria-current={active ? 'page' : undefined}
                      >
                        <Icon size={18} />
                        <span className={`text-[9px] uppercase tracking-wide ${active ? 'font-semibold' : 'font-medium'}`}>{item.label}</span>
                      </NavLink>
                    </li>
                  )
                })}
              </ul>
            </nav>
          </div>

          {/* Right cluster */}
          <div className="flex shrink-0 items-center gap-2">
            <Link
              to="/knowledge/new"
              className="inline-flex items-center gap-2 rounded-2xl bg-primary px-3 py-2 text-sm font-semibold text-primary-foreground shadow-sm transition-transform hover:-translate-y-0.5 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              aria-label="New Knowledge"
            >
              <Plus size={15} />
              <span className="hidden lg:inline">New Knowledge</span>
            </Link>

            <TenantSwitcher />

            {isSuperAdmin && tenants && tenants.length > 0 && (
              <div className="hidden items-center gap-1 lg:inline-flex">
                <ArrowLeftRight size={12} className="text-purple-500" />
                <select
                  data-testid="sh-superadmin-tenant-select"
                  aria-label="Cross-tenant viewing context"
                  value={activeTenantId ?? ''}
                  onChange={handleTenantChange}
                  className="rounded-xl border border-border/40 bg-card/80 px-2 py-1 text-xs text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
                >
                  <option value="">My Tenant ({user?.tenantName ?? 'Default'})</option>
                  {tenants.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name} {t.id === user?.tenantId ? '(yours)' : ''}
                    </option>
                  ))}
                </select>
              </div>
            )}

            {isAuthenticated && <UserMenu />}
          </div>
        </div>

        {/* Row 2: pageMeta (see SH_PageMetaPreservation) */}
        <div data-testid="sh-pagemeta" className="border-t border-border/40 py-3">
          <div className="flex flex-wrap items-center gap-2">
            <p data-testid="sh-pagemeta-section" className="sh-kicker">
              {pageMeta.section}
            </p>
            {user?.tenantName && (
              <span className="inline-flex items-center gap-1 rounded-full border border-border/70 bg-card/70 px-2.5 py-1 text-[10px] font-medium text-muted-foreground">
                <Building2 size={11} />
                {user.tenantName}
              </span>
            )}
          </div>
          <div className="mt-1 min-w-0">
            <h1
              data-testid="sh-pagemeta-title"
              className="truncate text-xl font-semibold tracking-tight sm:text-2xl"
            >
              {pageMeta.title}
            </h1>
            <p
              data-testid="sh-pagemeta-description"
              className="hidden max-w-2xl truncate text-sm text-muted-foreground sm:block"
            >
              {pageMeta.description}
            </p>
          </div>
        </div>
      </div>

      {/* Mobile drawer */}
      {mobileMenuOpen && (
        <nav
          ref={drawerRef}
          id="sh-mobile-drawer"
          data-testid="sh-mobile-drawer"
          aria-label="Mobile"
          aria-hidden={!mobileMenuOpen}
          className="border-t border-border/70 bg-background/95 py-3 backdrop-blur lg:hidden"
        >
          <div className="mx-auto max-w-7xl px-4 sm:px-6">
            <ul className="flex flex-col gap-1">
              {visibleNavItems.map((item) => {
                const Icon = item.icon
                const active = isActivePath(item, pathname)
                const testId = `${testIdFor(item)}-mobile`
                return (
                  <li key={item.path}>
                    <NavLink
                      to={item.path}
                      end={item.end}
                      data-testid={testId}
                      aria-current={active ? 'page' : undefined}
                      className={() => mobileNavClass(active)}
                    >
                      <Icon size={18} />
                      <span>{item.label}</span>
                    </NavLink>
                  </li>
                )
              })}
            </ul>
          </div>
        </nav>
      )}
    </header>
  )
}
