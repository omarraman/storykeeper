import { request } from '../api/request'
import type { AdventureCampaign } from './contracts'

export type CampaignFactStatus = 'Proposed' | 'Active' | 'Resolved' | 'Superseded' | 'Discarded'

export interface CampaignFact {
  id: string
  sourceSessionId: string | null
  category: string
  statement: string
  status: CampaignFactStatus
  importance: number
  lastEditedAtUtc: string | null
}

export interface SessionSummary {
  sessionId: string
  sessionNumber: number
  summary: string
  endedAtUtc: string | null
  lastEditedAtUtc: string | null
}

export interface ContinuityRevision {
  id: string
  recordType: 'Fact' | 'Summary'
  recordId: string
  sourceSessionId: string | null
  previousContent: string | null
  newContent: string
  changedBy: string
  changedAtUtc: string
}

export interface CampaignContinuity {
  facts: CampaignFact[]
  summaries: SessionSummary[]
  revisions: ContinuityRevision[]
}

export interface CampaignContinuityClient {
  load(campaignId: string): Promise<CampaignContinuity>
  createFact(input: {
    campaignId: string
    category: string
    statement: string
    status: Exclude<CampaignFactStatus, 'Proposed'>
    importance: number
    sourceSessionId: string | null
  }): Promise<CampaignFact>
  updateFact(input: {
    campaignId: string
    fact: CampaignFact
  }): Promise<CampaignFact>
  saveSummary(input: {
    campaignId: string
    sessionId: string
    summary: string
  }): Promise<void>
  endSession(input: {
    campaignId: string
    sessionId: string
    summary: string
    parentPin: string
  }): Promise<AdventureCampaign>
}

export const campaignContinuityClient: CampaignContinuityClient = {
  load: (campaignId) =>
    request<CampaignContinuity>(`/api/campaigns/${encodeURIComponent(campaignId)}/continuity`),
  createFact: ({ campaignId, ...fact }) =>
    request<CampaignFact>(`/api/campaigns/${encodeURIComponent(campaignId)}/facts`, {
      method: 'POST',
      body: JSON.stringify(fact),
    }),
  updateFact: ({ campaignId, fact }) =>
    request<CampaignFact>(
      `/api/campaigns/${encodeURIComponent(campaignId)}/facts/${encodeURIComponent(fact.id)}`,
      {
        method: 'PUT',
        body: JSON.stringify({
          category: fact.category,
          statement: fact.statement,
          status: fact.status,
          importance: fact.importance,
        }),
      },
    ),
  saveSummary: async ({ campaignId, sessionId, summary }) => {
    await request(`/api/campaigns/${encodeURIComponent(campaignId)}/sessions/${encodeURIComponent(sessionId)}/summary`, {
      method: 'PUT',
      body: JSON.stringify({ summary }),
    })
  },
  endSession: async ({ campaignId, sessionId, summary, parentPin }) => {
    await request(`/api/campaigns/${encodeURIComponent(campaignId)}/sessions/${encodeURIComponent(sessionId)}/summary`, {
      method: 'PUT',
      headers: { 'X-Parent-Pin': parentPin },
      body: JSON.stringify({ summary }),
    })
    return request<AdventureCampaign>(`/api/campaigns/${encodeURIComponent(campaignId)}`)
  },
}
