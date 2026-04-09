import { useState } from 'react'
import { ArrowLeftRight, Loader2 } from 'lucide-react'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../lib/auth'
import { UserRole } from '../lib/types'

const roleLabels: Record<number, string> = {
  [UserRole.SuperAdmin]: 'SuperAdmin',
  [UserRole.Admin]: 'Admin',
  [UserRole.User]: 'User',
}

const roleBadgeStyles: Record<number, string> = {
  [UserRole.SuperAdmin]: 'bg-purple-100 dark:bg-purple-950/40 text-purple-700 dark:text-purple-400',
  [UserRole.Admin]: 'bg-blue-100 dark:bg-blue-950/40 text-blue-700 dark:text-blue-400',
  [UserRole.User]: 'bg-muted text-muted-foreground',
}

export default function TenantSwitcher() {
  const { user, availableTenants, switchTenant } = useAuth()
  const queryClient = useQueryClient()
  const [switching, setSwitching] = useState(false)
  const [open, setOpen] = useState(false)

  // Only show if user has 2+ tenants
  if (!user || availableTenants.length < 2) return null

  const currentTenant = availableTenants.find(t => t.tenantId === user.tenantId)
  const otherTenants = availableTenants.filter(t => t.tenantId !== user.tenantId)

  const handleSwitch = async (tenantId: string) => {
    setSwitching(true)
    try {
      await switchTenant(tenantId)
      queryClient.clear() // Clear all cached data - it's all tenant-scoped
      setOpen(false)
    } catch (err) {
      console.error('Failed to switch tenant:', err)
    } finally {
      setSwitching(false)
    }
  }

  return (
    <div className="px-3 py-2 border-b">
      <div className="flex items-center gap-1.5 mb-1">
        <ArrowLeftRight size={12} className="text-indigo-500" />
        <span className="text-[10px] font-semibold uppercase tracking-wider text-indigo-600 dark:text-indigo-400">
          Tenant
        </span>
      </div>

      <button
        onClick={() => setOpen(!open)}
        disabled={switching}
        className="w-full flex items-center justify-between px-2 py-1.5 text-xs border border-input rounded-md bg-card hover:bg-muted transition-colors text-left"
      >
        <div className="min-w-0 flex-1">
          <p className="font-medium truncate">{currentTenant?.tenantName ?? user.tenantName ?? 'Unknown'}</p>
          <span className={`inline-flex px-1 py-0 rounded text-[9px] font-medium ${roleBadgeStyles[currentTenant?.role ?? user.role] ?? roleBadgeStyles[UserRole.User]}`}>
            {roleLabels[currentTenant?.role ?? user.role] ?? 'User'}
          </span>
        </div>
        {switching && <Loader2 size={12} className="animate-spin ml-1" />}
      </button>

      {open && !switching && (
        <div className="mt-1 border border-input rounded-md bg-card shadow-sm overflow-hidden">
          {otherTenants.map((tenant) => (
            <button
              key={tenant.tenantId}
              onClick={() => handleSwitch(tenant.tenantId)}
              className="w-full flex items-center justify-between px-2 py-1.5 text-xs hover:bg-muted transition-colors text-left border-b last:border-b-0"
            >
              <div className="min-w-0 flex-1">
                <p className="font-medium truncate">{tenant.tenantName}</p>
                <span className={`inline-flex px-1 py-0 rounded text-[9px] font-medium ${roleBadgeStyles[tenant.role] ?? roleBadgeStyles[UserRole.User]}`}>
                  {roleLabels[tenant.role] ?? 'User'}
                </span>
              </div>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}
