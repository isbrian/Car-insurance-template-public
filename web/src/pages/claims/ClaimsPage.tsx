import { useEffect, useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  App,
  Button,
  Card,
  Descriptions,
  Form,
  Input,
  InputNumber,
  Modal,
  Pagination,
  Select,
  Space,
  Steps,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import {
  CheckCircleOutlined,
  FileSearchOutlined,
  ReloadOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons'
import {
  approveClaimAmount,
  completeClaimLiability,
  completeClaimPayment,
  denyClaim,
  getClaim,
  getClaims,
  transitionClaim,
} from '../../api/claims'
import type { Claim, ClaimStatus } from '../../api/claims'
import { ApiError } from '../../api/http'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'
import { queryKeys } from '../../query/queryKeys'

const pageSize = 10

const statusDefinitions: Record<ClaimStatus, { label: string; color: string }> = {
  pending_intake: { label: '待受理', color: 'orange' },
  reviewing: { label: '審核中', color: 'processing' },
  pending_documents: { label: '待補件', color: 'gold' },
  calculating: { label: '理算中', color: 'purple' },
  pending_payment: { label: '待給付', color: 'cyan' },
  closed: { label: '已結案', color: 'success' },
  denied: { label: '拒賠結案', color: 'error' },
}

const mainProgressSteps = [
  { title: '待受理' },
  { title: '審核' },
  { title: '理算' },
  { title: '待給付' },
  { title: '結案' },
]

const progressIndex: Record<ClaimStatus, number> = {
  pending_intake: 0,
  reviewing: 1,
  pending_documents: 1,
  calculating: 2,
  pending_payment: 3,
  closed: 4,
  denied: 1,
}

type TransitionKind = 'accept' | 'request_documents' | 'documents_complete'
type SettlementKind = 'liability' | 'approve' | 'pay' | 'deny'

type TransitionFormValues = {
  assignedAdjuster?: string
  reason?: string
}

type SettlementFormValues = {
  decision?: string
  liabilityPercentage?: number
  approvedAmount?: number
  paymentMethod?: string
  reason?: string
}

function formatMoney(value: number | null) {
  if (value == null) return '-'
  return new Intl.NumberFormat('zh-TW', {
    style: 'currency',
    currency: 'TWD',
    maximumFractionDigits: 0,
  }).format(value)
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-TW', { hour12: false })
}

function renderStatus(status: ClaimStatus) {
  const definition = statusDefinitions[status]
  return <Tag color={definition.color}>{definition.label}</Tag>
}

function ClaimsPage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState('')
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const [transitionKind, setTransitionKind] = useState<TransitionKind | null>(null)
  const [transitionForm] = Form.useForm<TransitionFormValues>()
  const [settlementKind, setSettlementKind] = useState<SettlementKind | null>(null)
  const [settlementForm] = Form.useForm<SettlementFormValues>()
  const claimsQuery = useQuery({
    queryKey: queryKeys.claims.list(status),
    queryFn: ({ signal }) => getClaims(status, signal),
  })
  const claims: Claim[] = claimsQuery.data ?? []
  const detailQuery = useQuery({
    queryKey: queryKeys.claims.detail(selectedId ?? 0),
    queryFn: ({ signal }) => getClaim(selectedId!, signal),
    enabled: selectedId != null,
  })
  const detail = detailQuery.data ?? null
  const syncClaim = async (next: Claim) => {
    queryClient.setQueryData(queryKeys.claims.detail(next.id), next)
    queryClient.setQueryData<Claim[]>(queryKeys.claims.list(status), (current = []) => (
      current.map((claim) => claim.id === next.id ? next : claim)
    ))
    await queryClient.invalidateQueries({ queryKey: queryKeys.claims.all })
  }
  const transitionMutation = useMutation({
    mutationFn: ({ id, request }: { id: number; request: Parameters<typeof transitionClaim>[1] }) => transitionClaim(id, request),
    onSuccess: syncClaim,
  })
  const settlementMutation = useMutation({
    mutationFn: (action: () => Promise<Claim>) => action(),
    onSuccess: syncClaim,
  })

  useEffect(() => {
    if (claims.length === 0) setSelectedId(null)
    else if (selectedId == null || !claims.some((claim) => claim.id === selectedId)) setSelectedId(claims[0].id)
  }, [claims, selectedId])

  const filteredClaims = useMemo(() => {
    const normalized = keyword.trim().toLowerCase()
    if (!normalized) return claims
    return claims.filter((claim) => [
      claim.claimNumber,
      claim.policyNumber,
      claim.claimantName,
      claim.licensePlate,
      claim.assignedAdjuster ?? '',
    ].some((value) => value.toLowerCase().includes(normalized)))
  }, [claims, keyword])

  useEffect(() => {
    const lastPage = Math.max(1, Math.ceil(filteredClaims.length / pageSize))
    if (page > lastPage) setPage(lastPage)
  }, [filteredClaims.length, page])

  const pagedClaims = useMemo(
    () => filteredClaims.slice((page - 1) * pageSize, page * pageSize),
    [filteredClaims, page],
  )

  const openTransition = (kind: TransitionKind) => {
    setTransitionKind(kind)
    transitionForm.resetFields()
    if (kind === 'accept' && detail?.assignedAdjuster) {
      transitionForm.setFieldValue('assignedAdjuster', detail.assignedAdjuster)
    }
  }

  const closeTransition = () => {
    setTransitionKind(null)
    transitionForm.resetFields()
  }

  const submitTransition = async () => {
    if (!detail || !transitionKind) return

    const values = await transitionForm.validateFields()
    const targetStatus: ClaimStatus = transitionKind === 'request_documents'
      ? 'pending_documents'
      : 'reviewing'

    try {
      await transitionMutation.mutateAsync({
        id: detail.id,
        request: {
          targetStatus,
          assignedAdjuster: transitionKind === 'accept' ? values.assignedAdjuster?.trim() : undefined,
          reason: values.reason?.trim() || undefined,
        },
      })
      message.success(
        transitionKind === 'accept'
          ? '案件已受理並指派理賠員。'
          : transitionKind === 'request_documents'
            ? '案件已轉為待補件。'
            : '補件已完成，案件回到審核中。',
      )
      closeTransition()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '更新案件狀態失敗。')
    }
  }

  const openSettlement = (kind: SettlementKind) => {
    setSettlementKind(kind)
    settlementForm.resetFields()
    if (kind === 'approve' && detail?.claimedAmount != null) {
      settlementForm.setFieldValue('approvedAmount', detail.claimedAmount)
    }
  }

  const closeSettlement = () => {
    setSettlementKind(null)
    settlementForm.resetFields()
  }

  const submitSettlement = async () => {
    if (!detail || !settlementKind) return
    const values = await settlementForm.validateFields()

    try {
      let action: () => Promise<Claim>
      if (settlementKind === 'liability') {
        action = () => completeClaimLiability(detail.id, {
          decision: values.decision?.trim() || '',
          liabilityPercentage: values.liabilityPercentage ?? 0,
          reason: values.reason?.trim() || undefined,
        })
      } else if (settlementKind === 'approve') {
        action = () => approveClaimAmount(detail.id, {
          approvedAmount: values.approvedAmount ?? 0,
          reason: values.reason?.trim() || undefined,
        })
      } else if (settlementKind === 'pay') {
        action = () => completeClaimPayment(detail.id, {
          paymentMethod: values.paymentMethod?.trim() || '',
          reason: values.reason?.trim() || undefined,
        })
      } else {
        action = () => denyClaim(detail.id, { reason: values.reason?.trim() || '' })
      }
      await settlementMutation.mutateAsync(action)

      message.success(
        settlementKind === 'liability'
          ? '責任認定已完成，案件進入理算中。'
          : settlementKind === 'approve'
            ? '理賠金額已核定，案件進入待給付。'
            : settlementKind === 'pay'
              ? '給付已完成，案件已結案。'
              : '案件已拒賠結案。',
      )
      closeSettlement()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '理賠作業失敗。')
    }
  }

  const columns = useMemo<ColumnsType<Claim>>(() => [
    {
      title: '案件編號',
      dataIndex: 'claimNumber',
      key: 'claimNumber',
      onCell: () => responsiveCell('案件編號'),
      render: (value: string) => <Typography.Text strong>{value}</Typography.Text>,
    },
    { title: '保單號碼', dataIndex: 'policyNumber', key: 'policyNumber', onCell: () => responsiveCell('保單號碼') },
    { title: '申請人', dataIndex: 'claimantName', key: 'claimantName', onCell: () => responsiveCell('申請人') },
    { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
    { title: '事故日期', dataIndex: 'accidentDate', key: 'accidentDate', onCell: () => responsiveCell('事故日期') },
    {
      title: '申請金額',
      dataIndex: 'claimedAmount',
      key: 'claimedAmount',
      align: 'right',
      onCell: () => responsiveCell('申請金額'),
      render: (value: number | null) => formatMoney(value),
    },
    {
      title: '狀態',
      dataIndex: 'status',
      key: 'status',
      onCell: () => responsiveCell('狀態'),
      render: (value: ClaimStatus) => renderStatus(value),
    },
    {
      title: '負責人',
      dataIndex: 'assignedAdjuster',
      key: 'assignedAdjuster',
      onCell: () => responsiveCell('負責人'),
      render: (value: string | null) => value || '-',
    },
    {
      title: '操作',
      key: 'actions',
      fixed: 'right',
      onCell: () => responsiveCell('操作'),
      render: (_: unknown, record: Claim) => (
        <Button type="link" icon={<FileSearchOutlined />} onClick={() => setSelectedId(record.id)}>
          查看
        </Button>
      ),
    },
  ], [])

  if (claimsQuery.error instanceof ApiError && claimsQuery.error.status === 403) {
    return <ForbiddenState requiredRole="具理賠資料查看權限的角色" contact="系統管理員" />
  }

  if (claimsQuery.error) {
    return (
      <ErrorState
        title="理賠案件讀取失敗"
        message={claimsQuery.error.message}
        onRetry={() => void claimsQuery.refetch()}
      />
    )
  }

  const modalTitle = transitionKind === 'accept'
    ? '受理案件'
    : transitionKind === 'request_documents'
      ? '要求補件'
      : '確認補件完成'

  const settlementTitle = settlementKind === 'liability'
    ? '責任認定'
    : settlementKind === 'approve'
      ? '核定理賠金額'
      : settlementKind === 'pay'
        ? '確認給付結案'
        : '拒賠結案'

  return (
    <Space direction="vertical" size="large" className="full-width claims-page">
      <Card>
        <Space direction="vertical" size="middle" className="full-width">
          <Space wrap className="claims-page-heading">
            <div>
              <Typography.Title level={3} style={{ margin: 0 }}>理賠管理</Typography.Title>
              <Typography.Text type="secondary">管理事故案件、受理分派、補件與審核進度。</Typography.Text>
            </div>
            <Button icon={<ReloadOutlined />} onClick={() => void claimsQuery.refetch()}>
              重新整理
            </Button>
          </Space>
          <Space wrap className="full-width">
            <Input.Search
              aria-label="搜尋理賠案件"
              allowClear
              value={keyword}
              placeholder="搜尋案件、保單、申請人、車牌或負責人"
              onChange={(event) => { setKeyword(event.target.value); setPage(1) }}
              style={{ minWidth: 300, flex: 1 }}
            />
            <Select
              aria-label="理賠狀態"
              allowClear
              value={status || undefined}
              placeholder="全部狀態"
              style={{ minWidth: 160 }}
              onChange={(value) => { setStatus(value || ''); setPage(1) }}
              options={Object.entries(statusDefinitions).map(([value, definition]) => ({
                value,
                label: definition.label,
              }))}
            />
          </Space>
        </Space>
      </Card>

        {claimsQuery.isPending && claims.length === 0 ? (
        <LoadingState rows={6} />
      ) : filteredClaims.length === 0 ? (
        <EmptyState description="目前沒有符合條件的理賠案件" />
      ) : (
        <div className="claims-workspace">
          <Card className="claims-list-card" styles={{ body: { padding: 0 } }}>
            <Table<Claim>
              className="responsive-data-table"
              rowKey="id"
              columns={columns}
              dataSource={pagedClaims}
              loading={claimsQuery.isFetching}
              pagination={false}
              scroll={{ x: 1180 }}
              rowClassName={(record) => record.id === selectedId ? 'claims-selected-row' : ''}
              onRow={(record) => ({ onClick: () => setSelectedId(record.id) })}
            />
            {filteredClaims.length > pageSize ? (
              <div className="claims-pagination">
                <Pagination
                  current={page}
                  pageSize={pageSize}
                  total={filteredClaims.length}
                  showSizeChanger={false}
                  onChange={setPage}
                />
              </div>
            ) : null}
          </Card>

          <Card
            className="claims-detail-card"
            title={detail ? detail.claimNumber : '案件詳情'}
            extra={detail ? renderStatus(detail.status) : null}
          >
            {detailQuery.isPending ? (
              <LoadingState rows={5} />
            ) : detailQuery.error instanceof ApiError && detailQuery.error.status === 403 ? (
              <ForbiddenState requiredRole="具理賠案件查看權限的角色" contact="系統管理員" />
            ) : detailQuery.error instanceof ApiError && detailQuery.error.status === 404 ? (
              <EmptyState description="指定的理賠案件不存在或已被移除" />
            ) : detailQuery.error ? (
              <ErrorState
                title="理賠案件詳情讀取失敗"
                message={detailQuery.error.message}
                onRetry={() => void detailQuery.refetch()}
              />
            ) : !detail ? (
              <EmptyState description="請選擇理賠案件" />
            ) : (
              <Space direction="vertical" size="large" className="full-width">
                <div>
                  <Typography.Text strong>審核進度</Typography.Text>
                  <Steps
                    size="small"
                    current={progressIndex[detail.status]}
                    status={detail.status === 'denied' ? 'error' : detail.status === 'closed' ? 'finish' : 'process'}
                    items={mainProgressSteps}
                    responsive
                    className="claims-progress"
                  />
                  {detail.status === 'pending_documents' ? (
                    <Alert
                      type="warning"
                      showIcon
                      message="等待客戶補件"
                      description={detail.lastTransitionReason || '文件不足，請確認需補充的資料。'}
                    />
                  ) : null}
                  {detail.status === 'denied' ? (
                    <Alert
                      type="error"
                      showIcon
                      message="案件已拒賠結案"
                      description={detail.lastTransitionReason || '案件不符承保範圍。'}
                    />
                  ) : null}
                </div>

                <Descriptions column={1} bordered size="small">
                  <Descriptions.Item label="保單號碼">{detail.policyNumber}</Descriptions.Item>
                  <Descriptions.Item label="申請人">{detail.claimantName}</Descriptions.Item>
                  <Descriptions.Item label="車牌">{detail.licensePlate}</Descriptions.Item>
                  <Descriptions.Item label="事故日期">{detail.accidentDate}</Descriptions.Item>
                  <Descriptions.Item label="事故地點">{detail.accidentLocation}</Descriptions.Item>
                  <Descriptions.Item label="事故描述">{detail.accidentDescription}</Descriptions.Item>
                  <Descriptions.Item label="申請金額">{formatMoney(detail.claimedAmount)}</Descriptions.Item>
                  <Descriptions.Item label="責任認定">{detail.liabilityDecision || '-'}</Descriptions.Item>
                  <Descriptions.Item label="責任比例">{detail.liabilityPercentage == null ? '-' : `${detail.liabilityPercentage}%`}</Descriptions.Item>
                  <Descriptions.Item label="核定金額">{formatMoney(detail.approvedAmount)}</Descriptions.Item>
                  <Descriptions.Item label="給付方式">{detail.paymentMethod || '-'}</Descriptions.Item>
                  <Descriptions.Item label="給付時間">{formatDateTime(detail.paidAtUtc)}</Descriptions.Item>
                  <Descriptions.Item label="拒賠原因">{detail.deniedReason || '-'}</Descriptions.Item>
                  <Descriptions.Item label="負責理賠員">{detail.assignedAdjuster || '-'}</Descriptions.Item>
                  <Descriptions.Item label="最後處理原因">{detail.lastTransitionReason || '-'}</Descriptions.Item>
                  <Descriptions.Item label="更新時間">{formatDateTime(detail.updatedAtUtc)}</Descriptions.Item>
                </Descriptions>

                <Card size="small" title="文件／補件">
                  <Space direction="vertical" size="small" className="full-width">
                    <Typography.Text>
                      {detail.status === 'pending_documents'
                        ? '此案件目前等待補件，請依上方補件原因向客戶確認資料。'
                        : '案件文件狀態會隨理賠流程呈現；正式上傳、下載與退回功能由文件管理工單提供。'}
                    </Typography.Text>
                    <Typography.Text type="secondary">
                      T-034 僅呈現補件流程；理賠文件 CRUD 將由 T-037/T-038 接續實作。
                    </Typography.Text>
                  </Space>
                </Card>

                <Space wrap>
                  {detail.status === 'pending_intake' ? (
                    <Button type="primary" icon={<UserSwitchOutlined />} onClick={() => openTransition('accept')}>
                      受理並指派
                    </Button>
                  ) : null}
                  {detail.status === 'reviewing' ? (
                    <>
                      <Button type="primary" onClick={() => openSettlement('liability')}>完成責任認定</Button>
                      <Button onClick={() => openTransition('request_documents')}>要求補件</Button>
                      <Button danger onClick={() => openSettlement('deny')}>拒賠結案</Button>
                    </>
                  ) : null}
                  {detail.status === 'pending_documents' ? (
                    <Button type="primary" icon={<CheckCircleOutlined />} onClick={() => openTransition('documents_complete')}>
                      確認補件完成
                    </Button>
                  ) : null}
                  {detail.status === 'calculating' ? (
                    <Button type="primary" onClick={() => openSettlement('approve')}>核定理賠金額</Button>
                  ) : null}
                  {detail.status === 'pending_payment' ? (
                    <Button type="primary" onClick={() => openSettlement('pay')}>確認給付結案</Button>
                  ) : null}
                </Space>
              </Space>
            )}
          </Card>
        </div>
      )}

      <Modal
        title={modalTitle}
        open={transitionKind !== null}
        confirmLoading={transitionMutation.isPending}
        okText="確認"
        cancelText="取消"
        onOk={() => void submitTransition()}
        onCancel={closeTransition}
        destroyOnHidden
      >
        <Form form={transitionForm} layout="vertical">
          {transitionKind === 'accept' ? (
            <Form.Item
              name="assignedAdjuster"
              label="理賠員"
              rules={[{ required: true, whitespace: true, message: '請填寫理賠員。' }]}
            >
              <Input maxLength={100} placeholder="例如：王專員" />
            </Form.Item>
          ) : null}
          <Form.Item
            name="reason"
            label={transitionKind === 'request_documents' ? '補件原因' : '備註'}
            rules={transitionKind === 'request_documents'
              ? [{ required: true, whitespace: true, message: '請填寫補件原因。' }]
              : undefined}
          >
            <Input.TextArea
              rows={4}
              maxLength={1000}
              showCount
              placeholder={transitionKind === 'request_documents' ? '請說明缺少哪些文件或資料' : '選填處理備註'}
            />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={settlementTitle}
        open={settlementKind !== null}
        confirmLoading={settlementMutation.isPending}
        okText="確認"
        cancelText="取消"
        onOk={() => void submitSettlement()}
        onCancel={closeSettlement}
        destroyOnHidden
      >
        <Form form={settlementForm} layout="vertical">
          {settlementKind === 'liability' ? (
            <>
              <Form.Item
                name="decision"
                label="責任認定結論"
                rules={[{ required: true, whitespace: true, message: '請填寫責任認定結論。' }]}
              >
                <Input.TextArea rows={4} maxLength={500} showCount placeholder="例如：本案我方負主要肇事責任" />
              </Form.Item>
              <Form.Item
                name="liabilityPercentage"
                label="責任比例（%）"
                rules={[{ required: true, message: '請填寫責任比例。' }]}
              >
                <InputNumber min={0} max={100} precision={0} className="full-width" />
              </Form.Item>
            </>
          ) : null}
          {settlementKind === 'approve' ? (
            <Form.Item
              name="approvedAmount"
              label="核定理賠金額"
              rules={[{ required: true, message: '請填寫核定理賠金額。' }]}
            >
              <InputNumber min={0} max={detail?.claimedAmount ?? undefined} precision={0} className="full-width" />
            </Form.Item>
          ) : null}
          {settlementKind === 'pay' ? (
            <Form.Item
              name="paymentMethod"
              label="給付方式"
              rules={[{ required: true, whitespace: true, message: '請填寫給付方式。' }]}
            >
              <Input maxLength={100} placeholder="例如：銀行轉帳" />
            </Form.Item>
          ) : null}
          <Form.Item
            name="reason"
            label={settlementKind === 'deny' ? '拒賠原因' : '備註'}
            rules={settlementKind === 'deny'
              ? [{ required: true, whitespace: true, message: '請填寫拒賠原因。' }]
              : undefined}
          >
            <Input.TextArea
              rows={4}
              maxLength={1000}
              showCount
              placeholder={settlementKind === 'deny' ? '請說明不符承保範圍的原因' : '選填處理備註'}
            />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}

export default ClaimsPage
