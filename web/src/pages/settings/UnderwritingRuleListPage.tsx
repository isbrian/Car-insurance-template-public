import { useEffect, useState } from 'react'
import { App as AntApp, Card, Space, Switch, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { ApiError } from '../../api/http'
import { getUnderwritingRules, updateUnderwritingRuleEnabled } from '../../api/underwritingRules'
import type { UnderwritingRule } from '../../api/underwritingRules'
import { useAuth } from '../../auth/AuthContext'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'

const actionLabels: Record<string, string> = {
  reject: '拒保',
  manual_review: '人工覆核',
  block_issue: '阻擋出單',
}

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

function UnderwritingRuleListPage() {
  const { message, modal } = AntApp.useApp()
  const { user } = useAuth()
  const [rules, setRules] = useState<UnderwritingRule[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const [reloadKey, setReloadKey] = useState(0)
  const [savingIds, setSavingIds] = useState<Set<number>>(() => new Set())
  const canManageRules = user?.role === '主管' || user?.role === '系統管理員'

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)

    void getUnderwritingRules(controller.signal)
      .then(setRules)
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取核保規則失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [reloadKey])

  const setSaving = (id: number, saving: boolean) => {
    setSavingIds((current) => {
      const next = new Set(current)
      if (saving) next.add(id)
      else next.delete(id)
      return next
    })
  }

  const replaceRule = (updated: UnderwritingRule) => {
    setRules((current) => current.map((rule) => rule.id === updated.id ? updated : rule))
  }

  const getUpdateErrorMessage = (requestError: unknown): string => {
    if (requestError instanceof ApiError && requestError.kind === 'forbidden') {
      return requestError.problem?.detail
        || requestError.problem?.title
        || '你沒有核保規則管理權限'
    }

    return requestError instanceof Error ? requestError.message : '核保規則狀態更新失敗'
  }

  const updateEnabled = async (rule: UnderwritingRule, isEnabled: boolean) => {
    if (isEnabled === rule.isEnabled || savingIds.has(rule.id)) return

    const previous = rule
    replaceRule({ ...rule, isEnabled })
    setSaving(rule.id, true)

    try {
      const updated = await updateUnderwritingRuleEnabled(rule, isEnabled)
      replaceRule(updated)
      message.success(`${updated.code} 已${updated.isEnabled ? '啟用' : '停用'}`)
    } catch (requestError: unknown) {
      replaceRule(previous)
      message.error(getUpdateErrorMessage(requestError))
    } finally {
      setSaving(rule.id, false)
    }
  }

  const confirmEnabledChange = (rule: UnderwritingRule, isEnabled: boolean) => {
    if (isEnabled === rule.isEnabled || savingIds.has(rule.id)) return

    const actionText = isEnabled ? '啟用' : '停用'
    modal.confirm({
      title: `確認${actionText}核保規則？`,
      content: (
        <Space direction="vertical" size={4}>
          <Typography.Text>{rule.code}／{rule.name}</Typography.Text>
          {!isEnabled && (
            <Typography.Text type="secondary">
              停用後，這項規則不再參與後續核保規則判定。
            </Typography.Text>
          )}
        </Space>
      ),
      okText: `確認${actionText}`,
      cancelText: '取消',
      okButtonProps: { danger: !isEnabled },
      onOk: () => updateEnabled(rule, isEnabled),
    })
  }

  const columns: ColumnsType<UnderwritingRule> = [
    {
      title: '規則',
      key: 'rule',
      width: 220,
      render: (_, rule) => (
        <Space direction="vertical" size={0}>
          <Typography.Text strong>{rule.code}</Typography.Text>
          <Typography.Text>{rule.name}</Typography.Text>
        </Space>
      ),
    },
    {
      title: '條件',
      key: 'condition',
      render: (_, rule) => (
        <Space direction="vertical" size={2}>
          <Typography.Text code>{rule.conditionType}</Typography.Text>
          <Typography.Text type="secondary">{rule.conditionJson}</Typography.Text>
        </Space>
      ),
    },
    {
      title: '動作',
      key: 'action',
      width: 260,
      render: (_, rule) => (
        <Space direction="vertical" size={2}>
          <Tag>{actionLabels[rule.action] ?? rule.action}</Tag>
          <Typography.Text type="secondary">{rule.message}</Typography.Text>
        </Space>
      ),
    },
    {
      title: '狀態',
      dataIndex: 'isEnabled',
      key: 'isEnabled',
      width: canManageRules ? 190 : 100,
      render: (isEnabled: boolean, rule) => (
        <Space>
          {canManageRules && (
            <Switch
              aria-label={`${rule.code} 核保規則狀態`}
              checked={isEnabled}
              checkedChildren="啟用"
              unCheckedChildren="停用"
              disabled={savingIds.has(rule.id)}
              loading={savingIds.has(rule.id)}
              onChange={(checked) => confirmEnabledChange(rule, checked)}
            />
          )}
          <Tag color={isEnabled ? 'success' : 'default'}>{isEnabled ? '啟用' : '停用'}</Tag>
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
    return <ForbiddenState requiredRole="具核保規則查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return (
      <ErrorState
        title="核保規則讀取失敗"
        message={error.message}
        onRetry={() => setReloadKey((current) => current + 1)}
      />
    )
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card>
        <Typography.Title level={3} style={{ marginBottom: 4 }}>核保規則</Typography.Title>
        <Typography.Text type="secondary">
          {canManageRules
            ? '可查閱核保規則，並在確認後啟用或停用規則。'
            : '目前可查閱核保規則；啟用與停用操作限具核保規則管理權限的角色。'}
        </Typography.Text>
      </Card>

      {loading && rules.length === 0 ? (
        <LoadingState rows={6} />
      ) : rules.length === 0 ? (
        <EmptyState description="尚無核保規則" />
      ) : (
        <Card styles={{ body: { padding: 0 } }}>
          <Table<UnderwritingRule>
            rowKey="id"
            columns={columns}
            dataSource={rules}
            loading={loading}
            pagination={false}
            scroll={{ x: 980 }}
          />
        </Card>
      )}
    </Space>
  )
}

export default UnderwritingRuleListPage
