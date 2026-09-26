import { useEffect, useMemo, useState } from 'react'
import { Card, Space, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { ApiError } from '../../api/http'
import { getPermissionMatrix } from '../../api/permissionMatrix'
import type { PermissionMatrix } from '../../api/permissionMatrix'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'

type PermissionRow = {
  permission: string
  roles: Record<string, boolean>
}

function PermissionMatrixPage() {
  const [matrix, setMatrix] = useState<PermissionMatrix | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)

    void getPermissionMatrix(controller.signal)
      .then(setMatrix)
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取權限矩陣失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [reloadKey])

  const rolePermissionSets = useMemo(() => {
    const result = new Map<string, Set<string>>()
    for (const item of matrix?.matrix ?? []) {
      result.set(item.role, new Set(item.permissions))
    }
    return result
  }, [matrix])

  const rows = useMemo<PermissionRow[]>(() => (
    (matrix?.permissions ?? []).map((permission) => ({
      permission,
      roles: Object.fromEntries(
        (matrix?.roles ?? []).map((role) => [role, rolePermissionSets.get(role)?.has(permission) ?? false]),
      ),
    }))
  ), [matrix, rolePermissionSets])

  const columns = useMemo<ColumnsType<PermissionRow>>(() => [
    {
      title: 'Permission',
      dataIndex: 'permission',
      key: 'permission',
      fixed: 'left',
      width: 240,
      render: (permission: string) => <Typography.Text code>{permission}</Typography.Text>,
    },
    ...(matrix?.roles ?? []).map((role) => ({
      title: role,
      key: role,
      align: 'center' as const,
      width: 140,
      render: (_: unknown, row: PermissionRow) => row.roles[role]
        ? <Tag color="success" aria-label={`${role} 具備 ${row.permission}`}>有權限</Tag>
        : <Tag aria-label={`${role} 不具備 ${row.permission}`}>—</Tag>,
    })),
  ], [matrix])

  if (error instanceof ApiError && error.kind === 'forbidden') {
    return <ForbiddenState requiredRole="系統管理員" contact="系統管理員" />
  }

  if (error) {
    return (
      <ErrorState
        title="權限矩陣讀取失敗"
        message={error.message}
        onRetry={() => setReloadKey((current) => current + 1)}
      />
    )
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>權限矩陣</Typography.Title>
        <Typography.Text type="secondary">
          此頁僅供查閱；角色與權限來源由後端固定 RBAC 設定統一提供，不提供編輯或儲存操作。
        </Typography.Text>
      </Card>

      {loading && !matrix ? (
        <LoadingState rows={6} />
      ) : !matrix || matrix.roles.length === 0 || matrix.permissions.length === 0 ? (
        <EmptyState description="尚無權限矩陣資料" />
      ) : (
        <Card styles={{ body: { padding: 0 } }}>
          <Table<PermissionRow>
            rowKey="permission"
            columns={columns}
            dataSource={rows}
            loading={loading}
            pagination={false}
            scroll={{ x: 940 }}
          />
        </Card>
      )}
    </Space>
  )
}

export default PermissionMatrixPage
