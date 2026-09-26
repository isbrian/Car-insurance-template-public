import { expect, test } from '@playwright/test'
import { loginAs } from './helpers/auth'

test.describe('T-051A Playwright 基礎 smoke', () => {
  test('sales 可完成帳密與 MFA 登入', async ({ page }) => {
    await loginAs(page, 'sales')

    await expect(page.getByText('客戶總數')).toBeVisible()
    await expect(page.getByRole('button', { name: '重新整理' })).toBeVisible()
  })

  test('sales 可導覽主要工作頁', async ({ page }) => {
    await loginAs(page, 'sales')

    const pages = [
      { link: '儀表板', path: '/dashboard' },
      { link: '客戶管理', path: '/customers' },
      { link: '車輛管理', path: '/vehicles' },
      { link: '車險報價', path: '/quotation' },
      { link: '投保申請', path: '/underwriting' },
    ]

    for (const destination of pages) {
      await page.getByRole('link', { name: destination.link }).click()
      await expect(page).toHaveURL(new RegExp(`${destination.path.replace('/', '\\/')}$`))
      await expect(page.getByText('找不到頁面')).toHaveCount(0)
    }
  })
})
