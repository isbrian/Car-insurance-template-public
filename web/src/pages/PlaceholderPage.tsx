import { Card, Typography } from 'antd'

type PlaceholderPageProps = {
  title: string
  description: string
}

function PlaceholderPage({ title, description }: PlaceholderPageProps) {
  return (
    <Card>
      <Typography.Title level={3}>{title}</Typography.Title>
      <Typography.Paragraph>{description}</Typography.Paragraph>
      <Typography.Text type="secondary">此功能頁面目前為開發占位，後續將接入實際資料與操作流程。</Typography.Text>
    </Card>
  )
}

export default PlaceholderPage
