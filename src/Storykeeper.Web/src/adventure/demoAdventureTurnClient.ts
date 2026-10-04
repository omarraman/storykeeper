import { request } from '../api/request'
import type {
  AdventureCampaign,
  AdventureState,
  AdventureTurnClient,
  AdventureTurnResponse,
  CheckDifficulty,
  CheckResolutionSummary,
  CheckResolutionResponse,
  ClueSummary,
  HeroStatus,
  RollRequired,
  StoryBeat,
} from './contracts'

const openingBeat: StoryBeat = {
  id: 'meadow-opening',
  speaker: 'Mira the mapmaker',
  narration: 'A little paper lantern drifts over Cloverhill Meadow, carrying a map with one corner folded into a tiny boat.',
  suggestedChoices: [
    { id: 'ask-mapmaker', text: 'Ask Mira where the lantern came from' },
    { id: 'follow-lantern', text: 'Follow the floating lantern' },
    { id: 'study-map', text: 'Study the folded map' },
    { id: 'help-firefly', text: 'Help a firefly stuck in a thistle' },
  ],
}

const nextBeat: StoryBeat = {
  id: 'meadow-next',
  speaker: 'Mira the mapmaker',
  narration: 'The lantern bobs toward a footpath. Its map now points to a hill shaped rather like a sleepy turtle.',
  suggestedChoices: [
    { id: 'follow-path', text: 'Take the gentle footpath' },
    { id: 'ask-directions', text: 'Ask a nearby gardener for directions' },
    { id: 'inspect-map', text: 'Look for another mark on the map' },
    { id: 'wave-lantern', text: 'Wave to the lantern and see what it does' },
  ],
}

const rollRequired: RollRequired = {
  prompt: 'Can you carefully catch the drifting map before the breeze carries it over the hill?',
  difficulty: 'Tricky',
  strength: null,
  risky: false,
}

const demoClues: ClueSummary[] = [
  { id: 'paper-boat', text: 'The map is folded into a tiny paper boat.' },
  { id: 'turtle-hill', text: 'A turtle-shaped hill is marked on the map.' },
]

const demoQuest = {
  title: 'Find the lantern map',
  description: 'Follow the paper lantern and discover where its map leads.',
}

const demoHero: HeroStatus = {
  id: 'demo-hero-scout',
  name: 'Pip',
  role: 'Curious scout',
  hearts: 3,
  sparkleTokens: 1,
  strengths: ['Noticing tiny details'],
  inventory: [{ name: 'Pocket notebook', description: 'A small book for important discoveries.', quantity: 1 }],
}

export class DemoAdventureTurnClient implements AdventureTurnClient {
  private turnNumber = 0

  openTurn(campaign: AdventureCampaign, _sessionId: string): AdventureTurnResponse {
    this.turnNumber = 0
    return this.storyResponse(openingBeat, this.stateFor(campaign))
  }

  async submitAction(input: {
    action: string
    choiceId: string | null
    campaign: AdventureCampaign
    sessionId: string
    heroId: string
  }): Promise<AdventureTurnResponse> {
    const needsRoll = input.choiceId === 'follow-lantern' ||
      /\b(catch|climb|jump|reach|sneak|balance)\b/i.test(input.action)
    if (needsRoll) return { type: 'roll_required', rollRequired }

    this.turnNumber++
    return this.storyResponse(nextBeat, this.stateFor(input.campaign))
  }

  async resolveRoll(input: {
    campaign: AdventureCampaign
    sessionId: string
    heroId: string
    roll: number
    difficulty: CheckDifficulty
    strength: string | null
    spendSparkleToken: boolean
    risky: boolean
  }): Promise<{ turn: AdventureTurnResponse; campaign: AdventureCampaign }> {
    const hero = input.campaign.heroes.find((item) => item.id === input.heroId)
    let result: CheckResolutionResponse
    let campaign = input.campaign

    if (hero) {
      result = await request<CheckResolutionResponse>(
        `/api/campaigns/${encodeURIComponent(input.campaign.id)}/sessions/${encodeURIComponent(input.sessionId)}/heroes/${encodeURIComponent(hero.id)}/checks`,
        {
          method: 'POST',
          body: JSON.stringify({
            roll: input.roll,
            difficulty: input.difficulty,
            strength: input.strength,
            spendSparkleToken: input.spendSparkleToken,
            risky: input.risky,
          }),
        },
      )
      campaign = await request<AdventureCampaign>(`/api/campaigns/${encodeURIComponent(input.campaign.id)}`)
    } else {
      result = {
        roll: input.roll,
        total: input.roll,
        target: 0,
        outcome: 'Demo roll',
        childReadableMessage: 'Your physical die shows the number entered. This preview does not resolve game rules or save the result.',
        sparkleTokenSpent: false,
        heartsAfter: demoHero.hearts,
        sparkleTokensAfter: demoHero.sparkleTokens,
      }
    }

    const source: CheckResolutionSummary['source'] = hero ? 'server' : 'preview'
    const checkResolution = {
      roll: result.roll,
      total: hero ? result.total : null,
      target: hero ? result.target : null,
      outcome: result.outcome,
      childReadableMessage: result.childReadableMessage,
      sparkleTokenSpent: result.sparkleTokenSpent,
      heartsAfter: result.heartsAfter,
      sparkleTokensAfter: result.sparkleTokensAfter,
      source,
    }

    this.turnNumber++
    return {
      turn: this.storyResponse(nextBeat, this.stateFor(campaign), checkResolution),
      campaign,
    }
  }

  async refreshCampaign(campaignId: string): Promise<AdventureCampaign> {
    return request<AdventureCampaign>(`/api/campaigns/${encodeURIComponent(campaignId)}`)
  }

  private stateFor(campaign: AdventureCampaign): AdventureState {
    const isPreview = campaign.heroes.length === 0
    const heroes = isPreview ? [demoHero] : campaign.heroes

    return {
      mode: isPreview ? 'preview' : 'campaign',
      heroes,
      currentQuest: campaign.currentQuest ?? (isPreview ? demoQuest : null),
      clues: demoClues.slice(0, Math.min(this.turnNumber + 1, demoClues.length)),
    }
  }

  private storyResponse(
    storyBeat: StoryBeat,
    state: AdventureState,
    checkResolution?: NonNullable<Extract<AdventureTurnResponse, { type: 'story_beat' }>['checkResolution']>,
  ): AdventureTurnResponse {
    return { type: 'story_beat', storyBeat, state, ...(checkResolution ? { checkResolution } : {}) }
  }
}
