import { useState, useRef, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { useConversations } from '../hooks/useConversations'
import type { ChatMessage, ChatRequestData } from '../lib/types'
import {
  Plus,
  Send,
  Trash2,
  MessagesSquare,
  ChevronLeft,
  ChevronRight,
  FlaskConical,
  Square,
} from 'lucide-react'

export default function ChatPage() {
  const {
    conversations,
    activeConversation,
    createConversation,
    selectConversation,
    addMessage,
    deleteConversation,
  } = useConversations()

  const [input, setInput] = useState('')
  const [selectedVaultId, setSelectedVaultId] = useState<string>('')
  const [researchMode, setResearchMode] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(true)
  const [isStreaming, setIsStreaming] = useState(false)
  const [streamingMessage, setStreamingMessage] = useState<ChatMessage | null>(null)
  const [streamError, setStreamError] = useState<string | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const abortControllerRef = useRef<AbortController | null>(null)
  const lastEscapeRef = useRef<number>(0)

  const { data: vaultsData } = useQuery({
    queryKey: ['vaults'],
    queryFn: () => api.listVaults(false),
    staleTime: 60_000,
  })

  // Scroll to bottom when messages change or streaming updates
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [activeConversation?.messages.length, streamingMessage?.content])

  // Sync vault selector when switching conversations
  useEffect(() => {
    if (activeConversation?.vaultId) {
      setSelectedVaultId(activeConversation.vaultId)
    }
  }, [activeConversation?.id, activeConversation?.vaultId])

  // Double-Escape handler to abort streaming
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        const now = Date.now()
        if (now - lastEscapeRef.current < 300) {
          abortControllerRef.current?.abort()
          lastEscapeRef.current = 0
        } else {
          lastEscapeRef.current = now
        }
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [])

  const handleSend = async () => {
    const trimmed = input.trim()
    if (!trimmed || isStreaming) return

    let conv = activeConversation
    if (!conv) {
      conv = createConversation(selectedVaultId || undefined)
    }

    const userMsg: ChatMessage = {
      id: crypto.randomUUID(),
      role: 'user',
      content: trimmed,
      timestamp: new Date().toISOString(),
    }
    addMessage(conv.id, userMsg)
    setInput('')

    // Build conversation history from existing messages (before the just-sent message)
    const history = conv.messages.map((m) => ({
      role: m.role,
      content: m.content,
    }))

    const requestData: ChatRequestData = {
      question: trimmed,
      conversationHistory: history.length > 0 ? history : undefined,
      vaultId: selectedVaultId || undefined,
      researchMode,
    }

    const controller = new AbortController()
    abortControllerRef.current = controller
    setIsStreaming(true)
    setStreamError(null)

    let content = ''
    let sources: { knowledgeId: string }[] = []
    let confidence = 0

    setStreamingMessage({
      id: crypto.randomUUID(),
      role: 'assistant',
      content: '',
      timestamp: new Date().toISOString(),
    })

    try {
      await api.chatStream(
        requestData,
        (event) => {
          switch (event.type) {
            case 'token':
              content += event.content
              setStreamingMessage((prev) =>
                prev ? { ...prev, content } : prev,
              )
              break
            case 'sources':
              sources = event.sources
              confidence = event.confidence
              break
            case 'done':
              break
            case 'error':
              setStreamError(event.message)
              break
          }
        },
        controller.signal,
      )
    } catch (err) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        // User cancelled - keep partial content
      } else if (err instanceof Error) {
        setStreamError(err.message)
      }
    } finally {
      // Add the final message to conversation
      if (content) {
        const assistantMsg: ChatMessage = {
          id: crypto.randomUUID(),
          role: 'assistant',
          content,
          sources: sources.length > 0 ? sources : undefined,
          confidence: confidence > 0 ? confidence : undefined,
          timestamp: new Date().toISOString(),
        }
        addMessage(conv!.id, assistantMsg)
      }
      setStreamingMessage(null)
      setIsStreaming(false)
      abortControllerRef.current = null
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  const handleNewChat = () => {
    createConversation(selectedVaultId || undefined)
    setInput('')
    setStreamError(null)
  }

  const formatTime = (timestamp: string) => {
    const d = new Date(timestamp)
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  }

  const formatDate = (timestamp: string) => {
    const d = new Date(timestamp)
    const now = new Date()
    if (d.toDateString() === now.toDateString()) return 'Today'
    const yesterday = new Date(now)
    yesterday.setDate(yesterday.getDate() - 1)
    if (d.toDateString() === yesterday.toDateString()) return 'Yesterday'
    return d.toLocaleDateString()
  }

  const renderAssistantBubble = (msg: ChatMessage) => (
    <div className="flex justify-start" key={msg.id}>
      <div className="max-w-[80%] rounded-xl px-4 py-3 bg-card border border-border/60 shadow-sm">
        <div className="whitespace-pre-wrap text-sm">{msg.content}</div>

        {msg.role === 'assistant' && (
          <div className="mt-2 flex items-center gap-2 flex-wrap">
            {msg.confidence != null && (
              <span className="text-[10px] px-1.5 py-0.5 bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-300 rounded">
                {Math.round(msg.confidence * 100)}% confidence
              </span>
            )}
            {msg.sources && msg.sources.length > 0 && (
              <div className="flex items-center gap-1 flex-wrap">
                <span className="text-[10px] text-muted-foreground">
                  Sources:
                </span>
                {msg.sources.map((s, idx) => {
                  const id = s.knowledgeId ?? (s as any).KnowledgeId ?? ''
                  return (
                    <Link
                      key={id || idx}
                      to={`/knowledge/${id}`}
                      className="text-[10px] text-primary hover:underline"
                    >
                      {id.slice(0, 8)}
                    </Link>
                  )
                })}
              </div>
            )}
          </div>
        )}

        <p className="text-[10px] mt-1 opacity-50">
          {formatTime(msg.timestamp)}
        </p>
      </div>
    </div>
  )

  return (
    <div className="flex h-[calc(100vh-3.5rem)] -m-4">
      {/* Conversation Sidebar */}
      <div
        className={`${
          sidebarOpen ? 'w-64' : 'w-0'
        } transition-all duration-200 overflow-hidden border-r bg-card flex flex-col`}
      >
        <div className="p-3 border-b">
          <button
            onClick={handleNewChat}
            className="w-full flex items-center justify-center gap-2 px-3 py-2 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={16} />
            New Chat
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-2 space-y-1">
          {conversations.length === 0 && (
            <p className="text-xs text-muted-foreground text-center py-4">
              No conversations yet
            </p>
          )}
          {conversations.map((conv) => (
            <div
              key={conv.id}
              className={`group flex items-center gap-2 px-3 py-2 rounded-lg cursor-pointer text-sm transition-colors duration-150 ${
                activeConversation?.id === conv.id
                  ? 'bg-primary/10 text-primary'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground'
              }`}
              onClick={() => selectConversation(conv.id)}
            >
              <MessagesSquare size={14} className="flex-shrink-0" />
              <div className="flex-1 min-w-0">
                <p className="truncate">{conv.title}</p>
                <p className="text-[10px] text-muted-foreground">
                  {formatDate(conv.updatedAt)}
                </p>
              </div>
              <button
                onClick={(e) => {
                  e.stopPropagation()
                  deleteConversation(conv.id)
                }}
                className="opacity-0 group-hover:opacity-100 p-1 rounded hover:bg-muted transition-opacity"
                title="Delete conversation"
              >
                <Trash2 size={12} />
              </button>
            </div>
          ))}
        </div>
      </div>

      {/* Toggle sidebar button */}
      <button
        onClick={() => setSidebarOpen(!sidebarOpen)}
        className="absolute left-0 top-1/2 -translate-y-1/2 z-10 p-1 bg-muted rounded-r-md hover:bg-accent transition-colors"
        style={{ left: sidebarOpen ? '16rem' : '0' }}
      >
        {sidebarOpen ? <ChevronLeft size={14} /> : <ChevronRight size={14} />}
      </button>

      {/* Main Chat Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top bar: vault selector + research mode */}
        <div className="flex items-center gap-3 px-4 py-2 border-b bg-card">
          <select
            value={selectedVaultId}
            onChange={(e) => setSelectedVaultId(e.target.value)}
            className="px-2 py-1.5 text-sm border border-input rounded-lg bg-card focus:outline-none focus:ring-1 focus:ring-ring transition-colors"
          >
            <option value="">All Vaults</option>
            {vaultsData?.vaults.map((v) => (
              <option key={v.id} value={v.id}>
                {v.name}
              </option>
            ))}
          </select>

          <label className="flex items-center gap-1.5 text-sm cursor-pointer">
            <button
              onClick={() => setResearchMode(!researchMode)}
              className={`flex items-center gap-1 px-2 py-1 rounded-lg text-xs font-medium transition-colors duration-150 ${
                researchMode
                  ? 'bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-300'
                  : 'bg-muted text-muted-foreground hover:bg-accent'
              }`}
            >
              <FlaskConical size={12} />
              Research
            </button>
          </label>
        </div>

        {/* Messages area */}
        <div className="flex-1 overflow-y-auto px-4 py-4 space-y-4">
          {!activeConversation || activeConversation.messages.length === 0 ? (
            <div className="flex flex-col items-center justify-center h-full text-center">
              <MessagesSquare
                size={48}
                className="text-muted-foreground/30 mb-4"
              />
              <h2 className="text-lg font-medium text-muted-foreground mb-2">
                Start a new conversation
              </h2>
              <p className="text-sm text-muted-foreground max-w-md">
                Ask questions about your knowledge base. Conversation history
                provides context for follow-up questions.
              </p>
            </div>
          ) : (
            activeConversation.messages.map((msg) => (
              <div
                key={msg.id}
                className={`flex ${
                  msg.role === 'user' ? 'justify-end' : 'justify-start'
                }`}
              >
                <div
                  className={`max-w-[80%] rounded-xl px-4 py-3 ${
                    msg.role === 'user'
                      ? 'bg-primary text-primary-foreground'
                      : 'bg-card border border-border/60 shadow-sm'
                  }`}
                >
                  <div className="whitespace-pre-wrap text-sm">
                    {msg.content}
                  </div>

                  {msg.role === 'assistant' && (
                    <div className="mt-2 flex items-center gap-2 flex-wrap">
                      {msg.confidence != null && (
                        <span className="text-[10px] px-1.5 py-0.5 bg-green-50 dark:bg-green-900/30 text-green-700 dark:text-green-300 rounded">
                          {Math.round(msg.confidence * 100)}% confidence
                        </span>
                      )}
                      {msg.sources && msg.sources.length > 0 && (
                        <div className="flex items-center gap-1 flex-wrap">
                          <span className="text-[10px] text-muted-foreground">
                            Sources:
                          </span>
                          {msg.sources.map((s, idx) => {
                            const id = s.knowledgeId ?? (s as any).KnowledgeId ?? ''
                            return (
                              <Link
                                key={id || idx}
                                to={`/knowledge/${id}`}
                                className="text-[10px] text-primary hover:underline"
                              >
                                {id.slice(0, 8)}
                              </Link>
                            )
                          })}
                        </div>
                      )}
                    </div>
                  )}

                  <p className="text-[10px] mt-1 opacity-50">
                    {formatTime(msg.timestamp)}
                  </p>
                </div>
              </div>
            ))
          )}

          {streamingMessage && renderAssistantBubble(streamingMessage)}

          {streamError && (
            <div className="flex justify-start">
              <div className="bg-red-50 dark:bg-red-900/20 text-red-600 dark:text-red-400 rounded-xl px-4 py-3 text-sm">
                {streamError}
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input area */}
        <div className="border-t bg-card px-4 py-3 shadow-[0_-2px_10px_rgba(0,0,0,0.04)]">
          <div className="flex items-end gap-2 max-w-3xl mx-auto">
            <textarea
              ref={textareaRef}
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Ask a question... (Shift+Enter for newline)"
              rows={1}
              className="flex-1 px-3 py-2 border border-input rounded-lg bg-card text-sm resize-none focus:outline-none focus:ring-1 focus:ring-ring max-h-32 transition-colors"
              style={{
                height: 'auto',
                minHeight: '2.5rem',
              }}
              onInput={(e) => {
                const target = e.target as HTMLTextAreaElement
                target.style.height = 'auto'
                target.style.height = Math.min(target.scrollHeight, 128) + 'px'
              }}
            />
            <button
              onClick={isStreaming ? () => abortControllerRef.current?.abort() : handleSend}
              disabled={!isStreaming && !input.trim()}
              className={`inline-flex items-center justify-center px-3 py-2 rounded-lg text-sm font-medium transition-all ${
                isStreaming
                  ? 'bg-red-600 text-white hover:bg-red-700'
                  : 'bg-primary text-primary-foreground disabled:opacity-50 hover:opacity-90'
              }`}
              title={isStreaming ? 'Stop generating' : 'Send message'}
            >
              {isStreaming ? <Square size={16} /> : <Send size={16} />}
            </button>
          </div>
          {isStreaming && (
            <p className="text-[10px] text-muted-foreground text-center mt-1">
              Press Esc Esc to stop
            </p>
          )}
        </div>
      </div>
    </div>
  )
}
