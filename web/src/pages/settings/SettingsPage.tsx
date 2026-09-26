import { Tabs } from 'antd'
import PermissionMatrixPage from './PermissionMatrixPage'
import UnderwritingRuleListPage from './UnderwritingRuleListPage'
import UserManagementPage from './UserManagementPage'

function SettingsPage() {
  return (
    <Tabs
      defaultActiveKey="users"
      items={[
        {
          key: 'users',
          label: '使用者管理',
          children: <UserManagementPage />,
        },
        {
          key: 'underwriting-rules',
          label: '核保規則',
          children: <UnderwritingRuleListPage />,
        },
        {
          key: 'permission-matrix',
          label: '權限矩陣',
          children: <PermissionMatrixPage />,
        },
      ]}
    />
  )
}

export default SettingsPage
