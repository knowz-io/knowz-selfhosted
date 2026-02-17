import { useState, useRef, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { useMutation, useQuery } from '@tanstack/react-query'
import { api } from '../lib/api-client'
import { useConversations } from '../hooks/useConversations'
import type { ChatMessage, ChatRequestData } from '../lib/types'
import {
  Plus,
  Send,
  Loader2,
  Trash2,
  MessagesSquare,
  ChevronLeft,
  ChevronRight,
  FlaskConical,
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
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const { data: vaultsData } = useQuery({
    queryKey: ['vaults'],
    queryFn: () => api.listVaults(false),
    staleTime: 60_000,
  })

  const chatMutation = useMutation({
    mutationFn: (data: ChatRequestData) => api.chat(data),
    onSuccess: (response) => {
      if (!activeConversation) return
      const assistantMsg: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'assistant',
        content: response.answer,
        sources: response.sources,
        confidence: response.confidence,
        timestamp: new Date().toISOString(),
      }
      addMessage(activeConversation.id, assistantMsg)
    },
  })

  // Scroll to bottom when messages change
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [activeConversation?.messages.length])

  // Sync vault selector when switching conversations
  useEffect(() => {
    if (activeConversation?.vaultId) {
      setSelectedVaultId(activeConversation.vaultId)
    }
  }, [activeConversation?.id, activeConversation?.vaultId])

  const handleSend = () => {
    const trimmed = input.trim()
    if (!trimmed || chatMutation.isPending) return

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

    chatMutation.mutate(requestData)
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
    chatMutation.reset()
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

  return (
    <div className="flex h-[calc(100vh-3.5rem)] -m-4">
      {/* Conversation Sidebar */}
      <div
        className={`${
          sidebarOpen ? 'w-64' : 'w-0'
        } transition-all duration-200 overflow-hidden border-r border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 flex flex-col`}
      >
        <div className="p-3 border-b border-gray-200 dark:border-gray-800">
          <button
            onClick={handleNewChat}
            className="w-full flex items-center justify-center gap-2 px-3 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus size={16} />
            New Chat
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-2 space-y-1">
          {conversations.length === 0 && (
            <p className="text-xs text-gray-400 dark:text-gray-500 text-center py-4">
              No conversations yet
            </p>
          )}
          {conversations.map((conv) => (
            <div
              key={conv.id}
              className={`group flex items-center gap-2 px-3 py-2 rounded-md cursor-pointer text-sm transition-colors ${
                activeConversation?.id === conv.id
                  ? 'bg-gray-200 dark:bg-gray-800 text-gray-900 dark:text-white'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800'
              }`}
              onClick={() => selectConversation(conv.id)}
            >
              <MessagesSquare size={14} className="flex-shrink-0" />
              <div className="flex-1 min-w-0">
                <p className="truncate">{conv.title}</p>
                <p className="text-[10px] text-gray-400 dark:text-gray-500">
                  {formatDate(conv.updatedAt)}
                </p>
              </div>
              <button
                onClick={(e) => {
                  e.stopPropagation()
                  deleteConversation(conv.id)
                }}
                className="opacity-0 group-hover:opacity-100 p-1 rounded hover:bg-gray-300 dark:hover:bg-gray-700 transition-opacity"
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
        className="absolute left-0 top-1/2 -translate-y-1/2 z-10 p-1 bg-gray-200 dark:bg-gray-800 rounded-r-md hover:bg-gray-300 dark:hover:bg-gray-700"
        style={{ left: sidebarOpen ? '16rem' : '0' }}
      >
        {sidebarOpen ? <ChevronLeft size={14} /> : <ChevronRight size={14} />}
      </button>

      {/* Main Chat Area */}
      <div className="flex-1 flex flex-col min-w-0">
        {/* Top bar: vault selector + research mode */}
        <div className="flex items-center gap-3 px-4 py-2 border-b border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-950">
          <select
            value={selectedVaultId}
            onChange={(e) => setSelectedVaultId(e.target.value)}
            className="px-2 py-1.5 text-sm border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-gray-900 dark:text-white focus:outline-none focus:ring-1 focus:ring-blue-500"
          >
            <option value="">All Vaults</option>
            {vaultsData?.vaults.map((v) => (
              <option key={v.id} value={v.id}>
                {v.name}
              </option>
            ))}
          </select>

          <label className="flex items-center gap-1.5 text-sm text-gray-600 dark:text-gray-400 cursor-pointer">
            <button
              onClick={() => setResearchMode(!researchMode)}
              className={`flex items-center gap-1 px-2 py-1 rounded-md text-xs font-medium transition-colors ${
                researchMode
                  ? 'bg-purple-100 dark:bg-purple-900/30 text-purple-700 dark:text-purple-300'
                  : 'bg-gray-100 dark:bg-gray-800 text-gray-500 dark:text-gray-400 hover:bg-gray-200 dark:hover:bg-gray-700'
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
                className="text-gray-300 dark:text-gray-700 mb-4"
              />
              <h2 className="text-lg font-medium text-gray-500 dark:text-gray-400 mb-2">
                Start a new conversation
              </h2>
              <p className="text-sm text-gray-400 dark:text-gray-500 max-w-md">
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
                  className={`max-w-[80%] rounded-lg px-4 py-3 ${
                    msg.role === 'user'
                      ? 'bg-gray-900 dark:bg-gray-100 text-white dark:text-gray-900'
                      : 'bg-gray-100 dark:bg-gray-800 text-gray-900 dark:text-gray-100'
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
                          <span className="text-[10px] text-gray-400 dark:text-gray-500">
                            Sources:
                          </span>
                          {msg.sources.map((s) => (
                            <Link
                              key={s.knowledgeId}
                              to={`/knowledge/${s.knowledgeId}`}
                              className="text-[10px] text-blue-600 dark:text-blue-400 hover:underline"
                            >
                              {s.knowledgeId.slice(0, 8)}
                            </Link>
                          ))}
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

          {chatMutation.isPending && (
            <div className="flex justify-start">
              <div className="bg-gray-100 dark:bg-gray-800 rounded-lg px-4 py-3">
                <div className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
                  <Loader2 size={14} className="animate-spin" />
                  Thinking...
                </div>
              </div>
            </div>
          )}

          {chatMutation.error && (
            <div className="flex justify-start">
              <div className="bg-red-50 dark:bg-red-900/20 text-red-600 dark:text-red-400 rounded-lg px-4 py-3 text-sm">
                {chatMutation.error instanceof Error
                  ? chatMutation.error.message
                  : 'Failed to get response'}
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input area */}
        <div className="border-t border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-950 px-4 py-3">
          <div className="flex items-end gap-2 max-w-3xl mx-auto">
            <textarea
              ref={textareaRef}
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Ask a question... (Shift+Enter for newline)"
              rows={1}
              className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-700 rounded-md bg-white dark:bg-gray-900 text-sm resize-none focus:outline-none focus:ring-1 focus:ring-blue-500 max-h-32"
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
              onClick={handleSend}
              disabled={chatMutation.isPending || !input.trim()}
              className="inline-flex items-center justify-center px-3 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium disabled:opacity-50 hover:opacity-90 transition-opacity"
              title="Send message"
            >
              {chatMutation.isPending ? (
                <Loader2 size={16} className="animate-spin" />
              ) : (
                <Send size={16} />
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
