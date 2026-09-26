import type { ReactNode } from 'react'
import { Button, Card, Empty, Result, Skeleton } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'

type EmptyStateProps = {
  description: string
  action?: ReactNode
}

type LoadingStateProps = {
  rows?: number
}

type ForbiddenStateProps = {
  requiredRole: string
  contact: string
}

type ErrorStateProps = {
  title: string
  message: string
  onRetry?: () => void
  fallback?: ReactNode
}

export function EmptyState({ description, action }: EmptyStateProps) {
  return (
    <Card>
      <Empty description={description}>{action}</Empty>
    </Card>
  )
}

export function LoadingState({ rows = 4 }: LoadingStateProps) {
  return (
    <Card>
      <Skeleton active paragraph={{ rows }} title={false} />
    </Card>
  )
}

export function ForbiddenState({ requiredRole, contact }: ForbiddenStateProps) {
  return (
    <Result
      status="403"
      title="沒有權限存取此功能"
      subTitle={`需要角色：${requiredRole}。如需權限，請聯絡 ${contact}。`}
    />
  )
}

export function ErrorState({ title, message, onRetry, fallback }: ErrorStateProps) {
  return (
    <Result
      status="error"
      title={title}
      subTitle={message}
      extra={(
        <>
          {onRetry ? <Button icon={<ReloadOutlined />} onClick={onRetry}>重試</Button> : null}
          {fallback}
        </>
      )}
    />
  )
}
