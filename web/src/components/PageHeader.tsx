import { BellOutlined, LogoutOutlined, UserOutlined } from '@ant-design/icons'
import { Avatar, Button, Space, Typography } from 'antd'
import { useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const pageTitles: Record<string, string> = {
  dashboard: '儀表板',
  customers: '客戶管理',
  vehicles: '車輛管理',
  quotation: '車險報價',
  underwriting: '投保申請',
  policies: '保單管理',
  claims: '理賠管理',
  documents: '文件管理',
  notifications: '通知中心',
  audit: '稽核軌跡',
  renewals: '續保管理',
  reports: '報表中心',
  settings: '系統設定',
}

function PageHeader() {
  const location = useLocation()
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const pageKey = location.pathname.split('/').filter(Boolean)[0] || 'dashboard'
  const title = pageTitles[pageKey] ?? '找不到頁面'

  const handleLogout = () => {
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <header className="page-header">
      <div>
        <Typography.Title level={3} className="page-title">
          {title}
        </Typography.Title>
        <Typography.Text type="secondary">掌握車險業務與客戶服務狀態</Typography.Text>
      </div>
      <Space size="middle">
        <Button aria-label="通知" type="text" icon={<BellOutlined />} />
        <Space>
          <Avatar icon={<UserOutlined />} />
          <div className="header-user-copy">
            <Typography.Text strong>{user?.displayName ?? user?.username ?? '使用者'}</Typography.Text>
            {user?.role && <Typography.Text type="secondary">{user.role}</Typography.Text>}
          </div>
        </Space>
        <Button type="text" icon={<LogoutOutlined />} onClick={handleLogout}>
          登出
        </Button>
      </Space>
    </header>
  )
}

export default PageHeader
