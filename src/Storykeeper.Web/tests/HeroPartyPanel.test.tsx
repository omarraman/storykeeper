import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { HeroPartyPanel, type PartyHero } from '../src/campaign/HeroPartyPanel'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('HeroPartyPanel', () => {
  it('creates a server-owned hero using the parent PIN', async () => {
    const hero: PartyHero = {
      id: 'hero-1',
      name: 'Pip',
      description: 'A curious young explorer.',
      role: 'Scout',
      strengths: ['Noticing tiny details'],
      hearts: 3,
      sparkleTokens: 0,
      inventory: [],
    }
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => hero,
    })
    vi.stubGlobal('fetch', fetchMock)
    const onHeroAdded = vi.fn()

    render(
      <HeroPartyPanel
        campaignId="campaign-1"
        partyName="The Adventurers"
        heroes={[]}
        canManageHeroes
        activeSession={false}
        onHeroAdded={onHeroAdded}
      />,
    )

    fireEvent.change(screen.getByLabelText('Hero name'), { target: { value: 'Pip' } })
    fireEvent.change(screen.getByLabelText('What are they like?'), {
      target: { value: 'A curious young explorer.' },
    })
    fireEvent.change(screen.getByLabelText('Role'), { target: { value: 'Scout' } })
    fireEvent.change(screen.getByPlaceholderText(/Noticing tiny details/), {
      target: { value: 'Noticing tiny details' },
    })
    fireEvent.change(screen.getByLabelText('Parent PIN'), { target: { value: '246810' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save hero' }))

    await waitFor(() => expect(onHeroAdded).toHaveBeenCalledWith(hero))
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/campaigns/campaign-1/heroes',
      expect.objectContaining({
        method: 'POST',
        headers: expect.objectContaining({ 'X-Parent-Pin': '246810' }),
        body: JSON.stringify({
          name: 'Pip',
          description: 'A curious young explorer.',
          role: 'Scout',
          strengths: ['Noticing tiny details'],
        }),
      }),
    )
  })

  it('explains why the party cannot change during an active session', () => {
    render(
      <HeroPartyPanel
        campaignId="campaign-1"
        partyName="The Adventurers"
        heroes={[]}
        canManageHeroes={false}
        activeSession
        onHeroAdded={vi.fn()}
      />,
    )

    expect(screen.getByText('End the current adventure before changing your party.')).toBeTruthy()
    expect(screen.queryByLabelText('Hero name')).toBeNull()
  })
})
