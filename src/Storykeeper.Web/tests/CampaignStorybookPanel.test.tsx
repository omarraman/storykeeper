import { cleanup, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { CampaignStorybookPanel } from '../src/storybook/CampaignStorybookPanel'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('CampaignStorybookPanel', () => {
  it('shows campaign sessions, discoveries, hero moments, rewards, and the export link', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => ({
      ok: true,
      status: 200,
      json: async () => ({
        sessions: [{
          id: 'session-1',
          sessionNumber: 1,
          title: 'The Lantern Song',
          startedAtUtc: '2026-10-04T00:00:00Z',
          endedAtUtc: '2026-10-04T01:00:00Z',
          summary: 'Mira found the moonlit garden.',
          discoveries: [{ category: 'clue', statement: 'The silver feather points north.' }],
          heroAchievements: [{ heroName: 'Pip', description: 'Strong success on a tricky check' }],
        }],
        rewards: [{
          name: 'Star compass',
          description: 'A compass that hums near hidden paths.',
          heroName: 'Pip',
          isClaimed: true,
        }],
      }),
    } as Response)))

    render(<CampaignStorybookPanel campaignId="campaign-1" />)

    expect(await screen.findByRole('heading', { name: 'The Lantern Song' })).toBeTruthy()
    expect(screen.getByText('Mira found the moonlit garden.')).toBeTruthy()
    expect(screen.getByText(/The silver feather points north/)).toBeTruthy()
    expect(within(screen.getByRole('region', { name: 'Hero achievements' }))
      .getByRole('listitem').textContent).toContain('Strong success on a tricky check')
    expect(screen.getByText('Star compass')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Export campaign JSON' }).getAttribute('href'))
      .toBe('/api/campaigns/campaign-1/export')
  })
})
