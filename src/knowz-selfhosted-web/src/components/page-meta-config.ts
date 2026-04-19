export interface PageMeta {
  section: string
  title: string
  description: string
}

interface PageRegistryEntry {
  match: (pathname: string) => boolean
  meta: PageMeta
}

export const DEFAULT_PAGE_META: PageMeta = {
  section: 'Workspace',
  title: 'Knowz',
  description: 'Operate your self-hosted knowledge workspace with a cleaner shell.',
}

export const pageRegistry: ReadonlyArray<PageRegistryEntry> = Object.freeze<PageRegistryEntry[]>([
  {
    match: (p) => p === '/' || p === '/dashboard',
    meta: {
      section: 'Overview',
      title: 'Dashboard',
      description: 'Track the shape of your self-hosted workspace at a glance.',
    },
  },
  {
    match: (p) => p.startsWith('/knowledge'),
    meta: {
      section: 'Library',
      title: 'Knowledge',
      description: 'Browse, refine, and manage your self-hosted knowledge base.',
    },
  },
  {
    match: (p) => p.startsWith('/search') || p.startsWith('/ask'),
    meta: {
      section: 'Discover',
      title: 'Search',
      description: 'Search, filter, and ask questions across your self-hosted workspace.',
    },
  },
  {
    match: (p) => p.startsWith('/chat'),
    meta: {
      section: 'Workspace',
      title: 'Chat',
      description: 'Talk directly to your vaults with traceable, self-hosted context.',
    },
  },
  {
    match: (p) => p.startsWith('/vaults'),
    meta: {
      section: 'Library',
      title: 'Vaults',
      description: 'Organize collections and shape how knowledge is grouped.',
    },
  },
  {
    match: (p) => p.startsWith('/settings') || p.startsWith('/account'),
    meta: {
      section: 'Control',
      title: 'Settings',
      description: 'Configure connections, account settings, and self-hosted capabilities.',
    },
  },
  {
    match: (p) => p.startsWith('/files'),
    meta: {
      section: 'Assets',
      title: 'Files',
      description: 'Inspect uploaded files and manage attachment-heavy knowledge workflows.',
    },
  },
  {
    match: (p) => p.startsWith('/inbox'),
    meta: {
      section: 'Capture',
      title: 'Inbox',
      description: 'Review staged content before it becomes durable knowledge.',
    },
  },
  {
    match: (p) => p.startsWith('/organize'),
    meta: {
      section: 'Structure',
      title: 'Organize',
      description: 'Navigate tags, topics, and entities with a cleaner self-hosted frame.',
    },
  },
  {
    match: (p) => p.startsWith('/admin'),
    meta: {
      section: 'Administration',
      title: 'Admin',
      description: 'Manage tenants, users, audit history, and self-hosted operations.',
    },
  },
])

export function getPageMeta(pathname: string): PageMeta {
  return pageRegistry.find((entry) => entry.match(pathname))?.meta ?? DEFAULT_PAGE_META
}
