export type Difficulty = 'Beginner' | 'Intermediate' | 'Advanced'
export type Setting = 'Employment' | 'Coursework' | 'PersonalProject' | 'Other'
export type EvidenceSignal = 'SelfDeclaration' | 'ProfessionalExposure' | 'Explanation' | 'Scenario' | 'Recall' | 'ClientReportedAssessment'
export type EvidenceSource = 'Declaration' | 'Attempt' | 'FeedbackRevision' | 'ApprovedExperienceRevision'
export type Assistance = 'None' | 'HintUsed' | 'ReferenceReviewed' | 'SelfReviewed'
export type EvidenceStatus = 'Active' | 'Disputed' | 'Superseded' | 'Withdrawn'
export type ImpactState = 'Unknown' | 'Qualitative' | 'Measured'
export type ApprovalState = 'Draft' | 'Approved' | 'Retracted'

export interface Concept {
  id: string
  key: string
  name: string
  description: string
  locale: string
  difficulty: Difficulty
  isActive: boolean
  createdAt: string
  version: number
}

export interface WorkEpisode {
  id: string
  ownerId: string
  title: string
  setting: Setting
  eventDate: string
  role: string
  summary: string
  isArchived: boolean
  createdAt: string
  updatedAt: string
  version: number
}

export interface EvidenceObservation {
  id: string
  ownerId: string
  conceptId: string
  aspect: string
  signalKind: EvidenceSignal
  sourceKind: EvidenceSource
  sourceId: string
  assistance: Assistance
  conditions: string
  status: EvidenceStatus
  observedAt: string
  version: number
}

export interface ExperienceRevision {
  number: number
  title: string
  setting: Setting
  context: string
  role: string
  actions: string
  alternatives: string
  outcome: string
  impactState: ImpactState
  conceptIds: string[]
  approvalState: ApprovalState
  createdAt: string
  approvedAt: string | null
}

export interface ExperienceEntry {
  id: string
  ownerId: string
  isArchived: boolean
  createdAt: string
  version: number
  revisions: ExperienceRevision[]
}

export interface UserAccount {
  id: string
  email: string
  displayName: string
  uiLocale: string
  answerLanguage: string
  timeZone: string
  createdAt: string
  version: number
}

export interface ApiProblem {
  title?: string
  detail?: string
  status?: number
}
