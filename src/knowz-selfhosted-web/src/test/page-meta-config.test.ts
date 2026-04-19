import { describe, it, expect } from 'vitest'
import {
  DEFAULT_PAGE_META,
  getPageMeta,
  pageRegistry,
} from '../components/page-meta-config'

describe('page-meta-config — module shape', () => {
  it('Should_FreezePageRegistry', () => {
    expect(Object.isFrozen(pageRegistry)).toBe(true)
  })

  it('Should_HaveExactlyTenEntries', () => {
    expect(pageRegistry.length).toBe(10)
  })

  it('Should_ExposeDefaultMeta_WithExpectedCopy', () => {
    expect(DEFAULT_PAGE_META.section).toBe('Workspace')
    expect(DEFAULT_PAGE_META.title).toBe('Knowz')
    expect(DEFAULT_PAGE_META.description).toBe(
      'Operate your self-hosted knowledge workspace with a cleaner shell.',
    )
  })
})

describe('page-meta-config — per-route copy preservation', () => {
  const cases: Array<[string, { section: string; title: string }]> = [
    ['/', { section: 'Overview', title: 'Dashboard' }],
    ['/dashboard', { section: 'Overview', title: 'Dashboard' }],
    ['/knowledge', { section: 'Library', title: 'Knowledge' }],
    ['/search', { section: 'Discover', title: 'Search' }],
    ['/chat', { section: 'Workspace', title: 'Chat' }],
    ['/vaults', { section: 'Library', title: 'Vaults' }],
    ['/settings', { section: 'Control', title: 'Settings' }],
    ['/files', { section: 'Assets', title: 'Files' }],
    ['/inbox', { section: 'Capture', title: 'Inbox' }],
    ['/organize', { section: 'Structure', title: 'Organize' }],
    ['/admin', { section: 'Administration', title: 'Admin' }],
  ]

  for (const [pathname, expected] of cases) {
    it(`Should_ReturnExpectedMeta_ForPath_${pathname.replace(/\W+/g, '_')}`, () => {
      const meta = getPageMeta(pathname)
      expect(meta.section).toBe(expected.section)
      expect(meta.title).toBe(expected.title)
    })
  }
})

describe('page-meta-config — matching rules', () => {
  it('Should_ReturnDefault_ForUnknownPath', () => {
    expect(getPageMeta('/unknown-route-xyz')).toEqual(DEFAULT_PAGE_META)
  })

  it('Should_MatchAdminPrefix_ForAdminSubroute', () => {
    expect(getPageMeta('/admin/users').title).toBe('Admin')
  })

  it('Should_MatchKnowledgePrefix_ForKnowledgeDetail', () => {
    expect(getPageMeta('/knowledge/abc-123').title).toBe('Knowledge')
  })
})
