import { expect, test, type Locator, type Page } from '@playwright/test'
import { loginAs } from './helpers/auth'

async function expectMinimumTargetSize(locator: Locator, label: string): Promise<void> {
  const box = await locator.boundingBox()
  expect(box, `${label} 應有可量測的 bounding box`).not.toBeNull()
  expect(box!.width, `${label} 寬度應至少 24 CSS px`).toBeGreaterThanOrEqual(24)
  expect(box!.height, `${label} 高度應至少 24 CSS px`).toBeGreaterThanOrEqual(24)
}

async function expectFocusInsideModal(page: Page): Promise<void> {
  await expect.poll(() => page.evaluate(() => Boolean(document.activeElement?.closest('.ant-modal-wrap')))).toBe(true)
}

test.describe('T-049C Modal／導航／Focus A11y', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page, 'sales')
  })

  test('提供 skip link、主要導覽 landmark 與 aria-current', async ({ page }) => {
    const navigation = page.getByRole('navigation', { name: '主要功能' })
    await expect(navigation).toBeVisible()

    const dashboardLink = navigation.getByRole('link', { name: '儀表板' })
    await expect(dashboardLink).toHaveAttribute('aria-current', 'page')
    await expectMinimumTargetSize(dashboardLink, '儀表板導航連結')

    const skipLink = page.getByRole('link', { name: '跳至主要內容' })
    await skipLink.focus()
    await expect(skipLink).toBeFocused()
    await expect(skipLink).toBeVisible()
    await skipLink.press('Enter')

    const main = page.locator('#main-content')
    await expect(main).toBeFocused()
  })

  test('路由切換後焦點移至主要內容並更新目前頁狀態', async ({ page }) => {
    const navigation = page.getByRole('navigation', { name: '主要功能' })
    const customerLink = navigation.getByRole('link', { name: '客戶管理' })

    await customerLink.focus()
    await expect(customerLink).toBeFocused()
    await customerLink.press('Enter')

    await expect(page).toHaveURL(/\/customers$/)
    await expect(page.locator('#main-content')).toBeFocused()
    await expect(customerLink).toHaveAttribute('aria-current', 'page')
    await expect(navigation.getByRole('link', { name: '儀表板' })).not.toHaveAttribute('aria-current', 'page')

    const viewport = page.viewportSize()
    const mainBox = await page.locator('#main-content').boundingBox()
    expect(mainBox).not.toBeNull()
    expect(mainBox!.y).toBeGreaterThanOrEqual(0)
    if (viewport) expect(mainBox!.y).toBeLessThan(viewport.height)
  })

  test('Modal 鍵盤 focus 不逃逸、Escape 關閉後回到觸發按鈕，控制項 target size 足夠', async ({ page }) => {
    await page.goto('/customers')

    const trigger = page.getByRole('button', { name: '新增客戶' })
    await trigger.focus()
    await expect(trigger).toBeFocused()
    await expectMinimumTargetSize(trigger, '新增客戶按鈕')
    await trigger.press('Enter')

    const dialog = page.getByRole('dialog', { name: '新增客戶' })
    await expect(dialog).toBeVisible()
    await expectFocusInsideModal(page)

    const closeButton = dialog.locator('.ant-modal-close')
    const cancelButton = dialog.getByRole('button', { name: /取\s*消/ })
    await expectMinimumTargetSize(closeButton, 'Modal 關閉按鈕')
    await expectMinimumTargetSize(cancelButton, 'Modal 取消按鈕')

    for (let index = 0; index < 12; index += 1) {
      await page.keyboard.press('Tab')
      await expectFocusInsideModal(page)
    }

    for (let index = 0; index < 4; index += 1) {
      await page.keyboard.press('Shift+Tab')
      await expectFocusInsideModal(page)
    }

    await page.keyboard.press('Escape')
    await expect(dialog).toBeHidden()
    await expect(trigger).toBeFocused()
  })
})
