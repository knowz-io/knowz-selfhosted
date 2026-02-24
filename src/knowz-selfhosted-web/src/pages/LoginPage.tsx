import { useState, useEffect, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader2, AlertCircle, BookOpen } from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api, ApiError } from '../lib/api-client'
import type { SSOProviderInfo } from '../lib/types'

export default function LoginPage() {
  const { login, isAuthenticated, isLoading: authLoading } = useAuth()
  const location = useLocation()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [ssoProviders, setSSOProviders] = useState<SSOProviderInfo[]>([])
  const [ssoLoading, setSSOLoading] = useState(false)

  const from = (location.state as { from?: { pathname: string } })?.from?.pathname || '/'

  useEffect(() => {
    api.getSSOProviders()
      .then((res) => setSSOProviders(res.data ?? []))
      .catch(() => {
        // Silently ignore - SSO not available
      })
  }, [])

  if (authLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen bg-background">
        <div className="w-8 h-8 border-4 border-muted border-t-primary rounded-full animate-spin" />
      </div>
    )
  }

  if (isAuthenticated) {
    return <Navigate to={from} replace />
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError('')

    if (!username.trim() || !password.trim()) {
      setError('Please enter both username and password.')
      return
    }

    setIsSubmitting(true)
    try {
      await login(username.trim(), password)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('An unexpected error occurred. Please try again.')
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleSSOLogin = async (provider: string) => {
    setSSOLoading(true)
    setError('')
    try {
      const callbackUrl = `${window.location.origin}/auth/sso/callback`
      const result = await api.getSSOAuthorizeUrl(provider, callbackUrl)
      if (result.success && result.data.authorizationUrl) {
        window.location.href = result.data.authorizationUrl
      } else {
        setError('Failed to initiate SSO login.')
        setSSOLoading(false)
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Failed to initiate SSO login.')
      }
      setSSOLoading(false)
    }
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-background px-4">
      <div className="w-full max-w-sm animate-fade-in">
        {/* Logo / Branding */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 bg-primary rounded-xl mb-4">
            <BookOpen className="text-primary-foreground" size={28} />
          </div>
          <h1 className="text-2xl font-bold">Knowz</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Sign in to your account
          </p>
        </div>

        {/* Login Card */}
        <div className="bg-card border border-border/60 rounded-2xl shadow-lg p-6">
          <form onSubmit={handleSubmit} className="space-y-4">
            {error && (
              <div className="flex items-start gap-2 p-3 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg">
                <AlertCircle size={16} className="text-red-600 dark:text-red-400 mt-0.5 shrink-0" />
                <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
              </div>
            )}

            <div>
              <label htmlFor="username" className="block text-sm font-medium text-foreground mb-1">
                Username
              </label>
              <input
                id="username"
                type="text"
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="Enter your username"
                autoComplete="username"
                autoFocus
                disabled={isSubmitting}
                className="w-full px-3 py-2 border border-input rounded-lg bg-card text-sm placeholder-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent disabled:opacity-50 transition-colors"
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-foreground mb-1">
                Password
              </label>
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                autoComplete="current-password"
                disabled={isSubmitting}
                className="w-full px-3 py-2 border border-input rounded-lg bg-card text-sm placeholder-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:border-transparent disabled:opacity-50 transition-colors"
              />
            </div>

            <button
              type="submit"
              disabled={isSubmitting}
              className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-primary text-primary-foreground rounded-lg text-sm font-medium hover:opacity-90 transition-opacity disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isSubmitting ? (
                <>
                  <Loader2 size={16} className="animate-spin" />
                  Signing in...
                </>
              ) : (
                'Sign In'
              )}
            </button>
          </form>

          {/* SSO Providers */}
          {ssoProviders.length > 0 && (
            <>
              <div className="relative my-4">
                <div className="absolute inset-0 flex items-center">
                  <div className="w-full border-t" />
                </div>
                <div className="relative flex justify-center text-xs">
                  <span className="bg-card px-2 text-muted-foreground">or</span>
                </div>
              </div>

              <div className="space-y-2">
                {ssoProviders.map((provider) => (
                  <button
                    key={provider.provider}
                    type="button"
                    onClick={() => handleSSOLogin(provider.provider)}
                    disabled={ssoLoading}
                    className="w-full flex items-center justify-center gap-2 px-4 py-2.5 border border-input rounded-lg text-sm font-medium hover:bg-muted transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {ssoLoading ? (
                      <Loader2 size={16} className="animate-spin" />
                    ) : provider.provider === 'Microsoft' ? (
                      <svg width="16" height="16" viewBox="0 0 21 21" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <rect x="1" y="1" width="9" height="9" fill="#F25022"/>
                        <rect x="11" y="1" width="9" height="9" fill="#7FBA00"/>
                        <rect x="1" y="11" width="9" height="9" fill="#00A4EF"/>
                        <rect x="11" y="11" width="9" height="9" fill="#FFB900"/>
                      </svg>
                    ) : null}
                    {provider.displayName}
                  </button>
                ))}
              </div>
            </>
          )}
        </div>

        <p className="text-center text-xs text-muted-foreground mt-6">
          Self-hosted knowledge management platform
        </p>
      </div>
    </div>
  )
}
