import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { renderWithProviders } from './test-utils'
import CommentSection from '../components/CommentSection'
import type { Comment } from '../lib/types'

vi.mock('../lib/api-client', () => ({
  api: {
    listComments: vi.fn(),
    addComment: vi.fn(),
    updateComment: vi.fn(),
    deleteComment: vi.fn(),
    attachFileToComment: vi.fn(),
    getCommentAttachments: vi.fn(),
    detachFileFromComment: vi.fn(),
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

import { api } from '../lib/api-client'

const mockListComments = vi.mocked(api.listComments)
const mockAddComment = vi.mocked(api.addComment)
const mockUpdateComment = vi.mocked(api.updateComment)
const mockDeleteComment = vi.mocked(api.deleteComment)

const sampleComment: Comment = {
  id: 'comment-1',
  knowledgeId: 'knowledge-1',
  authorName: 'Alice',
  body: 'This is a contribution',
  isAnswer: false,
  createdAt: '2026-02-16T10:00:00Z',
  updatedAt: '2026-02-16T10:00:00Z',
  attachmentCount: 0,
  replies: [],
}

const commentWithReply: Comment = {
  ...sampleComment,
  replies: [
    {
      id: 'reply-1',
      knowledgeId: 'knowledge-1',
      parentCommentId: 'comment-1',
      authorName: 'Bob',
      body: 'This is a reply',
      isAnswer: false,
      createdAt: '2026-02-16T11:00:00Z',
      updatedAt: '2026-02-16T11:00:00Z',
      attachmentCount: 0,
    },
  ],
}

const commentWithAttachment: Comment = {
  ...sampleComment,
  attachmentCount: 2,
}

describe('CommentSection', () => {
  beforeEach(() => {
    mockListComments.mockResolvedValue([])
    mockAddComment.mockResolvedValue(sampleComment)
    mockUpdateComment.mockResolvedValue({ ...sampleComment, body: 'Updated' })
    mockDeleteComment.mockResolvedValue({ id: 'comment-1', deleted: true })
  })

  afterEach(() => {
    vi.clearAllMocks()
  })

  // --- Rendering ---

  it('Should_RenderContributionsHeader_WhenMounted', async () => {
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByText('Contributions')).toBeInTheDocument()
    })
  })

  it('Should_ShowEmptyMessage_WhenNoComments', async () => {
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByText(/no contributions yet/i)).toBeInTheDocument()
    })
  })

  it('Should_ShowAddContributionTextarea_WhenMounted', async () => {
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByPlaceholderText(/add a contribution/i)).toBeInTheDocument()
    })
  })

  it('Should_ShowAddContributionButton_WhenMounted', async () => {
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByRole('button', { name: /add contribution/i })).toBeInTheDocument()
    })
  })

  // --- Displaying Comments ---

  it('Should_DisplayCommentAuthorAndBody_WhenCommentsExist', async () => {
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument()
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
    })
  })

  it('Should_DisplayAttachmentBadge_WhenCommentHasAttachments', async () => {
    mockListComments.mockResolvedValue([commentWithAttachment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByText('2')).toBeInTheDocument()
    })
  })

  it('Should_DisplayRepliesIndented_WhenCommentHasReplies', async () => {
    mockListComments.mockResolvedValue([commentWithReply])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument()
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
      expect(screen.getByText('Bob')).toBeInTheDocument()
      expect(screen.getByText('This is a reply')).toBeInTheDocument()
    })
  })

  // --- Adding Comments ---

  it('Should_CallAddComment_WhenFormSubmitted', async () => {
    const user = userEvent.setup()
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/add a contribution/i)).toBeInTheDocument()
    })

    const textarea = screen.getByPlaceholderText(/add a contribution/i)
    await user.type(textarea, 'New contribution text')
    await user.click(screen.getByRole('button', { name: /add contribution/i }))

    await waitFor(() => {
      expect(mockAddComment).toHaveBeenCalledWith('knowledge-1', {
        body: 'New contribution text',
      })
    })
  })

  it('Should_DisableSubmit_WhenTextareaEmpty', async () => {
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      const btn = screen.getByRole('button', { name: /add contribution/i })
      expect(btn).toBeDisabled()
    })
  })

  // --- Reply ---

  it('Should_ShowReplyForm_WhenReplyClicked', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /reply/i }))

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/write a reply/i)).toBeInTheDocument()
    })
  })

  it('Should_SubmitReply_WithParentCommentId', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('Alice')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /reply/i }))
    const replyTextarea = screen.getByPlaceholderText(/write a reply/i)
    await user.type(replyTextarea, 'My reply')

    // Find the submit button within the reply form area
    const submitButtons = screen.getAllByRole('button', { name: /submit/i })
    await user.click(submitButtons[submitButtons.length - 1])

    await waitFor(() => {
      expect(mockAddComment).toHaveBeenCalledWith('knowledge-1', {
        body: 'My reply',
        parentCommentId: 'comment-1',
      })
    })
  })

  // --- Edit ---

  it('Should_ShowEditForm_WhenEditClicked', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /edit/i }))

    await waitFor(() => {
      expect(screen.getByDisplayValue('This is a contribution')).toBeInTheDocument()
    })
  })

  it('Should_CallUpdateComment_WhenEditSaved', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /edit/i }))
    const editTextarea = screen.getByDisplayValue('This is a contribution')
    await user.clear(editTextarea)
    await user.type(editTextarea, 'Edited text')
    await user.click(screen.getByRole('button', { name: /save/i }))

    await waitFor(() => {
      expect(mockUpdateComment).toHaveBeenCalledWith('comment-1', { body: 'Edited text' })
    })
  })

  // --- Delete ---

  it('Should_ShowDeleteConfirmation_WhenDeleteClicked', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /delete/i }))

    await waitFor(() => {
      expect(screen.getByText(/are you sure/i)).toBeInTheDocument()
    })
  })

  it('Should_CallDeleteComment_WhenDeleteConfirmed', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /delete/i }))

    await waitFor(() => {
      expect(screen.getByText(/are you sure/i)).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /confirm/i }))

    await waitFor(() => {
      expect(mockDeleteComment).toHaveBeenCalledWith('comment-1')
    })
  })

  it('Should_DismissDeleteConfirmation_WhenCancelClicked', async () => {
    const user = userEvent.setup()
    mockListComments.mockResolvedValue([sampleComment])
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)

    await waitFor(() => {
      expect(screen.getByText('This is a contribution')).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /delete/i }))

    await waitFor(() => {
      expect(screen.getByText(/are you sure/i)).toBeInTheDocument()
    })

    await user.click(screen.getByRole('button', { name: /cancel/i }))

    await waitFor(() => {
      expect(screen.queryByText(/are you sure/i)).not.toBeInTheDocument()
    })
  })

  // --- API calls ---

  it('Should_CallListComments_WithKnowledgeId', async () => {
    renderWithProviders(<CommentSection knowledgeId="knowledge-1" />)
    await waitFor(() => {
      expect(mockListComments).toHaveBeenCalledWith('knowledge-1')
    })
  })
})
