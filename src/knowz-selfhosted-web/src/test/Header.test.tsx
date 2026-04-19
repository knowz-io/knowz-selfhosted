import { describe, it, expect, vi, beforeEach } from 'vitest'
import { fireEvent, screen } from '@testing-library/react'
import { renderWithProviders } from './test-utils'
import { UserRole } from '../lib/types'

let currentRole: UserRole = UserRole.SuperAdmin
let isAuthed = true

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: isAuthed
      ? {
          id: 'u',
          username: 'testuser',
          email: null,
          displayName: 'Test User',
          role: currentRole,
          tenantId: 't',
          tenantName: 'Test Tenant',
          isActive: true,
          apiKey: null,
          createdAt: '2026-01-01T00:00:00Z',
          lastLoginAt: null,
        }
      : null,
    isAuthenticated: isAuthed,
    logout: vi.fn(),
    activeTenantId: null,
    setActiveTenantId: vi.fn(),
    availableTenants: [],
  }),
}))

vi.mock('../lib/theme', () => ({
  useTheme: () => ({
    theme: 'dark',
    toggle: vi.fn(),
  }),
}))

vi.mock('../lib/api-client', () => ({
  api: {
    listTenants: vi.fn().mockResolvedValue([]),
  },
}))

import Header from '../components/Header'

describe('Header — shell + landmarks', () => {
  beforeEach(() => {
    currentRole = UserRole.SuperAdmin
    isAuthed = true
  })

  it('Should_RenderHeaderWithBannerRole', () => {
    renderWithProviders(<Header />)
    const header = screen.getByTestId('sh-header')
    expect(header.tagName.toLowerCase()).toBe('header')
    expect(header).toHaveAttribute('role', 'banner')
  })

  it('Should_RenderPrimaryNav_WithAriaLabel', () => {
    renderWithProviders(<Header />)
    const nav = screen.getByTestId('sh-nav-primary')
    expect(nav.tagName.toLowerCase()).toBe('nav')
    expect(nav).toHaveAttribute('aria-label', 'Primary')
  })

  it('Should_RenderPageMetaTitle_ForCurrentRoute', () => {
    renderWithProviders(<Header />, { initialEntries: ['/knowledge'] })
    expect(screen.getByTestId('sh-pagemeta-title').textContent).toBe('Knowledge')
  })
})

describe('Header — flat primary nav', () => {
  beforeEach(() => {
    currentRole = UserRole.SuperAdmin
    isAuthed = true
  })

  it('Should_RenderEightPrimaryItems_ForAdmin', () => {
    currentRole = UserRole.Admin
    renderWithProviders(<Header />)
    expect(screen.queryByTestId('nav-link-root')).not.toBeInTheDocument()
    expect(screen.getByTestId('nav-link-knowledge')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-vaults')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-files')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-inbox')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-search')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-organize')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-chat')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-admin')).toBeInTheDocument()
  })

  it('Should_RenderBrainIcon_InLogoBlock', () => {
    renderWithProviders(<Header />)
    expect(screen.getByTestId('sh-logo-brain-icon')).toBeInTheDocument()
  })

  it('Should_RenderWordmark_AsLowercaseKnowz', () => {
    renderWithProviders(<Header />)
    const logo = screen.getByTestId('sh-logo-link')
    expect(logo.textContent).toContain('knowz')
    expect(logo.textContent).not.toContain('Knowz')
  })

  it('Should_HideAdminNavItem_ForPlainUser', () => {
    currentRole = UserRole.User
    renderWithProviders(<Header />)
    expect(screen.getByTestId('nav-link-knowledge')).toBeInTheDocument()
    expect(screen.queryByTestId('nav-link-admin')).not.toBeInTheDocument()
  })

  it('Should_MarkActiveNavItem_WithAriaCurrentPage', () => {
    renderWithProviders(<Header />, { initialEntries: ['/search'] })
    expect(screen.getByTestId('nav-link-search')).toHaveAttribute('aria-current', 'page')
    expect(screen.getByTestId('nav-link-chat')).not.toHaveAttribute('aria-current')
  })

  it('Should_RenderKnowledgeAsPlainNavLink_WithHrefToKnowledge', () => {
    renderWithProviders(<Header />)
    const knowledge = screen.getByTestId('nav-link-knowledge')
    expect(knowledge.tagName.toLowerCase()).toBe('a')
    expect(knowledge).toHaveAttribute('href', '/knowledge')
  })

  it('Should_NavigateToRoot_WhenLogoClicked', () => {
    renderWithProviders(<Header />)
    const logo = screen.getByTestId('sh-logo-link')
    expect(logo.tagName.toLowerCase()).toBe('a')
    expect(logo).toHaveAttribute('href', '/')
  })
})

describe('Header — mobile drawer', () => {
  beforeEach(() => {
    currentRole = UserRole.SuperAdmin
    isAuthed = true
  })

  it('Should_OpenMobileDrawer_WhenHamburgerClicked', () => {
    renderWithProviders(<Header />)
    expect(screen.queryByTestId('sh-mobile-drawer')).not.toBeInTheDocument()
    const hamburger = screen.getByTestId('sh-mobile-hamburger')
    fireEvent.click(hamburger)
    expect(screen.getByTestId('sh-mobile-drawer')).toBeInTheDocument()
    expect(hamburger).toHaveAttribute('aria-expanded', 'true')
  })

  it('Should_RenderFlatNavItems_InMobileDrawer', () => {
    renderWithProviders(<Header />)
    fireEvent.click(screen.getByTestId('sh-mobile-hamburger'))
    expect(screen.getByTestId('nav-link-knowledge-mobile')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-vaults-mobile')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-files-mobile')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-inbox-mobile')).toBeInTheDocument()
    expect(screen.getByTestId('nav-link-search-mobile')).toBeInTheDocument()
  })
})
