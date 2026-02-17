import { Routes, Route } from 'react-router-dom'
import { AuthProvider, ProtectedRoute, AdminRoute } from './lib/auth'
import Layout from './components/Layout'
import LoginPage from './pages/LoginPage'
import DashboardPage from './pages/DashboardPage'
import KnowledgeListPage from './pages/KnowledgeListPage'
import KnowledgeDetailPage from './pages/KnowledgeDetailPage'
import KnowledgeCreatePage from './pages/KnowledgeCreatePage'
import VaultListPage from './pages/VaultListPage'
import VaultDetailPage from './pages/VaultDetailPage'
import SearchPage from './pages/SearchPage'
import AskPage from './pages/AskPage'
import TopicsPage from './pages/TopicsPage'
import SettingsPage from './pages/SettingsPage'
import ApiKeysPage from './pages/ApiKeysPage'
import DataPortabilityPage from './pages/DataPortabilityPage'
import McpSetupPage from './pages/McpSetupPage'
import TagsPage from './pages/TagsPage'
import EntitiesPage from './pages/EntitiesPage'
import AccountPage from './pages/AccountPage'
import InboxPage from './pages/InboxPage'
import FilesPage from './pages/FilesPage'
import ChatPage from './pages/ChatPage'
import AdminDashboardPage from './pages/admin/AdminDashboardPage'
import TenantsPage from './pages/admin/TenantsPage'
import UsersPage from './pages/admin/UsersPage'
import AdminSettingsPage from './pages/admin/AdminSettingsPage'
import SSOSettingsPage from './pages/admin/SSOSettingsPage'
import SSOCallbackPage from './pages/SSOCallbackPage'

export default function App() {
  return (
    <AuthProvider>
      <Routes>
        {/* Public routes: no auth required */}
        <Route path="/login" element={<LoginPage />} />
        <Route path="/auth/sso/callback" element={<SSOCallbackPage />} />

        {/* Protected routes: require authentication */}
        <Route
          path="*"
          element={
            <ProtectedRoute>
              <Layout>
                <Routes>
                  <Route path="/" element={<DashboardPage />} />
                  <Route path="/knowledge" element={<KnowledgeListPage />} />
                  <Route path="/knowledge/new" element={<KnowledgeCreatePage />} />
                  <Route path="/knowledge/:id" element={<KnowledgeDetailPage />} />
                  <Route path="/vaults" element={<VaultListPage />} />
                  <Route path="/vaults/:id" element={<VaultDetailPage />} />
                  <Route path="/search" element={<SearchPage />} />
                  <Route path="/ask" element={<AskPage />} />
                  <Route path="/chat" element={<ChatPage />} />
                  <Route path="/inbox" element={<InboxPage />} />
                  <Route path="/files" element={<FilesPage />} />
                  <Route path="/topics" element={<TopicsPage />} />
                  <Route path="/tags" element={<TagsPage />} />
                  <Route path="/entities" element={<EntitiesPage />} />
                  <Route path="/account" element={<AccountPage />} />
                  <Route path="/settings" element={<SettingsPage />} />
                  <Route path="/api-keys" element={<ApiKeysPage />} />
                  <Route path="/data" element={<DataPortabilityPage />} />
                  <Route path="/mcp-setup" element={<McpSetupPage />} />

                  {/* Admin routes: require SuperAdmin role */}
                  <Route
                    path="/admin"
                    element={
                      <AdminRoute>
                        <AdminDashboardPage />
                      </AdminRoute>
                    }
                  />
                  <Route
                    path="/admin/tenants"
                    element={
                      <AdminRoute>
                        <TenantsPage />
                      </AdminRoute>
                    }
                  />
                  <Route
                    path="/admin/users"
                    element={
                      <AdminRoute>
                        <UsersPage />
                      </AdminRoute>
                    }
                  />
                  <Route
                    path="/admin/settings"
                    element={
                      <AdminRoute>
                        <AdminSettingsPage />
                      </AdminRoute>
                    }
                  />
                  <Route
                    path="/admin/sso"
                    element={
                      <AdminRoute>
                        <SSOSettingsPage />
                      </AdminRoute>
                    }
                  />

                  {/* SPA fallback */}
                  <Route path="*" element={<DashboardPage />} />
                </Routes>
              </Layout>
            </ProtectedRoute>
          }
        />
      </Routes>
    </AuthProvider>
  )
}
