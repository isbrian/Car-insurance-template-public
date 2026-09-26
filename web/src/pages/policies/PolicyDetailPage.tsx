import { useEffect, useState } from 'react'
import { App, Button, Card, Descriptions, Space, Tag, Timeline, Typography } from 'antd'
import { ArrowLeftOutlined, DownloadOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { downloadPolicyDocument, getPolicy, getPolicyTimeline } from '../../api/policies'
import type { PolicyDetails, PolicyTimeline } from '../../api/policies'
import { ApiError } from '../../api/http'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'

const statusLabels: Record<string, { text: string; color: string }> = {
  issued: { text: '已出單', color: 'processing' },
  active: { text: '有效', color: 'success' },
  endorsement_pending: { text: '批改中', color: 'warning' },
  renewal_pending: { text: '續保中', color: 'warning' },
  terminated: { text: '已終止', color: 'error' },
  expired: { text: '已到期', color: 'default' },
  cancelled: { text: '已取消', color: 'error' },
  paid: { text: '已付款', color: 'success' },
  completed: { text: '完成', color: 'success' },
}

function StatusTag({ status }: { status: string }) {
  const definition = statusLabels[status]
  return <Tag color={definition?.color}>{definition?.text ?? status}</Tag>
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('zh-TW', { style: 'currency', currency: 'TWD', maximumFractionDigits: 0 }).format(value)
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
  }).format(new Date(value))
}

function maskNationalId(value: string | null) {
  if (!value) return '—'
  return value.length <= 3 ? '***' : `${value[0]}****${value.slice(-2)}`
}

function maskPhone(value: string) {
  return value.length <= 5 ? '*****' : `${value.slice(0, 2)}**-***-${value.slice(-3)}`
}

function PolicyDetailPage() {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const { id } = useParams()
  const numericId = Number(id)
  const [policy, setPolicy] = useState<PolicyDetails | null>(null)
  const [timeline, setTimeline] = useState<PolicyTimeline | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const [downloading, setDownloading] = useState(false)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    if (!Number.isInteger(numericId) || numericId <= 0) {
      setLoading(false)
      setPolicy(null)
      return
    }

    const controller = new AbortController()
    setLoading(true)
    setError(null)
    void Promise.all([
      getPolicy(numericId, controller.signal),
      getPolicyTimeline(numericId, controller.signal),
    ])
      .then(([policyResult, timelineResult]) => {
        setPolicy(policyResult)
        setTimeline(timelineResult)
      })
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取保單詳情失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [numericId, reloadKey])

  const handleDownload = async () => {
    if (!policy) return
    setDownloading(true)
    try {
      const { blob, fileName } = await downloadPolicyDocument(policy.id)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName
      document.body.appendChild(anchor)
      anchor.click()
      anchor.remove()
      URL.revokeObjectURL(url)
      message.success('保單 PDF 已開始下載')
    } catch (requestError: unknown) {
      message.error(requestError instanceof Error ? requestError.message : '保單 PDF 下載失敗')
    } finally {
      setDownloading(false)
    }
  }

  if (loading && !policy) return <LoadingState rows={8} />

  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenState requiredRole="具保單資料查看權限的角色" contact="系統管理員" />
  }

  if (error instanceof ApiError && error.status === 404) {
    return <EmptyState description="指定的保單不存在或已被移除。" action={<Button onClick={() => navigate('/policies')}>返回保單列表</Button>} />
  }

  if (error) {
    return (
      <ErrorState
        title="保單詳情讀取失敗"
        message={error.message}
        onRetry={() => setReloadKey((current) => current + 1)}
        fallback={<Button onClick={() => navigate('/policies')}>返回保單列表</Button>}
      />
    )
  }

  if (!policy) {
    return <EmptyState description="指定的保單不存在或編號無效。" action={<Button onClick={() => navigate('/policies')}>返回保單列表</Button>} />
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
        <Space>
          <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/policies')}>返回保單列表</Button>
          <Typography.Title level={3} style={{ margin: 0 }}>{policy?.policyNumber || '保單詳情'}</Typography.Title>
          {policy ? <StatusTag status={policy.status} /> : null}
        </Space>
        <Button type="primary" icon={<DownloadOutlined />} loading={downloading} disabled={!policy} onClick={handleDownload}>下載保單 PDF</Button>
      </Space>

      <Card loading={loading} title="保單基本資料">
        {policy ? <Descriptions bordered column={{ xs: 1, sm: 2, lg: 3 }}>
          <Descriptions.Item label="保單號碼">{policy.policyNumber}</Descriptions.Item>
          <Descriptions.Item label="投保申請">{policy.applicationNumber}</Descriptions.Item>
          <Descriptions.Item label="報價單號">{policy.quoteNumber}</Descriptions.Item>
          <Descriptions.Item label="保險期間">{policy.insuranceStartDate} — {policy.insuranceEndDate}</Descriptions.Item>
          <Descriptions.Item label="總保費">{formatMoney(policy.totalPremium)}</Descriptions.Item>
          <Descriptions.Item label="出單時間">{formatDateTime(policy.issuedAtUtc)}</Descriptions.Item>
        </Descriptions> : null}
      </Card>

      <Card loading={loading} title="客戶／被保險人">
        {policy ? <Descriptions bordered column={{ xs: 1, sm: 2, lg: 3 }}>
          <Descriptions.Item label="客戶姓名">{policy.customerName}</Descriptions.Item>
          <Descriptions.Item label="客戶身分證">{maskNationalId(policy.customerNationalId)}</Descriptions.Item>
          <Descriptions.Item label="聯絡電話">{maskPhone(policy.customerPhone)}</Descriptions.Item>
          <Descriptions.Item label="電子郵件">{policy.customerEmail || '—'}</Descriptions.Item>
          <Descriptions.Item label="被保險人">{policy.insuredName || policy.customerName}</Descriptions.Item>
          <Descriptions.Item label="被保險人身分證">{maskNationalId(policy.insuredNationalId)}</Descriptions.Item>
          <Descriptions.Item label="地址" span={3}>{policy.customerAddress || '—'}</Descriptions.Item>
        </Descriptions> : null}
      </Card>

      <Card loading={loading} title="車輛資料">
        {policy ? <Descriptions bordered column={{ xs: 1, sm: 2, lg: 4 }}>
          <Descriptions.Item label="車牌號碼">{policy.licensePlate}</Descriptions.Item>
          <Descriptions.Item label="車身號碼">{policy.vin}</Descriptions.Item>
          <Descriptions.Item label="廠牌／車型代號">{policy.makeModelCode}</Descriptions.Item>
          <Descriptions.Item label="使用性質">{policy.usageType}</Descriptions.Item>
        </Descriptions> : null}
      </Card>

      <Card loading={loading} title="繳費與出單資訊">
        {policy ? <Descriptions bordered column={{ xs: 1, sm: 2, lg: 3 }}>
          <Descriptions.Item label="付款單號">{policy.paymentNumber}</Descriptions.Item>
          <Descriptions.Item label="繳別">{policy.paymentFrequency}</Descriptions.Item>
          <Descriptions.Item label="付款方式">{policy.paymentMethod}</Descriptions.Item>
          <Descriptions.Item label="業務員">{policy.salesAgent}</Descriptions.Item>
          <Descriptions.Item label="通路">{policy.channel}</Descriptions.Item>
          <Descriptions.Item label="費率版本">{policy.rateVersion}</Descriptions.Item>
          <Descriptions.Item label="PDF 範本版本">{policy.templateVersion}</Descriptions.Item>
          <Descriptions.Item label="PDF 大小">{new Intl.NumberFormat('zh-TW').format(policy.pdfSizeBytes)} bytes</Descriptions.Item>
          <Descriptions.Item label="PDF SHA-256"><Typography.Text code copyable={{ text: policy.pdfSha256 }}>{policy.pdfSha256.slice(0, 16)}…</Typography.Text></Descriptions.Item>
        </Descriptions> : null}
      </Card>

      <Card loading={loading} title="保全歷程" extra={timeline?.currentLifecycleStatus ? <Space>目前狀態 <StatusTag status={timeline.currentLifecycleStatus} /></Space> : null}>
        {timeline ? (
          <Timeline
            items={timeline.events.map((event) => ({
              children: (
                <Space direction="vertical" size={0}>
                  <Space><Typography.Text strong>{event.title}</Typography.Text><StatusTag status={event.status} /></Space>
                  <Typography.Text type="secondary">{formatDateTime(event.occurredAtUtc)}</Typography.Text>
                  {event.description ? <Typography.Text>{event.description}</Typography.Text> : null}
                </Space>
              ),
            }))}
          />
        ) : null}
      </Card>
    </Space>
  )
}

export default PolicyDetailPage
