import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { DraftNarratorGuidePanel } from '../src/campaign/DraftNarratorGuidePanel'
import { NarratorGuidePanel } from '../src/campaign/NarratorGuidePanel'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('narrator guide parent flows', () => {
  it('shows the complete draft guide and preserves editor contents on an over-limit response', async () => {
    const guide = `A private authored truth.\n${'x'.repeat(2_300)}\nFINAL GUIDE SENTINEL`
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/verify')) return new Response(null, { status: 204 })
      if (url.endsWith('/narrator-guide') && init?.method !== 'PUT') return jsonResponse({ text: guide })
      if (url.endsWith('/narrator-guide') && init?.method === 'PUT') {
        return jsonResponse({ errors: { text: ['The full narrator guide cannot exceed 30000 characters.'] } }, 422)
      }
      return jsonResponse({})
    })
    vi.stubGlobal('fetch', fetchMock)
    const onSaved = vi.fn()
    const onDirtyChange = vi.fn()
    render(
      <DraftNarratorGuidePanel
        draft={{ id: 'draft-1', status: 'Approved' }}
        onSaved={onSaved}
        onDirtyChange={onDirtyChange}
      />,
    )

    fireEvent.change(screen.getByLabelText('Parent PIN to review the private guide'), {
      target: { value: '246810' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Load guide' }))
    const editor = await screen.findByLabelText('Full narrator guide (private)') as HTMLTextAreaElement
    expect(editor.value).toBe(guide)
    expect(screen.getByText(/30,000 characters is not a fixed token count/)).toBeTruthy()
    expect(editor.value).toContain('FINAL GUIDE SENTINEL')

    const oversized = `${guide}${'z'.repeat(30_001)}`
    fireEvent.change(editor, { target: { value: oversized } })
    fireEvent.click(screen.getByRole('button', { name: 'Save guide and reset approval' }))
    expect((await screen.findByRole('alert')).textContent).toMatch(/cannot exceed 30000 characters/)
    expect((screen.getByLabelText('Full narrator guide (private)') as HTMLTextAreaElement).value).toBe(oversized)
    expect(onSaved).not.toHaveBeenCalled()
  })

  it('keeps active text unchanged until approval and communicates the active-session restriction', async () => {
    const activeText = 'Approved text.'
    let pending: { activeText: string | null; pendingText: string; hasPendingRevision: boolean; activeRevision: number; pendingRevision: number } = {
      activeText,
      pendingText: '',
      hasPendingRevision: false,
      activeRevision: 1,
      pendingRevision: 1,
    }
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input)
      if (url.endsWith('/verify')) return new Response(null, { status: 204 })
      if (url.endsWith('/narrator-guide/pending')) {
        pending = { ...pending, pendingText: JSON.parse(String(init?.body)).text, hasPendingRevision: true, pendingRevision: 2 }
        return jsonResponse(pending)
      }
      return jsonResponse(pending)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<NarratorGuidePanel campaignId="campaign-1" activeSession onDirtyChange={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /Full narrator guide \(private\)/ }))
    expect(screen.getByText(/End the session normally before editing/)).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Parent PIN'), { target: { value: '246810' } })
    fireEvent.click(screen.getByRole('button', { name: 'Unlock' }))

    const editor = await screen.findByLabelText('Full narrator guide (private)') as HTMLTextAreaElement
    expect(editor.value).toBe(activeText)
    expect((screen.getByRole('button', { name: 'Save pending revision' }) as HTMLButtonElement).disabled).toBe(true)
    expect(pending.activeText).toBe(activeText)
    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
  })

  it('requires confirmation before removing a draft guide', async () => {
    const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
      if (String(input).endsWith('/verify')) return new Response(null, { status: 204 })
      return jsonResponse({ text: 'Existing guide.' })
    })
    vi.stubGlobal('fetch', fetchMock)
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false)
    const onSaved = vi.fn()
    render(
      <DraftNarratorGuidePanel
        draft={{ id: 'draft-1', status: 'Approved' }}
        onSaved={onSaved}
        onDirtyChange={vi.fn()}
      />,
    )

    fireEvent.change(screen.getByLabelText('Parent PIN to review the private guide'), {
      target: { value: '246810' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Load guide' }))
    const editor = await screen.findByLabelText('Full narrator guide (private)') as HTMLTextAreaElement
    fireEvent.change(editor, { target: { value: '' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save guide and reset approval' }))

    expect(confirm).toHaveBeenCalledWith('Remove this narrator guide from the draft?')
    expect(fetchMock).toHaveBeenCalledTimes(2)
    expect(onSaved).not.toHaveBeenCalled()
  })
})
