import { expect, test } from '@playwright/test'
import { stat } from 'node:fs/promises'
import { loginAs } from './helpers/auth'
import { createSubmittedApplicationFixture, loginApi } from './helpers/apiFixture'

test('核保 → 繳費 → 出單 → 保單 PDF', async ({ page, request }) => {
  const fixture = await createSubmittedApplicationFixture(request)

  await loginAs(page, 'underwriter')
  await page.goto('/uwqueue')
  await expect(page.getByRole('heading', { name: '核保作業' })).toBeVisible()

  await page.getByRole('button', { name: '同步送審案件' }).click()
  await expect(page.getByText(/同步完成：處理 \d+ 件/)).toBeVisible()

  const queueSearch = page.getByPlaceholder('案件／被保險人／車牌')
  await queueSearch.fill(fixture.applicationNumber)
  await queueSearch.press('Enter')

  const underwritingRow = page.locator('tr').filter({ hasText: fixture.applicationNumber })
  await expect(underwritingRow).toBeVisible()
  await underwritingRow.getByRole('button', { name: '審核' }).click()

  await expect(page.getByText(`規則命中結果 ${fixture.applicationNumber}`)).toBeVisible()
  await expect(page.getByText('待覆核', { exact: true }).last()).toBeVisible()

  await page.getByRole('button', { name: /核\s*准/ }).click()
  await expect(page.getByText('核准完成。')).toBeVisible()
  await expect(page.getByText('已核准', { exact: true }).last()).toBeVisible()

  await page.goto('/issue')
  await expect(page.getByRole('heading', { name: '出單／繳費' })).toBeVisible()

  const pendingPaymentRow = page.locator('tr').filter({ hasText: fixture.applicationNumber })
  await expect(pendingPaymentRow).toBeVisible()
  await pendingPaymentRow.getByRole('button', { name: '產生繳費單' }).click()
  await expect(page.getByText(/已產生繳費單/)).toBeVisible()

  await page.getByPlaceholder('收款參考編號（選填）').fill(`T051C-${fixture.applicationId}`)
  await page.getByRole('button', { name: '確認收款並出單' }).click()

  await expect(page.locator('.ant-alert-message').filter({ hasText: /^正式保單已建立$/ })).toBeVisible()
  const policyNumber = page.getByText(/^POL-\d{6}-\d{4}$/).first()
  await expect(policyNumber).toBeVisible()
  const policyNumberText = (await policyNumber.textContent())?.trim()
  expect(policyNumberText).toMatch(/^POL-\d{6}-\d{4}$/)

  await page.getByRole('button', { name: '前往保單管理' }).click()
  await expect(page).toHaveURL(/\/policies\/\d+$/)
  await expect(page.getByRole('heading', { name: policyNumberText! })).toBeVisible()
  await expect(page.getByText(fixture.applicationNumber, { exact: true })).toBeVisible()
  await expect(page.getByText(fixture.licensePlate)).toBeVisible()
  await expect(page.getByText('PDF SHA-256')).toBeVisible()

  const downloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: '下載保單 PDF' }).click()
  const download = await downloadPromise
  expect(download.suggestedFilename()).toMatch(/\.pdf$/i)
  expect(await download.failure()).toBeNull()
  const downloadPath = await download.path()
  expect(downloadPath).not.toBeNull()
  const fileStat = await stat(downloadPath!)
  expect(fileStat.size).toBeGreaterThan(0)

  const policyId = Number(page.url().match(/\/policies\/(\d+)$/)?.[1])
  expect(policyId).toBeGreaterThan(0)
  const underwriterToken = await loginApi(request, 'underwriter')
  const documentResponse = await request.get(`/api/policies/${policyId}/document`, {
    headers: { Authorization: `Bearer ${underwriterToken}` },
  })
  expect(documentResponse.ok()).toBeTruthy()
  expect(documentResponse.headers()['content-type']).toContain('application/pdf')
  expect((await documentResponse.body()).byteLength).toBeGreaterThan(0)
})
