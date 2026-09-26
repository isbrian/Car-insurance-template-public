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
  Radio,
  Result,
  Row,
  Select,
  Space,
  Statistic,
  Switch,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { CalculatorOutlined, FileAddOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { ApiError } from '../../api/http'
import { getCustomers } from '../../api/customers'
import type { Customer } from '../../api/customers'
import { getVehiclesByCustomer } from '../../api/vehicles'
import type { Vehicle } from '../../api/vehicles'
import {
  calculatePremium,
  createQuote,
  getRateVersions,
  updateQuote,
} from '../../api/quotes'
import type {
  PremiumBreakdownItem,
  PremiumCalculationRequest,
  PremiumCalculationResponse,
  Quote,
  RateVersion,
} from '../../api/quotes'
import { EmptyState, ErrorState, ForbiddenState, LoadingState } from '../../components/shared/DataState'
import { responsiveCell } from '../../components/shared/responsiveTable'
import {
  channelOptions,
  deductibleOptions,
  driverAccidentOptions,
  excessLiabilityOptions,
  ownDamageOptions,
  riderOptions,
  thirdPartyOptions,
  vehicleCategoryOptions,
} from './quotationOptions'

type QuotationFormValues = {
  customerId?: number
  vehicleId?: number
  driverBirthDate?: string
  driverGender: 'male' | 'female'
  claimRecordLevel: number
  claimCountLast3Years: number
  vehicleCategory: string
  insuranceStartDate?: string
  salesAgent: string
  channel: string
  thirdPartyPlan: string
  excessLiability: string
  ownDamageType: string
  deductible: number
  theft: boolean
  driverAccident: string
  riders: string[]
  isRenewal: boolean
  isMultiVehicle: boolean
  isOnline: boolean
}

const currencyFormatter = new Intl.NumberFormat('zh-TW', {
  style: 'currency',
  currency: 'TWD',
  maximumFractionDigits: 0,
})

function getTaipeiToday() {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Taipei',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date())
  const values = Object.fromEntries(parts.map((part) => [part.type, part.value]))
  return `${values.year}-${values.month}-${values.day}`
}

function toDateOnly(value: string): string {
  return value.slice(0, 10)
}

function parseNullableNumber(value: string): number | null {
  return value === 'none' ? null : Number(value)
}

function parseThirdParty(value: string): [number | null, number | null] {
  if (value === 'none') return [null, null]
  const [bodilyInjury, propertyDamage] = value.split('|').map(Number)
  return [bodilyInjury, propertyDamage]
}

function QuotationPage() {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const [form] = Form.useForm<QuotationFormValues>()
  const vehicleRequest = useRef<AbortController | null>(null)
  const [customers, setCustomers] = useState<Customer[]>([])
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [rateVersions, setRateVersions] = useState<RateVersion[]>([])
  const [loadingInitial, setLoadingInitial] = useState(true)
  const [loadingVehicles, setLoadingVehicles] = useState(false)
  const [loadError, setLoadError] = useState<Error | null>(null)
  const [calculating, setCalculating] = useState(false)
  const [calculationError, setCalculationError] = useState<string | null>(null)
  const [premium, setPremium] = useState<PremiumCalculationResponse | null>(null)
  const [lastCalculation, setLastCalculation] = useState<PremiumCalculationRequest | null>(null)
  const [savingQuote, setSavingQuote] = useState(false)
  const [activatingQuote, setActivatingQuote] = useState(false)
  const [quoteError, setQuoteError] = useState<string | null>(null)
  const [createdQuote, setCreatedQuote] = useState<Quote | null>(null)
  const calculationRevision = useRef(0)

  const customerId = Form.useWatch('customerId', form)
  const vehicleId = Form.useWatch('vehicleId', form)
  const ownDamageType = Form.useWatch('ownDamageType', form)
  const selectedCustomer = useMemo(
    () => customers.find((customer) => customer.id === customerId),
    [customers, customerId],
  )
  const selectedVehicle = useMemo(
    () => vehicles.find((vehicle) => vehicle.id === vehicleId),
    [vehicles, vehicleId],
  )
  const selectedRateVersion = useMemo(() => {
    const active = rateVersions.filter((version) => version.isActive)
    return active.find((version) => version.insuranceProductCode === 'CTP-AUTO') ?? active[0] ?? null
  }, [rateVersions])

  const taipeiToday = useMemo(getTaipeiToday, [])

  const loadInitial = useCallback(async (signal?: AbortSignal) => {
    setLoadingInitial(true)
    setLoadError(null)
    try {
      const [customerResult, rateVersionResult] = await Promise.all([
        getCustomers('', 1, 100, signal),
        getRateVersions(signal),
      ])
      setCustomers(customerResult.items)
      setRateVersions(rateVersionResult)
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        setLoadError(error instanceof Error ? error : new Error('初始化報價資料失敗。'))
      }
    } finally {
      if (!signal?.aborted) setLoadingInitial(false)
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    void loadInitial(controller.signal)
    return () => controller.abort()
  }, [loadInitial])

  useEffect(() => () => vehicleRequest.current?.abort(), [])

  const invalidateCalculation = () => {
    calculationRevision.current += 1
    setPremium(null)
    setLastCalculation(null)
    setCalculationError(null)
    setQuoteError(null)
    setCreatedQuote(null)
  }

  const handleCustomerChange = async (nextCustomerId: number) => {
    invalidateCalculation()
    form.setFieldsValue({ vehicleId: undefined })
    setVehicles([])

    const customer = customers.find((item) => item.id === nextCustomerId)
    if (customer?.birthDate) {
      form.setFieldsValue({ driverBirthDate: toDateOnly(customer.birthDate) })
    }

    vehicleRequest.current?.abort()
    const controller = new AbortController()
    vehicleRequest.current = controller
    setLoadingVehicles(true)

    try {
      setVehicles(await getVehiclesByCustomer(nextCustomerId, controller.signal))
    } catch (error) {
      if ((error as Error).name !== 'AbortError') {
        message.error(error instanceof Error ? error.message : '載入客戶車輛失敗。')
      }
    } finally {
      if (vehicleRequest.current === controller) setLoadingVehicles(false)
    }
  }

  const buildCalculation = (values: QuotationFormValues): PremiumCalculationRequest => {
    if (!selectedRateVersion) throw new Error('目前沒有可使用的費率版本。')
    if (!selectedVehicle) throw new Error('請先選擇車輛。')
    if (!selectedVehicle.manufactureDate) throw new Error('選定車輛缺少出廠日期，無法試算。')
    if (!values.driverBirthDate) throw new Error('請填寫主要駕駛人出生日期。')

    const [thirdPartyBodilyInjuryPerPerson, thirdPartyPropertyDamagePerAccident] = parseThirdParty(values.thirdPartyPlan)
    const ownDamage = values.ownDamageType === 'none' ? null : values.ownDamageType
    const riders = values.riders ?? []
    if (!ownDamage && riders.includes('天災(颱風地震洪水)')) {
      throw new Error('天災附加條款需先投保車體險。')
    }

    return {
      rateVersionId: selectedRateVersion.id,
      driverBirthDate: values.driverBirthDate,
      driverGender: values.driverGender,
      claimRecordLevel: values.claimRecordLevel,
      claimCountLast3Years: values.claimCountLast3Years,
      vehicleCategory: values.vehicleCategory,
      makeModelCode: selectedVehicle.makeModelCode,
      manufactureDate: toDateOnly(selectedVehicle.manufactureDate),
      usageType: selectedVehicle.usageType,
      thirdPartyBodilyInjuryPerPerson,
      thirdPartyPropertyDamagePerAccident,
      excessLiabilityCoverage: parseNullableNumber(values.excessLiability),
      ownDamageType: ownDamage,
      deductible: ownDamage ? values.deductible : null,
      theft: values.theft,
      driverAccidentCoverage: parseNullableNumber(values.driverAccident),
      riders,
      isRenewal: values.isRenewal,
      isMultiVehicle: values.isMultiVehicle,
      isOnline: values.isOnline,
    }
  }

  const handleCalculate = async () => {
    setCalculationError(null)
    setCreatedQuote(null)
    setQuoteError(null)

    try {
      const values = await form.validateFields()
      const request = buildCalculation(values)
      const revision = calculationRevision.current
      setCalculating(true)
      const response = await calculatePremium(request)
      if (revision !== calculationRevision.current) return
      setPremium(response)
      setLastCalculation(request)
      message.success('保費試算完成')
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return
      setPremium(null)
      setLastCalculation(null)
      setCalculationError(error instanceof Error ? error.message : '保費試算失敗。')
    } finally {
      setCalculating(false)
    }
  }

  const handleCreateQuote = async () => {
    if (!premium || !lastCalculation) return
    setQuoteError(null)

    try {
      const values = await form.validateFields()
      if (!values.customerId || !values.vehicleId || !values.insuranceStartDate) {
        throw new Error('客戶、車輛與保險起日皆為必填。')
      }

      setSavingQuote(true)
      const quote = await createQuote({
        customerId: values.customerId,
        vehicleId: values.vehicleId,
        insuranceStartDate: values.insuranceStartDate,
        salesAgent: values.salesAgent.trim(),
        channel: values.channel,
        status: 'draft',
        calculation: lastCalculation,
      })
      setCreatedQuote(quote)
      message.success(`報價草稿 ${quote.quoteNumber} 已建立`)
    } catch (error) {
      if (error && typeof error === 'object' && 'errorFields' in error) return
      setQuoteError(error instanceof Error ? error.message : '建立報價失敗。')
    } finally {
      setSavingQuote(false)
    }
  }

  const handleActivateQuote = async () => {
    if (!createdQuote) return
    setQuoteError(null)
    setActivatingQuote(true)

    try {
      const quote = await updateQuote(createdQuote.id, {
        customerId: createdQuote.customerId,
        vehicleId: createdQuote.vehicleId,
        insuranceStartDate: createdQuote.insuranceStartDate.slice(0, 10),
        salesAgent: createdQuote.salesAgent,
        channel: createdQuote.channel,
        status: 'active',
        calculation: createdQuote.calculation,
      })
      setCreatedQuote(quote)
      message.success(`報價 ${quote.quoteNumber} 已啟用，可進行投保`)
      navigate('/underwriting')
    } catch (error) {
      setQuoteError(error instanceof Error ? error.message : '啟用報價失敗。')
    } finally {
      setActivatingQuote(false)
    }
  }

  const breakdownColumns: ColumnsType<PremiumBreakdownItem> = [
    {
      title: '區段',
      dataIndex: 'section',
      width: 96,
      onCell: () => responsiveCell('區段'),
      render: (section: string) => section === 'compulsory'
        ? <Tag color="blue">強制險</Tag>
        : <Tag color="geekblue">任意險</Tag>,
    },
    { title: '項目', dataIndex: 'label', width: 190, onCell: () => responsiveCell('項目') },
    {
      title: '運算',
      dataIndex: 'operation',
      width: 100,
      onCell: () => responsiveCell('運算'),
      render: (operation: string) => ({
        base: '基本',
        multiply: '乘數',
        discount: '折扣',
        add: '加項',
        divide: '除數',
        subtotal: '小計',
      }[operation] ?? operation),
    },
    {
      title: '係數／值',
      dataIndex: 'value',
      width: 120,
      onCell: () => responsiveCell('係數／值'),
      render: (value: number) => Number.isInteger(value) ? value.toLocaleString('zh-TW') : value.toFixed(4),
    },
    {
      title: '計算後金額',
      dataIndex: 'amount',
      width: 145,
      align: 'right',
      onCell: () => responsiveCell('計算後金額'),
      render: (amount: number) => currencyFormatter.format(amount),
    },
    { title: '說明', dataIndex: 'note', onCell: () => responsiveCell('說明'), render: (note: string | null) => note || '—' },
  ]

  if (loadingInitial) {
    return <LoadingState rows={8} />
  }

  if (loadError instanceof ApiError && loadError.status === 403) {
    return <ForbiddenState requiredRole="具報價資料查看權限的角色" contact="系統管理員" />
  }

  if (loadError) {
    return <ErrorState title="報價資料載入失敗" message={loadError.message} onRetry={() => void loadInitial()} />
  }

  if (customers.length === 0) {
    return <EmptyState description="尚無可供報價的客戶資料，請先建立客戶。" />
  }

  if (!selectedRateVersion) {
    return <EmptyState description="目前沒有可使用的費率版本，請先建立並啟用費率版本。" />
  }

  return (
    <div className="quotation-page">
      <div className="quotation-intro">
        <div>
          <Typography.Title level={2}>車險報價試算</Typography.Title>
          <Typography.Paragraph type="secondary">
            選擇客戶與車輛後，分別設定強制險與任意險保障，系統會逐項拆解保費計算結果。
          </Typography.Paragraph>
        </div>
        {selectedRateVersion ? (
          <Space direction="vertical" align="end" size={2}>
            <Tag color={selectedRateVersion.confidence === 'official' ? 'green' : 'orange'}>
              {selectedRateVersion.version}
            </Tag>
            <Typography.Text type="secondary">{selectedRateVersion.insuranceProductName}</Typography.Text>
          </Space>
        ) : null}
      </div>

      <Form<QuotationFormValues>
        form={form}
        layout="vertical"
        initialValues={{
          driverGender: 'male',
          claimRecordLevel: 4,
          claimCountLast3Years: 0,
          vehicleCategory: '自用小客車',
          insuranceStartDate: taipeiToday,
          salesAgent: '管理員',
          channel: '業務櫃檯',
          thirdPartyPlan: 'none',
          excessLiability: 'none',
          ownDamageType: 'none',
          deductible: 0,
          theft: false,
          driverAccident: 'none',
          riders: [],
          isRenewal: false,
          isMultiVehicle: false,
          isOnline: false,
        }}
        onValuesChange={invalidateCalculation}
      >
        <Card title="客戶與車輛" className="quotation-card">
          <Row gutter={[16, 0]}>
            <Col xs={24} lg={12}>
              <Form.Item name="customerId" label="客戶" rules={[{ required: true, message: '請選擇客戶' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  placeholder="選擇車主／客戶"
                  options={customers.map((customer) => ({ label: `${customer.name} · ${customer.phone}`, value: customer.id }))}
                  onChange={handleCustomerChange}
                />
              </Form.Item>
            </Col>
            <Col xs={24} lg={12}>
              <Form.Item name="vehicleId" label="車輛" rules={[{ required: true, message: '請選擇車輛' }]}>
                <Select
                  showSearch
                  optionFilterProp="label"
                  disabled={!customerId}
                  loading={loadingVehicles}
                  placeholder={customerId ? '選擇客戶名下車輛' : '請先選擇客戶'}
                  options={vehicles.map((vehicle) => ({ label: `${vehicle.licensePlate} · ${vehicle.makeModelCode}`, value: vehicle.id }))}
                />
              </Form.Item>
            </Col>
          </Row>

          {selectedCustomer || selectedVehicle ? (
            <Descriptions size="small" column={{ xs: 1, sm: 2, lg: 4 }} bordered>
              <Descriptions.Item label="客戶">{selectedCustomer?.name ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="車牌">{selectedVehicle?.licensePlate ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="車型代號">{selectedVehicle?.makeModelCode ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="使用性質">{selectedVehicle?.usageType ?? '—'}</Descriptions.Item>
              <Descriptions.Item label="出廠日期">{selectedVehicle?.manufactureDate ? toDateOnly(selectedVehicle.manufactureDate) : '未設定'}</Descriptions.Item>
            </Descriptions>
          ) : null}
        </Card>

        <Row gutter={[20, 20]}>
          <Col xs={24} xl={12}>
            <Card title="強制險與從人因素" className="quotation-card quotation-card-full">
              <Row gutter={[16, 0]}>
                <Col xs={24} md={12}>
                  <Form.Item name="driverBirthDate" label="主要駕駛人出生日期" rules={[{ required: true, message: '請填寫出生日期' }]}>
                    <Input type="date" max={taipeiToday} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="driverGender" label="性別" rules={[{ required: true }]}>
                    <Radio.Group aria-label="性別" optionType="button" buttonStyle="solid" options={[{ label: '男性', value: 'male' }, { label: '女性', value: 'female' }]} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="claimRecordLevel" label="賠款紀錄等級" rules={[{ required: true }]}>
                    <InputNumber min={1} max={10} precision={0} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="claimCountLast3Years" label="前三年出險次數" rules={[{ required: true }]}>
                    <InputNumber min={0} precision={0} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col span={24}>
                  <Form.Item name="vehicleCategory" label="強制險車種" rules={[{ required: true }]}>
                    <Select options={vehicleCategoryOptions} />
                  </Form.Item>
                </Col>
              </Row>
            </Card>
          </Col>

          <Col xs={24} xl={12}>
            <Card title="報價資料" className="quotation-card quotation-card-full">
              <Row gutter={[16, 0]}>
                <Col xs={24} md={12}>
                  <Form.Item name="insuranceStartDate" label="保險起日" rules={[{ required: true, message: '請選擇保險起日' }]}>
                    <Input type="date" min={taipeiToday} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="channel" label="通路" rules={[{ required: true }]}>
                    <Select options={channelOptions} />
                  </Form.Item>
                </Col>
                <Col span={24}>
                  <Form.Item name="salesAgent" label="招攬人" rules={[{ required: true, whitespace: true, message: '請填寫招攬人' }]}>
                    <Input maxLength={100} />
                  </Form.Item>
                </Col>
              </Row>
              <Alert type="info" showIcon message="強制險生效規則" description="保險起日不得早於台北今日；正式生效時間仍以保單與強制險規範為準。" />
            </Card>
          </Col>
        </Row>

        <Card title="任意險保障" className="quotation-card">
          <Row gutter={[16, 0]}>
            <Col xs={24} md={12} xl={8}>
              <Form.Item name="thirdPartyPlan" label="第三人責任險">
                <Select options={thirdPartyOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} xl={8}>
              <Form.Item name="excessLiability" label="超額責任險">
                <Select options={excessLiabilityOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} xl={8}>
              <Form.Item name="driverAccident" label="駕駛人傷害險">
                <Select options={driverAccidentOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} xl={8}>
              <Form.Item name="ownDamageType" label="車體險式別">
                <Select options={ownDamageOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} xl={8}>
              <Form.Item name="deductible" label="自負額">
                <Select disabled={!ownDamageType || ownDamageType === 'none'} options={deductibleOptions} />
              </Form.Item>
            </Col>
            <Col xs={24} md={12} xl={8}>
              <Form.Item name="theft" label="竊盜損失險" valuePropName="checked">
                <Switch aria-label="竊盜損失險" checkedChildren="投保" unCheckedChildren="不投保" />
              </Form.Item>
            </Col>
            <Col span={24}>
              <Form.Item name="riders" label="附加條款">
                <Checkbox.Group aria-label="附加條款" options={riderOptions} />
              </Form.Item>
            </Col>
          </Row>

          <Divider orientation="left">折扣條件</Divider>
          <Space wrap size="large">
            <Form.Item name="isRenewal" valuePropName="checked" noStyle><Checkbox>續保折扣</Checkbox></Form.Item>
            <Form.Item name="isMultiVehicle" valuePropName="checked" noStyle><Checkbox>多車優惠</Checkbox></Form.Item>
            <Form.Item name="isOnline" valuePropName="checked" noStyle><Checkbox>線上投保折扣</Checkbox></Form.Item>
          </Space>
        </Card>

        <div className="quotation-actions">
          <Button
            type="primary"
            size="large"
            icon={<CalculatorOutlined />}
            loading={calculating}
            disabled={!selectedRateVersion}
            onClick={handleCalculate}
          >
            試算保費
          </Button>
          <Typography.Text type="secondary">修改任何報價條件後，需重新試算才能建立報價。</Typography.Text>
        </div>
      </Form>

      {calculationError ? <Alert type="error" showIcon message="試算失敗" description={calculationError} closable /> : null}

      {premium ? (
        <Card title="試算結果" className="quotation-card quotation-result-card">
          {premium.isProvisional ? (
            <Alert
              className="provisional-warning"
              type="warning"
              showIcon
              message="暫定費率試算"
              description={premium.displayWarning}
            />
          ) : null}

          <Row gutter={[16, 16]} className="premium-summary">
            <Col xs={24} md={8}><Card size="small"><Statistic title="強制險" value={premium.compulsoryPremium} formatter={(value) => currencyFormatter.format(Number(value))} /></Card></Col>
            <Col xs={24} md={8}><Card size="small"><Statistic title="任意險" value={premium.voluntaryPremium} formatter={(value) => currencyFormatter.format(Number(value))} /></Card></Col>
            <Col xs={24} md={8}><Card size="small" className="premium-total"><Statistic title="應繳總保費" value={premium.totalPremium} formatter={(value) => currencyFormatter.format(Number(value))} /></Card></Col>
          </Row>

          <Space wrap className="rate-meta">
            <Tag>{premium.sourceVersion}</Tag>
            <Typography.Text type="secondary">RateVersion #{premium.rateVersionId}</Typography.Text>
          </Space>

          <Divider orientation="left">保費拆解</Divider>
          <Table<PremiumBreakdownItem>
            className="responsive-data-table"
            rowKey={(item) => `${item.section}-${item.code}`}
            columns={breakdownColumns}
            dataSource={premium.breakdown}
            pagination={false}
            scroll={{ x: 900 }}
            size="middle"
          />

          <div className="quotation-save-row">
            <Button type="primary" size="large" icon={<FileAddOutlined />} loading={savingQuote} onClick={handleCreateQuote}>
              建立報價草稿
            </Button>
            <Typography.Text type="secondary">報價建立後有效期固定 30 天。</Typography.Text>
          </div>
        </Card>
      ) : (
        <Card className="quotation-card quotation-empty-result">
          <Result icon={<CalculatorOutlined />} title="尚未試算" subTitle="完成上方條件後按下「試算保費」，這裡會顯示保費與逐項拆解。" />
        </Card>
      )}

      {quoteError ? <Alert type="error" showIcon message="建立報價失敗" description={quoteError} /> : null}

      {createdQuote ? (
        <Card className="quotation-card quote-created-card">
          <Result
            status="success"
            title={`報價草稿 ${createdQuote.quoteNumber} 已建立`}
            subTitle={`有效期限：${new Date(createdQuote.validUntilUtc).toLocaleString('zh-TW', { timeZone: 'Asia/Taipei' })}`}
            extra={(
              <Space wrap>
                <Tag color="blue">草稿</Tag>
                <Typography.Text strong>{currencyFormatter.format(createdQuote.totalPremium)}</Typography.Text>
                <Button type="primary" loading={activatingQuote} onClick={() => void handleActivateQuote()}>
                  啟用並前往投保
                </Button>
              </Space>
            )}
          />
        </Card>
      ) : null}
    </div>
  )
}

export default QuotationPage
