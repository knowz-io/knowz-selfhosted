import type { LucideIcon } from 'lucide-react'
import {
  BookOpen,
  Archive,
  FileText,
  Inbox,
  Search,
  Layers,
  MessagesSquare,
  Shield,
} from 'lucide-react'
import { UserRole, type UserDto } from '../lib/types'

export interface NavItem {
  path: string
  label: string
  icon: LucideIcon
  end?: boolean
  adminOnly?: boolean
  superAdminOnly?: boolean
  testId?: string
}

export const primaryNav: ReadonlyArray<NavItem> = Object.freeze<NavItem[]>([
  { path: '/knowledge', label: 'Knowledge', icon: BookOpen, testId: 'knowledge' },
  { path: '/vaults', label: 'Vaults', icon: Archive, testId: 'vaults' },
  { path: '/files', label: 'Files', icon: FileText, testId: 'files' },
  { path: '/inbox', label: 'Inbox', icon: Inbox, testId: 'inbox' },
  { path: '/search', label: 'Search', icon: Search, testId: 'search' },
  { path: '/organize', label: 'Organize', icon: Layers, testId: 'organize' },
  { path: '/chat', label: 'Chat', icon: MessagesSquare, testId: 'chat' },
  { path: '/admin', label: 'Admin', icon: Shield, adminOnly: true, testId: 'admin' },
])

export const SUPER_ADMIN_ONLY_ADMIN_PATHS: ReadonlySet<string> = new Set([
  '/admin/tenants',
  '/admin/sso',
  '/admin/settings',
])

function itemVisibleTo(
  user: UserDto,
  item: Pick<NavItem, 'adminOnly' | 'superAdminOnly'>,
): boolean {
  if (item.adminOnly && item.superAdminOnly) {
    throw new Error('NavItem cannot declare both adminOnly and superAdminOnly')
  }
  if (item.superAdminOnly) return user.role === UserRole.SuperAdmin
  if (item.adminOnly) return user.role >= UserRole.Admin
  return true
}

export function filterNavItems(
  items: ReadonlyArray<NavItem>,
  user: UserDto | null,
): NavItem[] {
  if (!user) return []
  const result: NavItem[] = []
  for (const item of items) {
    if (!itemVisibleTo(user, item)) continue
    result.push(item)
  }
  return result
}

export function isActivePath(item: NavItem, pathname: string): boolean {
  if (item.end) return pathname === item.path
  if (item.path === '/') return pathname === '/'
  return pathname === item.path || pathname.startsWith(item.path + '/')
}
