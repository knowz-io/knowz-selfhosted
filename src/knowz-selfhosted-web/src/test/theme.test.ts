import { describe, it, expect, beforeEach, vi } from 'vitest'

// We need to test getStoredTheme logic. Since it's not exported directly,
// we test via initTheme and useTheme behavior.
// We'll test the module by importing and checking the DOM effects.

describe('DarkModeDefault', () => {
  beforeEach(() => {
    localStorage.clear()
    document.documentElement.classList.remove('dark')
  })

  it('Should_DefaultToDarkMode_WhenNoLocalStorageSet', async () => {
    // No localStorage 'theme' key set
    expect(localStorage.getItem('theme')).toBeNull()

    const { initTheme } = await import('../lib/theme')
    initTheme()

    // Dark class should be applied since default is 'dark'
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })

  it('Should_RespectLightMode_WhenExplicitlySetInLocalStorage', async () => {
    localStorage.setItem('theme', 'light')

    // Re-import to get fresh module
    vi.resetModules()
    const { initTheme } = await import('../lib/theme')
    initTheme()

    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })

  it('Should_RespectDarkMode_WhenExplicitlySetInLocalStorage', async () => {
    localStorage.setItem('theme', 'dark')

    vi.resetModules()
    const { initTheme } = await import('../lib/theme')
    initTheme()

    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
})
