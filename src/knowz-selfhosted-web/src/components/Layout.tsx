import { useEffect, useRef, type ReactNode } from 'react'
import { useLocation } from 'react-router-dom'
import Header from './Header'

interface LayoutProps {
  children: ReactNode
}

export default function Layout({ children }: LayoutProps) {
  const location = useLocation()
  const mainRef = useRef<HTMLElement>(null)

  useEffect(() => {
    mainRef.current?.focus()
  }, [location.pathname])

  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <a
        href="#main-content"
        data-testid="sh-skip-link"
        className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-[100] focus:rounded-md focus:bg-primary focus:px-4 focus:py-2 focus:text-primary-foreground focus:shadow-lg"
      >
        Skip to content
      </a>
      <Header />
      <main
        id="main-content"
        ref={mainRef}
        tabIndex={-1}
        role="main"
        className="flex-1 w-full focus:outline-none"
      >
        <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
          {children}
        </div>
      </main>
    </div>
  )
}
