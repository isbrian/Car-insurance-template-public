import { Descriptions, Drawer, Space, Tag, Typography } from 'antd'
import type { AuditLog } from '../../api/auditLogs'

type AuditLogDetailDrawerProps = {
  auditLog: AuditLog | null
  open: boolean
  onClose: () => void
}

function formatOccurredAt(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value || '—'

  return new Intl.DateTimeFormat('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(date)
}

function formatSnapshot(value: string | null): string | null {
  const normalized = value?.trim()
  if (!normalized) return null

  try {
    return JSON.stringify(JSON.parse(normalized), null, 2)
  } catch {
    return value
  }
}

function SnapshotBlock({ title, value }: { title: string; value: string | null }) {
  const formatted = formatSnapshot(value)

  return (
    <Space direction="vertical" size="small" style={{ width: '100%' }}>
      <Typography.Title level={5} style={{ margin: 0 }}>{title}</Typography.Title>
      {formatted ? (
        <Typography.Paragraph
          style={{
            margin: 0,
            padding: 12,
            background: 'rgba(0, 0, 0, 0.03)',
            borderRadius: 6,
            whiteSpace: 'pre-wrap',
            overflowWrap: 'anywhere',
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
          }}
          copyable={{ text: formatted }}
        >
          {formatted}
        </Typography.Paragraph>
      ) : (
        <Typography.Text type="secondary">無快照</Typography.Text>
      )}
    </Space>
  )
}

function AuditLogDetailDrawer({ auditLog, open, onClose }: AuditLogDetailDrawerProps) {
  return (
    <Drawer
      title="稽核異動詳情"
      open={open}
      onClose={onClose}
      width={720}
      destroyOnHidden
    >
      {auditLog ? (
        <Space direction="vertical" size="large" style={{ width: '100%' }}>
          <Descriptions bordered size="small" column={1}>
            <Descriptions.Item label="稽核 Id">{auditLog.id}</Descriptions.Item>
            <Descriptions.Item label="發生時間">{formatOccurredAt(auditLog.occurredAtUtc)}</Descriptions.Item>
            <Descriptions.Item label="操作人">{auditLog.actor}</Descriptions.Item>
            <Descriptions.Item label="角色"><Tag>{auditLog.role}</Tag></Descriptions.Item>
            <Descriptions.Item label="動作"><Typography.Text code>{auditLog.action}</Typography.Text></Descriptions.Item>
            <Descriptions.Item label="物件">{auditLog.objectType} #{auditLog.objectId}</Descriptions.Item>
            <Descriptions.Item label="來源 IP">{auditLog.sourceIp || '—'}</Descriptions.Item>
          </Descriptions>

          <SnapshotBlock title="Before" value={auditLog.beforeJson} />
          <SnapshotBlock title="After" value={auditLog.afterJson} />
        </Space>
      ) : null}
    </Drawer>
  )
}

export default AuditLogDetailDrawer
