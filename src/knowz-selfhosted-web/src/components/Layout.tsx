import { useState, useRef, useEffect, type ReactNode } from 'react'
import { Menu, LogOut, ChevronDown, User } from 'lucide-react'
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

export default function Layout({ children }: LayoutProps) {
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const { user, isAuthenticated, logout } = useAuth()
  const [userMenuOpen, setUserMenuOpen] = useState(false)
  const menuRef = useRef<HTMLDivElement>(null)

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
    <div className="flex h-screen overflow-hidden bg-background">
      <Sidebar open={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <div className="flex-1 flex flex-col overflow-hidden">
        <header className="h-14 flex items-center justify-between px-4 border-b border-border/50 bg-card/80 backdrop-blur-md lg:hidden">
          <div className="flex items-center">
            <button
              onClick={() => setSidebarOpen(true)}
              className="p-1 rounded hover:bg-muted transition-colors"
              aria-label="Open menu"
            >
              <Menu size={22} />
            </button>
            <span className="ml-3 text-lg font-bold tracking-tight">Knowz</span>
          </div>

          {/* Mobile user menu */}
          {isAuthenticated && user && (
            <div className="relative" ref={menuRef}>
              <button
                onClick={() => setUserMenuOpen(!userMenuOpen)}
                className="flex items-center gap-1.5 px-2 py-1 rounded hover:bg-muted transition-colors"
              >
                <div className="w-7 h-7 rounded-full bg-primary/10 flex items-center justify-center">
                  <User size={14} className="text-primary" />
                </div>
                <ChevronDown size={14} className="text-muted-foreground" />
              </button>

              {userMenuOpen && (
                <div className="absolute right-0 top-full mt-1.5 w-56 bg-card border border-border/40 rounded-xl shadow-elevated py-1 z-50 animate-scale-in">
                  <div className="px-3 py-2 border-b">
                    <p className="text-sm font-medium truncate">
                      {user.displayName || user.username}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {roleLabels[user.role] ?? 'User'}
                    </p>
                    {user.tenantName && (
                      <p className="text-xs text-muted-foreground truncate">
                        {user.tenantName}
                      </p>
                    )}
                  </div>
                  <button
                    onClick={() => {
                      setUserMenuOpen(false)
                      logout()
                    }}
                    className="flex items-center gap-2 w-full px-3 py-2 text-sm hover:bg-muted transition-colors"
                  >
                    <LogOut size={14} />
                    Sign out
                  </button>
                </div>
              )}
            </div>
          )}
        </header>
        <main className="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
          <div className="max-w-6xl mx-auto animate-fade-in">{children}</div>
        </main>
      </div>
    </div>
  )
}
