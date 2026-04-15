import { describe, it, expect, vi } from 'vitest'
import { screen } from '@testing-library/react'
import { renderWithProviders } from './test-utils'
import Sidebar from '../components/Sidebar'

// Mock the auth hook to avoid needing the full auth provider
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
    logout: vi.fn(),
    activeTenantId: null,
    setActiveTenantId: vi.fn(),
  }),
}))

vi.mock('../lib/theme', () => ({
  useTheme: () => ({
    theme: 'dark',
    toggle: vi.fn(),
  }),
}))

describe('Sidebar', () => {
  it('Should_RenderFilesNavLink_WhenSidebarDisplayed', () => {
    renderWithProviders(<Sidebar open={true} onClose={() => {}} />)
    expect(screen.getByText('Files')).toBeInTheDocument()
  })

  it('Should_HaveCorrectFilesHref_WhenRendered', () => {
    renderWithProviders(<Sidebar open={true} onClose={() => {}} />)
    const filesLink = screen.getByText('Files').closest('a')
    expect(filesLink).toHaveAttribute('href', '/files')
  })

  it('Should_RenderPrimaryCreateAction_ForNewKnowledge', () => {
    renderWithProviders(<Sidebar open={true} onClose={() => {}} />)
    const createLink = screen.getByRole('link', { name: /new knowledge/i })
    expect(createLink).toHaveAttribute('href', '/knowledge/new')
  })
})
