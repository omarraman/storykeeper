const fallbackMessage = 'Narrated playback is temporarily unavailable.'

export async function fetchNarrationAudio(
  campaignId: string,
  sessionId: string,
  storyBeatId: string,
  signal: AbortSignal,
): Promise<Blob> {
  const response = await fetch(
    `/api/campaigns/${encodeURIComponent(campaignId)}/sessions/${encodeURIComponent(sessionId)}` +
      `/story-beats/${encodeURIComponent(storyBeatId)}/audio`,
    { headers: { Accept: 'audio/*, application/problem+json' }, signal },
  )

  if (!response.ok) {
    const details = await response.json().catch(() => null) as { message?: string; detail?: string } | null
    throw new Error(details?.message ?? details?.detail ?? fallbackMessage)
  }

  const contentType = response.headers.get('Content-Type') ?? ''
  if (!contentType.toLowerCase().startsWith('audio/')) {
    throw new Error(fallbackMessage)
  }
  return response.blob()
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}
