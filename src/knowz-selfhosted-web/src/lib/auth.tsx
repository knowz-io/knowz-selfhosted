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
import { UserRole, type UserDto } from './types'

interface AuthContextValue {
  user: UserDto | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (username: string, password: string) => Promise<void>
  loginWithToken: (token: string) => Promise<void>
  logout: () => void
  /** Active tenant override (SuperAdmin only). null = use own tenant. */
  activeTenantId: string | null
  setActiveTenantId: (tenantId: string | null) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

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

  useEffect(() => {
    const storedToken = localStorage.getItem('authToken')
    if (storedToken) {
      setToken(storedToken)
      api
        .getMe()
        .then((userData) => {
          setUser(userData)
        })
        .catch((err) => {
          if (err instanceof ApiError && err.status === 401) {
            localStorage.removeItem('authToken')
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

  const login = useCallback(async (username: string, password: string) => {
    const response = await api.login(username, password)
    localStorage.setItem('authToken', response.token)
    setToken(response.token)
    setUser(response.user)
    // Clear tenant override on fresh login
    localStorage.removeItem('activeTenantId')
    setActiveTenantIdState(null)
  }, [])

  const loginWithToken = useCallback(async (jwtToken: string) => {
    localStorage.setItem('authToken', jwtToken)
    setToken(jwtToken)
    const userData = await api.getMe()
    setUser(userData)
    localStorage.removeItem('activeTenantId')
    setActiveTenantIdState(null)
  }, [])

  const logout = useCallback(() => {
    localStorage.removeItem('authToken')
    localStorage.removeItem('activeTenantId')
    setToken(null)
    setUser(null)
    setActiveTenantIdState(null)
  }, [])

  const setActiveTenantId = useCallback((tenantId: string | null) => {
    if (tenantId) {
      localStorage.setItem('activeTenantId', tenantId)
    } else {
      localStorage.removeItem('activeTenantId')
    }
    setActiveTenantIdState(tenantId)
  }, [])

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
        activeTenantId,
        setActiveTenantId,
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
