import { expect, test, type Locator, type Page } from '@playwright/test'
import { loginAs } from './helpers/auth'

async function expectValidationLinked(control: Locator, errorText: string): Promise<void> {
  await expect(control).toHaveAttribute('aria-invalid', 'true')
  const describedBy = await control.getAttribute('aria-describedby')
  expect(describedBy, `${errorText} 應透過 aria-describedby 關聯欄位`).toBeTruthy()
  await expect(control.page().locator(`#${describedBy}`)).toContainText(errorText)
}

async function openCustomerModal(page: Page): Promise<Locator> {
  await page.goto('/customers')
  const trigger = page.getByRole('button', { name: '新增客戶' })
  await trigger.focus()
  await expect(trigger).toBeFocused()
  await trigger.press('Enter')
  const dialog = page.getByRole('dialog', { name: '新增客戶' })
  await expect(dialog).toBeVisible()
  return dialog
}

async function openVehicleModal(page: Page): Promise<Locator> {
  await page.goto('/vehicles')
  const trigger = page.getByRole('button', { name: '新增車輛' })
  await trigger.focus()
  await expect(trigger).toBeFocused()
  await trigger.press('Enter')
  const dialog = page.getByRole('dialog', { name: '新增車輛' })
  await expect(dialog).toBeVisible()
  return dialog
}

test.describe('T-049A 主檔／報價表單 A11y', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'sales')
  })

  test('客戶新增表單具有可辨識 label、錯誤關聯與鍵盤操作', async ({ page }) => {
    const dialog = await openCustomerModal(page)

    for (const label of ['姓名', '身分證字號', '聯絡電話', '電子郵件', '地址']) {
      await expect(dialog.getByLabel(label)).toBeVisible()
    }

    await expect(dialog.getByRole('button', { name: /取\s*消/ })).toBeVisible()
    const save = dialog.getByRole('button', { name: /儲\s*存/ })
    await save.focus()
    await expect(save).toBeFocused()
    await save.press('Enter')

    await expectValidationLinked(dialog.getByLabel('姓名'), '請輸入姓名')
    await expectValidationLinked(dialog.getByLabel('身分證字號'), '請輸入身分證字號')
    await expectValidationLinked(dialog.getByLabel('聯絡電話'), '請輸入聯絡電話')
  })

  test('車輛新增表單具有可辨識 label、錯誤關聯與鍵盤操作', async ({ page }) => {
    const dialog = await openVehicleModal(page)

    for (const label of ['車牌號碼', '車身號碼 VIN', '廠牌／車型代碼', '出廠日期', '使用性質', '車主']) {
      await expect(dialog.getByLabel(label)).toBeVisible()
    }

    const save = dialog.getByRole('button', { name: /儲\s*存/ })
    await save.focus()
    await expect(save).toBeFocused()
    await save.press('Enter')

    await expectValidationLinked(dialog.getByLabel('車牌號碼'), '請輸入車牌號碼')
    await expectValidationLinked(dialog.getByLabel('車身號碼 VIN'), '請輸入車身號碼 VIN')
    await expectValidationLinked(dialog.getByLabel('廠牌／車型代碼'), '請輸入廠牌／車型代碼')
    await expectValidationLinked(dialog.getByLabel('使用性質'), '請選擇使用性質')
    await expectValidationLinked(dialog.getByLabel('車主'), '請選擇車主')
  })

  test('報價表單主要互動元件具有 accessible name，必填錯誤可關聯欄位', async ({ page }) => {
    await page.goto('/quotation')
    await expect(page.getByRole('heading', { name: '車險報價試算' })).toBeVisible()

    const labels = [
      '客戶',
      '車輛',
      '主要駕駛人出生日期',
      '性別',
      '賠款紀錄等級',
      '前三年出險次數',
      '強制險車種',
      '保險起日',
      '通路',
      '招攬人',
      '第三人責任險',
      '超額責任險',
      '駕駛人傷害險',
      '車體險式別',
      '自負額',
      '竊盜損失險',
      '附加條款',
      '續保折扣',
      '多車優惠',
      '線上投保折扣',
    ]

    for (const label of labels) {
      await expect(page.getByLabel(label)).toBeAttached()
    }

    const calculate = page.getByRole('button', { name: /試算保費/ })
    await calculate.focus()
    await expect(calculate).toBeFocused()

    const customerSelect = page.getByLabel('客戶')
    await customerSelect.click()
    await customerSelect.fill('王小明')
    await customerSelect.press('Enter')
    await page.getByLabel('主要駕駛人出生日期').fill('')
    await page.getByLabel('招攬人').fill('')
    await calculate.click()

    await expectValidationLinked(page.getByLabel('主要駕駛人出生日期'), '請填寫出生日期')
    await expectValidationLinked(page.getByLabel('招攬人'), '請填寫招攬人')
  })
})
