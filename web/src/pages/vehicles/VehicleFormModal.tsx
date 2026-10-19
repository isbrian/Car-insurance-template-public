import { useQuery } from '@tanstack/react-query'
import { Alert, Form, Input, Modal, Select } from 'antd'
import { getCustomers } from '../../api/customers'
import type { Customer } from '../../api/customers'
import type { VehicleCreateRequest } from '../../api/vehicles'
import { queryKeys } from '../../query/queryKeys'

type VehicleFormModalProps = {
  open: boolean
  confirmLoading: boolean
  onCancel: () => void
  onSubmit: (values: VehicleCreateRequest) => Promise<void>
}

function VehicleFormModal({ open, confirmLoading, onCancel, onSubmit }: VehicleFormModalProps) {
  const [form] = Form.useForm<VehicleCreateRequest>()
  const customersQuery = useQuery({
    queryKey: queryKeys.customers.list('', 1, 100),
    queryFn: ({ signal }) => getCustomers('', 1, 100, signal),
    enabled: open,
  })
  const customers: Customer[] = customersQuery.data?.items ?? []

  const handleOk = async () => {
    try {
      const values = await form.validateFields()
      await onSubmit(values)
      form.resetFields()
    } catch {
      // 欄位驗證或 API 錯誤時保留 Modal，讓使用者修正後重試。
    }
  }

  const handleCancel = () => {
    form.resetFields()
    onCancel()
  }

  return (
    <Modal
      title="新增車輛"
      open={open}
      okText="儲存"
      cancelText="取消"
      confirmLoading={confirmLoading}
      onCancel={handleCancel}
      onOk={() => void handleOk()}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        {customersQuery.error ? <Alert type="error" showIcon message="車主資料讀取失敗" description={customersQuery.error.message} style={{ marginBottom: 16 }} /> : null}
        <Form.Item name="licensePlate" label="車牌號碼" rules={[{ required: true, whitespace: true, message: '請輸入車牌號碼' }]}>
          <Input placeholder="例如：ABC-1234" />
        </Form.Item>
        <Form.Item name="vin" label="車身號碼 VIN" rules={[{ required: true, whitespace: true, message: '請輸入車身號碼 VIN' }]}>
          <Input placeholder="請輸入完整 VIN" />
        </Form.Item>
        <Form.Item name="makeModelCode" label="廠牌／車型代碼" rules={[{ required: true, whitespace: true, message: '請輸入廠牌／車型代碼' }]}>
          <Input placeholder="例如：TOYOTA-COROLLA" />
        </Form.Item>
        <Form.Item name="manufactureDate" label="出廠日期">
          <Input type="date" />
        </Form.Item>
        <Form.Item name="usageType" label="使用性質" rules={[{ required: true, message: '請選擇使用性質' }]}>
          <Select
            placeholder="請選擇使用性質"
            options={[
              { value: '自用', label: '自用' },
              { value: '營業用', label: '營業用' },
            ]}
          />
        </Form.Item>
        <Form.Item name="customerId" label="車主" rules={[{ required: true, message: '請選擇車主' }]}>
          <Select
            showSearch
            optionFilterProp="label"
            loading={customersQuery.isFetching}
            disabled={Boolean(customersQuery.error)}
            placeholder="請選擇車主"
            options={customers.map((customer) => ({
              value: customer.id,
              label: `${customer.name}（${customer.nationalId}）`,
            }))}
          />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export default VehicleFormModal
