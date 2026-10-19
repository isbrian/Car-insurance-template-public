import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { Button, Card, Input, Select, Space, Table, Tabs, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { ApiError } from '../../api/http'
import { getAuditLogs } from '../../api/auditLogs'
import type { AuditLog, AuditLogQuery } from '../../api/auditLogs'
import ListPageShell from '../../components/shared/ListPageShell'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import AuditLogDetailDrawer from './AuditLogDetailDrawer'
import { queryKeys } from '../../query/queryKeys'

const PAGE_SIZE = 20
const ROLE_OPTIONS = ['業務員', '核保員', '理賠員', '主管', '系統管理員'].map((value) => ({ value, label: value }))

const AUDIT_CATEGORIES = [
  { key: 'all', label: '全部' },
  { key: 'Customer', label: '客戶' },
  { key: 'UserAccount', label: '使用者' },
  { key: 'UnderwritingRule', label: '核保規則' },
  { key: 'Quote', label: '報價' },
  { key: 'InsuranceApplication', label: '投保' },
  { key: 'UnderwritingCase', label: '核保案件' },
  { key: 'Payment', label: '收款' },
  { key: 'Policy', label: '保單' },
  { key: 'Endorsement', label: '批改' },
  { key: 'Claim', label: '理賠' },
  { key: 'ApplicationDocument', label: '文件' },
  { key: 'Notification', label: '通知' },
] as const

type AuditCategoryKey = (typeof AUDIT_CATEGORIES)[number]['key']

type FilterDraft = {
  actor: string
  role?: string
  action: string
  objectType: string
  objectId: string
  fromLocal: string
  toLocal: string
}

const emptyFilters: FilterDraft = {
  actor: '',
  role: undefined,
  action: '',
  objectType: '',
  objectId: '',
  fromLocal: '',
  toLocal: '',
}

function toUtcIso(value: string): string | undefined {
  if (!value) return undefined
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString()
}

function formatOccurredAt(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value || '—'

  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date)
}

function AuditLogPage() {
  const [page, setPage] = useState(1)
  const [draft, setDraft] = useState<FilterDraft>(emptyFilters)
  const [applied, setApplied] = useState<FilterDraft>(emptyFilters)
  const [activeCategory, setActiveCategory] = useState<AuditCategoryKey>('all')
  const [selectedAuditLog, setSelectedAuditLog] = useState<AuditLog | null>(null)

  const auditQuery: AuditLogQuery = {
    page,
    pageSize: PAGE_SIZE,
    actor: applied.actor,
    role: applied.role,
    action: applied.action,
    objectType: activeCategory === 'all' ? applied.objectType : activeCategory,
    objectId: applied.objectId,
    fromUtc: toUtcIso(applied.fromLocal),
    toUtc: toUtcIso(applied.toLocal),
  }
  const auditLogsQuery = useQuery({
    queryKey: queryKeys.auditLogs.list(auditQuery),
    queryFn: ({ signal }) => getAuditLogs(auditQuery, signal),
    placeholderData: keepPreviousData,
  })
  const items: AuditLog[] = auditLogsQuery.data?.items ?? []
  const total = auditLogsQuery.data?.total ?? 0

  const applyFilters = () => {
    setPage(1)
    setApplied({ ...draft })
  }

  const clearFilters = () => {
    setDraft(emptyFilters)
    setApplied(emptyFilters)
    setPage(1)
  }

  const changeCategory = (category: string) => {
    setActiveCategory(category as AuditCategoryKey)
    setDraft((current) => ({ ...current, objectType: '' }))
    setApplied((current) => ({ ...current, objectType: '' }))
    setPage(1)
  }

  const columns: ColumnsType<AuditLog> = [
    {
      title: '時間',
      dataIndex: 'occurredAtUtc',
      key: 'occurredAtUtc',
      width: 190,
      render: formatOccurredAt,
    },
    {
      title: '操作人',
      dataIndex: 'actor',
      key: 'actor',
      width: 150,
    },
    {
      title: '角色',
      dataIndex: 'role',
      key: 'role',
      width: 130,
      render: (role: string) => <Tag>{role}</Tag>,
    },
    {
      title: '動作',
      dataIndex: 'action',
      key: 'action',
      width: 230,
      render: (action: string) => <Typography.Text code>{action}</Typography.Text>,
    },
    {
      title: '物件',
      key: 'object',
      width: 220,
      render: (_, item) => (
        <Space direction="vertical" size={0}>
          <Typography.Text>{item.objectType}</Typography.Text>
          <Typography.Text type="secondary">#{item.objectId}</Typography.Text>
        </Space>
      ),
    },
    {
      title: '來源 IP',
      dataIndex: 'sourceIp',
      key: 'sourceIp',
      width: 160,
    },
    {
      title: '詳情',
      key: 'detail',
      width: 90,
      fixed: 'right',
      render: (_, item) => <Button type="link" onClick={() => setSelectedAuditLog(item)}>查看</Button>,
    },
  ]

  if (auditLogsQuery.error instanceof ApiError && auditLogsQuery.error.kind === 'forbidden') {
    return <ForbiddenState requiredRole="主管或系統管理員" contact="系統管理員" />
  }

  if (auditLogsQuery.error) {
    return (
      <ErrorState
        title="稽核軌跡讀取失敗"
        message={auditLogsQuery.error.message}
        onRetry={() => void auditLogsQuery.refetch()}
      />
    )
  }

  const filterControls = (
    <Space wrap>
      <Select
        allowClear
        placeholder="角色"
        value={draft.role}
        options={ROLE_OPTIONS}
        style={{ width: 150 }}
        onChange={(role) => setDraft((current) => ({ ...current, role }))}
      />
      <Input
        placeholder="動作（exact match）"
        value={draft.action}
        style={{ width: 210 }}
        onChange={(event) => setDraft((current) => ({ ...current, action: event.target.value }))}
      />
      <Input
        placeholder="物件類型"
        value={activeCategory === 'all' ? draft.objectType : activeCategory}
        disabled={activeCategory !== 'all'}
        style={{ width: 180 }}
        onChange={(event) => setDraft((current) => ({ ...current, objectType: event.target.value }))}
      />
      <Input
        placeholder="物件編號"
        value={draft.objectId}
        style={{ width: 140 }}
        onChange={(event) => setDraft((current) => ({ ...current, objectId: event.target.value }))}
      />
      <Input
        aria-label="起始時間"
        type="datetime-local"
        value={draft.fromLocal}
        style={{ width: 205 }}
        onChange={(event) => setDraft((current) => ({ ...current, fromLocal: event.target.value }))}
      />
      <Input
        aria-label="結束時間"
        type="datetime-local"
        value={draft.toLocal}
        style={{ width: 205 }}
        onChange={(event) => setDraft((current) => ({ ...current, toLocal: event.target.value }))}
      />
      <Button type="primary" onClick={applyFilters}>查詢</Button>
      <Button onClick={clearFilters}>清除</Button>
    </Space>
  )

  return (
    <>
      <ListPageShell
        title="稽核軌跡"
        searchPlaceholder="操作人（exact match）"
        keyword={draft.actor}
        onKeywordChange={(actor) => setDraft((current) => ({ ...current, actor }))}
        filters={filterControls}
        batchActions={(
          <Tabs
            activeKey={activeCategory}
            items={AUDIT_CATEGORIES.map(({ key, label }) => ({ key, label }))}
            onChange={changeCategory}
          />
        )}
        page={page}
        pageSize={PAGE_SIZE}
        total={total}
        onPageChange={setPage}
      >
        {auditLogsQuery.isPending && items.length === 0 ? (
          <LoadingState rows={6} />
        ) : items.length === 0 ? (
          <EmptyState description="目前查詢條件下沒有稽核紀錄" />
        ) : (
          <Card styles={{ body: { padding: 0 } }}>
            <Table<AuditLog>
              rowKey="id"
              columns={columns}
              dataSource={items}
              loading={auditLogsQuery.isFetching}
              pagination={false}
              scroll={{ x: 1180 }}
            />
          </Card>
        )}
      </ListPageShell>

      <AuditLogDetailDrawer
        auditLog={selectedAuditLog}
        open={selectedAuditLog !== null}
        onClose={() => setSelectedAuditLog(null)}
      />
    </>
  )
}

export default AuditLogPage
