import { useEffect, useState } from 'react'
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

function CustomerListPage() {
  const navigate = useNavigate()
  const [items, setItems] = useState<Customer[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [keyword, setKeyword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [debouncedKeyword, setDebouncedKeyword] = useState('')
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedKeyword(keyword), 300)
    return () => window.clearTimeout(timer)
  }, [keyword])

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    void getCustomers(debouncedKeyword, page, 20, controller.signal)
      .then((response) => {
        setItems(response.items)
        setTotal(response.total)
      })
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setError(requestError instanceof Error ? requestError : new Error('讀取客戶資料失敗'))
      })
      .finally(() => setLoading(false))

    return () => controller.abort()
  }, [debouncedKeyword, page, reloadKey])

  const handleSearchChange = (value: string) => {
    setKeyword(value)
    setPage(1)
  }

  const handleCreate = async (values: CustomerCreateRequest) => {
    setSaving(true)
    try {
      await createCustomer(values)
      message.success('客戶新增成功')
      setModalOpen(false)
      setPage(1)
      const response = await getCustomers(debouncedKeyword, 1, 20)
      setItems(response.items)
      setTotal(response.total)
    } catch (requestError: unknown) {
      message.error(requestError instanceof Error ? requestError.message : '新增客戶失敗')
      throw requestError
    } finally {
      setSaving(false)
    }
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

  if (error instanceof ApiError && error.status === 403) {
    return <ForbiddenState requiredRole="具客戶資料查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return <ErrorState title="客戶資料讀取失敗" message={error.message} onRetry={() => setReloadKey((current) => current + 1)} />
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
      {loading && items.length === 0 ? (
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
            loading={loading}
            pagination={false}
            rowSelection={{}}
            scroll={{ x: 760 }}
          />
        </Card>
      )}
      <CustomerFormModal open={modalOpen} confirmLoading={saving} onCancel={() => setModalOpen(false)} onSubmit={handleCreate} />
    </ListPageShell>
  )
}

export default CustomerListPage
