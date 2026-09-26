import { Typography } from 'antd'

type MaskedValueProps = {
  value?: string | null
  emptyText?: string
}

function MaskedValue({ value, emptyText = '—' }: MaskedValueProps) {
  return <Typography.Text>{value?.trim() ? value : emptyText}</Typography.Text>
}

export default MaskedValue
