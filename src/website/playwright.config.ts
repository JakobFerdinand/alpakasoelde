import { defineConfig, devices } from '@playwright/test';

// Deliberately not 4321: the e2e run brings up its own dev server and must not
// collide with (or silently reuse) a `pnpm dev` you already have running.
const port = Number(process.env.E2E_PORT ?? 4331);
const baseURL = `http://127.0.0.1:${port}`;

export default defineConfig({
  testDir: 'e2e',
  timeout: 30_000,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? 'github' : 'list',
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  use: {
    baseURL,
    viewport: { width: 1280, height: 720 },
    trace: 'retain-on-failure',
  },
  webServer: {
    // `--ignore-lock` keeps this server out of the `astro dev stop/status` lock
    // file, and ASTRO_DEV_BACKGROUND makes Astro skip its agent auto-detection
    // and stay in the foreground — Playwright kills the process it spawned, so
    // a daemonised server would be reported as "exited early".
    command:
      `pnpm exec astro dev --config astro.config.e2e.mjs ` +
      `--host 127.0.0.1 --port ${port} --ignore-lock`,
    env: { ASTRO_DEV_BACKGROUND: '1' },
    url: baseURL,
    reuseExistingServer: false,
    stdout: 'pipe',
    stderr: 'pipe',
  },
});
