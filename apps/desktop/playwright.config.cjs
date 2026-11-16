const { defineConfig } = require('@playwright/test');
module.exports = defineConfig({
  testDir: './tests', testMatch: '**/ui.spec.cjs', workers: 1,
  use: { viewport: { width: 1480, height: 920 }, browserName: 'chromium', channel: process.env.RELAY_UI_CHANNEL || 'chromium', screenshot: 'only-on-failure' },
  reporter: 'list'
});
