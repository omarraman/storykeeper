import { request } from '../api/request'
import { DemoAdventureTurnClient } from './demoAdventureTurnClient'
import type { AdventureCampaign, AdventureTurnClient, AdventureTurnResponse } from './contracts'

const demoClient = new DemoAdventureTurnClient()

class StorykeeperAdventureTurnClient implements AdventureTurnClient {
  private pendingAction: { action: string; choiceId: string | null; heroId: string } | null = null

  openTurn(campaign: AdventureCampaign, _sessionId: string): AdventureTurnResponse {
    if (campaign.heroes.length === 0) return demoClient.openTurn(campaign, _sessionId)

    const quest = campaign.currentQuest
    const adventureOpening = quest?.adventureOpening
    const situation = campaign.currentSituation?.trim()
    const worldDescription = campaign.worldDescription?.trim()
    const opening = adventureOpening?.opening?.trim()
    const questDescription = quest?.description.trim()
    const npc = adventureOpening?.featuredNpc
    const npcName = npc?.name?.trim()
    const npcDescription = npc?.description?.trim()
    const npcDisposition = npc?.disposition?.trim()
    const context = [situation, opening].filter((part): part is string => Boolean(part))

    if (context.length === 0 && questDescription) context.push(questDescription)
    if (context.length === 0 && worldDescription) context.push(worldDescription)
    if (context.length === 0) context.push(`Welcome to ${campaign.name}. What would you like to explore first?`)
    if (npcName && npcDescription) {
      context.push(`${npcName} is ${npcDescription}${npcDisposition ? ` ${npcDisposition}` : ''}`)
    }

    const choices = [
      { id: 'look-around', text: 'Look around for clues' },
      npcName
        ? { id: 'ask-featured-npc', text: `Ask ${npcName} what they know` }
        : { id: 'ask-for-help', text: 'Ask someone nearby for help' },
      { id: 'take-a-step', text: 'Take a first step toward the quest' },
      { id: 'make-up-an-idea', text: 'Make up your own idea' },
    ]

    return {
      type: 'story_beat',
      storyBeat: {
        id: 'campaign-opening',
        speaker: null,
        narration: context.join(' '),
        suggestedChoices: choices,
      },
      state: {
        mode: 'campaign',
        heroes: campaign.heroes,
        currentQuest: quest,
        clues: [],
      },
    }
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
