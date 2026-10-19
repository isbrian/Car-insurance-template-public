import { expect, test, type Locator, type Page } from '@playwright/test'
import { loginAs } from './helpers/auth'
import { createSubmittedApplicationFixture } from './helpers/apiFixture'

const MINIMAL_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z6i8AAAAASUVORK5CYII=',
  'base64',
)

async function expectValidationLinked(control: Locator, errorText: string): Promise<void> {
  await expect(control).toHaveAttribute('aria-invalid', 'true')
  const describedBy = await control.getAttribute('aria-describedby')
  expect(describedBy, `${errorText} 應透過 aria-describedby 關聯欄位`).toBeTruthy()
  await expect(control.page().locator(`#${describedBy}`)).toContainText(errorText)
}

async function selectAntOption(page: Page, field: string | RegExp, option: string | RegExp) {
  const input = page.getByLabel(field)
  await input.locator('xpath=ancestor::div[contains(@class, "ant-select-selector")]').click()
  const optionContents = page.locator('.ant-select-dropdown:visible .ant-select-item-option-content')

  if (typeof option === 'string') {
    const escaped = option.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
    await optionContents.filter({ hasText: new RegExp(`^${escaped}$`) }).click()
    return
  }

  await optionContents.filter({ hasText: option }).click()
}

async function drawOnSignaturePad(canvas: Locator) {
  await expect(canvas).toBeVisible()
  await canvas.evaluate((element) => {
    const target = element as HTMLCanvasElement
    const rect = target.getBoundingClientRect()
    const pointerId = 1
    const points = [
      { x: rect.left + rect.width * 0.2, y: rect.top + rect.height * 0.45 },
      { x: rect.left + rect.width * 0.4, y: rect.top + rect.height * 0.5 },
      { x: rect.left + rect.width * 0.6, y: rect.top + rect.height * 0.4 },
    ]

    const dispatch = (type: string, point: { x: number; y: number }, buttons: number) => {
      target.dispatchEvent(new PointerEvent(type, {
        bubbles: true,
        pointerId,
        pointerType: 'mouse',
        isPrimary: true,
        clientX: point.x,
        clientY: point.y,
        button: 0,
        buttons,
      }))
    }

    dispatch('pointerdown', points[0], 1)
    dispatch('pointermove', points[1], 1)
    dispatch('pointermove', points[2], 1)
    dispatch('pointerup', points[2], 0)
  })
}

async function signRole(page: Page, roleLabel: '要保人' | '被保險人' | '招攬人') {
  const row = page.locator('.signature-signer-row').filter({ hasText: roleLabel })
  await row.click()

  const canvas = page.getByLabel(new RegExp(`${roleLabel}.*電子簽名板`))
  await drawOnSignaturePad(canvas)
  await page.getByRole('button', { name: '完成簽署並封存' }).click()
  await expect(row.getByText('已簽署')).toBeVisible()
}

function claimFixture(status: 'pending_intake' | 'reviewing') {
  return {
    id: 9001,
    claimNumber: 'CLM-A11Y-0001',
    policyId: 7001,
    policyNumber: 'POL-A11Y-0001',
    claimantName: '王小明',
    licensePlate: 'ABC-1234',
    status,
    accidentDate: '2026-09-20',
    accidentLocation: '台北市信義區',
    accidentDescription: 'T-049B A11y fixture',
    claimedAmount: 50000,
    liabilityDecision: null,
    liabilityPercentage: null,
    approvedAmount: null,
    paymentMethod: null,
    paidAtUtc: null,
    deniedReason: null,
    assignedAdjuster: status === 'pending_intake' ? null : '林理賠',
    lastTransition: status === 'pending_intake' ? 'claim_created' : 'claim_accepted',
    lastTransitionReason: null,
    statusChangedAtUtc: '2026-09-21T00:00:00Z',
    createdAtUtc: '2026-09-21T00:00:00Z',
    updatedAtUtc: '2026-09-21T00:00:00Z',
  }
}

async function mockClaims(page: Page, status: 'pending_intake' | 'reviewing') {
  const fixture = claimFixture(status)

  await page.route('**/api/claims**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const pathname = url.pathname

    if (request.method() === 'GET' && pathname === '/api/claims') {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([fixture]) })
      return
    }

    if (request.method() === 'GET' && pathname === `/api/claims/${fixture.id}`) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(fixture) })
      return
    }

    await route.continue()
  })
}

test.describe('T-049B 投保／核保／理賠表單 A11y', () => {
  test('投保五步主要控制具有 accessible name、錯誤關聯與鍵盤操作', async ({ page }) => {
    test.setTimeout(90_000)
    await loginAs(page, 'sales')

    await page.goto('/quotation')
    await expect(page.getByRole('heading', { name: '車險報價試算' })).toBeVisible()
    await selectAntOption(page, '客戶', /王小明/)
    await selectAntOption(page, '車輛', /ABC-1234/)
    await page.getByRole('button', { name: '試算保費' }).click()
    await expect(page.getByText('試算結果')).toBeVisible()
    await page.getByRole('button', { name: '建立報價草稿' }).click()
    const createdQuoteTitle = page.locator('.ant-result-title').filter({ hasText: /報價草稿 .* 已建立/ })
    await expect(createdQuoteTitle).toBeVisible()
    const quoteNumber = (await createdQuoteTitle.textContent())?.match(/報價草稿 (\S+) 已建立/)?.[1]
    if (!quoteNumber) throw new Error('無法從建立成功訊息取得報價編號')
    await page.getByRole('button', { name: '啟用並前往投保' }).click()

    await expect(page).toHaveURL(/\/underwriting$/)
    await expect(page.getByRole('heading', { name: '投保申請', level: 2 })).toBeVisible()
    await expect(page.getByRole('combobox', { name: '續填既有申請' })).toBeAttached()
    const quoteSelect = page.getByRole('combobox', { name: '從有效報價建立申請' })
    await expect(quoteSelect).toBeAttached()

    await quoteSelect.click()
    await page
      .locator('.ant-select-dropdown:visible .ant-select-item-option-content')
      .filter({ hasText: quoteNumber })
      .click()
    await page.getByRole('button', { name: '建立申請' }).click()

    await expect(page.getByText('第 1 步：要保資料')).toBeVisible()
    for (const label of ['要保人', '與車主關係', '繳別']) {
      await expect(page.getByLabel(label)).toBeAttached()
    }

    const firstSave = page.getByRole('button', { name: '儲存並繼續' })
    await page.getByLabel('要保人').fill('')
    await firstSave.focus()
    await expect(firstSave).toBeFocused()
    await firstSave.press('Enter')
    await expectValidationLinked(page.getByLabel('要保人'), '請輸入要保人姓名')

    await page.getByLabel('要保人').fill('王小明')
    await firstSave.click()

    await expect(page.getByText('第 2 步：被保險人')).toBeVisible()
    for (const label of ['被保險人姓名', '身分證字號', '出生日期', '與車主關係', '聯絡電話', 'Email（電子保單寄送）', '通訊地址']) {
      await expect(page.getByLabel(label)).toBeAttached()
    }
    await page.getByLabel('被保險人姓名').fill('王小明')
    await page.getByLabel('身分證字號').fill('A123456789')
    await page.getByLabel('出生日期').fill('1988-05-20')
    await selectAntOption(page, '與車主關係', '本人')
    await page.getByLabel('聯絡電話').fill('0912345678')
    await page.getByLabel('Email（電子保單寄送）').fill('ming@example.com')
    await page.getByLabel('通訊地址').fill('台北市中正區重慶南路一段 1 號')
    await page.getByRole('button', { name: '儲存並繼續' }).click()

    await expect(page.getByText('第 3 步：受益人')).toBeVisible()
    await expect(page.getByLabel('受益人指定方式')).toBeAttached()
    await selectAntOption(page, '受益人指定方式', '法定繼承人')
    await page.getByRole('button', { name: '儲存並繼續' }).click()

    await expect(page.getByText('第 4 步：電子簽名')).toBeVisible()
    for (const role of ['要保人', '被保險人', '招攬人'] as const) {
      await expect(page.getByLabel(new RegExp(`${role}.*電子簽名板`))).toBeVisible()
      await signRole(page, role)
    }
    await page.getByRole('button', { name: '前往文件確認' }).click()

    await expect(page.getByText('第 5 步：文件確認')).toBeVisible()
    const uploadInputs = page.locator('input[type="file"]')
    await expect(uploadInputs).toHaveCount(2)
    for (let i = 0; i < 2; i += 1) {
      await expect(uploadInputs.nth(i)).toHaveAttribute('aria-label', /上傳$/)
    }

    await uploadInputs.nth(0).setInputFiles({ name: 'identity-document.png', mimeType: 'image/png', buffer: MINIMAL_PNG })
    await uploadInputs.nth(1).setInputFiles({ name: 'vehicle-registration.png', mimeType: 'image/png', buffer: MINIMAL_PNG })
    await expect(page.getByRole('checkbox', { name: '已詳閱並同意保險條款與個資告知事項' })).toBeVisible()
    await expect(page.getByRole('checkbox', { name: '同意以電子保單取代紙本' })).toBeVisible()
  })

  test('核保搜尋、狀態與覆核說明具有明確 accessible name', async ({ page, request }) => {
    test.setTimeout(90_000)
    const fixture = await createSubmittedApplicationFixture(request)

    await loginAs(page, 'underwriter')
    await page.goto('/uwqueue')
    await expect(page.getByRole('heading', { name: '核保作業' })).toBeVisible()
    await page.getByRole('button', { name: '同步送審案件' }).click()
    await expect(page.getByText(/同步完成：處理 \d+ 件/)).toBeVisible()

    const search = page.getByLabel('搜尋核保案件')
    const status = page.getByRole('combobox', { name: '核保狀態' })
    await expect(search).toBeVisible()
    await expect(status).toBeAttached()
    await search.fill(fixture.applicationNumber)
    await search.press('Enter')

    const row = page.locator('tr').filter({ hasText: fixture.applicationNumber })
    await expect(row).toBeVisible()
    const review = row.getByRole('button', { name: '審核' })
    await review.focus()
    await expect(review).toBeFocused()
    await review.press('Enter')

    await expect(page.getByText(`規則命中結果 ${fixture.applicationNumber}`)).toBeVisible()
    const opinion = page.getByLabel('覆核說明')
    await expect(opinion).toBeVisible()
    await opinion.fill('T-049B 可存取性覆核說明')
    const approve = page.getByRole('button', { name: /核\s*准/ })
    await expect(approve).toBeVisible()
    await approve.focus()
    await expect(approve).toBeFocused()
    await expect(page.getByRole('button', { name: '要求補件' })).toBeVisible()
  })

  test('理賠受理 Modal 必填欄位可辨識且錯誤可關聯', async ({ page }) => {
    await loginAs(page, 'claims')
    await mockClaims(page, 'pending_intake')
    await page.goto('/claims')

    await expect(page.getByLabel('搜尋理賠案件')).toBeVisible()
    await expect(page.getByRole('combobox', { name: '理賠狀態' })).toBeAttached()
    await expect(page.getByRole('cell', { name: '案件編號' }).getByText('CLM-A11Y-0001')).toBeVisible()

    const open = page.getByRole('button', { name: '受理並指派' })
    await open.focus()
    await expect(open).toBeFocused()
    await open.press('Enter')

    const dialog = page.getByRole('dialog', { name: '受理案件' })
    await expect(dialog.getByLabel('理賠員')).toBeVisible()
    await expect(dialog.getByLabel('備註')).toBeVisible()
    await dialog.getByRole('button', { name: /確\s*認/ }).click()
    await expectValidationLinked(dialog.getByLabel('理賠員'), '請填寫理賠員。')
  })

  test('理賠責任認定與補件 Modal 必填錯誤可關聯欄位', async ({ page }) => {
    await loginAs(page, 'claims')
    await mockClaims(page, 'reviewing')
    await page.goto('/claims')

    await page.getByRole('button', { name: '完成責任認定' }).click()
    let dialog = page.getByRole('dialog', { name: '責任認定' })
    await expect(dialog.getByLabel('責任認定結論')).toBeVisible()
    await expect(dialog.getByLabel('責任比例（%）')).toBeVisible()
    await dialog.getByRole('button', { name: /確\s*認/ }).click()
    await expectValidationLinked(dialog.getByLabel('責任認定結論'), '請填寫責任認定結論。')
    await expectValidationLinked(dialog.getByLabel('責任比例（%）'), '請填寫責任比例。')
    await dialog.getByRole('button', { name: /取\s*消/ }).click()

    await page.getByRole('button', { name: '要求補件' }).click()
    dialog = page.getByRole('dialog', { name: '要求補件' })
    await expect(dialog.getByLabel('補件原因')).toBeVisible()
    await dialog.getByRole('button', { name: /確\s*認/ }).click()
    await expectValidationLinked(dialog.getByLabel('補件原因'), '請填寫補件原因。')
  })
})
