import {
  AuditOutlined,
  BarChartOutlined,
  BellOutlined,
  CalculatorOutlined,
  CarOutlined,
  DashboardOutlined,
  FileTextOutlined,
  DollarOutlined,
  SettingOutlined,
  SolutionOutlined,
  UserOutlined,
} from '@ant-design/icons'
import { Layout, Menu } from 'antd'
import { Link } from 'react-router-dom'

const navigationItems = [
  { key: 'dashboard', icon: <DashboardOutlined />, path: '/dashboard', label: '儀表板' },
  { key: 'customers', icon: <UserOutlined />, path: '/customers', label: '客戶管理' },
  { key: 'vehicles', icon: <CarOutlined />, path: '/vehicles', label: '車輛管理' },
  { key: 'quotation', icon: <CalculatorOutlined />, path: '/quotation', label: '車險報價' },
  { key: 'underwriting', icon: <SolutionOutlined />, path: '/underwriting', label: '投保申請' },
  { key: 'uwqueue', icon: <SolutionOutlined />, path: '/uwqueue', label: '核保作業' },
  { key: 'issue', icon: <DollarOutlined />, path: '/issue', label: '出單／繳費' },
  { key: 'policies', icon: <FileTextOutlined />, path: '/policies', label: '保單管理' },
  { key: 'claims', icon: <SolutionOutlined />, path: '/claims', label: '理賠管理' },
  { key: 'documents', icon: <FileTextOutlined />, path: '/documents', label: '文件管理' },
  { key: 'notifications', icon: <BellOutlined />, path: '/notifications', label: '通知中心' },
  { key: 'audit', icon: <AuditOutlined />, path: '/audit', label: '稽核軌跡' },
  { key: 'renewals', icon: <FileTextOutlined />, path: '/renewals', label: '續保管理' },
  { key: 'reports', icon: <BarChartOutlined />, path: '/reports', label: '報表中心' },
  { key: 'settings', icon: <SettingOutlined />, path: '/settings', label: '系統設定' },
]

type SideNavigationProps = {
  selectedKey?: string
}

function SideNavigation({ selectedKey = 'dashboard' }: SideNavigationProps) {
  const items = navigationItems.map((item) => ({
    key: item.key,
    icon: item.icon,
    label: (
      <Link to={item.path} aria-current={selectedKey === item.key ? 'page' : undefined}>
        {item.label}
      </Link>
    ),
  }))

  return (
    <Layout.Sider className="app-sider" breakpoint="lg" collapsedWidth={0} width={240}>
      <div className="brand-mark">
        <CarOutlined />
        <span>車險管理系統</span>
      </div>
      <nav className="side-navigation" aria-label="主要功能">
        <Menu mode="inline" selectedKeys={[selectedKey]} items={items} />
      </nav>
    </Layout.Sider>
  )
}

export default SideNavigation
