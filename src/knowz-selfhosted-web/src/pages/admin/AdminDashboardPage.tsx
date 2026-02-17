import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  Building2,
  Users,
  Shield,
  Activity,
  ArrowRight,
  RefreshCw,
  Loader2,
} from 'lucide-react'
import { api } from '../../lib/api-client'
import { UserRole } from '../../lib/types'

export default function AdminDashboardPage() {
  const tenantsQuery = useQuery({
    queryKey: ['admin', 'tenants'],
    queryFn: () => api.listTenants(),
  })

  const usersQuery = useQuery({
    queryKey: ['admin', 'users'],
    queryFn: () => api.listUsers(),
  })

  const isLoading = tenantsQuery.isLoading || usersQuery.isLoading
  const error = tenantsQuery.error || usersQuery.error

  if (error) {
    return (
      <div className="text-center py-12">
        <p className="text-red-600 dark:text-red-400 mb-4">
          {error instanceof Error ? error.message : 'Failed to load admin dashboard'}
        </p>
        <button
          onClick={() => {
            tenantsQuery.refetch()
            usersQuery.refetch()
          }}
          className="inline-flex items-center gap-2 px-4 py-2 bg-gray-900 dark:bg-white text-white dark:text-gray-900 rounded-md text-sm font-medium"
        >
          <RefreshCw size={16} /> Retry
        </button>
      </div>
    )
  }

  if (isLoading) {
    return (
      <div className="space-y-6">
        <h1 className="text-2xl font-bold">Administration</h1>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {[1, 2, 3].map((i) => (
            <div key={i} className="h-32 bg-gray-100 dark:bg-gray-800 rounded-lg animate-pulse" />
          ))}
        </div>
      </div>
    )
  }

  const tenants = tenantsQuery.data ?? []
  const users = usersQuery.data ?? []
  const activeTenants = tenants.filter((t) => t.isActive).length
  const activeUsers = users.filter((u) => u.isActive).length
  const superAdminCount = users.filter((u) => u.role === UserRole.SuperAdmin).length
  const adminCount = users.filter((u) => u.role === UserRole.Admin).length

  const recentUsers = [...users]
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
    .slice(0, 5)

  const recentTenants = [...tenants]
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
    .slice(0, 5)

  return (
    <div className="space-y-8">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Administration</h1>
        <button
          onClick={() => {
            tenantsQuery.refetch()
            usersQuery.refetch()
          }}
          disabled={tenantsQuery.isFetching || usersQuery.isFetching}
          className="inline-flex items-center gap-2 px-3 py-1.5 border border-gray-300 dark:border-gray-700 rounded-md text-sm font-medium hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors disabled:opacity-50"
        >
          {tenantsQuery.isFetching || usersQuery.isFetching ? (
            <Loader2 size={14} className="animate-spin" />
          ) : (
            <RefreshCw size={14} />
          )}
          Refresh
        </button>
      </div>

      {/* Stats Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-blue-50 dark:bg-blue-950/30 rounded-lg">
              <Building2 size={18} className="text-blue-600 dark:text-blue-400" />
            </div>
            <span className="text-sm font-medium text-gray-500 dark:text-gray-400">Tenants</span>
          </div>
          <p className="text-3xl font-bold">{tenants.length}</p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {activeTenants} active
          </p>
        </div>

        <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-green-50 dark:bg-green-950/30 rounded-lg">
              <Users size={18} className="text-green-600 dark:text-green-400" />
            </div>
            <span className="text-sm font-medium text-gray-500 dark:text-gray-400">Users</span>
          </div>
          <p className="text-3xl font-bold">{users.length}</p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {activeUsers} active
          </p>
        </div>

        <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-purple-50 dark:bg-purple-950/30 rounded-lg">
              <Shield size={18} className="text-purple-600 dark:text-purple-400" />
            </div>
            <span className="text-sm font-medium text-gray-500 dark:text-gray-400">Admins</span>
          </div>
          <p className="text-3xl font-bold">{superAdminCount + adminCount}</p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">
            {superAdminCount} super, {adminCount} admin
          </p>
        </div>

        <div className="p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-amber-50 dark:bg-amber-950/30 rounded-lg">
              <Activity size={18} className="text-amber-600 dark:text-amber-400" />
            </div>
            <span className="text-sm font-medium text-gray-500 dark:text-gray-400">System</span>
          </div>
          <p className="text-lg font-bold text-green-600 dark:text-green-400">Healthy</p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-1">All services running</p>
        </div>
      </div>

      {/* Quick Links */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <Link
          to="/admin/tenants"
          className="group flex items-center justify-between p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg hover:border-gray-300 dark:hover:border-gray-700 transition-colors"
        >
          <div className="flex items-center gap-3">
            <Building2 size={20} className="text-gray-400" />
            <div>
              <p className="font-medium">Tenant Management</p>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Create and manage tenants
              </p>
            </div>
          </div>
          <ArrowRight size={18} className="text-gray-400 group-hover:text-gray-600 dark:group-hover:text-gray-300 transition-colors" />
        </Link>

        <Link
          to="/admin/users"
          className="group flex items-center justify-between p-5 bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg hover:border-gray-300 dark:hover:border-gray-700 transition-colors"
        >
          <div className="flex items-center gap-3">
            <Users size={20} className="text-gray-400" />
            <div>
              <p className="font-medium">User Management</p>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                Create users and manage access
              </p>
            </div>
          </div>
          <ArrowRight size={18} className="text-gray-400 group-hover:text-gray-600 dark:group-hover:text-gray-300 transition-colors" />
        </Link>
      </div>

      {/* Recent Activity */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Recent Tenants */}
        <div className="bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 dark:border-gray-800">
            <h2 className="font-semibold">Recent Tenants</h2>
            <Link
              to="/admin/tenants"
              className="text-sm text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white transition-colors"
            >
              View all
            </Link>
          </div>
          {recentTenants.length === 0 ? (
            <div className="p-5 text-center text-sm text-gray-500 dark:text-gray-400">
              No tenants yet. Create your first tenant to get started.
            </div>
          ) : (
            <div className="divide-y divide-gray-100 dark:divide-gray-800">
              {recentTenants.map((tenant) => (
                <div key={tenant.id} className="flex items-center justify-between px-5 py-3">
                  <div>
                    <p className="text-sm font-medium">{tenant.name}</p>
                    <p className="text-xs text-gray-500 dark:text-gray-400">{tenant.slug}</p>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-gray-500 dark:text-gray-400">
                      {tenant.userCount} users
                    </span>
                    <span
                      className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${
                        tenant.isActive
                          ? 'bg-green-50 dark:bg-green-950/30 text-green-700 dark:text-green-400'
                          : 'bg-red-50 dark:bg-red-950/30 text-red-700 dark:text-red-400'
                      }`}
                    >
                      {tenant.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Recent Users */}
        <div className="bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded-lg">
          <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 dark:border-gray-800">
            <h2 className="font-semibold">Recent Users</h2>
            <Link
              to="/admin/users"
              className="text-sm text-gray-500 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white transition-colors"
            >
              View all
            </Link>
          </div>
          {recentUsers.length === 0 ? (
            <div className="p-5 text-center text-sm text-gray-500 dark:text-gray-400">
              No users yet. Create your first user to get started.
            </div>
          ) : (
            <div className="divide-y divide-gray-100 dark:divide-gray-800">
              {recentUsers.map((user) => (
                <div key={user.id} className="flex items-center justify-between px-5 py-3">
                  <div>
                    <p className="text-sm font-medium">{user.displayName || user.username}</p>
                    <p className="text-xs text-gray-500 dark:text-gray-400">{user.email || user.username}</p>
                  </div>
                  <RoleBadge role={user.role} />
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function RoleBadge({ role }: { role: number }) {
  const config = {
    [UserRole.SuperAdmin]: {
      label: 'SuperAdmin',
      classes: 'bg-purple-50 dark:bg-purple-950/30 text-purple-700 dark:text-purple-400',
    },
    [UserRole.Admin]: {
      label: 'Admin',
      classes: 'bg-blue-50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-400',
    },
    [UserRole.User]: {
      label: 'User',
      classes: 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-400',
    },
  }
  const c = config[role as UserRole] ?? config[UserRole.User]
  return (
    <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${c.classes}`}>
      {c.label}
    </span>
  )
}
