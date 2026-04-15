import { describe, it, expect, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { renderWithProviders } from './test-utils'
import Layout from '../components/Layout'

vi.mock('../lib/auth', () => ({
  useAuth: () => ({
    user: {
      id: 'user-1',
      username: 'testuser',
      email: null,
      displayName: 'Test User',
      role: 2,
      tenantId: 'tenant-1',
      tenantName: 'Test Tenant',
      isActive: true,
      apiKey: null,
      createdAt: '2026-01-01T00:00:00Z',
      lastLoginAt: null,
    },
    token: 'test-token',
    isAuthenticated: true,
    isLoading: false,
    login: vi.fn(),
    loginWithToken: vi.fn(),
    logout: vi.fn(),
    refreshUser: vi.fn(),
    activeTenantId: null,
    setActiveTenantId: vi.fn(),
    availableTenants: [],
    currentTenantName: 'Test Tenant',
    selectTenant: vi.fn(),
    switchTenant: vi.fn(),
    pendingUserId: null,
  }),
}))

vi.mock('../components/Sidebar', () => ({
  default: () => <div data-testid="sidebar-stub" />,
}))

describe('Layout', () => {
  it('Should_RenderRouteAwarePageTitle_WhenNavigatingWithinShell', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>,
      { initialEntries: ['/search'] },
    )

    expect(screen.getByText('Discover')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: 'Search' }),
    ).toBeInTheDocument()
  })

  it('Should_RenderRouteDescription_ForKnowledgePages', () => {
    renderWithProviders(
      <Layout>
        <div>Content</div>
      </Layout>,
      { initialEntries: ['/knowledge'] },
    )

    expect(
      screen.getByText('Browse, refine, and manage your self-hosted knowledge base.'),
    ).toBeInTheDocument()
  })
})
