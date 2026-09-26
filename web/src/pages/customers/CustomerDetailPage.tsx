import { useEffect, useState } from 'react'
import { Alert, Button, Card, Descriptions, Empty, Space, Table, Tag, Typography } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { getCustomer } from '../../api/customers'
import type { CustomerDetails } from '../../api/customers'
import { ApiError } from '../../api/http'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'

function CustomerDetailPage() {
  const navigate = useNavigate()
  const { id } = useParams()
  const [customer, setCustomer] = useState<CustomerDetails | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)

  const loadCustomer = () => {
    const customerId = Number(id)
    if (!id || !Number.isInteger(customerId) || customerId <= 0) {
      setCustomer(null)
      setError(null)
      setLoading(false)
      return
    }

    const controller = new AbortController()
    setLoading(true)
    setError(null)
    void getCustomer(customerId, controller.signal)
      .then(setCustomer)
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取客戶資料失敗'))
      })
      .finally(() => setLoading(false))
    return () => controller.abort()
  }

  useEffect(() => loadCustomer(), [id])

  if (loading && !customer && !error) {
    return <LoadingState rows={8} />
  }

  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenState requiredRole="具客戶資料查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return (
      <ErrorState
        title="客戶資料讀取失敗"
        message={error.message}
        onRetry={loadCustomer}
        fallback={<Button onClick={() => navigate('/customers')}>返回客戶列表</Button>}
      />
    )
  }

  if (!customer) {
    return (
      <EmptyState
        description="找不到指定的客戶資料"
        action={<Button type="primary" onClick={() => navigate('/customers')}>返回客戶列表</Button>}
      />
    )
  }

  const vehicleColumns = [
    { title: '車牌號碼', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌號碼') },
    { title: '車身號碼', dataIndex: 'vin', key: 'vin', onCell: () => responsiveCell('車身號碼') },
    { title: '廠牌／車型', dataIndex: 'makeModelCode', key: 'makeModelCode', onCell: () => responsiveCell('廠牌／車型') },
    { title: '出廠年份', dataIndex: 'manufactureYear', key: 'manufactureYear', onCell: () => responsiveCell('出廠年份'), render: (year: number) => year || '—' },
    { title: '使用性質', dataIndex: 'usageType', key: 'usageType', onCell: () => responsiveCell('使用性質') },
  ]

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/customers')}>返回客戶列表</Button>
        <Typography.Title level={3} style={{ margin: 0 }}>客戶詳情</Typography.Title>
      </Space>
      <Card title="基本資料">
        <Descriptions bordered column={{ xs: 1, sm: 2, md: 3 }}>
          <Descriptions.Item label="姓名">{customer.name}</Descriptions.Item>
          <Descriptions.Item label="身分證字號">{customer.nationalId}</Descriptions.Item>
          <Descriptions.Item label="聯絡電話">{customer.phone}</Descriptions.Item>
          <Descriptions.Item label="電子郵件">{customer.email || '—'}</Descriptions.Item>
          <Descriptions.Item label="出生日期">{customer.birthDate?.slice(0, 10) || '—'}</Descriptions.Item>
          <Descriptions.Item label="驗證狀態"><Tag color={customer.isVerified ? 'success' : 'warning'}>{customer.isVerified ? '已驗證' : '待驗證'}</Tag></Descriptions.Item>
          <Descriptions.Item label="地址" span={3}>{customer.address || '—'}</Descriptions.Item>
        </Descriptions>
      </Card>
      <Card title="名下車輛">
        {customer.vehicles.length > 0 ? <Table className="responsive-data-table" rowKey="id" columns={vehicleColumns} dataSource={customer.vehicles} pagination={false} scroll={{ x: 760 }} /> : <Empty description="目前沒有名下車輛" />}
      </Card>
      <Card title="有效保單">
        <Alert type="info" showIcon message="目前尚未建立保單資料，暫無可顯示的有效保單。" />
      </Card>
      <Card title="歷史理賠">
        <Alert type="info" showIcon message="目前尚未建立理賠資料，暫無可顯示的歷史理賠。" />
      </Card>
    </Space>
  )
}

export default CustomerDetailPage
