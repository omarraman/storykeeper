export type CheckDifficulty = 'Easy' | 'Tricky' | 'Heroic'

export interface SuggestedChoice {
  id: string
  text: string
}

export interface StoryBeat {
  id: string
  narration: string
  speaker: string | null
  npcDialogue?: { npcName: string; text: string }[]
  suggestedChoices: SuggestedChoice[]
}

export interface RollRequired {
  prompt: string
  difficulty: CheckDifficulty
  strength: string | null
  risky: boolean
}

export interface InventoryItemSummary {
  name: string
  description: string
  quantity: number
}

export interface HeroStatus {
  id: string
  name: string
  role: string
  hearts: number
  sparkleTokens: number
  strengths: string[]
  inventory: InventoryItemSummary[]
}

export interface QuestSummary {
  title: string
  description: string
}

export interface ClueSummary {
  id: string
  text: string
}

export interface CheckResolutionSummary {
  id?: string
  roll: number
  total: number | null
  target: number | null
  outcome: string
  childReadableMessage: string
  sparkleTokenSpent: boolean
  heartsAfter: number
  sparkleTokensAfter: number
  source: 'server' | 'preview'
}

export interface AdventureState {
  mode: 'campaign' | 'preview'
  heroes: HeroStatus[]
  currentQuest: QuestSummary | null
  clues: ClueSummary[]
}

export type AdventureTurnResponse =
  | { type: 'story_beat'; storyBeat: StoryBeat; state: AdventureState; checkResolution?: CheckResolutionSummary }
  | { type: 'roll_required'; rollRequired: RollRequired }
  | { type: 'error'; message: string; retryable: boolean }

export interface AdventureCampaign {
  id: string
  name: string
  status: 'Active' | 'Completed' | 'Archived'
  heroes: {
    id: string
    name: string
    role: string
    strengths: string[]
    hearts: number
    sparkleTokens: number
    inventory: InventoryItemSummary[]
  }[]
  currentQuest: QuestSummary | null
  latestSession: {
    id: string
    sessionNumber: number
    startedAtUtc: string
    endedAtUtc: string | null
    summary: string | null
  } | null
}

export interface SessionStartResponse {
  id: string
  campaignId: string
  sessionNumber: number
  startedAtUtc: string
  heroes: { heroId: string; heroName: string; hearts: number; sparkleTokens: number }[]
}

export interface CheckResolutionResponse {
  id?: string
  roll: number
  total: number
  target: number
  outcome: string
  childReadableMessage: string
  sparkleTokenSpent: boolean
  heartsAfter: number
  sparkleTokensAfter: number
}

export interface AdventureTurnClient {
  openTurn(campaign: AdventureCampaign, sessionId: string): AdventureTurnResponse
  submitAction(input: {
    action: string
    choiceId: string | null
    campaign: AdventureCampaign
    sessionId: string
    heroId: string
  }): Promise<AdventureTurnResponse>
  resolveRoll(input: {
    campaign: AdventureCampaign
    sessionId: string
    heroId: string
    roll: number
    difficulty: CheckDifficulty
    strength: string | null
    spendSparkleToken: boolean
    risky: boolean
  }): Promise<{ turn: AdventureTurnResponse; campaign: AdventureCampaign }>
  refreshCampaign(campaignId: string): Promise<AdventureCampaign>
}
