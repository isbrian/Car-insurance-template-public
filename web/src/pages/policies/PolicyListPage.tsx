import { useEffect, useState } from 'react'
import { Button, Card, Select, Table, Tag } from 'antd'
import { FileSearchOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { getPolicies } from '../../api/policies'
import type { PolicyListItem } from '../../api/policies'
import { ApiError } from '../../api/http'
import ListPageShell from '../../components/shared/ListPageShell'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'

const statusLabels: Record<string, { text: string; color: string }> = {
  issued: { text: '已出單', color: 'processing' },
  active: { text: '有效', color: 'success' },
  endorsement_pending: { text: '批改中', color: 'warning' },
  renewal_pending: { text: '續保中', color: 'warning' },
  terminated: { text: '已終止', color: 'error' },
  expired: { text: '已到期', color: 'default' },
  cancelled: { text: '已取消', color: 'error' },
}

function renderStatus(status: string) {
  const definition = statusLabels[status]
  return <Tag color={definition?.color}>{definition?.text ?? status}</Tag>
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('zh-TW', { style: 'currency', currency: 'TWD', maximumFractionDigits: 0 }).format(value)
}

function PolicyListPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState<PolicyListItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [keyword, setKeyword] = useState('')
  const [debouncedKeyword, setDebouncedKeyword] = useState('')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedKeyword(keyword), 300)
    return () => window.clearTimeout(timer)
  }, [keyword])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    void getPolicies(debouncedKeyword, status, page, 20, controller.signal)
      .then((response) => {
        setItems(response.items)
        setTotal(response.total)
      })
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取保單資料失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [debouncedKeyword, status, page, reloadKey])

  const columns = [
    { title: '保單號碼', dataIndex: 'policyNumber', key: 'policyNumber', onCell: () => responsiveCell('保單號碼'), render: (value: string) => <strong>{value}</strong> },
    { title: '被保險人', dataIndex: 'insuredName', key: 'insuredName', onCell: () => responsiveCell('被保險人'), render: (value: string | null, item: PolicyListItem) => value || item.customerName },
    { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
    { title: '保險期間', key: 'period', onCell: () => responsiveCell('保險期間'), render: (_: unknown, item: PolicyListItem) => `${item.insuranceStartDate} — ${item.insuranceEndDate}` },
    { title: '保費', dataIndex: 'totalPremium', key: 'totalPremium', align: 'right' as const, onCell: () => responsiveCell('保費'), render: formatMoney },
    { title: '狀態', dataIndex: 'status', key: 'status', onCell: () => responsiveCell('狀態'), render: renderStatus },
    { title: '操作', key: 'actions', onCell: () => responsiveCell('操作'), render: (_: unknown, item: PolicyListItem) => <Button type="link" icon={<FileSearchOutlined />} onClick={() => navigate(`/policies/${item.id}`)}>查看／文件</Button> },
  ]

  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenState requiredRole="具保單資料查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return <ErrorState title="保單資料讀取失敗" message={error.message} onRetry={() => setReloadKey((current) => current + 1)} />
  }

  return (
    <ListPageShell
      title="出單／保單查詢"
      searchPlaceholder="搜尋保單號碼、被保險人或車牌"
      keyword={keyword}
      onKeywordChange={(value) => { setKeyword(value); setPage(1) }}
      filters={(
        <Select
          value={status || undefined}
          allowClear
          placeholder="全部狀態"
          style={{ minWidth: 160 }}
          onChange={(value) => { setStatus(value || ''); setPage(1) }}
          options={Object.entries(statusLabels).map(([value, definition]) => ({ value, label: definition.text }))}
        />
      )}
      page={page}
      pageSize={20}
      total={total}
      onPageChange={setPage}
    >
      {loading && items.length === 0 ? (
        <LoadingState />
      ) : items.length === 0 ? (
        <EmptyState description="目前沒有符合條件的保單資料" />
      ) : (
        <Card styles={{ body: { padding: 0 } }}>
          <Table
            className="responsive-data-table"
            rowKey="id"
            columns={columns}
            dataSource={items}
            loading={loading}
            pagination={false}
            scroll={{ x: 1000 }}
          />
        </Card>
      )}
    </ListPageShell>
  )
}

export default PolicyListPage
