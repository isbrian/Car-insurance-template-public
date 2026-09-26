import type { ReactNode } from 'react'
import { Card, Input, Pagination, Space, Typography } from 'antd'

type ListPageShellProps = {
  title: string
  primaryAction?: ReactNode
  searchPlaceholder: string
  keyword: string
  onKeywordChange: (value: string) => void
  filters?: ReactNode
  batchActions?: ReactNode
  children: ReactNode
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
}

function ListPageShell({
  title,
  primaryAction,
  searchPlaceholder,
  keyword,
  onKeywordChange,
  filters,
  batchActions,
  children,
  page,
  pageSize,
  total,
  onPageChange,
}: ListPageShellProps) {
  return (
    <Space direction="vertical" size="large" style={{ width: '100%' }}>
      <Card>
        <Space direction="vertical" size="middle" style={{ width: '100%' }}>
          <Space wrap style={{ width: '100%', justifyContent: 'space-between' }}>
            <Typography.Title level={3} style={{ margin: 0 }}>{title}</Typography.Title>
            {primaryAction}
          </Space>
          <Space wrap style={{ width: '100%' }}>
            <Input.Search
              allowClear
              placeholder={searchPlaceholder}
              value={keyword}
              onChange={(event) => onKeywordChange(event.target.value)}
              style={{ minWidth: 280, flex: 1 }}
            />
            {filters}
          </Space>
          {batchActions}
        </Space>
      </Card>

      {children}

      {total > pageSize ? (
        <Card size="small">
          <Pagination
            current={page}
            pageSize={pageSize}
            total={total}
            showSizeChanger={false}
            onChange={onPageChange}
          />
        </Card>
      ) : null}
    </Space>
  )
}

export default ListPageShell
