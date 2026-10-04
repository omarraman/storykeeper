import { request } from '../api/request'
import { DemoAdventureTurnClient } from './demoAdventureTurnClient'
import type { AdventureCampaign, AdventureTurnClient, AdventureTurnResponse } from './contracts'

const demoClient = new DemoAdventureTurnClient()

class StorykeeperAdventureTurnClient implements AdventureTurnClient {
  private pendingAction: { action: string; choiceId: string | null; heroId: string } | null = null

  openTurn(campaign: AdventureCampaign, sessionId: string): AdventureTurnResponse {
    return demoClient.openTurn(campaign, sessionId)
  }

  async submitAction(input: {
    action: string
    choiceId: string | null
    campaign: AdventureCampaign
    sessionId: string
    heroId: string
  }): Promise<AdventureTurnResponse> {
    if (input.campaign.heroes.length === 0) {
      return demoClient.submitAction(input)
    }

    const response = await request<AdventureTurnResponse>(`/api/campaigns/${encodeURIComponent(input.campaign.id)}/actions`, {
      method: 'POST',
      body: JSON.stringify({
        action: input.action,
        sessionId: input.sessionId,
        heroId: input.heroId,
      }),
    })
    this.pendingAction = response.type === 'roll_required'
      ? { action: input.action, choiceId: input.choiceId, heroId: input.heroId }
      : null
    return response
  }

  async resolveRoll(input: Parameters<AdventureTurnClient['resolveRoll']>[0]) {
    const result = await demoClient.resolveRoll(input)
    if (input.campaign.heroes.length === 0) return result

    const checkResolution = result.turn.type === 'story_beat' ? result.turn.checkResolution : undefined
    if (!checkResolution?.id) {
      throw new Error('The saved check could not be linked to the story. Refresh saved party status before continuing.')
    }

    const action = this.pendingAction?.action ?? 'Continue the story after the resolved check.'
    this.pendingAction = null
    const turn = await request<AdventureTurnResponse>(
      `/api/campaigns/${encodeURIComponent(input.campaign.id)}/actions`,
      {
        method: 'POST',
        body: JSON.stringify({
          action,
          sessionId: input.sessionId,
          heroId: input.heroId,
          checkResolutionId: checkResolution.id,
        }),
      },
    )

    return {
      turn: turn.type === 'story_beat' ? { ...turn, checkResolution } : turn,
      campaign: result.campaign,
    }
  }

  refreshCampaign = demoClient.refreshCampaign.bind(demoClient)
}

export const adventureTurnClient: AdventureTurnClient = new StorykeeperAdventureTurnClient()
