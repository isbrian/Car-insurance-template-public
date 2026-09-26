import { useEffect, useState } from 'react'
import { Button, Card, Descriptions, Space, Typography } from 'antd'
import { ArrowLeftOutlined } from '@ant-design/icons'
import { useNavigate, useParams } from 'react-router-dom'
import { getVehicle } from '../../api/vehicles'
import type { Vehicle } from '../../api/vehicles'
import { ApiError } from '../../api/http'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'

function VehicleDetailPage() {
  const navigate = useNavigate()
  const { id } = useParams()
  const [vehicle, setVehicle] = useState<Vehicle | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)

  const loadVehicle = () => {
    const vehicleId = Number(id)
    if (!id || !Number.isInteger(vehicleId) || vehicleId <= 0) {
      setVehicle(null)
      setError(null)
      setLoading(false)
      return
    }

    const controller = new AbortController()
    setLoading(true)
    setError(null)
    void getVehicle(vehicleId, controller.signal)
      .then(setVehicle)
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取車輛資料失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }

  useEffect(() => loadVehicle(), [id])

  if (loading && !vehicle && !error) {
    return <LoadingState rows={6} />
  }

  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenState requiredRole="具車輛資料查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return (
      <ErrorState
        title="車輛資料讀取失敗"
        message={error.message}
        onRetry={loadVehicle}
        fallback={<Button onClick={() => navigate('/vehicles')}>返回車輛列表</Button>}
      />
    )
  }

  if (!vehicle) {
    return (
      <EmptyState
        description="找不到指定的車輛資料"
        action={<Button type="primary" onClick={() => navigate('/vehicles')}>返回車輛列表</Button>}
      />
    )
  }

  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Space wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/vehicles')}>返回車輛列表</Button>
        <Typography.Title level={3} style={{ margin: 0 }}>車輛詳情</Typography.Title>
      </Space>
      <Card title="車籍資料">
        <Descriptions bordered column={{ xs: 1, sm: 2, md: 3 }}>
          <Descriptions.Item label="車牌">{vehicle.licensePlate}</Descriptions.Item>
          <Descriptions.Item label="車身號碼">{vehicle.vin}</Descriptions.Item>
          <Descriptions.Item label="廠牌／車型">{vehicle.makeModelCode}</Descriptions.Item>
          <Descriptions.Item label="出廠日期">{vehicle.manufactureDate?.slice(0, 10) || '—'}</Descriptions.Item>
          <Descriptions.Item label="使用性質">{vehicle.usageType}</Descriptions.Item>
          <Descriptions.Item label="車主">{vehicle.customerName}</Descriptions.Item>
          <Descriptions.Item label="車主 ID">{vehicle.customerId}</Descriptions.Item>
        </Descriptions>
      </Card>
    </Space>
  )
}

export default VehicleDetailPage
