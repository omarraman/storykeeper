import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ParentControlsPanel } from '../src/adventure/ParentControlsPanel'
import type { ParentSafetySettings } from '../src/adventure/contracts'

const settings: ParentSafetySettings = {
  fearLevel: 'low',
  combatMode: 'avoid',
  excludedContent: [],
  maxNarrationWords: 120,
  sessionLengthMinutes: 45,
}

describe('ParentControlsPanel', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('checks the parent PIN before saving settings or sending live controls', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input).endsWith('/verify')) return new Response(null, { status: 204 })
      if (String(input).endsWith('/parent-controls')) {
        return Response.json(settings)
      }
      const action = JSON.parse(String(init?.body)).action
      return Response.json({ isPaused: action === 'pause', endSessionRequested: action === 'endSession' })
    })
    vi.stubGlobal('fetch', fetchMock)
    const onSettingsSaved = vi.fn()
    const onPausedChange = vi.fn()
    const onEndSessionRequested = vi.fn()

    render(
      <ParentControlsPanel
        campaignId="campaign-1"
        sessionId="session-1"
        settings={settings}
        onSettingsSaved={onSettingsSaved}
        onPausedChange={onPausedChange}
        onEndSessionRequested={onEndSessionRequested}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: /Parent controls/ }))
    fireEvent.change(screen.getByLabelText('Parent PIN'), { target: { value: '246810' } })
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }))
    await screen.findByRole('button', { name: 'Save settings' })
    fireEvent.click(screen.getByRole('button', { name: 'Save settings' }))
    await waitFor(() => expect(onSettingsSaved).toHaveBeenCalledWith(settings))

    fireEvent.click(screen.getByRole('button', { name: 'Pause story' }))
    await waitFor(() => expect(onPausedChange).toHaveBeenCalledWith(true))
    fireEvent.click(screen.getByRole('button', { name: 'End session' }))
    await waitFor(() => expect(onEndSessionRequested).toHaveBeenCalledOnce())

    const protectedCalls = fetchMock.mock.calls.filter(([url]) =>
      String(url).includes('/parent-controls') || String(url).includes('/parent-actions'))
    expect(protectedCalls).toHaveLength(4)
    for (const [, init] of protectedCalls) {
      expect(new Headers(init?.headers).get('X-Parent-Pin')).toBe('246810')
    }
  })
})
