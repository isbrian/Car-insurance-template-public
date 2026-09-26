import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  Alert,
  App,
  Button,
  Card,
  Checkbox,
  Col,
  Descriptions,
  Divider,
  Form,
  Input,
  InputNumber,
  Modal,
  Result,
  Row,
  Select,
  Space,
  Steps,
  Tag,
  Typography,
} from 'antd'
import {
  DeleteOutlined,
  PlusOutlined,
  SaveOutlined,
  SendOutlined,
} from '@ant-design/icons'
import { ApiError } from '../../api/http'
import {
  clearApplicationSignature,
  createApplication,
  getApplication,
  getApplicationDocumentContentUrl,
  getApplicationSignatureImageUrl,
  getApplications,
  returnApplicationDocument,
  signApplication,
  submitApplication,
  updateApplicant,
  updateBeneficiaries,
  updateDocumentConfirmation,
  updateInsured,
  uploadApplicationDocument,
} from '../../api/applications'
import type {
  ApplicantStepRequest,
  ApplicationDocument,
  ApplicationDocumentType,
  ApplicationSignerRole,
  BeneficiariesStepRequest,
  InsuranceApplication,
  InsuredStepRequest,
} from '../../api/applications'
import SignaturePad from '../../components/SignaturePad'
import type { SignaturePadHandle } from '../../components/SignaturePad'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { getQuotes } from '../../api/quotes'
import type { Quote } from '../../api/quotes'

type BeneficiaryFormItem = {
  name: string
  nationalId: string
  relationshipToInsured: string
  percentage: number
}

type BeneficiaryFormValues = {
  designation: 'legal_heirs' | 'designated'
  beneficiaries: BeneficiaryFormItem[]
}

type DocumentFormValues = {
  termsAccepted: boolean
  electronicPolicyAccepted: boolean
}

const currencyFormatter = new Intl.NumberFormat('zh-TW', {
  style: 'currency',
  currency: 'TWD',
  maximumFractionDigits: 0,
})

const applicationSteps = [
  { title: '要保資料' },
  { title: '被保險人' },
  { title: '受益人' },
  { title: '電子簽名' },
  { title: '文件確認' },
]

const paymentFrequencyOptions = [
  { label: '年繳', value: 'annual' },
  { label: '半年繳', value: 'semiannual' },
  { label: '季繳', value: 'quarterly' },
]

const relationshipOptions = ['本人', '配偶', '直系親屬', '公司名義', '其他'].map((value) => ({
  label: value,
  value,
}))

function getInsuranceEndDate(startDate: string): string {
  if (!startDate) return '—'
  const [year, month, day] = startDate.slice(0, 10).split('-').map(Number)
  const date = new Date(Date.UTC(year + 1, month - 1, day))
  date.setUTCDate(date.getUTCDate() - 1)
  return date.toISOString().slice(0, 10)
}

function formatDateTime(value: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleString('zh-TW', { timeZone: 'Asia/Taipei' })
}

function clampStep(step: number): number {
  return Math.max(0, Math.min(4, step))
}

function getResumeStep(item: InsuranceApplication): number {
  if (item.status === 'submitted') return 4

  if (item.currentStep <= 1) {
    const applicantCompleted = Boolean(
      item.applicantName &&
      item.applicantRelationshipToOwner &&
      item.paymentFrequency,
    )
    return applicantCompleted ? 1 : 0
  }

  if (item.currentStep >= 4 && !(item.applicantSigned && item.insuredSigned && item.agentSigned)) {
    return 3
  }

  return clampStep(item.currentStep)
}

function UnderwritingPage() {
  const { message } = App.useApp()
  const [applicantForm] = Form.useForm<ApplicantStepRequest>()
  const [insuredForm] = Form.useForm<InsuredStepRequest>()
  const [beneficiaryForm] = Form.useForm<BeneficiaryFormValues>()
  const [documentForm] = Form.useForm<DocumentFormValues>()
  const signaturePadRef = useRef<SignaturePadHandle | null>(null)

  const [quotes, setQuotes] = useState<Quote[]>([])
  const [applications, setApplications] = useState<InsuranceApplication[]>([])
  const [application, setApplication] = useState<InsuranceApplication | null>(null)
  const [currentStep, setCurrentStep] = useState(0)
  const [selectedQuoteId, setSelectedQuoteId] = useState<number | undefined>()
  const [loadingInitial, setLoadingInitial] = useState(true)
  const [loadError, setLoadError] = useState<Error | null>(null)
  const [loadingApplication, setLoadingApplication] = useState(false)
  const [creatingApplication, setCreatingApplication] = useState(false)
  const [savingStep, setSavingStep] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [uploadingDocumentType, setUploadingDocumentType] = useState<ApplicationDocumentType | null>(null)
  const [returningDocumentType, setReturningDocumentType] = useState<ApplicationDocumentType | null>(null)
  const [returnReason, setReturnReason] = useState('')
  const [selectedSignerRole, setSelectedSignerRole] = useState<ApplicationSignerRole>('applicant')
  const [actionError, setActionError] = useState<string | null>(null)

  const designation = Form.useWatch('designation', beneficiaryForm)
  const beneficiaryItems = Form.useWatch('beneficiaries', beneficiaryForm) ?? []
  const beneficiaryTotal = useMemo(
    () => beneficiaryItems.reduce((sum, item) => sum + Number(item?.percentage ?? 0), 0),
    [beneficiaryItems],
  )

  const isSubmitted = application?.status === 'submitted'

  useEffect(() => {
    if (!application) return

    if (currentStep === 0) {
      applicantForm.setFieldsValue({
        applicantName: application.applicantName ?? application.customerName,
        relationshipToOwner: application.applicantRelationshipToOwner ?? '本人',
        paymentFrequency: (application.paymentFrequency as ApplicantStepRequest['paymentFrequency'] | null) ?? 'annual',
      })
      return
    }

    if (currentStep === 1) {
      insuredForm.setFieldsValue({
        name: application.insuredName ?? application.customerName,
        nationalId: application.insuredNationalId ?? '',
        birthDate: application.insuredBirthDate?.slice(0, 10) ?? '',
        relationshipToOwner: application.insuredRelationshipToOwner ?? '本人',
        phone: application.insuredPhone ?? '',
        email: application.insuredEmail ?? '',
        address: application.insuredAddress ?? '',
      })
      return
    }

    if (currentStep === 2) {
      beneficiaryForm.setFieldsValue({
        designation: (application.beneficiaryDesignation as BeneficiaryFormValues['designation'] | null) ?? 'legal_heirs',
        beneficiaries: application.beneficiaries.map((beneficiary) => ({
          name: beneficiary.name,
          nationalId: beneficiary.nationalId,
          relationshipToInsured: beneficiary.relationshipToInsured,
          percentage: beneficiary.percentage,
        })),
      })
      return
    }

    if (currentStep === 3) {
      return
    }

    documentForm.setFieldsValue({
      termsAccepted: application.termsAccepted,
      electronicPolicyAccepted: application.electronicPolicyAccepted,
    })
  }, [application, applicantForm, beneficiaryForm, currentStep, documentForm, insuredForm])

  const loadOverview = useCallback(async (signal?: AbortSignal) => {
    setLoadingInitial(true)
    setLoadError(null)
    try {
      const [quoteItems, applicationItems] = await Promise.all([
        getQuotes(signal),
        getApplications(signal),
      ])
      setQuotes(quoteItems)
      setApplications(applicationItems)
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setLoadError(error instanceof Error ? error : new Error('載入投保資料失敗。'))
      }
    } finally {
      if (!signal?.aborted) setLoadingInitial(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void loadOverview(controller.signal)
    return () => controller.abort()
  }, [loadOverview])

  const usedQuoteIds = useMemo(
    () => new Set(applications.map((item) => item.quoteId)),
    [applications],
  )

  const availableQuotes = useMemo(() => quotes.filter((quote) => {
    if (usedQuoteIds.has(quote.id)) return false
    if (quote.status !== 'active') return false
    return new Date(quote.validUntilUtc).getTime() > Date.now()
  }), [quotes, usedQuoteIds])

  const selectedQuote = useMemo(
    () => quotes.find((quote) => quote.id === selectedQuoteId),
    [quotes, selectedQuoteId],
  )

  const handleSelectApplication = async (id: number) => {
    setLoadingApplication(true)
    setActionError(null)
    try {
      const item = await getApplication(id)
      setApplication(item)
      setCurrentStep(getResumeStep(item))
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '載入投保申請失敗。')
    } finally {
      setLoadingApplication(false)
    }
  }

  const handleCreateApplication = async () => {
    if (!selectedQuoteId) {
      setActionError('請先選擇要沿用的有效報價。')
      return
    }

    setCreatingApplication(true)
    setActionError(null)
    try {
      const created = await createApplication({ quoteId: selectedQuoteId })
      setApplications((previous) => [...previous, created])
      setApplication(created)
      setCurrentStep(0)
      setSelectedQuoteId(undefined)
      message.success(`投保申請 ${created.applicationNumber} 已建立`)
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '建立投保申請失敗。')
      await loadOverview()
    } finally {
      setCreatingApplication(false)
    }
  }

  const saveApplicant = async () => {
    if (!application) return
    const values = await applicantForm.validateFields()
    setSavingStep(true)
    setActionError(null)
    try {
      const updated = await updateApplicant(application.id, values)
      setApplication(updated)
      setApplications((items) => items.map((item) => item.id === updated.id ? updated : item))
      message.success('要保資料已儲存')
      setCurrentStep(1)
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '儲存要保資料失敗。')
    } finally {
      setSavingStep(false)
    }
  }

  const saveInsured = async () => {
    if (!application) return
    const values = await insuredForm.validateFields()
    setSavingStep(true)
    setActionError(null)
    try {
      const updated = await updateInsured(application.id, {
        ...values,
        email: values.email?.trim() || null,
      })
      setApplication(updated)
      setApplications((items) => items.map((item) => item.id === updated.id ? updated : item))
      message.success('被保險人資料已儲存')
      setCurrentStep(2)
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '儲存被保險人資料失敗。')
    } finally {
      setSavingStep(false)
    }
  }

  const saveBeneficiaries = async () => {
    if (!application) return
    const values = await beneficiaryForm.validateFields()
    if (values.designation === 'designated') {
      if (!values.beneficiaries?.length) {
        setActionError('指定受益人時至少需要一位受益人。')
        return
      }
      if (Math.abs(beneficiaryTotal - 100) > 0.001) {
        setActionError(`受益人比例目前合計 ${beneficiaryTotal}%，必須恰為 100%。`)
        return
      }
    }

    const request: BeneficiariesStepRequest = {
      designation: values.designation,
      beneficiaries: values.designation === 'legal_heirs' ? [] : values.beneficiaries,
    }

    setSavingStep(true)
    setActionError(null)
    try {
      const updated = await updateBeneficiaries(application.id, request)
      setApplication(updated)
      setApplications((items) => items.map((item) => item.id === updated.id ? updated : item))
      message.success('受益人資料已儲存')
      setCurrentStep(3)
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '儲存受益人資料失敗。')
    } finally {
      setSavingStep(false)
    }
  }

  const syncApplication = (updated: InsuranceApplication) => {
    setApplication(updated)
    setApplications((items) => items.map((item) => item.id === updated.id ? updated : item))
  }

  const handleSign = async () => {
    if (!application || !signaturePadRef.current) return
    if (signaturePadRef.current.isEmpty()) {
      setActionError('請先在簽名板完成簽名。')
      return
    }

    setSavingStep(true)
    setActionError(null)
    try {
      const updated = await signApplication(application.id, {
        signerRole: selectedSignerRole,
        signatureDataUrl: signaturePadRef.current.toDataUrl(),
      })
      syncApplication(updated)
      signaturePadRef.current.clear()
      const nextUnsigned = (['applicant', 'insured', 'agent'] as ApplicationSignerRole[])
        .find((role) => !updated.signatures.some((signature) => signature.signerRole === role))
      if (nextUnsigned) setSelectedSignerRole(nextUnsigned)
      message.success('電子簽章已封存')
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '電子簽署失敗。')
    } finally {
      setSavingStep(false)
    }
  }

  const handleClearSignature = async (signerRole: ApplicationSignerRole) => {
    if (!application) return
    setSavingStep(true)
    setActionError(null)
    try {
      const updated = await clearApplicationSignature(application.id, signerRole)
      syncApplication(updated)
      setSelectedSignerRole(signerRole)
      signaturePadRef.current?.clear()
      message.success('原簽章已清除，請重新簽署')
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '清除電子簽章失敗。')
    } finally {
      setSavingStep(false)
    }
  }

  const continueAfterSignatures = () => {
    if (!application) return
    if (!application.applicantSigned || !application.insuredSigned || !application.agentSigned) {
      setActionError('要保人、被保險人與招攬人皆完成電子簽章後，才能進入文件確認。')
      return
    }
    setActionError(null)
    setCurrentStep(4)
  }

  const handleDocumentUpload = async (
    documentType: ApplicationDocumentType,
    file: File | undefined,
  ) => {
    if (!application || !file) return
    setUploadingDocumentType(documentType)
    setActionError(null)
    try {
      const documents = await uploadApplicationDocument(application.id, documentType, file)
      syncApplication({ ...application, documents })
      message.success('文件已上傳')
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '文件上傳失敗。')
    } finally {
      setUploadingDocumentType(null)
    }
  }

  const handleReturnDocument = async () => {
    if (!application || !returningDocumentType) return
    const reason = returnReason.trim()
    if (!reason) {
      setActionError('請輸入補件退回原因。')
      return
    }

    setSavingStep(true)
    setActionError(null)
    try {
      const documents = await returnApplicationDocument(application.id, returningDocumentType, reason)
      syncApplication({ ...application, documents })
      setReturningDocumentType(null)
      setReturnReason('')
      message.success('文件已退回補件')
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '退回補件失敗。')
    } finally {
      setSavingStep(false)
    }
  }

  const getDocumentStatusTag = (document: ApplicationDocument) => {
    if (document.status === 'uploaded') return <Tag color="green">已上傳</Tag>
    if (document.status === 'returned') return <Tag color="red">退回補件</Tag>
    return <Tag color="orange">缺件</Tag>
  }

  const formatFileSize = (sizeBytes: number | null) => {
    if (!sizeBytes) return '—'
    if (sizeBytes < 1024) return `${sizeBytes} B`
    if (sizeBytes < 1024 * 1024) return `${(sizeBytes / 1024).toFixed(1)} KB`
    return `${(sizeBytes / 1024 / 1024).toFixed(1)} MB`
  }

  const saveDocumentConfirmation = async () => {
    if (!application) return null
    const values = await documentForm.validateFields()
    setSavingStep(true)
    setActionError(null)
    try {
      const updated = await updateDocumentConfirmation(application.id, values)
      setApplication(updated)
      setApplications((items) => items.map((item) => item.id === updated.id ? updated : item))
      message.success('文件確認狀態已儲存')
      return updated
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '儲存文件確認狀態失敗。')
      return null
    } finally {
      setSavingStep(false)
    }
  }

  const handleSubmit = async () => {
    if (!application) return
    const saved = await saveDocumentConfirmation()
    if (!saved) return

    setSubmitting(true)
    setActionError(null)
    try {
      const submitted = await submitApplication(application.id)
      setApplication(submitted)
      setApplications((items) => items.map((item) => item.id === submitted.id ? submitted : item))
      message.success(`投保申請 ${submitted.applicationNumber} 已送出`)
    } catch (error) {
      setActionError(error instanceof Error ? error.message : '送出投保申請失敗。')
    } finally {
      setSubmitting(false)
    }
  }

  const renderStepActions = (onSave: () => Promise<void>, isLast = false) => (
    <div className="underwriting-actions">
      {currentStep > 0 ? <Button onClick={() => setCurrentStep((step) => step - 1)}>上一步</Button> : null}
      <Button
        type="primary"
        icon={<SaveOutlined />}
        loading={savingStep}
        disabled={isSubmitted}
        onClick={() => void onSave()}
      >
        {isLast ? '儲存確認' : '儲存並繼續'}
      </Button>
    </div>
  )

  const renderApplicantStep = () => (
    <Card title="第 1 步：要保資料" className="underwriting-card">
      <Alert
        type="info"
        showIcon
        message="要保人若非車主，後續正式流程需另行檢附關係證明。"
        className="underwriting-alert"
      />
      <Form form={applicantForm} layout="vertical" disabled={isSubmitted}>
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Form.Item name="applicantName" label="要保人" rules={[{ required: true, message: '請輸入要保人姓名' }]}>
              <Input maxLength={100} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="relationshipToOwner" label="與車主關係" rules={[{ required: true, message: '請選擇與車主關係' }]}>
              <Select options={relationshipOptions} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="paymentFrequency" label="繳別" rules={[{ required: true, message: '請選擇繳別' }]}>
              <Select options={paymentFrequencyOptions} />
            </Form.Item>
          </Col>
        </Row>
      </Form>

      {application ? (
        <Descriptions bordered size="small" column={{ xs: 1, md: 2 }} className="underwriting-summary">
          <Descriptions.Item label="投保車輛">{application.licensePlate}</Descriptions.Item>
          <Descriptions.Item label="沿用報價">{application.quoteNumber}</Descriptions.Item>
          <Descriptions.Item label="保險起日">{application.insuranceStartDate}</Descriptions.Item>
          <Descriptions.Item label="保險迄日">{getInsuranceEndDate(application.insuranceStartDate)}</Descriptions.Item>
          <Descriptions.Item label="招攬人／通路">{application.salesAgent}／{application.channel}</Descriptions.Item>
          <Descriptions.Item label="總保費">{currencyFormatter.format(application.totalPremium)}</Descriptions.Item>
        </Descriptions>
      ) : null}
      {renderStepActions(saveApplicant)}
    </Card>
  )

  const renderInsuredStep = () => (
    <Card title="第 2 步：被保險人" className="underwriting-card">
      <Alert
        type="warning"
        showIcon
        message="強制險被保險人須為車主本人。完整身分證、電話等資料不會從遮罩 API 自動回填，請輸入原始資料。"
        className="underwriting-alert"
      />
      <Form form={insuredForm} layout="vertical" disabled={isSubmitted}>
        <Row gutter={16}>
          <Col xs={24} md={12}>
            <Form.Item name="name" label="被保險人姓名" rules={[{ required: true, message: '請輸入姓名' }]}>
              <Input maxLength={100} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="nationalId" label="身分證字號" rules={[{ required: true, message: '請輸入身分證字號' }]}>
              <Input maxLength={20} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="birthDate" label="出生日期" rules={[{ required: true, message: '請選擇出生日期' }]}>
              <Input type="date" />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="relationshipToOwner" label="與車主關係" rules={[{ required: true, message: '請選擇關係' }]}>
              <Select options={relationshipOptions} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="phone" label="聯絡電話" rules={[{ required: true, message: '請輸入聯絡電話' }]}>
              <Input maxLength={30} />
            </Form.Item>
          </Col>
          <Col xs={24} md={12}>
            <Form.Item name="email" label="Email（電子保單寄送）" rules={[{ type: 'email', message: 'Email 格式不正確' }]}>
              <Input maxLength={254} />
            </Form.Item>
          </Col>
          <Col span={24}>
            <Form.Item name="address" label="通訊地址" rules={[{ required: true, message: '請輸入完整地址' }]}>
              <Input maxLength={300} />
            </Form.Item>
          </Col>
        </Row>
      </Form>
      {renderStepActions(saveInsured)}
    </Card>
  )

  const renderBeneficiaryStep = () => (
    <Card title="第 3 步：受益人" className="underwriting-card">
      <Alert
        type="info"
        showIcon
        message="指定受益人可為多人，比例合計必須恰為 100%。"
        className="underwriting-alert"
      />
      <Form form={beneficiaryForm} layout="vertical" disabled={isSubmitted}>
        <Form.Item name="designation" label="受益人指定方式" rules={[{ required: true }]}>
          <Select
            options={[
              { label: '法定繼承人', value: 'legal_heirs' },
              { label: '指定受益人', value: 'designated' },
            ]}
          />
        </Form.Item>

        {designation === 'designated' ? (
          <Form.List name="beneficiaries">
            {(fields, { add, remove }) => (
              <Space direction="vertical" size="middle" className="beneficiary-list">
                {fields.map(({ key, name, ...restField }, index) => (
                  <Card
                    key={key}
                    size="small"
                    title={`受益人 ${index + 1}`}
                    extra={(
                      <Button
                        type="text"
                        danger
                        icon={<DeleteOutlined />}
                        disabled={isSubmitted}
                        onClick={() => remove(name)}
                      >
                        刪除
                      </Button>
                    )}
                  >
                    <Row gutter={16}>
                      <Col xs={24} md={12}>
                        <Form.Item {...restField} name={[name, 'name']} label="姓名" rules={[{ required: true, message: '請輸入姓名' }]}>
                          <Input maxLength={100} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item {...restField} name={[name, 'nationalId']} label="身分證字號" rules={[{ required: true, message: '請輸入身分證字號' }]}>
                          <Input maxLength={20} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item {...restField} name={[name, 'relationshipToInsured']} label="與被保險人關係" rules={[{ required: true, message: '請輸入關係' }]}>
                          <Input maxLength={50} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item
                          {...restField}
                          name={[name, 'percentage']}
                          label="比例（%）"
                          rules={[{ required: true, message: '請輸入比例' }]}
                        >
                          <InputNumber min={0.01} max={100} precision={2} className="full-width" />
                        </Form.Item>
                      </Col>
                    </Row>
                  </Card>
                ))}
                <Button
                  icon={<PlusOutlined />}
                  disabled={isSubmitted}
                  onClick={() => add({ name: '', nationalId: '', relationshipToInsured: '', percentage: 0 })}
                >
                  新增受益人
                </Button>
              </Space>
            )}
          </Form.List>
        ) : null}
      </Form>

      {designation === 'designated' ? (
        <div className="beneficiary-total">
          <Tag color={Math.abs(beneficiaryTotal - 100) < 0.001 ? 'success' : 'warning'}>
            比例合計 {beneficiaryTotal}%
          </Tag>
        </div>
      ) : (
        <Typography.Text type="secondary">法定繼承人模式不需逐筆輸入受益人。</Typography.Text>
      )}
      {renderStepActions(saveBeneficiaries)}
    </Card>
  )

  const renderSignatureStep = () => {
    if (!application) return null

    const signerRows: Array<{ role: ApplicationSignerRole; label: string; name: string }> = [
      { role: 'applicant', label: '要保人', name: application.applicantName || application.customerName },
      { role: 'insured', label: '被保險人', name: application.insuredName || application.customerName },
      { role: 'agent', label: '招攬人', name: application.salesAgent },
    ]
    const selectedSigner = signerRows.find((item) => item.role === selectedSignerRole) ?? signerRows[0]
    const selectedSignature = application.signatures.find((signature) => signature.signerRole === selectedSignerRole)
    const allSigned = application.applicantSigned && application.insuredSigned && application.agentSigned

    return (
      <Card title="第 4 步：電子簽名" className="underwriting-card">
        <Alert
          type="info"
          showIcon
          message="簽署完成後，系統會保存簽章、UTC 時戳、IP、裝置資訊與 SHA-256 雜湊，作為電子簽署存證。"
          className="underwriting-alert"
        />

        <div className="signature-status-list">
          {signerRows.map((signer) => {
            const signature = application.signatures.find((item) => item.signerRole === signer.role)
            const selected = signer.role === selectedSignerRole
            return (
              <button
                key={signer.role}
                type="button"
                className={`signature-status-row signature-signer-row${selected ? ' signature-signer-row-selected' : ''}`}
                onClick={() => {
                  setSelectedSignerRole(signer.role)
                  signaturePadRef.current?.clear()
                }}
              >
                <span>
                  <strong>{signer.name}</strong><br />
                  <Typography.Text type="secondary">{signer.label}</Typography.Text>
                </span>
                <span className="signature-row-meta">
                  <Tag color={signature ? 'success' : 'default'}>{signature ? '已簽署' : '待簽署'}</Tag>
                  <Typography.Text type="secondary">
                    {signature ? formatDateTime(signature.signedAtUtc) : '尚未簽署'}
                  </Typography.Text>
                </span>
              </button>
            )
          })}
        </div>

        <Divider />

        <div className="signature-workspace">
          <div className="signature-workspace-heading">
            <div>
              <Typography.Title level={5}>{selectedSigner.name}｜{selectedSigner.label}</Typography.Title>
              <Typography.Text type="secondary">
                {selectedSignature ? '此角色已有存證；可查看原簽名，或清除後重新簽署。' : '請使用滑鼠、觸控或手寫筆在下方完成簽名。'}
              </Typography.Text>
            </div>
            {selectedSignature ? <Tag color="green">已封存</Tag> : <Tag>待簽署</Tag>}
          </div>

          {selectedSignature ? (
            <div className="signature-evidence-card">
              <img
                src={getApplicationSignatureImageUrl(application.id, selectedSignerRole)}
                alt={`${selectedSigner.label} ${selectedSigner.name} 的電子簽名`}
                className="signature-preview"
              />
              <Descriptions size="small" column={{ xs: 1, md: 2 }}>
                <Descriptions.Item label="簽署時間">{formatDateTime(selectedSignature.signedAtUtc)}</Descriptions.Item>
                <Descriptions.Item label="方式">Canvas 手寫簽名</Descriptions.Item>
                <Descriptions.Item label="IP">{selectedSignature.ipAddress || '未取得'}</Descriptions.Item>
                <Descriptions.Item label="裝置">{selectedSignature.userAgent || '未取得'}</Descriptions.Item>
                <Descriptions.Item label="SHA-256" span={2}>
                  <Typography.Text code copyable>{selectedSignature.sha256}</Typography.Text>
                </Descriptions.Item>
              </Descriptions>
            </div>
          ) : (
            <SignaturePad
              ref={signaturePadRef}
              disabled={Boolean(isSubmitted)}
              ariaLabel={`${selectedSigner.label} ${selectedSigner.name} 電子簽名板`}
            />
          )}

          {!isSubmitted ? (
            <div className="signature-pad-actions">
              {selectedSignature ? (
                <>
                  <Button
                    href={getApplicationSignatureImageUrl(application.id, selectedSignerRole)}
                    target="_blank"
                  >
                    查看原始簽名
                  </Button>
                  <Button
                    danger
                    loading={savingStep}
                    onClick={() => void handleClearSignature(selectedSignerRole)}
                  >
                    清除重簽
                  </Button>
                </>
              ) : (
                <>
                  <Button onClick={() => signaturePadRef.current?.clear()}>清除筆跡</Button>
                  <Button type="primary" loading={savingStep} onClick={() => void handleSign()}>
                    完成簽署並封存
                  </Button>
                </>
              )}
              <Button disabled title="需串接 SMS 供應商與 OTP 安全規則後啟用">
                改用簡訊驗證碼簽署
              </Button>
            </div>
          ) : null}
        </div>

        <div className="underwriting-actions">
          <Button onClick={() => setCurrentStep(2)}>上一步</Button>
          <Button type="primary" disabled={!allSigned} onClick={continueAfterSignatures}>
            前往文件確認
          </Button>
        </div>
      </Card>
    )
  }

  const renderDocumentStep = () => (
    <Card title="第 5 步：文件確認" className="underwriting-card">
      <Alert
        type="info"
        showIcon
        message="請確認投保文件狀態；缺件或退回文件可直接補件上傳。"
        description="要保書將於後續出單流程自動產製；本階段可上傳身分證明與行車執照。"
        className="underwriting-alert"
      />
      <div className="document-upload-list">
        <div className="document-upload-row">
          <div className="document-upload-main">
            <strong>要保書</strong>
            <Typography.Text type="secondary">後續出單流程自動產製</Typography.Text>
          </div>
          <Tag>後續自動產製</Tag>
        </div>
        {(application?.documents ?? []).map((document) => (
          <div className="document-upload-row" key={document.documentType}>
            <div className="document-upload-main">
              <Space wrap>
                <strong>{document.displayName}</strong>
                {getDocumentStatusTag(document)}
              </Space>
              {document.originalFileName ? (
                <Typography.Text type="secondary">
                  {document.originalFileName} · {formatFileSize(document.sizeBytes)}
                </Typography.Text>
              ) : (
                <Typography.Text type="secondary">尚未上傳</Typography.Text>
              )}
              {document.status === 'returned' && document.returnReason ? (
                <Alert
                  type="error"
                  showIcon
                  message="補件原因"
                  description={document.returnReason}
                />
              ) : null}
            </div>
            <div className="document-upload-actions">
              {document.id ? (
                <Button
                  href={getApplicationDocumentContentUrl(application!.id, document.documentType)}
                  target="_blank"
                  rel="noreferrer"
                >
                  預覽
                </Button>
              ) : null}
              {document.status === 'uploaded' ? (
                <Button
                  danger
                  disabled={savingStep}
                  onClick={() => {
                    setReturningDocumentType(document.documentType)
                    setReturnReason('')
                  }}
                >
                  退回補件
                </Button>
              ) : null}
              {!isSubmitted || document.status !== 'uploaded' ? (
                <label className="document-file-picker">
                  <span className="ant-btn ant-btn-default">
                    {uploadingDocumentType === document.documentType ? '上傳中…' : document.id ? '重新上傳' : '選擇檔案'}
                  </span>
                  {/* ponytail: native: 使用瀏覽器原生 file input，避免新增上傳套件 | upgrade if: 需要拖放、進度或多檔佇列 */}
                  <input
                    type="file"
                    aria-label={`${document.displayName} 上傳`}
                    accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
                    disabled={uploadingDocumentType !== null}
                    onChange={(event) => {
                      const file = event.currentTarget.files?.[0]
                      void handleDocumentUpload(document.documentType, file)
                      event.currentTarget.value = ''
                    }}
                  />
                </label>
              ) : null}
            </div>
          </div>
        ))}
      </div>
      <Divider />
      <Form form={documentForm} layout="vertical" disabled={isSubmitted}>
        <Form.Item name="termsAccepted" valuePropName="checked">
          <Checkbox>已詳閱並同意保險條款與個資告知事項</Checkbox>
        </Form.Item>
        <Form.Item name="electronicPolicyAccepted" valuePropName="checked">
          <Checkbox>同意以電子保單取代紙本</Checkbox>
        </Form.Item>
      </Form>
      <div className="underwriting-actions underwriting-submit-actions">
        <Button onClick={() => setCurrentStep(3)}>上一步</Button>
        <Button
          icon={<SaveOutlined />}
          loading={savingStep}
          disabled={isSubmitted}
          onClick={() => void saveDocumentConfirmation()}
        >
          儲存確認
        </Button>
        <Button
          type="primary"
          icon={<SendOutlined />}
          loading={submitting}
          disabled={isSubmitted}
          onClick={() => void handleSubmit()}
        >
          送出投保申請
        </Button>
      </div>
      <Modal
        title="退回補件"
        open={returningDocumentType !== null}
        okText="確認退回"
        cancelText="取消"
        confirmLoading={savingStep}
        onOk={() => void handleReturnDocument()}
        onCancel={() => {
          setReturningDocumentType(null)
          setReturnReason('')
        }}
      >
        <Typography.Paragraph type="secondary">
          請填寫退回原因，使用者重新上傳後會恢復為已上傳狀態。
        </Typography.Paragraph>
        <Input.TextArea
          aria-label="退回補件原因"
          value={returnReason}
          maxLength={500}
          showCount
          rows={4}
          placeholder="例如：影像模糊，請重新上傳"
          onChange={(event) => setReturnReason(event.target.value)}
        />
      </Modal>
    </Card>
  )

  if (loadingInitial) {
    return <LoadingState rows={8} />
  }

  if (loadError instanceof ApiError && loadError.status === 403) {
    return <ForbiddenState requiredRole="具投保資料查看權限的角色" contact="系統管理員" />
  }

  if (loadError) {
    return <ErrorState title="載入投保資料失敗" message={loadError.message} onRetry={() => void loadOverview()} />
  }

  return (
    <div className="underwriting-page">
      <div className="underwriting-intro">
        <div>
          <Typography.Title level={2}>投保申請</Typography.Title>
          <Typography.Paragraph type="secondary">
            從有效報價建立五步投保草稿，或選擇既有案件繼續填寫。
          </Typography.Paragraph>
        </div>
        {application ? (
          <Space wrap>
            <Tag color={isSubmitted ? 'green' : 'blue'}>{isSubmitted ? '已送出' : '草稿'}</Tag>
            <Typography.Text strong>{application.applicationNumber}</Typography.Text>
          </Space>
        ) : null}
      </div>

      <Card className="underwriting-card" title="案件選擇">
        <Row gutter={[16, 16]}>
          <Col xs={24} lg={12}>
            <Typography.Text strong>續填既有申請</Typography.Text>
            <Select
              aria-label="續填既有申請"
              className="full-width selector-spacing"
              placeholder="選擇投保申請"
              loading={loadingApplication}
              value={application?.id}
              onChange={(id) => void handleSelectApplication(id)}
              options={applications.map((item) => ({
                value: item.id,
                label: `${item.applicationNumber}｜${item.customerName}｜${item.licensePlate}｜${item.status === 'submitted' ? '已送出' : '草稿'}`,
              }))}
            />
          </Col>
          <Col xs={24} lg={12}>
            <Typography.Text strong>從有效報價建立申請</Typography.Text>
            <Space.Compact block className="selector-spacing">
              <Select
                aria-label="從有效報價建立申請"
                placeholder={availableQuotes.length ? '選擇尚未投保的有效報價' : '目前沒有可建立的有效報價'}
                value={selectedQuoteId}
                onChange={setSelectedQuoteId}
                options={availableQuotes.map((quote) => ({
                  value: quote.id,
                  label: `${quote.quoteNumber}｜${quote.customerName}｜${quote.licensePlate}｜${currencyFormatter.format(quote.totalPremium)}`,
                }))}
              />
              <Button type="primary" loading={creatingApplication} disabled={!selectedQuoteId} onClick={() => void handleCreateApplication()}>
                建立申請
              </Button>
            </Space.Compact>
          </Col>
        </Row>
        {selectedQuote ? (
          <Typography.Text type="secondary">
            有效至 {formatDateTime(selectedQuote.validUntilUtc)}，總保費 {currencyFormatter.format(selectedQuote.totalPremium)}
          </Typography.Text>
        ) : null}
      </Card>

      {actionError ? <Alert type="error" showIcon closable onClose={() => setActionError(null)} message="操作失敗" description={actionError} /> : null}

      {application ? (
        <>
          <Card className="underwriting-card underwriting-overview">
            <Descriptions column={{ xs: 1, sm: 2, lg: 4 }} size="small">
              <Descriptions.Item label="客戶">{application.customerName}</Descriptions.Item>
              <Descriptions.Item label="車牌">{application.licensePlate}</Descriptions.Item>
              <Descriptions.Item label="報價">{application.quoteNumber}</Descriptions.Item>
              <Descriptions.Item label="總保費">{currencyFormatter.format(application.totalPremium)}</Descriptions.Item>
            </Descriptions>
          </Card>

          {isSubmitted ? (
            <Result
              status="success"
              title={`投保申請 ${application.applicationNumber} 已送出`}
              subTitle={`送出時間：${formatDateTime(application.submittedAtUtc)}。案件已鎖定為唯讀，後續核保流程將由 T-022 串接。`}
            />
          ) : null}

          <Card className="underwriting-card underwriting-steps-card">
            <Steps
              current={currentStep}
              items={applicationSteps}
              responsive
              onChange={(step) => {
                if (step <= Math.max(currentStep, application.currentStep - 1)) setCurrentStep(step)
              }}
            />
          </Card>

          {currentStep === 0 ? renderApplicantStep() : null}
          {currentStep === 1 ? renderInsuredStep() : null}
          {currentStep === 2 ? renderBeneficiaryStep() : null}
          {currentStep === 3 ? renderSignatureStep() : null}
          {currentStep === 4 ? renderDocumentStep() : null}
        </>
      ) : (
        <EmptyState description="尚未選擇投保申請。請從既有草稿續填，或從有效報價建立新申請。" />
      )}
    </div>
  )
}

export default UnderwritingPage
