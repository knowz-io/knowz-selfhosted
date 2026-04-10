import {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
  type ReactNode,
} from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { api, ApiError } from './api-client'
import { UserRole, type UserDto, type TenantMembershipDto, type MultiTenantLoginResponse } from './types'

interface AuthContextValue {
  user: UserDto | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (username: string, password: string) => Promise<MultiTenantLoginResponse>
  loginWithToken: (token: string) => Promise<void>
  logout: () => void
  /** Re-fetches the current user from the server (for preference updates, etc.). */
  refreshUser: () => Promise<void>
  /** Active tenant override (SuperAdmin only). null = use own tenant. */
  activeTenantId: string | null
  setActiveTenantId: (tenantId: string | null) => void
  // Multi-tenant fields
  availableTenants: TenantMembershipDto[]
  currentTenantName: string | null
  selectTenant: (tenantId: string) => Promise<void>
  switchTenant: (tenantId: string) => Promise<void>
  pendingUserId: string | null
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return ctx
}

interface AuthProviderProps {
  children: ReactNode
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<UserDto | null>(null)
  const [token, setToken] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [activeTenantId, setActiveTenantIdState] = useState<string | null>(
    () => localStorage.getItem('activeTenantId'),
  )
  const [availableTenants, setAvailableTenants] = useState<TenantMembershipDto[]>([])
  const [pendingUserId, setPendingUserId] = useState<string | null>(null)
  const [pendingSelectionToken, setPendingSelectionToken] = useState<string | null>(null)

  useEffect(() => {
    const storedToken = sessionStorage.getItem('authToken')
    if (storedToken) {
      setToken(storedToken)
      api
        .getMe()
        .then((userData) => {
          setUser(userData)
          // Fetch available tenants for authenticated user
          api.getUserTenants()
            .then(tenants => setAvailableTenants(tenants))
            .catch(() => {}) // Silently ignore if endpoint not available
        })
        .catch((err) => {
          if (err instanceof ApiError && err.status === 401) {
            sessionStorage.removeItem('authToken')
            setToken(null)
          }
        })
        .finally(() => {
          setIsLoading(false)
        })
    } else {
      setIsLoading(false)
    }
  }, [])

  const currentTenantName = user
    ? availableTenants.find(t => t.tenantId === user.tenantId)?.tenantName ?? user.tenantName
    : null

  const login = useCallback(async (username: string, password: string): Promise<MultiTenantLoginResponse> => {
    const response = await api.login(username, password)

    if (response.requiresTenantSelection) {
      // Multi-tenant: store tenants and selection token for next step, don't set token yet
      setAvailableTenants(response.availableTenants)
      setPendingUserId(response.userId)
      setPendingSelectionToken(response.selectionToken)
      return response
    }

    // Single tenant: proceed as before
    sessionStorage.setItem('authToken', response.token)
    setToken(response.token)
    setUser(response.user!)
    // Clear tenant override on fresh login
    localStorage.removeItem('activeTenantId')
    setActiveTenantIdState(null)
    // Fetch available tenants
    api.getUserTenants()
      .then(tenants => setAvailableTenants(tenants))
      .catch(() => {})
    return response
  }, [])

  const selectTenant = useCallback(async (tenantId: string) => {
    if (!pendingUserId || !pendingSelectionToken) throw new Error('No pending user for tenant selection')
    const response = await api.selectTenant({ userId: pendingUserId, tenantId, selectionToken: pendingSelectionToken })
    sessionStorage.setItem('authToken', response.token)
    setToken(response.token)
    setUser(response.user)
    setPendingUserId(null)
    setPendingSelectionToken(null)
    localStorage.removeItem('activeTenantId')
    setActiveTenantIdState(null)
    // Fetch available tenants
    api.getUserTenants()
      .then(tenants => setAvailableTenants(tenants))
      .catch(() => {})
  }, [pendingUserId, pendingSelectionToken])

  const switchTenant = useCallback(async (tenantId: string) => {
    const response = await api.switchTenant({ tenantId })
    sessionStorage.setItem('authToken', response.token)
    setToken(response.token)
    setUser(response.user)
    localStorage.removeItem('activeTenantId')
    setActiveTenantIdState(null)
  }, [])

  const loginWithToken = useCallback(async (jwtToken: string) => {
    sessionStorage.setItem('authToken', jwtToken)
    setToken(jwtToken)
    const userData = await api.getMe()
    setUser(userData)
    localStorage.removeItem('activeTenantId')
    setActiveTenantIdState(null)
    // Fetch available tenants
    api.getUserTenants()
      .then(tenants => setAvailableTenants(tenants))
      .catch(() => {})
  }, [])

  const logout = useCallback(() => {
    sessionStorage.removeItem('authToken')
    localStorage.removeItem('activeTenantId')
    setToken(null)
    setUser(null)
    setActiveTenantIdState(null)
    setAvailableTenants([])
    setPendingUserId(null)
  }, [])

  const setActiveTenantId = useCallback((tenantId: string | null) => {
    if (tenantId) {
      localStorage.setItem('activeTenantId', tenantId)
    } else {
      localStorage.removeItem('activeTenantId')
    }
    setActiveTenantIdState(tenantId)
  }, [])

  /**
   * Re-fetches the current user via /api/v1/auth/me so the context picks
   * up any server-side changes (e.g. timezone preference, display name).
   * Silently ignores failures — the existing user stays in context.
   */
  const refreshUser = useCallback(async () => {
    if (!token) return
    try {
      const userData = await api.getMe()
      setUser(userData)
    } catch {
      // Intentional: keep the stale user rather than blowing up the UI.
    }
  }, [token])

  const isAuthenticated = !!token && !!user

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isAuthenticated,
        isLoading,
        login,
        loginWithToken,
        logout,
        refreshUser,
        activeTenantId,
        setActiveTenantId,
        availableTenants,
        currentTenantName,
        selectTenant,
        switchTenant,
        pendingUserId,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

interface ProtectedRouteProps {
  children: ReactNode
}

export function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated, isLoading } = useAuth()
  const location = useLocation()

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="w-8 h-8 border-4 border-muted border-t-primary rounded-full animate-spin" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }

  return <>{children}</>
}

interface AdminRouteProps {
  children: ReactNode
}

export function AdminRoute({ children }: AdminRouteProps) {
  const { user, isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="w-8 h-8 border-4 border-muted border-t-primary rounded-full animate-spin" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (user?.role !== UserRole.SuperAdmin && user?.role !== UserRole.Admin) {
    return <Navigate to="/" replace />
  }

  return <>{children}</>
}

interface SuperAdminRouteProps {
  children: ReactNode
}

export function SuperAdminRoute({ children }: SuperAdminRouteProps) {
  const { user, isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-screen">
        <div className="w-8 h-8 border-4 border-muted border-t-primary rounded-full animate-spin" />
      </div>
    )
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (user?.role !== UserRole.SuperAdmin) {
    return <Navigate to="/admin" replace />
  }

  return <>{children}</>
}
