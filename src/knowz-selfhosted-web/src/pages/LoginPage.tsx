import { useState, useEffect, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader2, AlertCircle, BookOpen } from 'lucide-react'
import { useAuth } from '../lib/auth'
import { api, ApiError } from '../lib/api-client'
import { UserRole } from '../lib/types'
import type { SSOProviderInfo, TenantMembershipDto } from '../lib/types'

type LoginStep = 'credentials' | 'tenantSelection'

const roleLabels: Record<number, string> = {
  [UserRole.SuperAdmin]: 'SuperAdmin',
  [UserRole.Admin]: 'Admin',
  [UserRole.User]: 'User',
}

const roleStyles: Record<number, string> = {
  [UserRole.SuperAdmin]: 'bg-purple-50 dark:bg-purple-950/30 text-purple-700 dark:text-purple-400',
  [UserRole.Admin]: 'bg-blue-50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-400',
  [UserRole.User]: 'bg-muted text-muted-foreground',
}

export default function LoginPage() {
  const { login, selectTenant, isAuthenticated, isLoading: authLoading } = useAuth()
  const location = useLocation()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [ssoProviders, setSSOProviders] = useState<SSOProviderInfo[]>([])
  const [ssoLoading, setSSOLoading] = useState(false)
  const [step, setStep] = useState<LoginStep>('credentials')
  const [tenantOptions, setTenantOptions] = useState<TenantMembershipDto[]>([])

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
      const response = await login(username.trim(), password)
      if (response.requiresTenantSelection) {
        setTenantOptions(response.availableTenants)
        setStep('tenantSelection')
      }
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

  const handleTenantSelect = async (tenantId: string) => {
    setIsSubmitting(true)
    setError('')
    try {
      await selectTenant(tenantId)
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message)
      } else {
        setError('Failed to select tenant.')
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
    <div className="flex items-center justify-center min-h-screen bg-gradient-to-br from-blue-50/80 via-background to-indigo-50/50 dark:from-background dark:via-background dark:to-primary/5 px-4">
      <div className="w-full max-w-sm animate-fade-in">
        {/* Logo / Branding */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-primary rounded-2xl mb-5 shadow-lg shadow-primary/20">
            <BookOpen className="text-primary-foreground" size={32} />
          </div>
          <h1 className="text-3xl font-bold tracking-tight">Knowz</h1>
          <p className="text-sm text-muted-foreground mt-1.5">
            Sign in to your account
          </p>
        </div>

        {/* Login Card */}
        <div className="bg-card border border-border/40 rounded-2xl shadow-elevated p-6 backdrop-blur-sm">
          {step === 'tenantSelection' ? (
            <div className="space-y-3">
              <h2 className="text-lg font-semibold text-center">Select Tenant</h2>
              <p className="text-sm text-muted-foreground text-center">
                Your account has access to multiple tenants. Choose one to continue.
              </p>

              {error && (
                <div className="flex items-start gap-2 p-3 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg">
                  <AlertCircle size={16} className="text-red-600 dark:text-red-400 mt-0.5 shrink-0" />
                  <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
                </div>
              )}

              {tenantOptions.map((tenant) => (
                <button
                  key={tenant.tenantId}
                  onClick={() => handleTenantSelect(tenant.tenantId)}
                  disabled={isSubmitting}
                  className="w-full flex items-center justify-between p-3 border border-input rounded-lg hover:bg-muted transition-colors text-left disabled:opacity-50"
                >
                  <div>
                    <p className="font-medium text-sm">{tenant.tenantName}</p>
                    <p className="text-xs text-muted-foreground">{tenant.tenantSlug}</p>
                  </div>
                  <span className={`text-xs px-2 py-0.5 rounded ${roleStyles[tenant.role] ?? 'bg-muted text-muted-foreground'}`}>
                    {roleLabels[tenant.role] ?? 'User'}
                  </span>
                </button>
              ))}

              <button
                onClick={() => { setStep('credentials'); setError('') }}
                className="w-full text-sm text-muted-foreground hover:text-foreground transition-colors py-2"
              >
                Back to login
              </button>
            </div>
          ) : (
            <>
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
                    className="w-full px-3 py-2.5 border border-input rounded-xl bg-background text-sm placeholder-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring/50 focus:border-primary/30 disabled:opacity-50 transition-all duration-200"
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
                    className="w-full px-3 py-2.5 border border-input rounded-xl bg-background text-sm placeholder-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring/50 focus:border-primary/30 disabled:opacity-50 transition-all duration-200"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isSubmitting}
                  className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-primary text-primary-foreground rounded-xl text-sm font-semibold hover:brightness-110 active:scale-[0.98] transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed shadow-sm shadow-primary/20"
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
                        className="w-full flex items-center justify-center gap-2 px-4 py-2.5 border border-input rounded-xl text-sm font-medium hover:bg-muted/80 hover:border-border transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
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
            </>
          )}
        </div>

        <p className="text-center text-xs text-muted-foreground/60 mt-8">
          Powered by Knowz &mdash; Self-hosted knowledge platform
        </p>
      </div>
    </div>
  )
}
