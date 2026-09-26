import { useEffect, useMemo, useState } from 'react'
import { App as AntApp, Card, Input, Select, Space, Switch, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { ApiError } from '../../api/http'
import { getUsers, updateUserActive, updateUserRole, USER_ROLES } from '../../api/users'
import type { UserAccount, UserRole } from '../../api/users'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'

function formatUpdatedAt(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
}

function UserManagementPage() {
  const { message } = AntApp.useApp()
  const [users, setUsers] = useState<UserAccount[]>([])
  const [keyword, setKeyword] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [savingIds, setSavingIds] = useState<Set<number>>(() => new Set())

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)

    void getUsers(controller.signal)
      .then(setUsers)
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取使用者資料失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [reloadKey])

  const filteredUsers = useMemo(() => {
    const normalized = keyword.trim().toLocaleLowerCase('zh-TW')
    if (!normalized) return users

    return users.filter((user) => [
      user.displayName,
      user.username,
      user.email,
      user.phone,
      user.role,
    ].some((value) => value.toLocaleLowerCase('zh-TW').includes(normalized)))
  }, [keyword, users])

  const setSaving = (id: number, saving: boolean) => {
    setSavingIds((current) => {
      const next = new Set(current)
      if (saving) next.add(id)
      else next.delete(id)
      return next
    })
  }

  const replaceUser = (updated: UserAccount) => {
    setUsers((current) => current.map((user) => user.id === updated.id ? updated : user))
  }

  const handleRoleChange = async (user: UserAccount, role: UserRole) => {
    if (role === user.role) return

    const previous = user
    replaceUser({ ...user, role })
    setSaving(user.id, true)

    try {
      const updated = await updateUserRole(user.id, role)
      replaceUser(updated)
      message.success(`${user.displayName} 的角色已更新為「${updated.role}」`)
    } catch (requestError: unknown) {
      replaceUser(previous)
      message.error(requestError instanceof Error ? requestError.message : '角色更新失敗')
    } finally {
      setSaving(user.id, false)
    }
  }

  const handleActiveChange = async (user: UserAccount, isActive: boolean) => {
    if (isActive === user.isActive) return

    const previous = user
    replaceUser({ ...user, isActive })
    setSaving(user.id, true)

    try {
      const updated = await updateUserActive(user.id, isActive)
      replaceUser(updated)
      message.success(`${user.displayName} 已${updated.isActive ? '啟用' : '停用'}`)
    } catch (requestError: unknown) {
      replaceUser(previous)
      message.error(requestError instanceof Error ? requestError.message : '帳號狀態更新失敗')
    } finally {
      setSaving(user.id, false)
    }
  }

  const columns: ColumnsType<UserAccount> = [
    {
      title: '使用者',
      key: 'user',
      render: (_, user) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong>{user.displayName}</Typography.Text>
          <Typography.Text type="secondary">{user.username}</Typography.Text>
        </Space>
      ),
    },
    { title: 'Email', dataIndex: 'email', key: 'email' },
    { title: '電話', dataIndex: 'phone', key: 'phone' },
    {
      title: '角色',
      dataIndex: 'role',
      key: 'role',
      width: 150,
      render: (role: UserRole, user) => (
        <Select<UserRole>
          aria-label={`${user.displayName} 的角色`}
          value={role}
          options={USER_ROLES.map((value) => ({ value, label: value }))}
          disabled={savingIds.has(user.id)}
          onChange={(value) => void handleRoleChange(user, value)}
          style={{ width: '100%' }}
        />
      ),
    },
    {
      title: '狀態',
      dataIndex: 'isActive',
      key: 'isActive',
      width: 150,
      render: (isActive: boolean, user) => (
        <Space>
          <Switch
            aria-label={`${user.displayName} 帳號狀態`}
            checked={isActive}
            checkedChildren="啟用"
            unCheckedChildren="停用"
            disabled={savingIds.has(user.id)}
            loading={savingIds.has(user.id)}
            onChange={(checked) => void handleActiveChange(user, checked)}
          />
          <Tag color={isActive ? 'success' : 'default'}>{isActive ? '使用中' : '已停用'}</Tag>
        </Space>
      ),
    },
    {
      title: '更新時間',
      dataIndex: 'updatedAtUtc',
      key: 'updatedAtUtc',
      width: 180,
      render: formatUpdatedAt,
    },
  ]

  if (error instanceof ApiError && error.kind === 'forbidden') {
    return <ForbiddenState requiredRole="系統管理員" contact="系統管理員" />
  }

  if (error) {
    return (
      <ErrorState
        title="使用者資料讀取失敗"
        message={error.message}
        onRetry={() => setReloadKey((current) => current + 1)}
      />
    )
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card>
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <div>
            <Typography.Title level={3} style={{ marginBottom: 4 }}>使用者管理</Typography.Title>
            <Typography.Text type="secondary">
              系統管理員可檢視帳號、調整固定角色，並啟用或停用使用者。
            </Typography.Text>
          </div>
          <Input.Search
            allowClear
            aria-label="搜尋使用者"
            placeholder="搜尋姓名、帳號、Email、電話或角色"
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
          />
        </Space>
      </Card>

      {loading && users.length === 0 ? (
        <LoadingState rows={6} />
      ) : users.length === 0 ? (
        <EmptyState description="尚無使用者資料" />
      ) : filteredUsers.length === 0 ? (
        <EmptyState description="找不到符合搜尋條件的使用者" />
      ) : (
        <Card styles={{ body: { padding: 0 } }}>
          <Table<UserAccount>
            rowKey="id"
            columns={columns}
            dataSource={filteredUsers}
            loading={loading}
            pagination={false}
            scroll={{ x: 980 }}
          />
        </Card>
      )}
    </Space>
  )
}

export default UserManagementPage
