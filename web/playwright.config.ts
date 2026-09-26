import { defineConfig, devices } from '@playwright/test'

const baseURL = process.env.E2E_BASE_URL ?? 'http://127.0.0.1:18081'
const useExternalServer = process.env.E2E_EXTERNAL_SERVER === '1'
const composeCommand = 'docker compose -p carinsurance-e2e -f ../docker-compose.e2e.yml'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: useExternalServer
    ? undefined
    : {
        command: `${composeCommand} down -v --remove-orphans && ${composeCommand} up --build --remove-orphans`,
        url: `${baseURL}/api/health`,
        reuseExistingServer: false,
        timeout: 180_000,
      },
})
