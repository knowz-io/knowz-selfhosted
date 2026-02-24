export interface KnowledgeItem {
  id: string
  title: string
  content: string
  summary?: string
  type: string
  source?: string
  filePath?: string
  topic?: { id: string; name: string }
  tags: string[]
  vaults: { id: string; name: string; isPrimary: boolean }[]
  createdAt: string
  updatedAt: string
  isIndexed: boolean
  indexedAt?: string
}

export interface KnowledgeListItem {
  id: string
  title: string
  summary: string
  type: string
  filePath?: string
  vaultId?: string
  vaultName?: string
  createdByUserId?: string
  createdByUserName?: string
  createdAt: string
  updatedAt: string
  isIndexed: boolean
}

export interface CreatorRef {
  id: string
  name: string
}

export interface KnowledgeListResponse {
  items: KnowledgeListItem[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface KnowledgeStats {
  totalKnowledgeItems: number
  byType: { type: string; count: number }[]
  byVault: { vault: string; count: number }[]
  dateRange?: { earliest: string; latest: string }
}

export interface Vault {
  id: string
  name: string
  description?: string
  vaultType?: string
  isDefault: boolean
  parentVaultId?: string
  knowledgeCount?: number
  createdAt: string
}

export interface VaultContentsResponse {
  vaultId: string
  items: KnowledgeListItem[]
  totalItems: number
}

export interface SearchResult {
  knowledgeId: string
  title: string
  content: string
  summary?: string
  vaultName?: string
  topicName?: string
  tags: string[]
  knowledgeType?: string
  filePath?: string
  score: number
}

export interface SearchResponse {
  items: SearchResult[]
  totalResults: number
}

export interface AskResponse {
  answer: string
  sources: { knowledgeId: string }[]
  confidence: number
}

export interface Topic {
  id: string
  name: string
  description?: string
  knowledgeCount: number
}

export interface TopicDetails {
  id: string
  name: string
  description?: string
  knowledgeItems: KnowledgeListItem[]
}

export interface CreateKnowledgeData {
  title?: string
  content: string
  type?: string
  vaultId?: string
  tags?: string[]
  source?: string
}

export interface UpdateKnowledgeData {
  title?: string
  content?: string
  source?: string
  tags?: string[]
  vaultId?: string
}

// --- Auth & Admin Types ---

export enum UserRole {
  User = 0,
  Admin = 1,
  SuperAdmin = 2,
}

const roleStringToNumber: Record<string, UserRole> = {
  SuperAdmin: UserRole.SuperAdmin,
  Admin: UserRole.Admin,
  User: UserRole.User,
}

/** Normalize role from API (may be string "SuperAdmin" or number 0) to numeric enum */
export function normalizeRole(role: number | string): UserRole {
  if (typeof role === 'string') {
    return roleStringToNumber[role] ?? UserRole.User
  }
  return role
}

/** Normalize a UserDto's role field from API response */
export function normalizeUser(u: UserDto): UserDto {
  return { ...u, role: normalizeRole(u.role) }
}

export interface UserDto {
  id: string
  username: string
  email: string | null
  displayName: string | null
  role: number
  tenantId: string
  tenantName: string | null
  isActive: boolean
  apiKey: string | null
  createdAt: string
  lastLoginAt: string | null
}

export interface TenantDto {
  id: string
  name: string
  slug: string
  description: string | null
  isActive: boolean
  userCount: number
  createdAt: string
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: UserDto
}

export interface CreateTenantData {
  name: string
  slug: string
  description?: string
}

export interface UpdateTenantData {
  name?: string
  slug?: string
  description?: string
  isActive?: boolean
}

export interface CreateUserData {
  tenantId: string
  username: string
  password: string
  email?: string
  displayName?: string
  role: number
}

export interface UpdateUserData {
  email?: string
  displayName?: string
  role?: number
  isActive?: boolean
}

export interface ResetPasswordData {
  newPassword: string
}

// --- Vault Access Types ---

export interface UserPermissionsDto {
  userId: string
  hasAllVaultsAccess: boolean
  canCreateVaults: boolean
}

export interface UserVaultAccessDto {
  id: string
  vaultId: string
  vaultName: string
  canRead: boolean
  canWrite: boolean
  canDelete: boolean
  canManage: boolean
}

export interface VaultAccessGrant {
  vaultId: string
  canRead: boolean
  canWrite: boolean
  canDelete: boolean
  canManage: boolean
}

// --- Tag & Entity Types ---

export interface TagItem {
  id: string
  name: string
  knowledgeCount: number
  createdAt: string
}

export interface EntityItem {
  id: string
  name: string
  createdAt: string
}

// --- Inbox Types ---

export interface InboxItemDto {
  id: string
  body: string
  type: string
  createdByUserId?: string | null
  createdAt: string
  updatedAt: string
}

export interface InboxListResponse {
  items: InboxItemDto[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
  inboxVisibilityScope: string
}

export interface ConvertResult {
  inboxItemId: string
  knowledgeId: string
  title: string
  converted: boolean
}

export interface BatchConvertResult {
  requested: number
  converted: number
  failed: number
  results: ConvertResult[]
}

// --- File Storage Types ---

export interface FileMetadataDto {
  id: string
  fileName: string
  contentType?: string
  sizeBytes: number
  blobUri?: string
  transcriptionText?: string
  extractedText?: string
  visionDescription?: string
  blobMigrationPending: boolean
  createdAt: string
  updatedAt: string
  knowledgeId?: string
  knowledgeTitle?: string
  vaultId?: string
  vaultName?: string
}

export interface FileUploadResult {
  fileRecordId: string
  fileName: string
  contentType: string
  sizeBytes: number
  blobUri: string
  success: boolean
}

export interface FileListResponse {
  items: FileMetadataDto[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface FileAttachmentDto {
  id: string
  fileRecordId: string
  knowledgeId?: string
  commentId?: string
  createdAt: string
}

// --- Admin Configuration Types ---

export interface ConfigCategoryDto {
  category: string
  displayName: string
  description: string
  requiresRestart: boolean
  entries: ConfigEntryDto[]
}

export interface ConfigEntryDto {
  key: string
  value: string | null
  isSecret: boolean
  requiresRestart: boolean
  description: string | null
  isSet: boolean
  lastModifiedAt: string | null
  lastModifiedBy: string | null
  source: string | null
}

export interface ConfigEntryUpdateDto {
  key: string
  value: string | null
}

export interface ConfigUpdateResult {
  success: boolean
  restartRequired: boolean
  errors: string[]
  entriesUpdated: number
}

export interface ServiceHealthResult {
  category: string
  displayName: string
  isHealthy: boolean
  status: string
  latencyMs: number | null
}

export interface DeploymentStatusDto {
  mode: string
  version: string
  startupTime: string
  restartRequired: boolean
  restartReasons: string[]
}

// --- Chat Types ---

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  sources?: { knowledgeId: string }[]
  confidence?: number
  timestamp: string
}

export interface ChatConversation {
  id: string
  title: string
  vaultId?: string
  messages: ChatMessage[]
  createdAt: string
  updatedAt: string
}

export interface ChatRequestData {
  question: string
  conversationHistory?: { role: string; content: string }[]
  vaultId?: string
  researchMode?: boolean
  maxTurns?: number
}

export interface ChatResponseData {
  answer: string
  sources: { knowledgeId: string }[]
  confidence: number
}

// --- Comment/Contribution Types ---

export interface Comment {
  id: string
  knowledgeId: string
  parentCommentId?: string
  authorName: string
  body: string
  isAnswer: boolean
  sentiment?: string
  createdAt: string
  updatedAt: string
  replies?: Comment[]
  attachmentCount: number
}

export interface CreateCommentData {
  body: string
  authorName?: string
  parentCommentId?: string
  sentiment?: string
}

export interface UpdateCommentData {
  body?: string
  sentiment?: string
}

// --- SSO Types ---

export interface SelfHostedSSOConfigDto {
  isEnabled: boolean
  clientId: string | null
  hasClientSecret: boolean
  directoryTenantId: string | null
  autoProvisionUsers: boolean
  defaultRole: string
  detectedMode: string
  lastTestedAt: string | null
  lastTestSucceeded: boolean | null
}

export interface SelfHostedSSOConfigRequest {
  isEnabled: boolean
  clientId: string | null
  clientSecret: string | null
  directoryTenantId: string | null
  autoProvisionUsers: boolean
  defaultRole: string
}

export interface SelfHostedSSOTestResultDto {
  success: boolean
  detectedMode: string | null
  errorMessage: string | null
  status: string | null
  validTenantIds: string[] | null
  testedAt: string
}

export interface SSOProviderInfo {
  provider: string
  displayName: string
  mode?: string
}

export interface SSOCallbackResultDto {
  success: boolean
  token: string
  expiresAt: string
  email: string | null
  displayName: string | null
  wasAutoProvisioned: boolean
}

// --- SSE Streaming Types ---

export interface SSESourcesEvent {
  type: 'sources'
  sources: { knowledgeId: string }[]
  confidence: number
}

export interface SSETokenEvent {
  type: 'token'
  content: string
}

export interface SSEDoneEvent {
  type: 'done'
}

export interface SSEErrorEvent {
  type: 'error'
  message: string
}

export type SSEEvent = SSESourcesEvent | SSETokenEvent | SSEDoneEvent | SSEErrorEvent
