/**
 * Dedicated test accounts for the selfhosted web E2E suite.
 *
 * The sh-test environment is a long-lived deployment used for Playwright
 * verification of the selfhosted stack. Credentials should be sourced from
 * environment variables in CI; the fallbacks here match the default seeded
 * admin account used during local/manual validation.
 */
export const testAccounts = {
  shTest: {
    baseUrl:
      'https://sh-test-web.icytree-ff4c5fa4.westus2.azurecontainerapps.io',
    apiUrl:
      'https://sh-test-api.icytree-ff4c5fa4.westus2.azurecontainerapps.io',
    username: 'admin',
    password: process.env.SH_TEST_ADMIN_PASSWORD || 'changeme',
    apiKey:
      process.env.SH_TEST_API_KEY || 'sh-e1cc594258136f1aea090931',
  },
} as const
