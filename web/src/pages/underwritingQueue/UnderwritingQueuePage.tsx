import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  App,
  Button,
  Card,
  Col,
  Input,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table'
import { ApiError } from '../../api/http'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  ReloadOutlined,
  StopOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import {
  approveUnderwritingCase,
  getUnderwritingCase,
  getUnderwritingQueue,
  reevaluateUnderwritingCase,
  rejectUnderwritingCase,
  requestUnderwritingDocuments,
  syncUnderwritingQueue,
} from '../../api/underwritingQueue'
import type {
  UnderwritingCaseDetail,
  UnderwritingQueueItem,
  UnderwritingQueueList,
  UnderwritingRuleResult,
} from '../../api/underwritingQueue'
import { queryKeys } from '../../query/queryKeys'

const currencyFormatter = new Intl.NumberFormat('zh-TW', {
  style: 'currency',
  currency: 'TWD',
  maximumFractionDigits: 0,
})

const statusLabels: Record<string, string> = {
  pending_review: '待覆核',
  pending_documents: '待補件',
  approved: '已核准',
  rejected: '已退件',
  auto_approved: '自動核准',
  auto_rejected: '自動退件',
}

const statusColors: Record<string, string> = {
  pending_review: 'orange',
  pending_documents: 'blue',
  approved: 'green',
  rejected: 'red',
  auto_approved: 'green',
  auto_rejected: 'red',
}

const riskLabels: Record<string, string> = {
  low: '低',
  medium: '中',
  high: '高',
}

const riskColors: Record<string, string> = {
  low: 'green',
  medium: 'orange',
  high: 'red',
}

function formatDateTime(value: string | null | undefined): string {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-TW', { hour12: false })
}

function resultVisual(result: UnderwritingRuleResult) {
  if (result.status === 'passed') {
    return { icon: <CheckCircleOutlined />, className: 'underwriting-rule-pass', label: '通過' }
  }
  if (result.status === 'unavailable') {
    return { icon: <ExclamationCircleOutlined />, className: 'underwriting-rule-unavailable', label: '無法判斷' }
  }
  if (result.action === 'manual_review') {
    return { icon: <ExclamationCircleOutlined />, className: 'underwriting-rule-warn', label: '警示' }
  }
  return { icon: <StopOutlined />, className: 'underwriting-rule-fail', label: '阻擋' }
}

function UnderwritingQueuePage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [keywordInput, setKeywordInput] = useState('')
  const [keyword, setKeyword] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [opinion, setOpinion] = useState('')
  const queueQuery = useQuery({
    queryKey: queryKeys.underwritingQueue.list(keyword, status, page, pageSize),
    queryFn: ({ signal }) => getUnderwritingQueue(keyword, status, page, pageSize, signal),
  })
  const queue: UnderwritingQueueList | null = queueQuery.data ?? null
  const detailQuery = useQuery({
    queryKey: queryKeys.underwritingQueue.detail(selectedId ?? 0),
    queryFn: ({ signal }) => getUnderwritingCase(selectedId!, signal),
    enabled: selectedId != null,
  })
  const detail = detailQuery.data ?? null
  const syncMutation = useMutation({
    mutationFn: syncUnderwritingQueue,
    onSuccess: async (result) => {
      message.success(`同步完成：處理 ${result.processedCount} 件，新建 ${result.createdCount} 件。`)
      await queryClient.invalidateQueries({ queryKey: queryKeys.underwritingQueue.all })
    },
  })
  const decisionMutation = useMutation({
    mutationFn: (action: () => Promise<UnderwritingCaseDetail>) => action(),
    onSuccess: async (next) => {
      queryClient.setQueryData(queryKeys.underwritingQueue.detail(next.id), next)
      setOpinion(next.opinion ?? '')
      await queryClient.invalidateQueries({ queryKey: queryKeys.underwritingQueue.all })
    },
  })

  useEffect(() => {
    const items = queue?.items ?? []
    if (items.length === 0) setSelectedId(null)
    else if (selectedId == null || !items.some((item) => item.id === selectedId)) setSelectedId(items[0].id)
  }, [queue, selectedId])

  useEffect(() => {
    setOpinion(detail?.opinion ?? '')
  }, [detail?.id, detail?.opinion])

  const handleSync = async () => {
    try {
      await syncMutation.mutateAsync()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '同步核保佇列失敗。')
    }
  }

  const runDecision = async (
    label: string,
    action: () => Promise<UnderwritingCaseDetail>,
  ) => {
    try {
      await decisionMutation.mutateAsync(action)
      message.success(`${label}完成。`)
    } catch (error) {
      message.error(error instanceof Error ? error.message : `${label}失敗。`)
    }
  }

  const handleRequestDocuments = async () => {
    if (!opinion.trim()) {
      message.warning('要求補件時請填寫核保意見。')
      return
    }
    if (!detail) return
    await runDecision('要求補件', () => requestUnderwritingDocuments(detail.id, opinion.trim()))
  }

  const handleReject = async () => {
    if (!opinion.trim()) {
      message.warning('退件時請填寫核保意見。')
      return
    }
    if (!detail) return
    await runDecision('退件', () => rejectUnderwritingCase(detail.id, opinion.trim()))
  }

  const columns = useMemo<ColumnsType<UnderwritingQueueItem>>(() => [
    {
      title: '案件編號',
      dataIndex: 'applicationNumber',
      key: 'applicationNumber',
      onCell: () => responsiveCell('案件編號'),
      render: (value: string) => <Typography.Text strong>{value}</Typography.Text>,
    },
    { title: '被保險人', dataIndex: 'insuredName', key: 'insuredName', onCell: () => responsiveCell('被保險人') },
    { title: '險種', dataIndex: 'productSummary', key: 'productSummary', onCell: () => responsiveCell('險種') },
    {
      title: '風險等級',
      dataIndex: 'riskLevel',
      key: 'riskLevel',
      onCell: () => responsiveCell('風險等級'),
      render: (value: string) => <Tag color={riskColors[value]}>{riskLabels[value] ?? value}</Tag>,
    },
    {
      title: '狀態',
      dataIndex: 'status',
      key: 'status',
      onCell: () => responsiveCell('狀態'),
      render: (value: string) => <Tag color={statusColors[value]}>{statusLabels[value] ?? value}</Tag>,
    },
    {
      title: '操作',
      key: 'action',
      onCell: () => responsiveCell('操作'),
      render: (_, record) => (
        <Button type="link" onClick={() => setSelectedId(record.id)}>
          審核
        </Button>
      ),
    },
  ], [])

  const handleTableChange = (pagination: TablePaginationConfig) => {
    setPage(pagination.current ?? 1)
    setPageSize(pagination.pageSize ?? 20)
  }

  const canReview = detail?.status === 'pending_review'
  const canRequestDocuments = detail?.status === 'pending_review' || detail?.status === 'pending_documents'
  const canReevaluate = detail?.status === 'pending_documents'

  if (queueQuery.isPending && !queue) {
    return <LoadingState rows={8} />
  }

  if (queueQuery.error instanceof ApiError && queueQuery.error.status === 403) {
    return <ForbiddenState requiredRole="具核保資料查看權限的角色" contact="系統管理員" />
  }

  if (queueQuery.error) {
    return <ErrorState title="核保佇列載入失敗" message={queueQuery.error.message} onRetry={() => void queueQuery.refetch()} />
  }

  return (
    <div className="underwriting-queue-page">
      <div className="underwriting-queue-intro">
        <div>
          <Typography.Title level={2}>核保作業</Typography.Title>
          <Typography.Text type="secondary">核保佇列、自動規則命中結果與人工覆核。</Typography.Text>
        </div>
        <Button type="primary" icon={<SyncOutlined />} loading={syncMutation.isPending} onClick={handleSync}>
          同步送審案件
        </Button>
      </div>

      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} xl={6}><Card><Statistic title="待核保" value={queue?.stats.pendingReviewCount ?? 0} /></Card></Col>
        <Col xs={24} sm={12} xl={6}><Card><Statistic title="自動核准" value={queue?.stats.autoApprovedCount ?? 0} /></Card></Col>
        <Col xs={24} sm={12} xl={6}><Card><Statistic title="待補件" value={queue?.stats.pendingDocumentsCount ?? 0} /></Card></Col>
        <Col xs={24} sm={12} xl={6}><Card><Statistic title="退件" value={queue?.stats.rejectedCount ?? 0} /></Card></Col>
      </Row>

      {queue && queue.items.length === 0 ? (
        <EmptyState
          description={keyword || status ? '目前篩選條件下沒有核保案件。' : '目前沒有核保案件。'}
          action={keyword || status ? (
            <Button onClick={() => {
              setKeywordInput('')
              setKeyword('')
              setStatus('')
              setPage(1)
            }}>清除篩選</Button>
          ) : undefined}
        />
      ) : (
      <div className="underwriting-queue-grid">
        <Card title="核保佇列" className="underwriting-queue-card">
          <div className="underwriting-queue-toolbar">
            <Input.Search
              aria-label="搜尋核保案件"
              allowClear
              value={keywordInput}
              placeholder="案件／被保險人／車牌"
              onChange={(event) => setKeywordInput(event.target.value)}
              onSearch={(value) => {
                setKeyword(value)
                setPage(1)
              }}
            />
            <Select
              aria-label="核保狀態"
              value={status}
              className="underwriting-status-select"
              onChange={(value) => {
                setStatus(value)
                setPage(1)
              }}
              options={[
                { value: '', label: '全部狀態' },
                { value: 'pending_review', label: '待覆核' },
                { value: 'pending_documents', label: '待補件' },
                { value: 'approved', label: '已核准' },
                { value: 'rejected', label: '已退件' },
                { value: 'auto_approved', label: '自動核准' },
                { value: 'auto_rejected', label: '自動退件' },
              ]}
            />
            <Button icon={<ReloadOutlined />} onClick={() => void queueQuery.refetch()}>重新整理</Button>
          </div>

          <Table<UnderwritingQueueItem>
            className="responsive-data-table"
            rowKey="id"
            loading={queueQuery.isFetching}
            columns={columns}
            dataSource={queue?.items ?? []}
            rowClassName={(record) => record.id === selectedId ? 'underwriting-selected-row' : ''}
            onRow={(record) => ({ onClick: () => setSelectedId(record.id) })}
            scroll={{ x: 760 }}
            pagination={{
              current: queue?.page ?? page,
              pageSize: queue?.pageSize ?? pageSize,
              total: queue?.total ?? 0,
              showSizeChanger: true,
              showTotal: (total) => `共 ${total} 件`,
            }}
            onChange={handleTableChange}
          />
        </Card>

        <Card
          className="underwriting-queue-card underwriting-detail-card"
          title={detail ? `規則命中結果 ${detail.applicationNumber}` : '規則命中結果'}
          extra={detail ? <Tag color={statusColors[detail.status]}>{statusLabels[detail.status] ?? detail.status}</Tag> : null}
        >
          {detailQuery.isPending ? (
            <LoadingState rows={4} />
          ) : detailQuery.error instanceof ApiError && detailQuery.error.status === 403 ? (
            <ForbiddenState requiredRole="具核保案件查看權限的角色" contact="系統管理員" />
          ) : detailQuery.error ? (
            <ErrorState
              title="核保案件載入失敗"
              message={detailQuery.error.message}
              onRetry={() => void detailQuery.refetch()}
            />
          ) : !detail ? (
            <EmptyState description="請從左側選擇核保案件" />
          ) : (
            <Space direction="vertical" size={16} className="full-width">
              <div className="underwriting-case-summary">
                <Typography.Text strong>{detail.insuredName}</Typography.Text>
                <Typography.Text type="secondary">車牌 {detail.licensePlate} · {detail.productSummary}</Typography.Text>
                <Typography.Text type="secondary">保費 {currencyFormatter.format(detail.totalPremium)}</Typography.Text>
                <Typography.Text type="secondary">送出 {formatDateTime(detail.submittedAtUtc)}</Typography.Text>
              </div>

              <div className="underwriting-rule-list">
                {detail.evaluation.results.map((result) => {
                  const visual = resultVisual(result)
                  return (
                    <div key={result.id} className={`underwriting-rule-item ${visual.className}`}>
                      <div className="underwriting-rule-icon">{visual.icon}</div>
                      <div className="underwriting-rule-body">
                        <div className="underwriting-rule-heading">
                          <Typography.Text strong>{result.ruleName}</Typography.Text>
                          <Tag>{visual.label}</Tag>
                        </div>
                        <Typography.Text type="secondary">{result.ruleCode} · {result.message}</Typography.Text>
                        <Typography.Text>{result.detail}</Typography.Text>
                      </div>
                    </div>
                  )
                })}
              </div>

              <Input.TextArea
                aria-label="覆核說明"
                rows={4}
                maxLength={1000}
                showCount
                value={opinion}
                placeholder="請輸入覆核說明（退件／要求補件必填）"
                onChange={(event) => setOpinion(event.target.value)}
                disabled={!canReview && !canRequestDocuments}
              />

              <div className="underwriting-decision-actions">
                <Button
                  type="primary"
                  disabled={!canReview}
                  loading={decisionMutation.isPending}
                  onClick={() => detail && void runDecision('核准', () => approveUnderwritingCase(detail.id, opinion.trim()))}
                >
                  核准
                </Button>
                <Button disabled={!canRequestDocuments} loading={decisionMutation.isPending} onClick={() => void handleRequestDocuments()}>
                  要求補件
                </Button>
                <Button danger disabled={!canReview} loading={decisionMutation.isPending} onClick={() => void handleReject()}>
                  退件
                </Button>
                {canReevaluate && (
                  <Button
                    icon={<ReloadOutlined />}
                    loading={decisionMutation.isPending}
                    onClick={() => detail && void runDecision('重新評估', () => reevaluateUnderwritingCase(detail.id))}
                  >
                    重新評估
                  </Button>
                )}
              </div>

              {detail.opinion && !canReview && !canRequestDocuments && (
                <Alert type="info" showIcon message="核保意見" description={detail.opinion} />
              )}
            </Space>
          )}
        </Card>
      </div>
      )}
    </div>
  )
}

export default UnderwritingQueuePage
