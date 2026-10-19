import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Card, Table, message } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { createVehicle, getVehicles } from '../../api/vehicles'
import type { Vehicle, VehicleCreateRequest } from '../../api/vehicles'
import { ApiError } from '../../api/http'
import ListPageShell from '../../components/shared/ListPageShell'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'
import VehicleFormModal from './VehicleFormModal'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { queryKeys } from '../../query/queryKeys'

function VehicleListPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [messageApi, messageContextHolder] = message.useMessage()
  const [page, setPage] = useState(1)
  const [keyword, setKeyword] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const debouncedKeyword = useDebouncedValue(keyword, 300)
  const vehiclesQuery = useQuery({
    queryKey: queryKeys.vehicles.list(debouncedKeyword, page, 20),
    queryFn: ({ signal }) => getVehicles(debouncedKeyword, page, 20, signal),
    placeholderData: keepPreviousData,
  })
  const createMutation = useMutation({
    mutationFn: createVehicle,
    onSuccess: async () => {
      messageApi.success('車輛新增成功')
      setModalOpen(false)
      setPage(1)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.vehicles.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.customers.all }),
      ])
    },
    onError: (requestError: Error) => {
      messageApi.error(requestError.message || '新增車輛失敗')
    },
  })

  const items: Vehicle[] = vehiclesQuery.data?.items ?? []
  const total = vehiclesQuery.data?.totalCount ?? 0

  const handleSearchChange = (value: string) => {
    setKeyword(value)
    setPage(1)
  }

  const handleCreate = async (values: VehicleCreateRequest) => {
    await createMutation.mutateAsync(values)
  }

  const columns = [
    { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
    { title: '車主', dataIndex: 'customerName', key: 'customerName', onCell: () => responsiveCell('車主') },
    { title: '廠牌／車型', dataIndex: 'makeModelCode', key: 'makeModelCode', onCell: () => responsiveCell('廠牌／車型') },
    {
      title: '出廠年份',
      dataIndex: 'manufactureDate',
      key: 'manufactureDate',
      onCell: () => responsiveCell('出廠年份'),
      render: (manufactureDate: string | null) => manufactureDate ? manufactureDate.slice(0, 4) : '—',
    },
    { title: '車身號碼', dataIndex: 'vin', key: 'vin', onCell: () => responsiveCell('車身號碼') },
    { title: '使用性質', dataIndex: 'usageType', key: 'usageType', onCell: () => responsiveCell('使用性質') },
    {
      title: '操作',
      key: 'actions',
      onCell: () => responsiveCell('操作'),
      render: (_: unknown, vehicle: Vehicle) => (
        <Button type="link" onClick={() => navigate(`/vehicles/${vehicle.id}`)}>檢視</Button>
      ),
    },
  ]

  if (vehiclesQuery.error instanceof ApiError && vehiclesQuery.error.status === 403) {
    return <ForbiddenState requiredRole="具車輛資料查看權限的角色" contact="系統管理員" />
  }

  if (vehiclesQuery.error) {
    return <ErrorState title="車輛資料讀取失敗" message={vehiclesQuery.error.message} onRetry={() => void vehiclesQuery.refetch()} />
  }

  return (
    <>
      {messageContextHolder}
      <ListPageShell
        title="車輛管理"
        primaryAction={<Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>新增車輛</Button>}
        searchPlaceholder="搜尋車牌、車身號碼或廠牌／車型"
        keyword={keyword}
        onKeywordChange={handleSearchChange}
        page={page}
        pageSize={20}
        total={total}
        onPageChange={setPage}
      >
        {vehiclesQuery.isPending && items.length === 0 ? (
          <LoadingState />
        ) : items.length === 0 ? (
          <EmptyState
            description="尚無符合條件的車輛資料"
            action={<Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>新增第一筆車輛</Button>}
          />
        ) : (
          <Card styles={{ body: { padding: 0 } }}>
            <Table
              className="responsive-data-table"
              rowKey="id"
              columns={columns}
              dataSource={items}
              loading={vehiclesQuery.isFetching}
              pagination={false}
              rowSelection={{}}
              scroll={{ x: 900 }}
            />
          </Card>
        )}
        <VehicleFormModal open={modalOpen} confirmLoading={createMutation.isPending} onCancel={() => setModalOpen(false)} onSubmit={handleCreate} />
      </ListPageShell>
    </>
  )
}

export default VehicleListPage
