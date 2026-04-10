import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import { renderWithProviders } from './test-utils'
import KnowledgeDetailPage from '../pages/KnowledgeDetailPage'

// Mock react-router-dom useParams
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return {
    ...actual,
    useParams: () => ({ id: 'knowledge-1' }),
    useNavigate: () => vi.fn(),
  }
})

// Mock the api module
vi.mock('../lib/api-client', () => ({
  api: {
    getKnowledge: vi.fn(),
    updateKnowledge: vi.fn(),
    deleteKnowledge: vi.fn(),
    getKnowledgeAttachments: vi.fn(),
    listFiles: vi.fn(),
    attachFileToKnowledge: vi.fn(),
    detachFileFromKnowledge: vi.fn(),
    uploadFile: vi.fn(),
    downloadFile: vi.fn(),
  },
  ApiError: class ApiError extends Error {
    status: number
    constructor(status: number, message: string) {
      super(message)
      this.status = status
      this.name = 'ApiError'
    }
  },
}))

// Mock format-markdown
vi.mock('../lib/format-markdown', () => ({
  formatMarkdown: (text: string) => text,
}))

import { api } from '../lib/api-client'

const mockGetKnowledge = vi.mocked(api.getKnowledge)
const mockGetAttachments = vi.mocked(api.getKnowledgeAttachments)

describe('KnowledgeDetailPage - Attachments', () => {
  beforeEach(() => {
    mockGetKnowledge.mockResolvedValue({
      id: 'knowledge-1',
      title: 'Test Knowledge',
      content: 'Some content here',
      type: 'Note',
      tags: ['test'],
      vaults: [{ id: 'vault-1', name: 'Default', isPrimary: true }],
      createdAt: '2026-01-15T10:00:00Z',
      updatedAt: '2026-01-15T10:00:00Z',
      isIndexed: true,
      indexedAt: '2026-01-15T10:01:00Z',
    })

    mockGetAttachments.mockResolvedValue([
      {
        id: 'file-1',
        fileName: 'attached-doc.pdf',
        contentType: 'application/pdf',
        sizeBytes: 2048,
        blobMigrationPending: false,
        createdAt: '2026-01-15T10:00:00Z',
        updatedAt: '2026-01-15T10:00:00Z',
      },
    ])
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  it('Should_ShowAttachmentsSection_WhenKnowledgeLoaded', async () => {
    renderWithProviders(<KnowledgeDetailPage />)
    await waitFor(() => {
      expect(screen.getByText('Attachments')).toBeInTheDocument()
    })
  })

  it('Should_DisplayAttachedFiles_WhenAttachmentsExist', async () => {
    renderWithProviders(<KnowledgeDetailPage />)
    await waitFor(() => {
      expect(screen.getByText('attached-doc.pdf')).toBeInTheDocument()
    })
  })

  it('Should_ShowAttachButton_WhenKnowledgeLoaded', async () => {
    renderWithProviders(<KnowledgeDetailPage />)
    await waitFor(() => {
      expect(screen.getByText('Attach File')).toBeInTheDocument()
    })
  })

  it('Should_ShowEmptyAttachmentsMessage_WhenNoAttachments', async () => {
    mockGetAttachments.mockResolvedValue([])
    renderWithProviders(<KnowledgeDetailPage />)
    await waitFor(() => {
      expect(screen.getByText('Attachments')).toBeInTheDocument()
    })
    expect(screen.getByText(/no attachments/i)).toBeInTheDocument()
  })
})
