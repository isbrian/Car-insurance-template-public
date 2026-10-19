import { useQuery } from '@tanstack/react-query'
import { ReloadOutlined } from '@ant-design/icons'
import { Button, Card, Statistic } from 'antd'
import { getDashboardSummary } from '../../api/dashboard'
import { ApiError } from '../../api/http'
import type { DashboardSummary } from '../../api/dashboard'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { queryKeys } from '../../query/queryKeys'

function isEmptySummary(summary: DashboardSummary): boolean {
  return summary.customerTotal === 0
    && summary.activePolicyTotal === 0
    && summary.pendingUnderwritingTotal === 0
    && summary.pendingClaimTotal === 0
}

function DashboardPage() {
  const {
    data: summary,
    error,
    isPending,
    isFetching,
    refetch,
  } = useQuery({
    queryKey: queryKeys.dashboard.summary,
    queryFn: ({ signal }) => getDashboardSummary(signal),
  })

  if (isPending && !summary) {
    return <LoadingState rows={5} />
  }

  if (error instanceof ApiError && error.kind === 'forbidden') {
    return <ForbiddenState requiredRole="具儀表板資料查看權限的角色" contact="系統管理員" />
  }

  if (error) {
    return <ErrorState title="儀表板資料讀取失敗" message={error.message} onRetry={() => void refetch()} />
  }

  if (!summary || isEmptySummary(summary)) {
    return (
      <EmptyState
        description="目前沒有可顯示的儀表板統計資料"
        action={<Button icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()}>重新整理</Button>}
      />
    )
  }

  return (
    <section className="dashboard-page">
      <div className="dashboard-actions">
        <Button icon={<ReloadOutlined />} loading={isFetching} onClick={() => void refetch()}>重新整理</Button>
      </div>
      <div className="dashboard-grid">
        <Card>
          <Statistic title="客戶總數" value={summary.customerTotal} />
        </Card>
        <Card>
          <Statistic title="有效保單" value={summary.activePolicyTotal} />
        </Card>
        <Card>
          <Statistic title="待核保" value={summary.pendingUnderwritingTotal} />
        </Card>
        <Card>
          <Statistic title="待處理理賠" value={summary.pendingClaimTotal} />
        </Card>
      </div>
    </section>
  )
}

export default DashboardPage
