import { useMemo, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App,
  Button,
  Card,
  Checkbox,
  Descriptions,
  Form,
  Input,
  Modal,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType, TablePaginationConfig } from 'antd/es/table'
import {
  DownloadOutlined,
  FileAddOutlined,
  FileSearchOutlined,
  HistoryOutlined,
  PlusOutlined,
  ReloadOutlined,
} from '@ant-design/icons'
import {
  addDocumentTemplateVersion,
  createDocumentTemplate,
  getCaseDocumentContentUrl,
  getCaseDocuments,
  getDocumentTemplate,
  getDocumentTemplates,
  setDocumentTemplateActiveVersion,
} from '../../api/documents'
import type {
  CaseDocument,
  CaseDocumentListResponse,
  DocumentTemplateCreateRequest,
  DocumentTemplateDetail,
  DocumentTemplateSummary,
  DocumentTemplateVersionCreateRequest,
} from '../../api/documents'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { ApiError } from '../../api/http'
import { responsiveCell } from '../../components/shared/responsiveTable'
import { queryKeys } from '../../query/queryKeys'

type DocumentTab = 'cases' | 'pending' | 'templates'

type CreateTemplateFormValues = {
  code: string
  name: string
  description?: string
}

type AddVersionFormValues = {
  version: string
  content?: string
  activate?: boolean
}

const documentTypeOptions = [
  { value: '', label: '全部文件類型' },
  { value: 'identity_document', label: '身分證件' },
  { value: 'vehicle_registration', label: '行車執照' },
]

const statusOptions = [
  { value: '', label: '全部狀態' },
  { value: 'uploaded', label: '已上傳' },
  { value: 'returned', label: '已退回' },
]

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('zh-TW', { hour12: false })
}

function formatFileSize(bytes: number) {
  if (!Number.isFinite(bytes) || bytes < 0) return '-'
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`
}

function renderDocumentStatus(status: string) {
  if (status === 'uploaded') return <Tag color="success">已上傳</Tag>
  if (status === 'returned') return <Tag color="error">已退回</Tag>
  return <Tag>{status}</Tag>
}

function DocumentsPage() {
  const { message } = App.useApp()
  const queryClient = useQueryClient()
  const [activeTab, setActiveTab] = useState<DocumentTab>('cases')
  const [keywordInput, setKeywordInput] = useState('')
  const [keyword, setKeyword] = useState('')
  const [status, setStatus] = useState('')
  const [documentType, setDocumentType] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [versionOpen, setVersionOpen] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [createForm] = Form.useForm<CreateTemplateFormValues>()
  const [versionForm] = Form.useForm<AddVersionFormValues>()

  const caseRequest = {
    keyword,
    status: activeTab === 'pending' ? 'returned' : status,
    documentType,
    page,
    pageSize,
  }
  const casesQuery = useQuery({
    queryKey: queryKeys.documents.cases(caseRequest),
    queryFn: ({ signal }) => getCaseDocuments(caseRequest, signal),
    enabled: activeTab !== 'templates',
  })
  const pendingTotalQuery = useQuery({
    queryKey: queryKeys.documents.pendingTotal,
    queryFn: ({ signal }) => getCaseDocuments({ status: 'returned', page: 1, pageSize: 1 }, signal),
  })
  const templatesQuery = useQuery({
    queryKey: queryKeys.documents.templates,
    queryFn: ({ signal }) => getDocumentTemplates(signal),
    enabled: activeTab === 'templates',
  })
  const detailQuery = useQuery({
    queryKey: queryKeys.documents.template(selectedTemplateId ?? 0),
    queryFn: ({ signal }) => getDocumentTemplate(selectedTemplateId!, signal),
    enabled: detailOpen && selectedTemplateId !== null,
  })
  const caseResult: CaseDocumentListResponse | undefined = casesQuery.data
  const caseLoading = casesQuery.isFetching
  const caseError = casesQuery.error
  const pendingTotal = activeTab === 'pending' && caseResult
    ? caseResult.total
    : (pendingTotalQuery.data?.total ?? 0)
  const templates: DocumentTemplateSummary[] = templatesQuery.data ?? []
  const templateLoading = templatesQuery.isFetching
  const templateError = templatesQuery.error
  const selectedTemplate: DocumentTemplateDetail | undefined = detailQuery.data
  const detailLoading = detailQuery.isFetching
  const detailError = detailQuery.error

  const openTemplateDetail = (id: number) => {
    setDetailOpen(true)
    setSelectedTemplateId(id)
  }

  const submitCreateTemplate = async () => {
    let values: CreateTemplateFormValues
    try {
      values = await createForm.validateFields()
    } catch {
      return
    }

    const request: DocumentTemplateCreateRequest = {
      code: values.code.trim(),
      name: values.name.trim(),
      description: values.description?.trim() || undefined,
    }

    setSubmitting(true)
    try {
      const created = await createDocumentTemplate(request)
      message.success('文件範本已建立。')
      setCreateOpen(false)
      createForm.resetFields()
      await queryClient.invalidateQueries({ queryKey: queryKeys.documents.templates })
      openTemplateDetail(created.id)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '建立文件範本失敗。')
    } finally {
      setSubmitting(false)
    }
  }

  const submitVersion = async () => {
    if (!selectedTemplate) return

    let values: AddVersionFormValues
    try {
      values = await versionForm.validateFields()
    } catch {
      return
    }

    const request: DocumentTemplateVersionCreateRequest = {
      version: values.version.trim(),
      content: values.content?.trim() || undefined,
      activate: Boolean(values.activate),
    }

    setSubmitting(true)
    try {
      const updated = await addDocumentTemplateVersion(selectedTemplate.id, request)
      queryClient.setQueryData(queryKeys.documents.template(selectedTemplate.id), updated)
      setVersionOpen(false)
      versionForm.resetFields()
      await queryClient.invalidateQueries({ queryKey: queryKeys.documents.templates })
      message.success(values.activate ? '新版本已新增並啟用。' : '新版本已新增。')
    } catch (error) {
      message.error(error instanceof Error ? error.message : '新增範本版本失敗。')
    } finally {
      setSubmitting(false)
    }
  }

  const activateVersion = async (versionId: number) => {
    if (!selectedTemplate || selectedTemplate.activeVersionId === versionId) return
    setSubmitting(true)
    try {
      const updated = await setDocumentTemplateActiveVersion(selectedTemplate.id, versionId)
      queryClient.setQueryData(queryKeys.documents.template(selectedTemplate.id), updated)
      await queryClient.invalidateQueries({ queryKey: queryKeys.documents.templates })
      message.success(`已切換啟用版本為 ${updated.activeVersion ?? '-' }。`)
    } catch (error) {
      message.error(error instanceof Error ? error.message : '切換啟用版本失敗。')
    } finally {
      setSubmitting(false)
    }
  }

  const resetCasePage = () => setPage(1)

  const caseColumns = useMemo<ColumnsType<CaseDocument>>(() => [
    {
      title: '案件編號',
      dataIndex: 'applicationNumber',
      key: 'applicationNumber',
      onCell: () => responsiveCell('案件編號'),
      render: (value: string) => <Typography.Text strong>{value}</Typography.Text>,
    },
    { title: '客戶', dataIndex: 'customerName', key: 'customerName', onCell: () => responsiveCell('客戶') },
    { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
    { title: '文件類型', dataIndex: 'displayName', key: 'displayName', onCell: () => responsiveCell('文件類型') },
    {
      title: '檔名',
      dataIndex: 'originalFileName',
      key: 'originalFileName',
      ellipsis: true,
      onCell: () => responsiveCell('檔名'),
    },
    {
      title: '狀態',
      dataIndex: 'status',
      key: 'status',
      onCell: () => responsiveCell('狀態'),
      render: renderDocumentStatus,
    },
    {
      title: '大小',
      dataIndex: 'sizeBytes',
      key: 'sizeBytes',
      align: 'right',
      onCell: () => responsiveCell('大小'),
      render: (value: number) => formatFileSize(value),
    },
    {
      title: '更新時間',
      dataIndex: 'updatedAtUtc',
      key: 'updatedAtUtc',
      onCell: () => responsiveCell('更新時間'),
      render: formatDateTime,
    },
    {
      title: '操作',
      key: 'action',
      fixed: 'right',
      width: 92,
      onCell: () => responsiveCell('操作'),
      render: (_, record) => (
        <Button
          type="link"
          icon={<DownloadOutlined />}
          href={getCaseDocumentContentUrl(record)}
          target="_blank"
          rel="noreferrer"
        >
          下載
        </Button>
      ),
    },
  ], [])

  const pendingColumns = useMemo<ColumnsType<CaseDocument>>(() => [
    {
      title: '案件編號',
      dataIndex: 'applicationNumber',
      key: 'applicationNumber',
      onCell: () => responsiveCell('案件編號'),
      render: (value: string) => <Typography.Text strong>{value}</Typography.Text>,
    },
    { title: '客戶', dataIndex: 'customerName', key: 'customerName', onCell: () => responsiveCell('客戶') },
    { title: '車牌', dataIndex: 'licensePlate', key: 'licensePlate', onCell: () => responsiveCell('車牌') },
    { title: '文件', dataIndex: 'displayName', key: 'displayName', onCell: () => responsiveCell('文件') },
    {
      title: '退回原因',
      dataIndex: 'returnReason',
      key: 'returnReason',
      onCell: () => responsiveCell('退回原因'),
      render: (value: string | null) => value || '-',
    },
    {
      title: '退回時間',
      dataIndex: 'returnedAtUtc',
      key: 'returnedAtUtc',
      onCell: () => responsiveCell('退回時間'),
      render: formatDateTime,
    },
    {
      title: '操作',
      key: 'action',
      width: 92,
      onCell: () => responsiveCell('操作'),
      render: (_, record) => (
        <Button
          type="link"
          icon={<DownloadOutlined />}
          href={getCaseDocumentContentUrl(record)}
          target="_blank"
          rel="noreferrer"
        >
          查看
        </Button>
      ),
    },
  ], [])

  const templateColumns = useMemo<ColumnsType<DocumentTemplateSummary>>(() => [
    { title: '代碼', dataIndex: 'code', key: 'code', onCell: () => responsiveCell('代碼') },
    {
      title: '範本名稱',
      dataIndex: 'name',
      key: 'name',
      onCell: () => responsiveCell('範本名稱'),
      render: (value: string) => <Typography.Text strong>{value}</Typography.Text>,
    },
    {
      title: '目前版本',
      dataIndex: 'activeVersion',
      key: 'activeVersion',
      onCell: () => responsiveCell('目前版本'),
      render: (value: string | null) => value ? <Tag color="blue">{value}</Tag> : <Tag>未啟用</Tag>,
    },
    { title: '歷史版本', dataIndex: 'versionCount', key: 'versionCount', align: 'right', onCell: () => responsiveCell('歷史版本') },
    {
      title: '更新時間',
      dataIndex: 'updatedAtUtc',
      key: 'updatedAtUtc',
      onCell: () => responsiveCell('更新時間'),
      render: formatDateTime,
    },
    {
      title: '操作',
      key: 'action',
      width: 110,
      onCell: () => responsiveCell('操作'),
      render: (_, record) => (
        <Button type="link" icon={<FileSearchOutlined />} onClick={() => openTemplateDetail(record.id)}>
          版本管理
        </Button>
      ),
    },
  ], [])

  const handleCaseTableChange = (pagination: TablePaginationConfig) => {
    setPage(pagination.current ?? 1)
    setPageSize(pagination.pageSize ?? 20)
  }

  const caseToolbar = (
    <div className="documents-toolbar">
      <Input.Search
        allowClear
        value={keywordInput}
        placeholder="案件編號／客戶／車牌／檔名"
        onChange={(event) => setKeywordInput(event.target.value)}
        onSearch={(value) => {
          setKeyword(value)
          resetCasePage()
        }}
      />
      {activeTab === 'cases' ? (
        <Select
          value={status}
          options={statusOptions}
          onChange={(value) => {
            setStatus(value)
            resetCasePage()
          }}
        />
      ) : null}
      <Select
        value={documentType}
        options={documentTypeOptions}
        onChange={(value) => {
          setDocumentType(value)
          resetCasePage()
        }}
      />
      <Button icon={<ReloadOutlined />} onClick={() => {
        void casesQuery.refetch()
        void pendingTotalQuery.refetch()
      }}>
        重新整理
      </Button>
    </div>
  )

  const renderCaseTab = () => {
    if (caseLoading && !caseResult) return <LoadingState rows={6} />
    if (caseError instanceof ApiError && caseError.kind === 'forbidden') {
      return <ForbiddenState requiredRole="具文件資料查看權限的角色" contact="系統管理員" />
    }
    if (caseError) {
      return (
        <ErrorState
          title="案件文件載入失敗"
          message={caseError.message}
          onRetry={() => void casesQuery.refetch()}
        />
      )
    }

    return (
      <Card>
        {caseToolbar}
        {(caseResult?.items.length ?? 0) === 0 ? (
          <EmptyState description={activeTab === 'pending' ? '目前沒有待補件文件' : '沒有符合條件的案件文件'} />
        ) : (
          <Table<CaseDocument>
            className="responsive-data-table"
            rowKey="id"
            loading={caseLoading}
            columns={activeTab === 'pending' ? pendingColumns : caseColumns}
            dataSource={caseResult?.items ?? []}
            scroll={{ x: activeTab === 'pending' ? 900 : 1180 }}
            pagination={{
              current: caseResult?.page ?? page,
              pageSize: caseResult?.pageSize ?? pageSize,
              total: caseResult?.total ?? 0,
              showSizeChanger: true,
              showTotal: (total) => `共 ${total} 份文件`,
            }}
            onChange={handleCaseTableChange}
          />
        )}
      </Card>
    )
  }

  const renderTemplateTab = () => {
    if (templateLoading && templates.length === 0) return <LoadingState rows={5} />
    if (templateError instanceof ApiError && templateError.kind === 'forbidden') {
      return <ForbiddenState requiredRole="具文件範本查看權限的角色" contact="系統管理員" />
    }
    if (templateError) {
      return (
        <ErrorState
          title="文件範本載入失敗"
          message={templateError.message}
          onRetry={() => void templatesQuery.refetch()}
        />
      )
    }

    return (
      <Card>
        <div className="documents-toolbar documents-template-toolbar">
          <Typography.Text type="secondary">歷史版本會永久保留，已出單資料應沿用當時版本。</Typography.Text>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={() => void templatesQuery.refetch()}>重新整理</Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreateOpen(true)}>建立範本</Button>
          </Space>
        </div>
        {templates.length === 0 ? (
          <EmptyState description="目前沒有文件範本" />
        ) : (
          <Table<DocumentTemplateSummary>
            className="responsive-data-table"
            rowKey="id"
            loading={templateLoading}
            columns={templateColumns}
            dataSource={templates}
            pagination={false}
            scroll={{ x: 820 }}
          />
        )}
      </Card>
    )
  }

  const tabItems = [
    { key: 'cases', label: '案件文件', children: activeTab === 'cases' ? renderCaseTab() : null },
    { key: 'pending', label: `待補件（${pendingTotal}）`, children: activeTab === 'pending' ? renderCaseTab() : null },
    { key: 'templates', label: '文件範本', children: activeTab === 'templates' ? renderTemplateTab() : null },
  ]

  return (
    <div className="documents-page">
      <div className="documents-intro">
        <div>
          <Typography.Title level={2}>文件管理</Typography.Title>
          <Typography.Text type="secondary">集中檢視案件文件、待補件與文件範本歷史版本。</Typography.Text>
        </div>
        <Tag icon={<HistoryOutlined />} color="blue">範本版本不可刪除</Tag>
      </div>

      <Card className="documents-tabs-card">
        <Tabs
          activeKey={activeTab}
          items={tabItems}
          onChange={(key) => {
            setActiveTab(key as DocumentTab)
            setPage(1)
            setKeywordInput('')
            setKeyword('')
            setStatus('')
            setDocumentType('')
          }}
        />
      </Card>

      <Modal
        title="建立文件範本"
        open={createOpen}
        confirmLoading={submitting}
        okText="建立"
        cancelText="取消"
        onOk={() => void submitCreateTemplate()}
        onCancel={() => {
          setCreateOpen(false)
          createForm.resetFields()
        }}
      >
        <Form form={createForm} layout="vertical">
          <Form.Item label="範本代碼" name="code" rules={[{ required: true, message: '請輸入範本代碼' }, { max: 50 }]}>
            <Input placeholder="例如 policy" />
          </Form.Item>
          <Form.Item label="範本名稱" name="name" rules={[{ required: true, message: '請輸入範本名稱' }, { max: 150 }]}>
            <Input placeholder="例如 保單範本" />
          </Form.Item>
          <Form.Item label="說明" name="description" rules={[{ max: 500 }]}>
            <Input.TextArea rows={3} showCount maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={selectedTemplate ? `${selectedTemplate.name}－版本管理` : '版本管理'}
        open={detailOpen}
        width={820}
        footer={null}
        onCancel={() => {
          setDetailOpen(false)
          setSelectedTemplateId(null)
        }}
      >
        {detailLoading ? (
          <LoadingState rows={5} />
        ) : detailError instanceof ApiError && detailError.kind === 'forbidden' ? (
          <ForbiddenState requiredRole="具文件範本查看權限的角色" contact="系統管理員" />
        ) : detailError instanceof ApiError && detailError.status === 404 ? (
          <EmptyState description="指定的文件範本不存在或已被移除" />
        ) : detailError ? (
          <ErrorState
            title="文件範本詳情載入失敗"
            message={detailError.message}
            onRetry={() => void detailQuery.refetch()}
          />
        ) : !selectedTemplate ? (
          <EmptyState description="沒有可顯示的文件範本詳情" />
        ) : (
          <Space direction="vertical" size="large" className="full-width">
            <Descriptions bordered size="small" column={{ xs: 1, sm: 2 }}>
              <Descriptions.Item label="代碼">{selectedTemplate.code}</Descriptions.Item>
              <Descriptions.Item label="目前版本">{selectedTemplate.activeVersion ?? '未啟用'}</Descriptions.Item>
              <Descriptions.Item label="說明">{selectedTemplate.description || '-'}</Descriptions.Item>
            </Descriptions>

            <div className="documents-template-actions">
              <Typography.Title level={5}>歷史版本</Typography.Title>
              <Button
                type="primary"
                icon={<FileAddOutlined />}
                onClick={() => {
                  versionForm.resetFields()
                  setVersionOpen(true)
                }}
              >
                新增版本
              </Button>
            </div>

            {selectedTemplate.versions.length === 0 ? (
              <EmptyState description="尚未建立任何版本" />
            ) : (
              <div className="documents-version-list">
                {selectedTemplate.versions.map((version) => {
                  const active = selectedTemplate.activeVersionId === version.id
                  return (
                    <Card key={version.id} size="small" className="documents-version-card">
                      <div className="documents-version-heading">
                        <Space wrap>
                          <Typography.Text strong>{version.version}</Typography.Text>
                          {active ? <Tag color="success">目前啟用</Tag> : <Tag>歷史版本</Tag>}
                        </Space>
                        <Button
                          size="small"
                          disabled={active || submitting}
                          onClick={() => void activateVersion(version.id)}
                        >
                          設為啟用版本
                        </Button>
                      </div>
                      <Typography.Paragraph type="secondary" copyable={{ text: version.sha256 }}>
                        SHA-256：{version.sha256}
                      </Typography.Paragraph>
                      <Typography.Text type="secondary">建立時間：{formatDateTime(version.createdAtUtc)}</Typography.Text>
                      <div className="documents-version-content">
                        <Typography.Text strong>內容</Typography.Text>
                        <pre>{version.content || '（無內容）'}</pre>
                      </div>
                    </Card>
                  )
                })}
              </div>
            )}
          </Space>
        )}
      </Modal>

      <Modal
        title={selectedTemplate ? `新增 ${selectedTemplate.name} 版本` : '新增版本'}
        open={versionOpen}
        confirmLoading={submitting}
        okText="新增版本"
        cancelText="取消"
        onOk={() => void submitVersion()}
        onCancel={() => {
          setVersionOpen(false)
          versionForm.resetFields()
        }}
      >
        <Form form={versionForm} layout="vertical" initialValues={{ activate: true }}>
          <Form.Item label="版本" name="version" rules={[{ required: true, message: '請輸入版本號' }, { max: 50 }]}>
            <Input placeholder="例如 v1.0" />
          </Form.Item>
          <Form.Item label="範本內容" name="content" rules={[{ max: 10000 }]}>
            <Input.TextArea rows={8} showCount maxLength={10000} placeholder="可留空，最多 10000 字" />
          </Form.Item>
          <Form.Item name="activate" valuePropName="checked">
            <Checkbox>新增後立即設為啟用版本</Checkbox>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  )
}

export default DocumentsPage
