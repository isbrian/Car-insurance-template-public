import { Form, Input, Modal } from 'antd'
import type { CustomerCreateRequest } from '../../api/customers'

type CustomerFormModalProps = {
  open: boolean
  confirmLoading: boolean
  onCancel: () => void
  onSubmit: (values: CustomerCreateRequest) => Promise<void>
}

function CustomerFormModal({ open, confirmLoading, onCancel, onSubmit }: CustomerFormModalProps) {
  const [form] = Form.useForm<CustomerCreateRequest>()

  const handleOk = async () => {
    const values = await form.validateFields()
    await onSubmit(values)
    form.resetFields()
  }

  return (
    <Modal
      title="新增客戶"
      open={open}
      okText="儲存"
      cancelText="取消"
      confirmLoading={confirmLoading}
      onCancel={onCancel}
      onOk={() => void handleOk()}
      destroyOnHidden
    >
      <Form form={form} layout="vertical">
        <Form.Item name="name" label="姓名" rules={[{ required: true, message: '請輸入姓名' }]}>
          <Input placeholder="請輸入姓名" />
        </Form.Item>
        <Form.Item
          name="nationalId"
          label="身分證字號"
          rules={[
            { required: true, message: '請輸入身分證字號' },
            { pattern: /^[A-Za-z][0-9]{9}$/, message: '格式須為英文字母加 9 碼數字' },
          ]}
        >
          <Input placeholder="例如：A123456789" />
        </Form.Item>
        <Form.Item name="phone" label="聯絡電話" rules={[{ required: true, message: '請輸入聯絡電話' }]}>
          <Input placeholder="請輸入聯絡電話" />
        </Form.Item>
        <Form.Item name="email" label="電子郵件" rules={[{ type: 'email', message: '電子郵件格式不正確' }]}>
          <Input placeholder="選填" />
        </Form.Item>
        <Form.Item name="address" label="地址">
          <Input.TextArea rows={2} placeholder="選填" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export default CustomerFormModal
