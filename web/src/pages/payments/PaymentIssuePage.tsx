import { useEffect, useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  App,
  Button,
  Card,
  Descriptions,
  Input,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd'
import { FileDoneOutlined, ReloadOutlined, SaveOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import {
  confirmPayment,
  createPayment,
  getPayments,
  getPolicyByApplication,
  issuePolicy,
  updatePayment,
} from '../../api/payments'
import type { Payment, PolicySummary } from '../../api/payments'
import { getApplications } from '../../api/applications'
import type { InsuranceApplication } from '../../api/applications'
import { getUnderwritingQueue } from '../../api/underwritingQueue'
import type { UnderwritingQueueItem } from '../../api/underwritingQueue'
import { ApiError } from '../../api/http'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'
import { queryKeys } from '../../query/queryKeys'

const frequencyOptions = [
  { value: 'annual', label: '年繳' },
  { value: 'semiannual', label: '半年繳' },
  { value: 'quarterly', label: '季繳' },
]

const paymentMethodOptions = [
  { value: 'credit_card', label: '信用卡' },
  { value: 'atm', label: 'ATM 轉帳' },
  { value: 'convenience_store', label: '超商代收' },
  { value: 'cash_counter', label: '現金／臨櫃' },
]

const paymentStatus: Record<string, { text: string; color: string }> = {
  pending: { text: '待繳費', color: 'warning' },
  paid: { text: '已收款', color: 'success' },
  overdue: { text: '逾期未繳', color: 'error' },
}

const lifecycleStatus: Record<string, { text: string; color: string }> = {
  pending_payment: { text: '待繳費', color: 'warning' },
  issued: { text: '已進入出單', color: 'processing' },
  cancelled: { text: '已取消', color: 'error' },
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('zh-TW', {
    style: 'currency',
    currency: 'TWD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDateTime(value: string | null) {
  if (!value) return '—'
  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

function renderStatus(status: string, definitions: Record<string, { text: string; color: string }>) {
  const definition = definitions[status]
  return <Tag color={definition?.color}>{definition?.text ?? status}</Tag>
}

function PaymentIssuePage() {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [createMethods, setCreateMethods] = useState<Record<number, 'credit_card' | 'atm' | 'convenience_store' | 'cash_counter'>>({})
  const [creatingPaymentId, setCreatingPaymentId] = useState<number | null>(null)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [keyword, setKeyword] = useState('')
  const [saving, setSaving] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [issuing, setIssuing] = useState(false)
  const [paymentFrequency, setPaymentFrequency] = useState<'annual' | 'semiannual' | 'quarterly'>('annual')
  const [paymentMethod, setPaymentMethod] = useState<'credit_card' | 'atm' | 'convenience_store' | 'cash_counter'>('credit_card')
  const [note, setNote] = useState('')
  const [referenceNumber, setReferenceNumber] = useState('')

  const paymentsQuery = useQuery({
    queryKey: queryKeys.payments.all,
    queryFn: ({ signal }) => getPayments(signal),
  })
  const approvedQuery = useQuery({
    queryKey: queryKeys.underwritingQueue.list('', 'approved', 1, 100),
    queryFn: ({ signal }) => getUnderwritingQueue('', 'approved', 1, 100, signal),
  })
  const autoApprovedQuery = useQuery({
    queryKey: queryKeys.underwritingQueue.list('', 'auto_approved', 1, 100),
    queryFn: ({ signal }) => getUnderwritingQueue('', 'auto_approved', 1, 100, signal),
  })
  const applicationsQuery = useQuery({
    queryKey: queryKeys.applications.all,
    queryFn: ({ signal }) => getApplications(signal),
  })
  const items: Payment[] = paymentsQuery.data ?? []
  const approvedCases: UnderwritingQueueItem[] = [
    ...(approvedQuery.data?.items ?? []),
    ...(autoApprovedQuery.data?.items ?? []),
  ]
  const applications: InsuranceApplication[] = applicationsQuery.data ?? []
  const error = paymentsQuery.error ?? approvedQuery.error ?? autoApprovedQuery.error ?? applicationsQuery.error
  const loading = paymentsQuery.isPending || approvedQuery.isPending
    || autoApprovedQuery.isPending || applicationsQuery.isPending
  const preferredSelectedId = items.find((item) => item.status === 'pending')?.id
    ?? items.find((item) => item.status === 'paid')?.id
    ?? items[0]?.id
    ?? null
  const effectiveSelectedId = selectedId && items.some((item) => item.id === selectedId)
    ? selectedId
    : preferredSelectedId

  const selected = useMemo(
    () => items.find((item) => item.id === effectiveSelectedId) ?? null,
    [effectiveSelectedId, items],
  )
  const policyQuery = useQuery({
    queryKey: queryKeys.payments.policyByApplication(selected?.applicationId ?? 0),
    queryFn: () => getPolicyByApplication(selected!.applicationId),
    enabled: Boolean(selected),
  })
  const policy: PolicySummary | null = policyQuery.data ?? null

  const filteredItems = useMemo(() => {
    const value = keyword.trim().toLowerCase()
    if (!value) return items
    return items.filter((item) => [
      item.applicationNumber,
      item.paymentNumber,
      item.customerName,
      item.licensePlate,
    ].some((field) => field.toLowerCase().includes(value)))
  }, [items, keyword])

  const paymentApplicationIds = useMemo(() => new Set(items.map((item) => item.applicationId)), [items])
  const pendingPaymentCases = useMemo(
    () => approvedCases.filter((item) => !paymentApplicationIds.has(item.applicationId)),
    [approvedCases, paymentApplicationIds],
  )
  const applicationById = useMemo(
    () => new Map(applications.map((item) => [item.id, item])),
    [applications],
  )
  const pendingCount = items.filter((item) => item.status === 'pending').length
  const actionableCount = items.filter((item) => item.status === 'pending' || item.status === 'paid').length + pendingPaymentCases.length

  useEffect(() => {
    if (!selected) return

    setPaymentFrequency(selected.paymentFrequency as typeof paymentFrequency)
    setPaymentMethod(selected.paymentMethod as typeof paymentMethod)
    setNote(selected.note ?? '')
    setReferenceNumber(selected.referenceNumber ?? '')
  }, [selected])

  useEffect(() => {
    if (policyQuery.error) {
      void message.error(policyQuery.error.message || '查詢保單失敗')
    }
  }, [message, policyQuery.error])

  function replacePayment(updated: Payment) {
    queryClient.setQueryData<Payment[]>(queryKeys.payments.all, (current = []) => (
      current.map((item) => item.id === updated.id ? updated : item)
    ))
  }

  async function handleCreatePayment(item: UnderwritingQueueItem) {
    const application = applicationById.get(item.applicationId)
    const frequency = application?.paymentFrequency
    if (frequency !== 'annual' && frequency !== 'semiannual' && frequency !== 'quarterly') {
      void message.error('此案件缺少有效繳別，請先回投保申請確認資料。')
      return
    }

    setCreatingPaymentId(item.applicationId)
    try {
      const created = await createPayment({
        applicationId: item.applicationId,
        paymentFrequency: frequency,
        paymentMethod: createMethods[item.applicationId] ?? 'credit_card',
      })
      queryClient.setQueryData<Payment[]>(queryKeys.payments.all, (current = []) => [created, ...current])
      setSelectedId(created.id)
      void message.success(`已產生繳費單 ${created.paymentNumber}`)
    } catch (requestError) {
      void message.error(requestError instanceof Error ? requestError.message : '產生繳費單失敗')
    } finally {
      setCreatingPaymentId(null)
    }
  }

  async function handleSave() {
    if (!selected || selected.status !== 'pending') return
    setSaving(true)
    try {
      const updated = await updatePayment(selected.id, {
        paymentFrequency,
        paymentMethod,
        note: note.trim() || null,
      })
      replacePayment(updated)
      void message.success('付款設定已儲存')
    } catch (requestError) {
      void message.error(requestError instanceof Error ? requestError.message : '付款設定儲存失敗')
    } finally {
      setSaving(false)
    }
  }

  async function ensureIssued(target: Payment) {
    setIssuing(true)
    try {
      const result = await issuePolicy(target.applicationId)
      queryClient.setQueryData(queryKeys.payments.policyByApplication(target.applicationId), result)
      void queryClient.invalidateQueries({ queryKey: queryKeys.policies.all })
      void message.success(`已建立正式保單 ${result.policyNumber}`)
      return result
    } catch (requestError) {
      void message.error(requestError instanceof Error ? requestError.message : '正式出單失敗')
      return null
    } finally {
      setIssuing(false)
    }
  }

  async function handleConfirmAndIssue() {
    if (!selected || selected.status !== 'pending') return
    setConfirming(true)
    try {
      const confirmed = await confirmPayment(selected.id, {
        referenceNumber: referenceNumber.trim() || null,
        note: note.trim() || null,
      })
      replacePayment(confirmed)
      void message.success('收款確認完成，準備建立正式保單')
      await ensureIssued(confirmed)
    } catch (requestError) {
      void message.error(requestError instanceof Error ? requestError.message : '收款確認失敗')
    } finally {
      setConfirming(false)
    }
  }

  const columns = [
    { title: '案件編號', dataIndex: 'applicationNumber', key: 'applicationNumber', onCell: () => responsiveCell('案件編號'), render: (value: string) => <strong>{value}</strong> },
    { title: '客戶', dataIndex: 'customerName', key: 'customerName', onCell: () => responsiveCell('客戶') },
    { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
    { title: '應繳保費', dataIndex: 'amountDue', key: 'amountDue', align: 'right' as const, onCell: () => responsiveCell('應繳保費'), render: formatMoney },
    {
      title: '繳別',
      dataIndex: 'paymentFrequency',
      key: 'paymentFrequency',
      onCell: () => responsiveCell('繳別'),
      render: (value: string) => frequencyOptions.find((option) => option.value === value)?.label ?? value,
    },
    { title: '繳費狀態', dataIndex: 'status', key: 'status', onCell: () => responsiveCell('繳費狀態'), render: (value: string) => renderStatus(value, paymentStatus) },
    {
      title: '生命週期',
      dataIndex: 'policyLifecycleStatus',
      key: 'policyLifecycleStatus',
      onCell: () => responsiveCell('生命週期'),
      render: (value: string | null) => value ? renderStatus(value, lifecycleStatus) : '—',
    },
    {
      title: '操作',
      key: 'action',
      onCell: () => responsiveCell('操作'),
      render: (_: unknown, item: Payment) => (
        <Button type="link" onClick={() => setSelectedId(item.id)}>
          {item.status === 'pending' ? '收款' : '查看'}
        </Button>
      ),
    },
  ]

  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenState requiredRole="具繳費／出單資料查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return <ErrorState title="付款資料讀取失敗" message={error.message} onRetry={() => {
      void paymentsQuery.refetch()
      void approvedQuery.refetch()
      void autoApprovedQuery.refetch()
      void applicationsQuery.refetch()
    }} />
  }

  if (loading && items.length === 0) return <LoadingState rows={8} />

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card>
        <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
          <div>
            <Typography.Title level={3} style={{ margin: 0 }}>出單／繳費</Typography.Title>
            <Typography.Text type="secondary">核保通過後的收款與正式保單產製。</Typography.Text>
          </div>
          <Space>
            <Tag color="warning">待繳費 {pendingCount} 件</Tag>
            <Tag color="processing">可處理 {actionableCount} 件</Tag>
            <Button icon={<ReloadOutlined />} onClick={() => {
              void paymentsQuery.refetch()
              void approvedQuery.refetch()
              void autoApprovedQuery.refetch()
              void applicationsQuery.refetch()
            }}>重新整理</Button>
          </Space>
        </Space>
      </Card>

      {pendingPaymentCases.length > 0 ? (
        <Card title="待建立繳費單" extra={<Tag color="gold">{pendingPaymentCases.length} 件</Tag>}>
          <Table
            className="responsive-data-table"
            rowKey="applicationId"
            pagination={false}
            dataSource={pendingPaymentCases}
            scroll={{ x: 850 }}
            columns={[
              { title: '案件編號', dataIndex: 'applicationNumber', key: 'applicationNumber', onCell: () => responsiveCell('案件編號'), render: (value: string) => <strong>{value}</strong> },
              { title: '被保險人', dataIndex: 'insuredName', key: 'insuredName', onCell: () => responsiveCell('被保險人') },
              { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
              { title: '應繳保費', dataIndex: 'totalPremium', key: 'totalPremium', align: 'right' as const, onCell: () => responsiveCell('應繳保費'), render: formatMoney },
              {
                title: '繳別',
                key: 'frequency',
                onCell: () => responsiveCell('繳別'),
                render: (_: unknown, item: UnderwritingQueueItem) => {
                  const value = applicationById.get(item.applicationId)?.paymentFrequency ?? ''
                  return frequencyOptions.find((option) => option.value === value)?.label ?? (value || '未設定')
                },
              },
              {
                title: '付款方式',
                key: 'paymentMethod',
                onCell: () => responsiveCell('付款方式'),
                render: (_: unknown, item: UnderwritingQueueItem) => (
                  <Select
                    value={createMethods[item.applicationId] ?? 'credit_card'}
                    options={paymentMethodOptions}
                    style={{ minWidth: 150 }}
                    onClick={(event) => event.stopPropagation()}
                    onChange={(value) => setCreateMethods((current) => ({ ...current, [item.applicationId]: value }))}
                  />
                ),
              },
              {
                title: '操作',
                key: 'create',
                onCell: () => responsiveCell('操作'),
                render: (_: unknown, item: UnderwritingQueueItem) => (
                  <Button
                    type="primary"
                    loading={creatingPaymentId === item.applicationId}
                    onClick={() => void handleCreatePayment(item)}
                  >
                    產生繳費單
                  </Button>
                ),
              },
            ]}
          />
        </Card>
      ) : null}

      {selected ? (
        <div className="payment-issue-grid">
          <Card title={<Space><span>繳費作業</span><Typography.Text type="secondary">{selected.applicationNumber}</Typography.Text></Space>}>
            <Descriptions column={{ xs: 1, sm: 2 }} size="small" bordered>
              <Descriptions.Item label="客戶">{selected.customerName}</Descriptions.Item>
              <Descriptions.Item label="車牌">{selected.licensePlate}</Descriptions.Item>
              <Descriptions.Item label="報價單">{selected.quoteNumber}</Descriptions.Item>
              <Descriptions.Item label="應繳保費"><strong>{formatMoney(selected.amountDue)}</strong></Descriptions.Item>
              <Descriptions.Item label="付款狀態">{renderStatus(selected.status, paymentStatus)}</Descriptions.Item>
              <Descriptions.Item label="生命週期">{selected.policyLifecycleStatus ? renderStatus(selected.policyLifecycleStatus, lifecycleStatus) : '—'}</Descriptions.Item>
              <Descriptions.Item label="繳費期限">{formatDateTime(selected.dueAtUtc)}</Descriptions.Item>
              <Descriptions.Item label="收款時間">{formatDateTime(selected.paidAtUtc)}</Descriptions.Item>
            </Descriptions>

            {selected.status === 'pending' ? (
              <Space direction="vertical" size="middle" style={{ width: '100%', marginTop: 20 }}>
                <div>
                  <Typography.Text strong>繳別</Typography.Text>
                  <Select
                    value={paymentFrequency}
                    options={frequencyOptions}
                    onChange={setPaymentFrequency}
                    style={{ width: '100%', marginTop: 8 }}
                  />
                </div>
                <div>
                  <Typography.Text strong>付款方式</Typography.Text>
                  <Radio.Group
                    value={paymentMethod}
                    onChange={(event) => setPaymentMethod(event.target.value as typeof paymentMethod)}
                    style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginTop: 8 }}
                  >
                    {paymentMethodOptions.map((option) => <Radio.Button key={option.value} value={option.value}>{option.label}</Radio.Button>)}
                  </Radio.Group>
                </div>
                <Input.TextArea value={note} onChange={(event) => setNote(event.target.value)} placeholder="付款備註（選填）" rows={3} />
                <Input value={referenceNumber} onChange={(event) => setReferenceNumber(event.target.value)} placeholder="收款參考編號（選填）" />
                <Alert type="info" showIcon message="收款確認後，系統會再呼叫正式出單 API 建立保單；若出單失敗，可在已收款狀態重試。" />
                <Space wrap style={{ justifyContent: 'flex-end', width: '100%' }}>
                  <Button icon={<SaveOutlined />} loading={saving} disabled={confirming || issuing} onClick={() => void handleSave()}>儲存付款設定</Button>
                  <Button type="primary" icon={<FileDoneOutlined />} loading={confirming || issuing} disabled={saving} onClick={() => void handleConfirmAndIssue()}>確認收款並出單</Button>
                </Space>
              </Space>
            ) : selected.status === 'paid' ? (
              <Space direction="vertical" size="middle" style={{ width: '100%', marginTop: 20 }}>
                <Alert type="success" showIcon message="收款已完成" description={policy ? `正式保單：${policy.policyNumber}` : '尚未查到正式保單，可重新執行出單。'} />
                <Space wrap style={{ justifyContent: 'flex-end', width: '100%' }}>
                  {policy ? (
                    <Button type="primary" onClick={() => navigate(`/policies/${policy.id}`)}>前往保單詳情</Button>
                  ) : (
                    <Button type="primary" loading={issuing} onClick={() => void ensureIssued(selected)}>建立正式保單</Button>
                  )}
                </Space>
              </Space>
            ) : (
              <Alert style={{ marginTop: 20 }} type="error" showIcon message="此付款案件已逾期取消，無法再執行收款或出單。" />
            )}
          </Card>

          <Card title="出單狀態">
            {policy ? (
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Alert type="success" showIcon message="正式保單已建立" />
                <Descriptions column={1} size="small" bordered>
                  <Descriptions.Item label="保單號">{policy.policyNumber}</Descriptions.Item>
                  <Descriptions.Item label="狀態">{policy.status}</Descriptions.Item>
                  <Descriptions.Item label="出單時間">{formatDateTime(policy.issuedAtUtc)}</Descriptions.Item>
                </Descriptions>
                <Button type="primary" block onClick={() => navigate(`/policies/${policy.id}`)}>前往保單管理</Button>
              </Space>
            ) : (
              <EmptyState description={selected.status === 'overdue' ? '此案件已取消，不會建立保單' : '目前尚未建立正式保單'} />
            )}
          </Card>
        </div>
      ) : (
        <EmptyState description="目前沒有付款案件" />
      )}

      <Card title="待出單清單" extra={<Input.Search allowClear placeholder="搜尋案件、付款單、客戶或車牌" value={keyword} onChange={(event) => setKeyword(event.target.value)} style={{ width: 300 }} />}>
        <Table
          className="responsive-data-table"
          rowKey="id"
          columns={columns}
          dataSource={filteredItems}
          loading={loading}
          pagination={{ pageSize: 10, showSizeChanger: false }}
          rowClassName={(item) => item.id === selectedId ? 'payment-selected-row' : ''}
          onRow={(item) => ({ onClick: () => setSelectedId(item.id) })}
          scroll={{ x: 1100 }}
        />
      </Card>
    </Space>
  )
}

export default PaymentIssuePage
