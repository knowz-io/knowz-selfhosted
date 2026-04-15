import { useState, useEffect, type FormEvent } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { Loader2, AlertCircle, BookOpen, Database, ShieldCheck, Sparkles } from 'lucide-react'
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
      <div className="flex min-h-screen items-center justify-center bg-background">
        <div className="h-10 w-10 rounded-full border-4 border-muted border-t-primary animate-spin" />
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
    <div className="relative min-h-screen overflow-hidden bg-background px-4 py-10 sm:px-6">
      <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_left,rgba(59,130,246,0.18),transparent_26%),radial-gradient(circle_at_bottom_right,rgba(14,165,233,0.12),transparent_28%)]" />
      <div className="relative mx-auto grid min-h-[calc(100vh-5rem)] max-w-6xl gap-6 lg:grid-cols-[1.05fr_0.95fr]">
        <section className="hidden rounded-[36px] border border-border/60 bg-sidebar p-8 text-sidebar-foreground shadow-elevated lg:flex lg:flex-col lg:justify-between">
          <div>
            <div className="inline-flex items-center gap-3 rounded-full border border-white/10 bg-white/6 px-4 py-2">
              <span className="flex h-10 w-10 items-center justify-center rounded-2xl bg-primary text-primary-foreground">
                <BookOpen size={20} />
              </span>
              <div>
                <p className="text-sm font-semibold">Knowz Self-Hosted</p>
                <p className="text-xs text-sidebar-foreground/65">Independent deployment, aligned experience.</p>
              </div>
            </div>

            <div className="mt-10 max-w-xl">
              <p className="text-[11px] font-semibold uppercase tracking-[0.24em] text-sidebar-accent">
                Workspace Access
              </p>
              <h1 className="mt-4 text-5xl font-semibold tracking-tight leading-[1.05]">
                The self-hosted client can feel first-class too.
              </h1>
              <p className="mt-5 text-base leading-7 text-sidebar-foreground/72">
                This shell keeps the deployment independence of self-hosted Knowz while bringing the calmer hierarchy,
                stronger surfaces, and more deliberate product framing of the main platform.
              </p>
            </div>
          </div>

          <div className="grid gap-4">
            <div className="rounded-[26px] border border-white/10 bg-white/6 p-5">
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/10 text-sidebar-accent">
                  <ShieldCheck size={18} />
                </div>
                <div>
                  <p className="font-semibold">Tenant-aware access</p>
                  <p className="text-sm text-sidebar-foreground/65">Move between roles and organizations without losing orientation.</p>
                </div>
              </div>
            </div>
            <div className="rounded-[26px] border border-white/10 bg-white/6 p-5">
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/10 text-sidebar-accent">
                  <Database size={18} />
                </div>
                <div>
                  <p className="font-semibold">Knowledge-first entry</p>
                  <p className="text-sm text-sidebar-foreground/65">Search, chat, and vault workflows start from the same visual rhythm.</p>
                </div>
              </div>
            </div>
            <div className="rounded-[26px] border border-white/10 bg-white/6 p-5">
              <div className="flex items-center gap-3">
                <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/10 text-sidebar-accent">
                  <Sparkles size={18} />
                </div>
                <div>
                  <p className="font-semibold">Platform-aligned polish</p>
                  <p className="text-sm text-sidebar-foreground/65">Richer elevation, spacing, and framing without touching the hosted platform code.</p>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section className="flex items-center justify-center">
          <div className="w-full max-w-xl rounded-[32px] border border-border/60 bg-card/94 p-6 shadow-elevated backdrop-blur-xl sm:p-8">
            <div className="mb-8 text-center lg:hidden">
              <div className="mx-auto mb-5 flex h-16 w-16 items-center justify-center rounded-[24px] bg-primary text-primary-foreground shadow-lg shadow-primary/20">
                <BookOpen size={30} />
              </div>
              <h1 className="text-3xl font-semibold tracking-tight">Knowz</h1>
              <p className="mt-2 text-sm text-muted-foreground">Self-hosted workspace access</p>
            </div>

            <div className="mb-6 hidden lg:block">
              <p className="sh-kicker">{step === 'tenantSelection' ? 'Tenant Selection' : 'Sign In'}</p>
              <h2 className="mt-3 text-3xl font-semibold tracking-tight">
                {step === 'tenantSelection' ? 'Choose a tenant' : 'Access your workspace'}
              </h2>
              <p className="mt-2 text-sm leading-6 text-muted-foreground">
                {step === 'tenantSelection'
                  ? 'Your account can work across multiple tenants. Pick the one you want to enter.'
                  : 'Use credentials or a configured SSO provider to continue into the self-hosted workspace.'}
              </p>
            </div>

            {step === 'tenantSelection' ? (
              <div className="space-y-3">
                {error && (
                  <div className="flex items-start gap-2 rounded-2xl border border-red-200 bg-red-50 p-3 dark:border-red-800 dark:bg-red-950/30">
                    <AlertCircle size={16} className="mt-0.5 shrink-0 text-red-600 dark:text-red-400" />
                    <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
                  </div>
                )}

                {tenantOptions.map((tenant) => (
                  <button
                    key={tenant.tenantId}
                    onClick={() => handleTenantSelect(tenant.tenantId)}
                    disabled={isSubmitting}
                    className="flex w-full items-center justify-between rounded-[24px] border border-border/70 bg-background/70 p-4 text-left transition-all duration-200 hover:-translate-y-0.5 hover:bg-card disabled:opacity-50"
                  >
                    <div>
                      <p className="font-medium text-sm">{tenant.tenantName}</p>
                      <p className="text-xs text-muted-foreground">{tenant.tenantSlug}</p>
                    </div>
                    <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${roleStyles[tenant.role] ?? 'bg-muted text-muted-foreground'}`}>
                      {roleLabels[tenant.role] ?? 'User'}
                    </span>
                  </button>
                ))}

                <button
                  onClick={() => { setStep('credentials'); setError('') }}
                  className="w-full rounded-2xl border border-input px-4 py-2.5 text-sm font-medium text-muted-foreground transition-colors hover:bg-muted"
                >
                  Back to login
                </button>
              </div>
            ) : (
              <>
                <form onSubmit={handleSubmit} className="space-y-4">
                  {error && (
                    <div className="flex items-start gap-2 rounded-2xl border border-red-200 bg-red-50 p-3 dark:border-red-800 dark:bg-red-950/30">
                      <AlertCircle size={16} className="mt-0.5 shrink-0 text-red-600 dark:text-red-400" />
                      <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
                    </div>
                  )}

                  <div>
                    <label htmlFor="username" className="mb-1 block text-sm font-medium text-foreground">
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
                      className="w-full rounded-2xl border border-input bg-background/80 px-3 py-3 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring/30 disabled:opacity-50"
                    />
                  </div>

                  <div>
                    <label htmlFor="password" className="mb-1 block text-sm font-medium text-foreground">
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
                      className="w-full rounded-2xl border border-input bg-background/80 px-3 py-3 text-sm placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring/30 disabled:opacity-50"
                    />
                  </div>

                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="flex w-full items-center justify-center gap-2 rounded-2xl bg-primary px-4 py-3 text-sm font-semibold text-primary-foreground shadow-sm shadow-primary/20 transition-all duration-200 hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-50"
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

                {ssoProviders.length > 0 && (
                  <>
                    <div className="relative my-5">
                      <div className="absolute inset-0 flex items-center">
                        <div className="w-full border-t border-border/70" />
                      </div>
                      <div className="relative flex justify-center text-xs">
                        <span className="bg-card px-3 text-muted-foreground">or continue with SSO</span>
                      </div>
                    </div>

                    <div className="space-y-2">
                      {ssoProviders.map((provider) => (
                        <button
                          key={provider.provider}
                          type="button"
                          onClick={() => handleSSOLogin(provider.provider)}
                          disabled={ssoLoading}
                          className="flex w-full items-center justify-center gap-2 rounded-2xl border border-input bg-background/70 px-4 py-3 text-sm font-medium transition-colors hover:bg-muted/70 disabled:cursor-not-allowed disabled:opacity-50"
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

            <p className="mt-8 text-center text-xs text-muted-foreground/70">
              Powered by Knowz Self-Hosted
            </p>
          </div>
        </section>
      </div>
    </div>
  )
}
