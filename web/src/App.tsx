import { App as AntApp, Alert, Button, Card, ConfigProvider, Result, Skeleton } from 'antd'
import { Link, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import AppLayout from './components/AppLayout'
import PlaceholderPage from './pages/PlaceholderPage'
import CustomerListPage from './pages/customers/CustomerListPage'
import CustomerDetailPage from './pages/customers/CustomerDetailPage'
import VehicleListPage from './pages/vehicles/VehicleListPage'
import VehicleDetailPage from './pages/vehicles/VehicleDetailPage'
import QuotationPage from './pages/quotation/QuotationPage'
import UnderwritingPage from './pages/underwriting/UnderwritingPage'
import UnderwritingQueuePage from './pages/underwritingQueue/UnderwritingQueuePage'
import PolicyListPage from './pages/policies/PolicyListPage'
import PolicyDetailPage from './pages/policies/PolicyDetailPage'
import PaymentIssuePage from './pages/payments/PaymentIssuePage'
import ClaimsPage from './pages/claims/ClaimsPage'
import DocumentsPage from './pages/documents/DocumentsPage'
import NotificationsPage from './pages/notifications/NotificationsPage'
import LoginPage from './pages/auth/LoginPage'
import SettingsPage from './pages/settings/SettingsPage'
import AuditLogPage from './pages/audit/AuditLogPage'
import DashboardPage from './pages/dashboard/DashboardPage'

const pageDefinitions: Record<string, { title: string; description: string }> = {
  customers: { title: '客戶管理', description: '管理投保人與客戶基本資料。' },
  vehicles: { title: '車輛管理', description: '管理車輛資料與車籍資訊。' },
  policies: { title: '保單管理', description: '管理車險保單與保障內容。' },
  claims: { title: '理賠管理', description: '管理事故通報、理賠案件與處理進度。' },
  renewals: { title: '續保管理', description: '管理即將到期保單與續保作業。' },
  reports: { title: '報表中心', description: '檢視車險業務與營運分析報表。' },
  settings: { title: '系統設定', description: '管理系統參數與使用者設定。' },
}

function NotFoundPage() {
  return (
    <Result
      status="404"
      title="找不到頁面"
      subTitle="您要前往的頁面不存在，請返回儀表板或從左側導覽選擇功能。"
      extra={<Link to="/dashboard">返回儀表板</Link>}
    />
  )
}

function RoutedContent() {
  const location = useLocation()
  const selectedKey = location.pathname.split('/').filter(Boolean)[0] || 'dashboard'

  return (
    <AppLayout selectedKey={selectedKey}>
      <Routes>
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/customers/:id" element={<CustomerDetailPage />} />
        <Route path="/vehicles/:id" element={<VehicleDetailPage />} />
        <Route path="/quotation" element={<QuotationPage />} />
        <Route path="/underwriting" element={<UnderwritingPage />} />
        <Route path="/uwqueue" element={<UnderwritingQueuePage />} />
        <Route path="/issue" element={<PaymentIssuePage />} />
        <Route path="/documents" element={<DocumentsPage />} />
        <Route path="/notifications" element={<NotificationsPage />} />
        <Route path="/audit" element={<AuditLogPage />} />
        <Route path="/policies/:id" element={<PolicyDetailPage />} />
        {Object.entries(pageDefinitions).map(([path, definition]) => (
          <Route
            key={path}
            path={`/${path}`}
            element={path === 'customers'
              ? <CustomerListPage />
              : path === 'vehicles'
                ? <VehicleListPage />
                : path === 'policies'
                  ? <PolicyListPage />
                  : path === 'claims'
                    ? <ClaimsPage />
                    : path === 'settings'
                      ? <SettingsPage />
                      : <PlaceholderPage title={definition.title} description={definition.description} />}
          />
        ))}
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </AppLayout>
  )
}

function AuthInitializationState() {
  const { initializationError, retryInitialization } = useAuth()

  return (
    <main className="login-shell">
      <Card className="login-card" bordered={false}>
        {initializationError ? (
          <Alert
            type="error"
            showIcon
            message="無法確認登入狀態"
            description={initializationError}
            action={<Button onClick={retryInitialization}>重試</Button>}
          />
        ) : (
          <Skeleton active title paragraph={{ rows: 4 }} />
        )}
      </Card>
    </main>
  )
}

function ProtectedApp() {
  const { status } = useAuth()

  if (status === 'loading') return <AuthInitializationState />
  if (status === 'unauthenticated') return <Navigate to="/login" replace />

  return <RoutedContent />
}

function LoginRoute() {
  const { status } = useAuth()

  if (status === 'loading') return <AuthInitializationState />
  if (status === 'authenticated') return <Navigate to="/dashboard" replace />

  return <LoginPage />
}

function App() {
  return (
    <ConfigProvider
      theme={{
        token: {
          colorPrimary: '#0958d9',
          colorSuccess: '#237804',
          colorSuccessBg: '#f6ffed',
          colorSuccessBorder: '#b7eb8f',
          colorWarning: '#874d00',
          colorWarningBg: '#fffbe6',
          colorWarningBorder: '#ffe58f',
          colorError: '#a8071a',
          colorErrorBg: '#fff2f0',
          colorErrorBorder: '#ffccc7',
          colorInfo: '#0958d9',
          colorInfoBg: '#e6f4ff',
          colorInfoBorder: '#91caff',
          colorTextSecondary: '#595959',
          colorTextDescription: '#595959',
          colorTextPlaceholder: '#595959',
          colorBorder: '#8c8c8c',
          borderRadius: 8,
        },
      }}
    >
      <AntApp>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginRoute />} />
            <Route path="*" element={<ProtectedApp />} />
          </Routes>
        </AuthProvider>
      </AntApp>
    </ConfigProvider>
  )
}

export default App
