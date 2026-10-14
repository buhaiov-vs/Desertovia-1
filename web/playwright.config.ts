import { defineConfig, devices } from '@playwright/test'
import { tmpdir } from 'node:os'
import { join } from 'node:path'

/**
 * Browser smoke tests. They start their own backend and Vite preview on separate ports with a separate database
 * (`desertod_e2e` on the docker compose PostgreSQL), so a running development setup is left alone.
 */
const API_PORT = 5181
const WEB_PORT = 5191
const connection =
  process.env.DESERTOD_E2E_CONNECTION ??
  'Host=localhost;Port=5432;Database=desertod_e2e;Username=desertod;Password=desertod'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  // Every page renders the village with PixiJS; headless browsers do that on the processor, so many at once crawl.
  workers: 1,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: `http://localhost:${WEB_PORT}`,
    locale: 'en-US',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'] } },
    { name: 'phone', use: { ...devices['Pixel 7'] } },
  ],
  webServer: [
    {
      // The artifacts path keeps this build from clashing with a development server holding src/Desertod.Api/bin.
      command: `dotnet run --project ../src/Desertod.Api --no-launch-profile --artifacts-path "${join(tmpdir(), 'desertod-e2e-build')}"`,
      url: `http://localhost:${API_PORT}/api/catalog`,
      timeout: 180_000,
      reuseExistingServer: !process.env.CI,
      stdout: 'ignore',
      stderr: 'pipe',
      env: {
        ASPNETCORE_ENVIRONMENT: 'Development',
        ASPNETCORE_URLS: `http://localhost:${API_PORT}`,
        ConnectionStrings__Desertod: connection,
      },
    },
    {
      // A production build: the dev server reloads the page when it optimizes dependencies, which breaks tests.
      command: `npx vite build --outDir dist/e2e --emptyOutDir --logLevel warn && npx vite preview --outDir dist/e2e --port ${WEB_PORT} --strictPort`,
      url: `http://localhost:${WEB_PORT}`,
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: { DESERTOD_BACKEND: `http://localhost:${API_PORT}` },
    },
  ],
})
