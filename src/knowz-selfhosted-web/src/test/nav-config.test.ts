import { describe, it, expect } from 'vitest'
import {
  primaryNav,
  filterNavItems,
  isActivePath,
  SUPER_ADMIN_ONLY_ADMIN_PATHS,
  type NavItem,
} from '../components/nav-config'
import { UserRole, type UserDto } from '../lib/types'

function makeUser(role: UserRole): UserDto {
  return {
    id: 'u',
    username: 'u',
    email: null,
    displayName: 'U',
    role,
    tenantId: 't',
    tenantName: 'T',
    isActive: true,
    apiKey: null,
    createdAt: '2026-01-01T00:00:00Z',
    lastLoginAt: null,
  }
}

describe('nav-config — structural', () => {
  it('Should_ContainEightFlatPrimaryItems', () => {
    const paths = primaryNav.map((i) => i.path)
    expect(paths).toEqual([
      '/knowledge',
      '/vaults',
      '/files',
      '/inbox',
      '/search',
      '/organize',
      '/chat',
      '/admin',
    ])
    expect(primaryNav.length).toBe(8)
  })

  it('Should_MarkAdmin_AsAdminOnly', () => {
    const admin = primaryNav.find((i) => i.path === '/admin') as NavItem
    expect(admin.adminOnly).toBe(true)
  })

  it('Should_FreezePrimaryNav_AtModuleLevel', () => {
    expect(Object.isFrozen(primaryNav)).toBe(true)
  })

  it('Should_ExposeVaultsFilesInbox_AsTopLevelFlatEntries', () => {
    const vaults = primaryNav.find((i) => i.path === '/vaults') as NavItem
    const files = primaryNav.find((i) => i.path === '/files') as NavItem
    const inbox = primaryNav.find((i) => i.path === '/inbox') as NavItem
    expect(vaults).toBeDefined()
    expect(vaults.label).toBe('Vaults')
    expect(files).toBeDefined()
    expect(files.label).toBe('Files')
    expect(inbox).toBeDefined()
    expect(inbox.label).toBe('Inbox')
    // Flat — no nested submenu shape on any item.
    for (const item of primaryNav) {
      expect('submenu' in item).toBe(false)
    }
  })

  it('Should_NotHaveSettings_InPrimary', () => {
    expect(primaryNav.some((i) => i.path === '/settings')).toBe(false)
  })
})

describe('nav-config — filterNavItems', () => {
  it('Should_ReturnEmptyArray_WhenUserIsNull', () => {
    expect(filterNavItems(primaryNav, null)).toEqual([])
  })

  it('Should_ReturnOnlyNonAdminItems_WhenUserRoleIsUser', () => {
    const visible = filterNavItems(primaryNav, makeUser(UserRole.User))
    expect(visible.some((i) => i.path === '/admin')).toBe(false)
    for (const item of visible) {
      expect(item.adminOnly).not.toBe(true)
      expect(item.superAdminOnly).not.toBe(true)
    }
  })

  it('Should_IncludeAdmin_WhenUserRoleIsAdmin', () => {
    const visible = filterNavItems(primaryNav, makeUser(UserRole.Admin))
    expect(visible.some((i) => i.path === '/admin')).toBe(true)
  })

  it('Should_ReturnIdenticalPaths_WhenAdminVsSuperAdmin', () => {
    const a = filterNavItems(primaryNav, makeUser(UserRole.Admin)).map((i) => i.path)
    const s = filterNavItems(primaryNav, makeUser(UserRole.SuperAdmin)).map((i) => i.path)
    expect(a).toEqual(s)
  })

  it('Should_PreserveOrder_OfOriginalArray', () => {
    const visible = filterNavItems(primaryNav, makeUser(UserRole.SuperAdmin))
    const expected = [
      '/knowledge',
      '/vaults',
      '/files',
      '/inbox',
      '/search',
      '/organize',
      '/chat',
      '/admin',
    ]
    expect(visible.map((i) => i.path)).toEqual(expected)
  })

  it('Should_Throw_WhenItemHasBothAdminOnlyAndSuperAdminOnly', () => {
    const bad: NavItem = {
      path: '/bad',
      label: 'Bad',
      icon: primaryNav[0].icon,
      adminOnly: true,
      superAdminOnly: true,
    }
    expect(() => filterNavItems([bad], makeUser(UserRole.SuperAdmin))).toThrow()
  })
})

describe('nav-config — isActivePath', () => {
  it('Should_MatchExactRoot_WhenEndTrue', () => {
    const root: NavItem = { path: '/', label: 'Dashboard', icon: primaryNav[0].icon, end: true }
    expect(isActivePath(root, '/')).toBe(true)
    expect(isActivePath(root, '/knowledge')).toBe(false)
  })

  it('Should_MatchPrefixWithSeparator_ByDefault', () => {
    const search = primaryNav.find((i) => i.path === '/search') as NavItem
    expect(isActivePath(search, '/search')).toBe(true)
    expect(isActivePath(search, '/search/foo')).toBe(true)
    expect(isActivePath(search, '/search-other')).toBe(false)
  })

  it('Should_NotCrossMatchKnowledgeAndVaults', () => {
    const knowledge = primaryNav.find((i) => i.path === '/knowledge') as NavItem
    const vaults = primaryNav.find((i) => i.path === '/vaults') as NavItem
    expect(isActivePath(knowledge, '/vaults')).toBe(false)
    expect(isActivePath(vaults, '/knowledge')).toBe(false)
    expect(isActivePath(knowledge, '/knowledge')).toBe(true)
    expect(isActivePath(knowledge, '/knowledge/new')).toBe(true)
    expect(isActivePath(vaults, '/vaults')).toBe(true)
  })
})

describe('nav-config — SUPER_ADMIN_ONLY_ADMIN_PATHS', () => {
  it('Should_ContainExactlyThreePaths', () => {
    expect(SUPER_ADMIN_ONLY_ADMIN_PATHS.size).toBe(3)
    expect(Array.from(SUPER_ADMIN_ONLY_ADMIN_PATHS).sort()).toEqual(
      ['/admin/settings', '/admin/sso', '/admin/tenants'].sort(),
    )
  })
})
