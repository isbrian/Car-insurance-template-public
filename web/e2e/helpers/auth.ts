import { expect, type Page } from '@playwright/test'

export const DEMO_PASSWORD = 'Demo123!'
export const DEMO_MFA_CODE = '123456'

export async function loginAs(page: Page, account = 'sales'): Promise<void> {
  await page.goto('/login')

  await page.getByLabel('帳號或 Email').fill(account)
  await page.getByLabel('密碼').fill(DEMO_PASSWORD)
  await page.getByRole('button', { name: /登\s*入/ }).click()

  await expect(page.getByText(/驗證碼已送至/)).toBeVisible()
  await page.getByLabel('MFA 驗證碼').fill(DEMO_MFA_CODE)
  await page.getByRole('button', { name: /驗\s*證\s*並\s*進\s*入\s*系\s*統/ }).click()

  await expect(page).toHaveURL(/\/dashboard$/)
  await expect(page.getByText('車險管理系統')).toBeVisible()
}
