import { expect, test, type Locator, type Page } from '@playwright/test'
import { loginAs } from './helpers/auth'

type Rgba = [number, number, number, number]

function parseCssColor(value: string): Rgba {
  const match = value.match(/rgba?\(([^)]+)\)/)
  if (!match) throw new Error(`無法解析 CSS 顏色：${value}`)

  const parts = match[1].split(/[\s,/]+/).filter(Boolean).map(Number)
  return [parts[0], parts[1], parts[2], parts.length > 3 ? parts[3] : 1]
}

function composite(foreground: Rgba, background: Rgba): Rgba {
  const alpha = foreground[3] + background[3] * (1 - foreground[3])
  if (alpha === 0) return [0, 0, 0, 0]

  return [
    (foreground[0] * foreground[3] + background[0] * background[3] * (1 - foreground[3])) / alpha,
    (foreground[1] * foreground[3] + background[1] * background[3] * (1 - foreground[3])) / alpha,
    (foreground[2] * foreground[3] + background[2] * background[3] * (1 - foreground[3])) / alpha,
    alpha,
  ]
}

function relativeLuminance(color: Rgba): number {
  const channels = color.slice(0, 3).map((channel) => {
    const normalized = channel / 255
    return normalized <= 0.04045
      ? normalized / 12.92
      : Math.pow((normalized + 0.055) / 1.055, 2.4)
  })

  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2]
}

function contrastRatio(foreground: Rgba, background: Rgba): number {
  const foregroundLuminance = relativeLuminance(foreground)
  const backgroundLuminance = relativeLuminance(background)
  const lighter = Math.max(foregroundLuminance, backgroundLuminance)
  const darker = Math.min(foregroundLuminance, backgroundLuminance)
  return (lighter + 0.05) / (darker + 0.05)
}

async function getEffectiveBackground(locator: Locator): Promise<Rgba> {
  const colors = await locator.evaluate((element) => {
    const values: string[] = []
    let current: Element | null = element

    while (current) {
      values.push(getComputedStyle(current).backgroundColor)
      current = current.parentElement
    }

    return values
  })

  let result: Rgba = [255, 255, 255, 1]
  for (const value of colors.reverse()) {
    result = composite(parseCssColor(value), result)
  }
  return result
}

async function expectTextContrast(locator: Locator, label: string, minimum = 4.5): Promise<void> {
  await expect(locator, `${label} 應可見`).toBeVisible()
  const foreground = parseCssColor(await locator.evaluate((element) => getComputedStyle(element).color))
  const background = await getEffectiveBackground(locator)
  const ratio = contrastRatio(composite(foreground, background), background)
  expect(ratio, `${label} 文字對比應至少 ${minimum}:1，實際 ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(minimum)
}

async function expectPlaceholderContrast(locator: Locator, label: string): Promise<void> {
  const foreground = parseCssColor(await locator.evaluate((element) => getComputedStyle(element, '::placeholder').color))
  const background = await getEffectiveBackground(locator)
  const ratio = contrastRatio(composite(foreground, background), background)
  expect(ratio, `${label} placeholder 對比應至少 4.5:1，實際 ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(4.5)
}

async function expectBorderContrast(locator: Locator, label: string, minimum = 3): Promise<void> {
  await expect(locator, `${label} 應可見`).toBeVisible()
  const border = parseCssColor(await locator.evaluate((element) => getComputedStyle(element).borderTopColor))
  const background = await getEffectiveBackground(locator)
  const ratio = contrastRatio(composite(border, background), background)
  expect(ratio, `${label} 邊界對比應至少 ${minimum}:1，實際 ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(minimum)
}

async function expectOutlineContrast(locator: Locator, label: string): Promise<void> {
  const outline = parseCssColor(await locator.evaluate((element) => getComputedStyle(element).outlineColor))
  const background = await getEffectiveBackground(locator)
  const ratio = contrastRatio(composite(outline, background), background)
  expect(ratio, `${label} focus indicator 對比應至少 3:1，實際 ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(3)
}

async function expectElementTextAndBackgroundContrast(locator: Locator, label: string): Promise<void> {
  await expect(locator, `${label} 應可見`).toBeVisible()
  const styles = await locator.evaluate((element) => {
    const computed = getComputedStyle(element)
    return { color: computed.color, backgroundColor: computed.backgroundColor }
  })
  const parentBackground = await getEffectiveBackground(locator.locator('..'))
  const background = composite(parseCssColor(styles.backgroundColor), parentBackground)
  const foreground = composite(parseCssColor(styles.color), background)
  const ratio = contrastRatio(foreground, background)
  expect(ratio, `${label} 文字／背景對比應至少 4.5:1，實際 ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(4.5)
}

async function injectContrastFixtures(page: Page): Promise<void> {
  await page.evaluate(() => {
    const host = document.createElement('div')
    host.id = 't049d-contrast-fixtures'
    host.innerHTML = `
      <div class="underwriting-rule-item underwriting-rule-pass"><span class="underwriting-rule-icon" data-fixture="pass">✓</span></div>
      <div class="underwriting-rule-item underwriting-rule-warn"><span class="underwriting-rule-icon" data-fixture="warn">!</span></div>
      <div class="underwriting-rule-item underwriting-rule-fail"><span class="underwriting-rule-icon" data-fixture="fail">×</span></div>
      <div class="underwriting-rule-item underwriting-rule-unavailable"><span class="underwriting-rule-icon" data-fixture="unavailable">−</span></div>
      <canvas class="signature-pad" data-fixture="signature"></canvas>
    `
    document.body.appendChild(host)
  })
}

test.describe('T-049D WCAG 2.2 AA 對比度回歸', () => {
  test('登入頁文字、placeholder、控制邊界、Primary Button 與錯誤提示達 AA', async ({ page }) => {
    await page.goto('/login')

    await expectTextContrast(page.locator('.login-heading .ant-typography-secondary'), '登入頁 secondary text')

    const accountInput = page.getByLabel('帳號或 Email')
    await expectPlaceholderContrast(accountInput, '帳號輸入框')
    await expectBorderContrast(accountInput.locator('..'), '帳號輸入框')

    const loginButton = page.getByRole('button', { name: /登\s*入/ })
    await expectElementTextAndBackgroundContrast(loginButton, '登入 Primary Button')

    await loginButton.click()
    const accountError = page.getByText('請輸入帳號或 Email', { exact: true })
    const passwordError = page.getByText('請輸入密碼', { exact: true })
    await expectTextContrast(accountError, '帳號必填錯誤')
    await expectTextContrast(passwordError, '密碼必填錯誤')
  })

  test('登入後 secondary text、目前導覽與狀態 Tag 達文字 AA', async ({ page }) => {
    await loginAs(page, 'sales')

    await expectTextContrast(page.getByText('掌握車險業務與客戶服務狀態', { exact: true }), 'Header secondary text')

    const customerLink = page.getByRole('navigation', { name: '主要功能' }).getByRole('link', { name: '客戶管理' })
    await customerLink.click()
    await expect(page).toHaveURL(/\/customers$/)
    await expectTextContrast(customerLink, '目前頁側邊導航文字')

    const tags = page.locator('.ant-tag')
    await expect(tags.first()).toBeVisible()
    const count = await tags.count()
    for (let index = 0; index < count; index += 1) {
      await expectElementTextAndBackgroundContrast(tags.nth(index), `客戶狀態 Tag #${index + 1}`)
    }

    await page.evaluate(() => {
      const host = document.createElement('div')
      host.id = 't049d-tag-palette'
      host.innerHTML = [
        'success',
        'warning',
        'error',
        'processing',
        'blue',
        'geekblue',
        'green',
        'orange',
        'red',
        'gold',
      ].map((color) => `<span class="ant-tag ant-tag-${color}" data-tag-color="${color}">${color}</span>`).join('')
      document.body.appendChild(host)
    })

    for (const color of ['success', 'warning', 'error', 'processing', 'blue', 'geekblue', 'green', 'orange', 'red', 'gold']) {
      await expectElementTextAndBackgroundContrast(page.locator(`[data-tag-color="${color}"]`), `${color} Tag`)
    }
  })

  test('focus indicator、自訂核保狀態 icon 與簽名板操作邊界達 AA', async ({ page }) => {
    await loginAs(page, 'sales')

    const quotationLink = page.getByRole('navigation', { name: '主要功能' }).getByRole('link', { name: '車險報價' })
    await quotationLink.click()
    const main = page.locator('#main-content')
    await expect(main).toBeFocused()
    await expectOutlineContrast(main, '主內容 route-change focus')

    await injectContrastFixtures(page)

    for (const [fixture, label] of [
      ['pass', '核保成功 icon'],
      ['warn', '核保警告 icon'],
      ['fail', '核保失敗 icon'],
      ['unavailable', '核保不可用 icon'],
    ] as const) {
      await expectElementTextAndBackgroundContrast(page.locator(`[data-fixture="${fixture}"]`), label)
    }

    await expectBorderContrast(page.locator('[data-fixture="signature"]'), '簽名板可互動邊界')
  })
})
