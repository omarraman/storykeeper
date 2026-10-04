export async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })

  if (!response.ok) {
    const details = await response.json().catch(() => null) as { title?: string; detail?: string; message?: string; errors?: Record<string, string[]> } | null
    const message = details?.errors
      ? Object.values(details.errors).flat().join(' ')
      : details?.message ?? details?.detail ?? details?.title ?? `The story server returned ${response.status}.`
    throw new Error(message)
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}
