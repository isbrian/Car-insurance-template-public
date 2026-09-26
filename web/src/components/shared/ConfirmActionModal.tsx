import type { ReactNode } from 'react'
import { Modal } from 'antd'

type ConfirmActionModalProps = {
  open: boolean
  title: string
  children: ReactNode
  confirmText?: string
  cancelText?: string
  confirmLoading?: boolean
  danger?: boolean
  onConfirm: () => void | Promise<void>
  onCancel: () => void
}

function ConfirmActionModal({
  open,
  title,
  children,
  confirmText = '確認',
  cancelText = '取消',
  confirmLoading = false,
  danger = false,
  onConfirm,
  onCancel,
}: ConfirmActionModalProps) {
  return (
    <Modal
      open={open}
      title={title}
      okText={confirmText}
      cancelText={cancelText}
      confirmLoading={confirmLoading}
      okButtonProps={{ danger }}
      onOk={() => void onConfirm()}
      onCancel={onCancel}
      destroyOnHidden
    >
      {children}
    </Modal>
  )
}

export default ConfirmActionModal
