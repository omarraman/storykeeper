import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PwaInstallControl } from '../src/PwaInstallControl'

afterEach(cleanup)

describe('PwaInstallControl', () => {
  it('explains Android browser-menu installation when no native prompt is available', () => {
    render(<PwaInstallControl />)

    fireEvent.click(screen.getByRole('button', { name: 'Install app' }))

    expect(screen.getByRole('dialog', { name: 'Install Storykeeper' })).toBeTruthy()
    expect(screen.getByText(/Chrome or Samsung Internet/)).toBeTruthy()
    expect(screen.getByText(/saved campaigns and story play still need the story server/)).toBeTruthy()
  })

  it('uses the browser install prompt and reflects an accepted install', async () => {
    render(<PwaInstallControl />)
    const prompt = vi.fn().mockResolvedValue(undefined)
    const installEvent = Object.assign(new Event('beforeinstallprompt', { cancelable: true }), {
      prompt,
      userChoice: Promise.resolve({ outcome: 'accepted' as const, platform: 'web' }),
    })
    act(() => window.dispatchEvent(installEvent))

    fireEvent.click(screen.getByRole('button', { name: 'Install app' }))

    await waitFor(() => expect(prompt).toHaveBeenCalledOnce())
    expect(await screen.findByRole('button', { name: 'App installed' })).toBeTruthy()
  })
})
