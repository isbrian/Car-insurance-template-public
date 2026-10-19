import { expect, test, type Locator, type Page } from '@playwright/test'
import { loginAs } from './helpers/auth'

const MINIMAL_PNG = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Z6i8AAAAASUVORK5CYII=',
  'base64',
)

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

test.describe('T-051B 報價 → 投保送出 E2E', () => {
  test('sales 可由新報價完成五步投保、簽章、文件上傳並送出', async ({ page }) => {
    test.setTimeout(90_000)
    await loginAs(page, 'sales')

    await page.getByRole('link', { name: '車險報價' }).click()
    await expect(page).toHaveURL(/\/quotation$/)
    await expect(page.getByRole('heading', { name: '車險報價試算' })).toBeVisible()

    await selectAntOption(page, '客戶', /王小明/)
    await expect(page.getByLabel('主要駕駛人出生日期')).toHaveValue('1988-05-20')
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

    const quoteSelect = page.getByRole('combobox').nth(1)
    await quoteSelect.click()
    await page
      .locator('.ant-select-dropdown:visible .ant-select-item-option-content')
      .filter({ hasText: quoteNumber })
      .click()
    await page.getByRole('button', { name: '建立申請' }).click()

    await expect(page.getByText('第 1 步：要保資料')).toBeVisible()
    await page.getByLabel('要保人').fill('王小明')
    await selectAntOption(page, '與車主關係', '本人')
    await selectAntOption(page, '繳別', '年繳')
    await page.getByRole('button', { name: '儲存並繼續' }).click()

    await expect(page.getByText('第 2 步：被保險人')).toBeVisible()
    await page.getByLabel('被保險人姓名').fill('王小明')
    await page.getByLabel('身分證字號').fill('A123456789')
    await page.getByLabel('出生日期').fill('1988-05-20')
    await selectAntOption(page, '與車主關係', '本人')
    await page.getByLabel('聯絡電話').fill('0912345678')
    await page.getByLabel('Email（電子保單寄送）').fill('ming@example.com')
    await page.getByLabel('通訊地址').fill('台北市中正區重慶南路一段 1 號')
    await page.getByRole('button', { name: '儲存並繼續' }).click()

    await expect(page.getByText('第 3 步：受益人')).toBeVisible()
    await selectAntOption(page, '受益人指定方式', '法定繼承人')
    await page.getByRole('button', { name: '儲存並繼續' }).click()

    await expect(page.getByText('第 4 步：電子簽名')).toBeVisible()
    await signRole(page, '要保人')
    await signRole(page, '被保險人')
    await signRole(page, '招攬人')
    await expect(page.getByRole('button', { name: '前往文件確認' })).toBeEnabled()
    await page.getByRole('button', { name: '前往文件確認' }).click()

    await expect(page.getByText('第 5 步：文件確認')).toBeVisible()
    const fileInputs = page.locator('input[type="file"]')
    await expect(fileInputs).toHaveCount(2)
    await fileInputs.nth(0).setInputFiles({
      name: 'identity-document.png',
      mimeType: 'image/png',
      buffer: MINIMAL_PNG,
    })
    await expect(page.getByText('identity-document.png')).toBeVisible()

    await fileInputs.nth(1).setInputFiles({
      name: 'vehicle-registration.png',
      mimeType: 'image/png',
      buffer: MINIMAL_PNG,
    })
    await expect(page.getByText('vehicle-registration.png')).toBeVisible()
    await expect(page.getByText('已上傳')).toHaveCount(2)

    await page.getByRole('checkbox', { name: '已詳閱並同意保險條款與個資告知事項' }).check()
    await page.getByRole('checkbox', { name: '同意以電子保單取代紙本' }).check()
    await page.getByRole('button', { name: '送出投保申請' }).click()

    await expect(page.locator('.ant-result-title').filter({ hasText: /投保申請 .* 已送出/ })).toBeVisible()
    await expect(page.locator('.underwriting-intro').getByText('已送出')).toBeVisible()
    await expect(page.getByLabel('已詳閱並同意保險條款與個資告知事項')).toBeDisabled()
    await expect(page.getByRole('button', { name: '送出投保申請' })).toBeDisabled()
  })
})
