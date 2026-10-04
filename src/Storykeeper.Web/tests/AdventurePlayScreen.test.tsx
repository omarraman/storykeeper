import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AdventurePlayScreen } from '../src/adventure/AdventurePlayScreen'
import { CampaignContinuityPanel } from '../src/adventure/CampaignContinuityPanel'
import type { CampaignContinuityClient } from '../src/adventure/continuityClient'
import { DemoAdventureTurnClient } from '../src/adventure/demoAdventureTurnClient'
import type {
  AdventureCampaign,
  AdventureState,
  AdventureTurnClient,
  AdventureTurnResponse,
  CheckResolutionSummary,
  StoryBeat,
} from '../src/adventure/contracts'

const campaign: AdventureCampaign = {
  id: 'campaign-1',
  name: 'Cloverhill',
  status: 'Active',
  heroes: [{
    id: 'hero-1',
    name: 'Rowan',
    role: 'Trail finder',
    strengths: ['Careful steps'],
    hearts: 3,
    sparkleTokens: 1,
    inventory: [{ name: 'Pouch', description: 'A small pouch.', quantity: 1 }],
  }],
  currentQuest: { title: 'Find the lantern', description: 'Follow the paper map.' },
  latestSession: {
    id: 'session-1',
    sessionNumber: 1,
    startedAtUtc: '2026-10-04T00:00:00Z',
    endedAtUtc: null,
    summary: null,
  },
}

const state: AdventureState = {
  mode: 'campaign',
  heroes: campaign.heroes,
  currentQuest: campaign.currentQuest,
  clues: [{ id: 'clue-1', text: 'The map is folded like a boat.' }],
}

const beat: StoryBeat = {
  id: 'opening',
  narration: 'A lantern floats across a sunny meadow.',
  speaker: 'Mira',
  suggestedChoices: [
    { id: 'choice-1', text: 'Follow it' },
    { id: 'choice-2', text: 'Study the map' },
    { id: 'choice-3', text: 'Ask Mira' },
    { id: 'choice-4', text: 'Wave hello' },
    { id: 'choice-5', text: 'Hidden fifth choice' },
  ],
}

const openingResponse: AdventureTurnResponse = { type: 'story_beat', storyBeat: beat, state }

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function createClient(overrides: Partial<AdventureTurnClient> = {}): AdventureTurnClient {
  return {
    openTurn: vi.fn(() => openingResponse),
    submitAction: vi.fn(async () => ({ type: 'roll_required', rollRequired: {
      prompt: 'Reach for the floating map?',
      difficulty: 'Tricky',
      strength: 'Careful steps',
      risky: false,
    } })),
    resolveRoll: vi.fn(async () => ({
      turn: {
        type: 'story_beat',
        storyBeat: { ...beat, id: 'after-roll', narration: 'The map settles safely in your hands.' },
        state,
        checkResolution: {
          roll: 9,
          total: 14,
          target: 12,
          outcome: 'Success',
          childReadableMessage: 'You did it!',
          sparkleTokenSpent: false,
          heartsAfter: 3,
          sparkleTokensAfter: 1,
          source: 'server',
        } satisfies CheckResolutionSummary,
      },
      campaign,
    })),
    refreshCampaign: vi.fn(async () => campaign),
    ...overrides,
  }
}

describe('AdventurePlayScreen', () => {
  it('shows no more than four suggested choices and always offers free text', () => {
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={createClient()}
        onCampaignChange={vi.fn()}
        onBack={vi.fn()}
      />,
    )

    expect(screen.getByText('A lantern floats across a sunny meadow.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Follow it' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Wave hello' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Hidden fifth choice' })).toBeNull()
    expect(screen.getByRole('button', { name: 'We have another idea!' })).toBeTruthy()
    expect(screen.getByText('Find the lantern')).toBeTruthy()
    expect(screen.getByText('The map is folded like a boat.')).toBeTruthy()
  })

  describe('DemoAdventureTurnClient', () => {
    it('returns a roll-required turn and keeps the empty-party preview roll local', async () => {
      const client = new DemoAdventureTurnClient()
      const emptyParty = { ...campaign, heroes: [] }
      const opening = client.openTurn(emptyParty, 'session-1')
      expect(opening.type).toBe('story_beat')
      if (opening.type !== 'story_beat') throw new Error('Expected an opening story beat.')
      expect(opening.state.mode).toBe('preview')

      const action = await client.submitAction({
        action: 'Follow the floating lantern',
        choiceId: 'follow-lantern',
        campaign: emptyParty,
        sessionId: 'session-1',
        heroId: opening.state.heroes[0].id,
      })
      expect(action.type).toBe('roll_required')

      const fetchMock = vi.fn()
      vi.stubGlobal('fetch', fetchMock)
      const resolved = await client.resolveRoll({
        campaign: emptyParty,
        sessionId: 'session-1',
        heroId: opening.state.heroes[0].id,
        roll: 17,
        difficulty: 'Tricky',
        strength: null,
        spendSparkleToken: false,
        risky: false,
      })
      expect(fetchMock).not.toHaveBeenCalled()
      expect(resolved.turn.type).toBe('story_beat')
      if (resolved.turn.type === 'story_beat') {
        expect(resolved.turn.checkResolution).toMatchObject({
          roll: 17,
          total: null,
          target: null,
          source: 'preview',
        })
      }
    })

    it('posts real-hero checks to the rules API and refreshes campaign state', async () => {
      const updatedCampaign = { ...campaign, heroes: campaign.heroes.map((hero) => ({ ...hero, hearts: 2 })) }
      const check = {
        roll: 9,
        total: 14,
        target: 12,
        outcome: 'Success',
        childReadableMessage: 'You did it!',
        sparkleTokenSpent: true,
        heartsAfter: 3,
        sparkleTokensAfter: 0,
      }
      const fetchMock = vi.fn()
        .mockResolvedValueOnce({ ok: true, json: async () => check })
        .mockResolvedValueOnce({ ok: true, json: async () => updatedCampaign })
      vi.stubGlobal('fetch', fetchMock)

      const client = new DemoAdventureTurnClient()
      const resolved = await client.resolveRoll({
        campaign,
        sessionId: 'session-1',
        heroId: 'hero-1',
        roll: 9,
        difficulty: 'Tricky',
        strength: 'Careful steps',
        spendSparkleToken: true,
        risky: false,
      })

      expect(fetchMock).toHaveBeenCalledTimes(2)
      expect(fetchMock.mock.calls[0][0]).toContain('/sessions/session-1/heroes/hero-1/checks')
      expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({
        roll: 9,
        difficulty: 'Tricky',
        strength: 'Careful steps',
        spendSparkleToken: true,
        risky: false,
      })
      expect(resolved.campaign).toEqual(updatedCampaign)
      expect(resolved.turn.type).toBe('story_beat')
      if (resolved.turn.type === 'story_beat') {
        expect(resolved.turn.checkResolution).toMatchObject({ total: 14, source: 'server' })
      }
    })
  })

  it('collects a physical d20 result, resolves it, and displays the server result', async () => {
    const client = createClient()
    const onCampaignChange = vi.fn()
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={client}
        onCampaignChange={onCampaignChange}
        onBack={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Follow it' }))
    expect(await screen.findByLabelText('Your physical d20 result')).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Your physical d20 result'), { target: { value: '9' } })
    fireEvent.change(screen.getByLabelText('Hero strength'), { target: { value: 'Careful steps' } })
    fireEvent.click(await screen.findByLabelText('Spend 1 sparkle token'))
    fireEvent.click(screen.getByRole('button', { name: 'Resolve check' }))

    expect(await screen.findByText('Success · 14')).toBeTruthy()
    expect(screen.getByText('The map settles safely in your hands.')).toBeTruthy()
    expect(client.resolveRoll).toHaveBeenCalledWith(expect.objectContaining({
      roll: 9,
      difficulty: 'Tricky',
      strength: 'Careful steps',
      spendSparkleToken: true,
      risky: false,
    }))
    expect(onCampaignChange).toHaveBeenCalledWith(campaign)
  })

  it('rejects a physical die value outside 1 to 20 without calling the client', async () => {
    const client = createClient()
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={client}
        onCampaignChange={vi.fn()}
        onBack={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Follow it' }))
    const rollInput = await screen.findByLabelText('Your physical d20 result')
    expect(rollInput.getAttribute('min')).toBe('1')
    expect(rollInput.getAttribute('max')).toBe('20')
    fireEvent.change(rollInput, { target: { value: '21' } })
    fireEvent.submit(rollInput.closest('form') as HTMLFormElement)

    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(client.resolveRoll).not.toHaveBeenCalled()
  })

  it('shows a loading state and disables choices while an action is pending', async () => {
    let completeAction: (response: AdventureTurnResponse) => void = () => {}
    const client = createClient({
      submitAction: vi.fn(() => new Promise<AdventureTurnResponse>((resolve) => { completeAction = resolve })),
    })
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={client}
        onCampaignChange={vi.fn()}
        onBack={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Follow it' }))
    expect(screen.getByText('The story is catching up…')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Follow it' }) as HTMLButtonElement).disabled).toBe(true)
    await act(async () => completeAction(openingResponse))
    await waitFor(() => expect(screen.queryByText('The story is catching up…')).toBeNull())
  })

  it('refreshes authoritative party state after a failed check without resubmitting the roll', async () => {
    const client = createClient({
      resolveRoll: vi.fn().mockRejectedValue(new Error('The server could not be reached.')),
    })
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={client}
        onCampaignChange={vi.fn()}
        onBack={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'Follow it' }))
    fireEvent.change(await screen.findByLabelText('Your physical d20 result'), { target: { value: '9' } })
    fireEvent.click(screen.getByRole('button', { name: 'Resolve check' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Refresh saved party status' }))

    await waitFor(() => expect(client.refreshCampaign).toHaveBeenCalledWith(campaign.id))
    expect(client.resolveRoll).toHaveBeenCalledTimes(1)
    expect(screen.getByText('A lantern floats across a sunny meadow.')).toBeTruthy()
  })

  it('preserves a typed action and offers a retry when the client reports a recoverable error', async () => {
    const client = createClient({
      submitAction: vi.fn()
        .mockResolvedValueOnce({ type: 'error', message: 'Try that again.', retryable: true })
        .mockResolvedValueOnce(openingResponse),
    })
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={client}
        onCampaignChange={vi.fn()}
        onBack={vi.fn()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: 'We have another idea!' }))
    const actionInput = screen.getByLabelText('Tell the Storykeeper your idea')
    fireEvent.change(actionInput, { target: { value: 'Ask the gardener about the map' } })
    fireEvent.click(screen.getByRole('button', { name: 'Try our idea' }))

    expect((await screen.findByRole('alert')).textContent).toContain('Try that again.')
    expect((screen.getByLabelText('Tell the Storykeeper your idea') as HTMLTextAreaElement).value)
      .toBe('Ask the gardener about the map')
    fireEvent.click(screen.getByRole('button', { name: 'Retry story action' }))
    await waitFor(() => expect(client.submitAction).toHaveBeenCalledTimes(2))
    expect(client.submitAction).toHaveBeenLastCalledWith(expect.objectContaining({
      action: 'Ask the gardener about the map',
    }))
  })

  it('ends a campaign session only after a parent PIN and factual summary', async () => {
    const continuityClient: CampaignContinuityClient = {
      load: vi.fn(),
      createFact: vi.fn(),
      updateFact: vi.fn(),
      saveSummary: vi.fn(),
      endSession: vi.fn(async () => campaign),
    }
    const onBack = vi.fn()
    render(
      <AdventurePlayScreen
        campaign={campaign}
        sessionId="session-1"
        client={createClient()}
        continuityClient={continuityClient}
        onCampaignChange={vi.fn()}
        onBack={onBack}
      />,
    )

    fireEvent.click(screen.getByText('Parent: wrap up this adventure'))
    fireEvent.change(screen.getByLabelText('Session summary'), {
      target: { value: 'Rowan found the folded map and promised to help Mira.' },
    })
    fireEvent.change(screen.getByLabelText('Parent PIN'), { target: { value: '246810' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save summary and end adventure' }))

    await waitFor(() => expect(continuityClient.endSession).toHaveBeenCalledWith({
      campaignId: campaign.id,
      sessionId: 'session-1',
      summary: 'Rowan found the folded map and promised to help Mira.',
      parentPin: '246810',
    }))
    expect(onBack).toHaveBeenCalledOnce()
  })

  it('provides parent correction controls for saved facts and summaries', async () => {
    const savedContinuity = {
      facts: [{
        id: 'fact-1',
        sourceSessionId: 'session-1',
        category: 'promise',
        statement: 'Mira promised to show the map room.',
        status: 'Proposed' as const,
        importance: 3,
        lastEditedAtUtc: null,
      }],
      summaries: [{
        sessionId: 'session-1',
        sessionNumber: 1,
        summary: 'Rowan found a folded map.',
        endedAtUtc: '2026-10-04T00:00:00Z',
        lastEditedAtUtc: null,
      }],
      revisions: [],
    }
    const continuityClient: CampaignContinuityClient = {
      load: vi.fn(async () => savedContinuity),
      createFact: vi.fn(async () => savedContinuity.facts[0]),
      updateFact: vi.fn(async ({ fact }) => fact),
      saveSummary: vi.fn(),
      endSession: vi.fn(async () => campaign),
    }
    render(
      <CampaignContinuityPanel
        campaignId={campaign.id}
        sourceSessionId="session-1"
        client={continuityClient}
      />,
    )
    fireEvent.click(screen.getByText('Campaign continuity'))

    expect(await screen.findByDisplayValue('Mira promised to show the map room.')).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Fact'), {
      target: { value: 'Mira promised to help Rowan find the map room.' },
    })
    fireEvent.change(screen.getByLabelText('Status'), { target: { value: 'Active' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save fact correction' }))

    await waitFor(() => expect(continuityClient.updateFact).toHaveBeenCalledWith({
      campaignId: campaign.id,
      fact: expect.objectContaining({
        id: 'fact-1',
        statement: 'Mira promised to help Rowan find the map room.',
        status: 'Active',
      }),
    }))
    expect(screen.getByDisplayValue('Rowan found a folded map.')).toBeTruthy()
  })
})
