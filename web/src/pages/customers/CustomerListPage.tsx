import { useState } from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Button, Card, Table, Tag, message } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { createCustomer, getCustomers } from '../../api/customers'
import type { Customer, CustomerCreateRequest } from '../../api/customers'
import { ApiError } from '../../api/http'
import ListPageShell from '../../components/shared/ListPageShell'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import MaskedValue from '../../components/shared/MaskedValue'
import { responsiveCell } from '../../components/shared/responsiveTable'
import CustomerFormModal from './CustomerFormModal'
import { useDebouncedValue } from '../../hooks/useDebouncedValue'
import { queryKeys } from '../../query/queryKeys'

function CustomerListPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [keyword, setKeyword] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const debouncedKeyword = useDebouncedValue(keyword, 300)
  const customersQuery = useQuery({
    queryKey: queryKeys.customers.list(debouncedKeyword, page, 20),
    queryFn: ({ signal }) => getCustomers(debouncedKeyword, page, 20, signal),
    placeholderData: keepPreviousData,
  })
  const createMutation = useMutation({
    mutationFn: createCustomer,
    onSuccess: async () => {
      message.success('客戶新增成功')
      setModalOpen(false)
      setPage(1)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.customers.all }),
        queryClient.invalidateQueries({ queryKey: queryKeys.dashboard.summary }),
      ])
    },
    onError: (requestError: Error) => {
      message.error(requestError.message || '新增客戶失敗')
    },
  })

  const items: Customer[] = customersQuery.data?.items ?? []
  const total = customersQuery.data?.total ?? 0

  const handleSearchChange = (value: string) => {
    setKeyword(value)
    setPage(1)
  }

  const handleCreate = async (values: CustomerCreateRequest) => {
    await createMutation.mutateAsync(values)
  }

  const columns = [
    { title: '客戶', dataIndex: 'name', key: 'name', onCell: () => responsiveCell('客戶') },
    { title: '身分證字號', dataIndex: 'nationalId', key: 'nationalId', onCell: () => responsiveCell('身分證字號'), render: (value: string) => <MaskedValue value={value} /> },
    { title: '聯絡電話', dataIndex: 'phone', key: 'phone', onCell: () => responsiveCell('聯絡電話'), render: (value: string) => <MaskedValue value={value} /> },
    { title: '保單數', key: 'policies', onCell: () => responsiveCell('保單數'), render: () => '—' },
    {
      title: '驗證狀態',
      dataIndex: 'isVerified',
      key: 'isVerified',
      onCell: () => responsiveCell('驗證狀態'),
      render: (isVerified: boolean) => (
        <Tag color={isVerified ? 'success' : 'warning'}>{isVerified ? '已驗證' : '待驗證'}</Tag>
      ),
    },
    { title: '操作', key: 'actions', onCell: () => responsiveCell('操作'), render: (_: unknown, customer: Customer) => <Button type="link" onClick={() => navigate(`/customers/${customer.id}`)}>檢視</Button> },
  ]

  if (customersQuery.error instanceof ApiError && customersQuery.error.status === 403) {
    return <ForbiddenState requiredRole="具客戶資料查看權限的角色" contact="系統管理員" />
  }

  if (customersQuery.error) {
    return <ErrorState title="客戶資料讀取失敗" message={customersQuery.error.message} onRetry={() => void customersQuery.refetch()} />
  }

  return (
    <ListPageShell
      title="客戶管理"
      primaryAction={<Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>新增客戶</Button>}
      searchPlaceholder="搜尋姓名、身分證字號或電話"
      keyword={keyword}
      onKeywordChange={handleSearchChange}
      page={page}
      pageSize={20}
      total={total}
      onPageChange={setPage}
    >
      {customersQuery.isPending && items.length === 0 ? (
        <LoadingState />
      ) : items.length === 0 ? (
        <EmptyState
          description="尚無客戶資料"
          action={<Button type="primary" icon={<PlusOutlined />} onClick={() => setModalOpen(true)}>新增第一筆客戶</Button>}
        />
      ) : (
        <Card styles={{ body: { padding: 0 } }}>
          <Table
            className="responsive-data-table"
            rowKey="id"
            columns={columns}
            dataSource={items}
            loading={customersQuery.isFetching}
            pagination={false}
            rowSelection={{}}
            scroll={{ x: 760 }}
          />
        </Card>
      )}
      <CustomerFormModal open={modalOpen} confirmLoading={createMutation.isPending} onCancel={() => setModalOpen(false)} onSubmit={handleCreate} />
    </ListPageShell>
  )
}

export default CustomerListPage
