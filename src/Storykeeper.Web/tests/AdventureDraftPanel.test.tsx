import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AdventureDraftPanel } from '../src/adventure/AdventureDraftPanel'
import type { AdventureDraft } from '../src/adventure/contracts'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function createDraft(status: AdventureDraft['status'] = 'PendingReview'): AdventureDraft {
  return {
    id: 'draft-1',
    campaignId: 'campaign-1',
    sessionLengthMinutes: 45,
    parentPreferences: null,
    status,
    generationNumber: 1,
    content: {
      title: 'The Lantern Song',
      premise: 'Help Mira find the lantern garden song.',
      arcType: 'standalone',
      arcConnection: null,
      opening: 'Mira hears a tune inside a lantern.',
      scenes: [
        { title: 'Follow the notes', description: 'Look for bright leaves.', npcName: 'Mira' },
        { title: 'Ask the birds', description: 'Listen to a friendly riddle.', npcName: 'Tumble' },
      ],
      solutionPaths: ['Follow the leaf clues.', 'Ask the birds to sing.'],
      clues: [
        { title: 'A silver leaf', description: 'It matches the tune.' },
        { title: 'A bird rhyme', description: 'It points to the old bellflower.' },
      ],
      featuredNpc: { name: 'Tumble', description: 'A small cloud bird.', disposition: 'Cheerful.' },
      finale: 'The lantern and birds sing together.',
      celebrationReward: 'The garden shares a picnic.',
    },
    activatedQuestId: null,
    createdAtUtc: '2026-10-04T00:00:00Z',
    updatedAtUtc: '2026-10-04T00:00:00Z',
    activatedAtUtc: null,
  }
}

function jsonResponse(body: unknown): Response {
  return {
    ok: true,
    status: 200,
    json: async () => body,
  } as Response
}

describe('AdventureDraftPanel', () => {
  it('generates, reviews, approves, and activates a paced campaign adventure', async () => {
    let draft = createDraft()
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/adventure-drafts')) {
        return jsonResponse(draft)
      }
      if (init?.method === 'POST' && url.endsWith('/approve')) {
        draft = { ...draft, status: 'Approved' }
        return jsonResponse(draft)
      }
      if (init?.method === 'POST' && url.endsWith('/activate')) {
        draft = {
          ...draft,
          status: 'Activated',
          activatedQuestId: 'quest-1',
          activatedAtUtc: '2026-10-04T00:01:00Z',
        }
        return jsonResponse(draft)
      }
      if (init?.method === 'DELETE') return jsonResponse(null)
      return jsonResponse([])
    })
    vi.stubGlobal('fetch', fetchMock)
    vi.stubGlobal('confirm', vi.fn(() => true))
    const onActivated = vi.fn(async () => {})

    render(<AdventureDraftPanel
      campaignId="campaign-1"
      campaignActive
      sessionActive={false}
      sessionLengthMinutes={60}
      onActivated={onActivated}
    />)

    fireEvent.change(screen.getByLabelText(/Parent preferences/), {
      target: { value: 'Add a gentle puzzle.' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Generate next adventure' }))

    expect(await screen.findByRole('heading', { name: 'The Lantern Song' })).toBeTruthy()
    expect(screen.getByText('Other ways forward')).toBeTruthy()
    const generationCall = fetchMock.mock.calls.find(([input, init]) =>
      String(input).endsWith('/adventure-drafts') && init?.method === 'POST')
    expect(JSON.parse(String(generationCall?.[1]?.body))).toEqual({
      sessionLengthMinutes: 60,
      parentPreferences: 'Add a gentle puzzle.',
    })

    fireEvent.click(screen.getByRole('button', { name: 'Approve adventure' }))
    await waitFor(() => expect(screen.getByText('Parent approved · ready to add to this campaign.')).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: 'Activate adventure' }))

    await waitFor(() => expect(onActivated).toHaveBeenCalledOnce())
    expect(screen.getByText('This adventure is part of the campaign\'s saved quest history.')).toBeTruthy()
  })

  it('requires the active session to be summarized before planning another adventure', () => {
    render(<AdventureDraftPanel
      campaignId="campaign-1"
      campaignActive
      sessionActive
      sessionLengthMinutes={45}
      onActivated={async () => {}}
    />)

    expect(screen.getByText('Finish and summarize the current session before planning the next episode.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Generate next adventure' })).toBeNull()
  })
})
