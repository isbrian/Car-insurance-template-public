import { useEffect, useState } from 'react'
import { Alert, Form, Input, Modal, Select } from 'antd'
import { getCustomers } from '../../api/customers'
import type { Customer } from '../../api/customers'
import type { VehicleCreateRequest } from '../../api/vehicles'

type VehicleFormModalProps = {
  open: boolean
  confirmLoading: boolean
  onCancel: () => void
  onSubmit: (values: VehicleCreateRequest) => Promise<void>
}

function VehicleFormModal({ open, confirmLoading, onCancel, onSubmit }: VehicleFormModalProps) {
  const [form] = Form.useForm<VehicleCreateRequest>()
  const [customers, setCustomers] = useState<Customer[]>([])
  const [customersLoading, setCustomersLoading] = useState(false)
  const [customersError, setCustomersError] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return

    const controller = new AbortController()
    setCustomersLoading(true)
    setCustomersError(null)

    void getCustomers('', 1, 100, controller.signal)
      .then((response) => setCustomers(response.items))
      .catch((requestError: unknown) => {
        if (requestError instanceof DOMException && requestError.name === 'AbortError') return
        setCustomersError(requestError instanceof Error ? requestError.message : '讀取車主資料失敗')
      })
      .finally(() => setCustomersLoading(false))

    return () => controller.abort()
  }, [open])

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
        {customersError ? <Alert type="error" showIcon message="車主資料讀取失敗" description={customersError} style={{ marginBottom: 16 }} /> : null}
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
            loading={customersLoading}
            disabled={Boolean(customersError)}
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
