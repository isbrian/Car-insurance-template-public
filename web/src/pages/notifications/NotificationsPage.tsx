import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  App,
  Badge,
  Button,
  Card,
  Form,
  Input,
  List,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table'
import {
  BellOutlined,
  CheckCircleOutlined,
  MailOutlined,
  MessageOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import {
  getNotifications,
  markNotificationRead,
  retryNotification,
} from '../../api/notifications'
import type { NotificationItem, NotificationListResponse } from '../../api/notifications'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { ApiError } from '../../api/http'
import { responsiveCell } from '../../components/shared/responsiveTable'

const channelOptions = [
  { value: '', label: '全部管道' },
  { value: 'in_app', label: '站內通知' },
  { value: 'sms', label: 'SMS' },
  { value: 'email', label: 'Email' },
]

const statusOptions = [
  { value: '', label: '全部狀態' },
  { value: 'pending', label: '待發送' },
  { value: 'sent', label: '成功' },
  { value: 'failed', label: '失敗' },
]

const channelLabel: Record<string, string> = {
  in_app: '站內',
  sms: 'SMS',
  email: 'Email',
}

const statusMeta: Record<string, { label: string; color: string }> = {
  pending: { label: '待發送', color: 'gold' },
  sent: { label: '成功', color: 'green' },
  failed: { label: '失敗', color: 'red' },
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-TW', { hour12: false })
}

function NotificationsPage() {
  const { message } = App.useApp()
  const [inAppItems, setInAppItems] = useState<NotificationItem[]>([])
  const [inAppLoading, setInAppLoading] = useState(true)
  const [inAppError, setInAppError] = useState<Error | null>(null)
  const [history, setHistory] = useState<NotificationListResponse>({ items: [], page: 1, pageSize: 10, total: 0 })
  const [historyLoading, setHistoryLoading] = useState(true)
  const [historyError, setHistoryError] = useState<Error | null>(null)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [channel, setChannel] = useState('')
  const [status, setStatus] = useState('')
  const [eventType, setEventType] = useState('')
  const [keywordInput, setKeywordInput] = useState('')
  const [keyword, setKeyword] = useState('')
  const [actionId, setActionId] = useState<number | null>(null)
  const [markingAll, setMarkingAll] = useState(false)
  const [retryTarget, setRetryTarget] = useState<NotificationItem | null>(null)
  const [retrying, setRetrying] = useState(false)
  const [retryForm] = Form.useForm<{ recipient: string }>()

  const unreadItems = useMemo(
    () => inAppItems.filter((item) => !item.readAtUtc),
    [inAppItems],
  )

  const loadInApp = useCallback(async (signal?: AbortSignal) => {
    setInAppLoading(true)
    setInAppError(null)
    try {
      const firstPage = await getNotifications({ channel: 'in_app', page: 1, pageSize: 100 }, signal)
      const items = [...firstPage.items]
      const pageCount = Math.ceil(firstPage.total / firstPage.pageSize)

      for (let nextPage = 2; nextPage <= pageCount; nextPage += 1) {
        const result = await getNotifications({ channel: 'in_app', page: nextPage, pageSize: 100 }, signal)
        items.push(...result.items)
      }

      setInAppItems(items)
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setInAppError(error instanceof Error ? error : new Error('載入站內通知失敗。'))
      }
    } finally {
      setInAppLoading(false)
    }
  }, [])

  const loadHistory = useCallback(async (signal?: AbortSignal) => {
    setHistoryLoading(true)
    setHistoryError(null)
    try {
      const result = await getNotifications({
        keyword: keyword || undefined,
        channel: channel || undefined,
        status: status || undefined,
        eventType: eventType.trim() || undefined,
        page,
        pageSize,
      }, signal)
      setHistory(result)
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setHistoryError(error instanceof Error ? error : new Error('載入發送紀錄失敗。'))
      }
    } finally {
      setHistoryLoading(false)
    }
  }, [channel, eventType, keyword, page, pageSize, status])

  useEffect(() => {
    const controller = new AbortController()
    void loadInApp(controller.signal)
    return () => controller.abort()
  }, [loadInApp])

  useEffect(() => {
    const controller = new AbortController()
    void loadHistory(controller.signal)
    return () => controller.abort()
  }, [loadHistory])

  const syncNotification = (updated: NotificationItem) => {
    setInAppItems((items) => items.map((item) => item.id === updated.id ? updated : item))
    setHistory((current) => ({
      ...current,
      items: current.items.map((item) => item.id === updated.id ? updated : item),
    }))
  }

  const handleMarkRead = async (item: NotificationItem) => {
    setActionId(item.id)
    try {
      const updated = await markNotificationRead(item.id)
      syncNotification(updated)
      message.success('已標記為已讀。')
    } catch (error) {
      message.error(error instanceof Error ? error.message : '標記已讀失敗。')
    } finally {
      setActionId(null)
    }
  }

  const handleMarkAll = async () => {
    if (unreadItems.length === 0) return
    setMarkingAll(true)
    try {
      for (const item of unreadItems) {
        const updated = await markNotificationRead(item.id)
        syncNotification(updated)
      }
      message.success(`已將 ${unreadItems.length} 則通知標記為已讀。`)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '全部標記已讀失敗。')
      await loadInApp()
      await loadHistory()
    } finally {
      setMarkingAll(false)
    }
  }

  const openRetry = (item: NotificationItem) => {
    setRetryTarget(item)
    retryForm.setFieldsValue({ recipient: item.recipient })
  }

  const handleRetry = async () => {
    if (!retryTarget) return
    const values = await retryForm.validateFields()
    setRetrying(true)
    try {
      const updated = await retryNotification(retryTarget.id, { recipient: values.recipient.trim() })
      syncNotification(updated)
      setRetryTarget(null)
      retryForm.resetFields()
      message.success(updated.status === 'sent' ? '通知重送成功。' : '已完成重送，但發送結果仍為失敗。')
      await loadHistory()
    } catch (error) {
      message.error(error instanceof Error ? error.message : '通知重送失敗。')
    } finally {
      setRetrying(false)
    }
  }

  const handleSearch = (value?: string) => {
    const nextKeyword = (value ?? keywordInput).trim()
    setKeywordInput(nextKeyword)
    setKeyword(nextKeyword)
    setPage(1)
  }

  const resetFilters = () => {
    setKeywordInput('')
    setKeyword('')
    setChannel('')
    setStatus('')
    setEventType('')
    setPage(1)
  }

  const handleTableChange = (pagination: TablePaginationConfig) => {
    setPage(pagination.current ?? 1)
    setPageSize(pagination.pageSize ?? 10)
  }

  const columns: ColumnsType<NotificationItem> = [
    {
      title: '時間',
      dataIndex: 'createdAtUtc',
      width: 170,
      onCell: () => responsiveCell('時間'),
      render: (value: string) => formatDateTime(value),
    },
    {
      title: '管道',
      dataIndex: 'channel',
      width: 90,
      onCell: () => responsiveCell('管道'),
      render: (value: string) => <Tag>{channelLabel[value] ?? value}</Tag>,
    },
    {
      title: '收件人',
      dataIndex: 'recipient',
      width: 190,
      ellipsis: true,
      onCell: () => responsiveCell('收件人'),
    },
    {
      title: '事件 / 標題',
      key: 'event',
      width: 260,
      onCell: () => responsiveCell('事件／標題'),
      render: (_, item) => (
        <div>
          <Typography.Text strong>{item.title}</Typography.Text>
          <div><Typography.Text type="secondary">{item.eventType}</Typography.Text></div>
        </div>
      ),
    },
    {
      title: '結果',
      dataIndex: 'status',
      width: 130,
      onCell: () => responsiveCell('結果'),
      render: (value: string, item) => {
        const meta = statusMeta[value] ?? { label: value, color: 'default' }
        return (
          <Space direction="vertical" size={2}>
            <Tag color={meta.color}>{meta.label}</Tag>
            {item.lastError ? <Typography.Text type="danger">{item.lastError}</Typography.Text> : null}
          </Space>
        )
      },
    },
    {
      title: '嘗試次數',
      dataIndex: 'attemptCount',
      width: 100,
      align: 'center',
      onCell: () => responsiveCell('嘗試次數'),
    },
    {
      title: '操作',
      key: 'actions',
      width: 100,
      fixed: 'right',
      onCell: () => responsiveCell('操作'),
      render: (_, item) => item.status === 'failed' && item.channel !== 'in_app'
        ? <Button type="link" onClick={() => openRetry(item)}>重送</Button>
        : '-',
    },
  ]

  if ((inAppError instanceof ApiError && inAppError.kind === 'forbidden')
    || (historyError instanceof ApiError && historyError.kind === 'forbidden')) {
    return <ForbiddenState requiredRole="具通知資料查看權限的角色" contact="系統管理員" />
  }

  return (
    <div className="notifications-page">
      <div className="notifications-intro">
        <div>
          <Typography.Title level={2}>通知中心</Typography.Title>
          <Typography.Text type="secondary">查看站內通知，以及 SMS／Email 的發送紀錄與失敗重送結果。</Typography.Text>
        </div>
        <Button
          icon={<CheckCircleOutlined />}
          disabled={unreadItems.length === 0}
          loading={markingAll}
          onClick={() => void handleMarkAll()}
        >
          全部標為已讀
        </Button>
      </div>

      <div className="notifications-grid">
        <Card
          title={(
            <Space>
              <BellOutlined />
              <span>站內通知</span>
              <Badge count={unreadItems.length} overflowCount={99} />
            </Space>
          )}
          extra={<Button type="text" icon={<ReloadOutlined />} onClick={() => void loadInApp()}>重新整理</Button>}
        >
          {inAppLoading && inAppItems.length === 0 ? <LoadingState rows={5} /> : null}
          {inAppError && inAppItems.length === 0 ? (
            <ErrorState title="無法載入站內通知" message={inAppError.message} onRetry={() => void loadInApp()} />
          ) : null}
          {!inAppLoading && !inAppError && inAppItems.length === 0 ? (
            <EmptyState description="目前沒有站內通知。" />
          ) : null}
          {inAppItems.length > 0 ? (
            <List
              className="notifications-inapp-list"
              dataSource={inAppItems}
              renderItem={(item) => (
                <List.Item
                  className={item.readAtUtc ? 'notification-read' : 'notification-unread'}
                  actions={!item.readAtUtc ? [
                    <Button
                      key="read"
                      type="link"
                      loading={actionId === item.id}
                      onClick={() => void handleMarkRead(item)}
                    >
                      標為已讀
                    </Button>,
                  ] : undefined}
                >
                  <List.Item.Meta
                    avatar={item.eventType.toLowerCase().includes('email') ? <MailOutlined /> : <MessageOutlined />}
                    title={(
                      <Space wrap>
                        <Typography.Text strong={!item.readAtUtc}>{item.title}</Typography.Text>
                        <Tag color={item.readAtUtc ? 'default' : 'blue'}>{item.readAtUtc ? '已讀' : '未讀'}</Tag>
                      </Space>
                    )}
                    description={(
                      <Space direction="vertical" size={2}>
                        <Typography.Text>{item.message}</Typography.Text>
                        <Typography.Text type="secondary">
                          {item.eventType}
                          {item.referenceId ? ` · ${item.referenceType ?? 'Reference'} ${item.referenceId}` : ''}
                          {` · ${formatDateTime(item.createdAtUtc)}`}
                        </Typography.Text>
                      </Space>
                    )}
                  />
                </List.Item>
              )}
            />
          ) : null}
        </Card>

        <Card title="發送紀錄">
          <div className="notifications-toolbar">
            <Input.Search
              allowClear
              value={keywordInput}
              placeholder="搜尋收件人／標題／內容／參照編號"
              onChange={(event) => setKeywordInput(event.target.value)}
              onSearch={handleSearch}
            />
            <Select
              value={channel}
              options={channelOptions}
              onChange={(value) => { setChannel(value); setPage(1) }}
            />
            <Select
              value={status}
              options={statusOptions}
              onChange={(value) => { setStatus(value); setPage(1) }}
            />
            <Input
              value={eventType}
              placeholder="事件類型"
              onChange={(event) => setEventType(event.target.value)}
              onPressEnter={() => setPage(1)}
            />
            <Button onClick={resetFilters}>清除篩選</Button>
          </div>

          {historyError && history.items.length === 0 ? (
            <ErrorState title="無法載入發送紀錄" message={historyError.message} onRetry={() => void loadHistory()} />
          ) : historyLoading && history.items.length === 0 ? (
            <LoadingState rows={5} />
          ) : history.items.length === 0 ? (
            <EmptyState description="目前沒有符合條件的發送紀錄。" />
          ) : (
            <Table<NotificationItem>
              className="responsive-data-table"
              rowKey="id"
              columns={columns}
              dataSource={history.items}
              loading={historyLoading}
              scroll={{ x: 1100 }}
              pagination={{
                current: history.page || page,
                pageSize: history.pageSize || pageSize,
                total: history.total,
                showSizeChanger: true,
                showTotal: (total) => `共 ${total} 筆`,
              }}
              onChange={handleTableChange}
            />
          )}
        </Card>
      </div>

      <Modal
        title="重送通知"
        open={Boolean(retryTarget)}
        okText="重送"
        cancelText="取消"
        confirmLoading={retrying}
        onOk={() => void handleRetry()}
        onCancel={() => {
          setRetryTarget(null)
          retryForm.resetFields()
        }}
      >
        <Form form={retryForm} layout="vertical">
          <Form.Item
            name="recipient"
            label="收件人"
            rules={[{ required: true, whitespace: true, message: '請輸入收件人。' }]}
          >
            <Input placeholder="可修正 Email 或手機號碼後重新發送" />
          </Form.Item>
          {retryTarget?.lastError ? (
            <Typography.Paragraph type="danger">前次失敗原因：{retryTarget.lastError}</Typography.Paragraph>
          ) : null}
        </Form>
      </Modal>
    </div>
  )
}

export default NotificationsPage
