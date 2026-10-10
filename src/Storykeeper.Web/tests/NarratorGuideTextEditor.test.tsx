import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  narratorGuideHelp,
  narratorGuideLimit,
  narratorGuideUploadByteLimit,
  NarratorGuideTextEditor,
} from '../src/campaign/NarratorGuideTextEditor'

afterEach(() => cleanup())

describe('NarratorGuideTextEditor', () => {
  it('explains private source use, shows a character count, and imports UTF-8 Markdown verbatim', async () => {
    const source = '# Hidden truth\r\n\nKeep this exact.\n'
    const onChange = vi.fn()
    render(<NarratorGuideTextEditor value={source} onChange={onChange} />)

    expect(screen.getByText(narratorGuideHelp)).toBeTruthy()
    expect(screen.getByText(/Private means hidden from players, not hidden from the configured AI provider/)).toBeTruthy()
    expect(screen.getByText(`${source.length} / ${narratorGuideLimit.toLocaleString()} characters`)).toBeTruthy()
    const file = new File([source], 'skylark.md', { type: 'text/markdown' })
    Object.defineProperty(file, 'arrayBuffer', {
      value: vi.fn(async () => new TextEncoder().encode(source).buffer),
    })
    fireEvent.change(screen.getByLabelText('Import UTF-8 .md or .txt'), {
      target: { files: [file] },
    })

    await waitFor(() => expect(onChange).toHaveBeenCalledWith(source))
  })

  it('rejects unsupported and oversized files without changing editor contents', async () => {
    const source = 'Keep existing text.'
    const onChange = vi.fn()
    render(<NarratorGuideTextEditor value={source} onChange={onChange} />)
    const input = screen.getByLabelText('Import UTF-8 .md or .txt')
    fireEvent.change(input, {
      target: { files: [new File(['<p>not parsed</p>'], 'brief.html', { type: 'text/html' })] },
    })
    expect(await screen.findByText(/Other document types are not supported/)).toBeTruthy()
    expect(onChange).not.toHaveBeenCalled()

    const oversized = new File(['x'.repeat(narratorGuideUploadByteLimit + 1)], 'large.txt', { type: 'text/plain' })
    fireEvent.change(input, { target: { files: [oversized] } })
    expect(await screen.findByText(/Uploads are limited to/)).toBeTruthy()
    expect(onChange).not.toHaveBeenCalled()
    expect((screen.getByLabelText('Full narrator guide (private)') as HTMLTextAreaElement).value).toBe(source)
  })
})
