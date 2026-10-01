import type { Concept } from "../types";
export type ExerciseType =
  | "Flashcard"
  | "Prediction"
  | "MultipleChoice"
  | "Ordering"
  | "Matching"
  | "BugSpotting";
export type Block = {
  kind: string;
  text: string;
  language?: string;
  url?: string;
  alt?: string;
  items?: string[];
};
export type Choice = { id: string; text: string };
export type Answer = {
  selected: string[];
  pairs?: Record<string, string>;
  noIssue?: boolean;
};
export type Draft = { answer: Answer | null; note: string };
export type Feedback = {
  attemptId: string;
  resultId: string;
  score: number | null;
  selfReview: string | null;
  answer: Answer | null;
  note: string;
  assisted: boolean;
  answerAware: boolean;
  previousAttemptId: string | null;
  explanation: string;
  referenceAnswer: string;
  receivedAt: string;
};
export type Item = {
  id: string;
  exerciseVersionId: string;
  conceptId: string;
  position: number;
  type: ExerciseType;
  locale: string;
  prompt: Block[];
  interaction: {
    choices: Choice[];
    counterparts?: Choice[];
    multiple: boolean;
    oneToOne: boolean;
  };
  draft: Draft;
  outcome: "Answered" | "SelfReviewed" | "Skipped" | null;
  hintUsed: boolean;
  referenceUsed: boolean;
  revealed: boolean;
  hintCount: number;
  materialVersionId: string | null;
  feedback: Feedback[];
};
export type Session = {
  id: string;
  state: "Active" | "Paused" | "Completed" | "EndedEarly";
  position: number;
  requestedCount: number;
  actualCount: number;
  returnContext: string;
  createdAt: string;
  versionToken: string;
  items: Item[];
};
export type Mutation = {
  outcomeId: string;
  receiptVersionToken: string;
  replayed: boolean;
  session: Session;
};
export type Setup = {
  count: number;
  conceptId?: string;
  roadmapStageId?: string;
  enrollmentId?: string;
  types?: ExerciseType[];
  locale: string;
  feedbackPolicy: "Immediate";
  returnContext: string;
};
export type Preview = {
  requestedCount: number;
  actualCount: number;
  estimatedSeconds: number;
  englishFallback: boolean;
  unavailableReason: string | null;
};
export type Topic = {
  concept: Concept;
  relations: { sourceId: string; targetId: string; kind: string }[];
  materialVersions: string[];
  availableFamilies: number;
};
export type Material = {
  id: string;
  conceptId: string;
  locale: string;
  schemaVersion: number;
  blocks: Block[];
};
export type Knowledge = {
  conceptId: string;
  familyCount: number;
  assistedCount: number;
  selfReviewCount: number;
  qualifyingFamilies: number;
  qualifyingSessions: number;
  correctness: number | null;
  observationIds: string[];
  nextPositiveReviewAt: string | null;
};
export type Goal = {
  id: string;
  intention: string;
  conceptIds: string[];
  roadmapVersionId: string | null;
  archived: boolean;
  versionToken: string;
};
export type Roadmap = {
  id: string;
  title: string;
  description: string;
  locale: string;
};
export type Enrollment = {
  id: string;
  roadmapVersionId: string;
  paused: boolean;
  currentStageId: string | null;
  versionToken: string;
};
export type RoadmapView = {
  roadmap: Roadmap;
  enrollment: Enrollment | null;
  stages: {
    stage: {
      id: string;
      conceptId: string;
      title: string;
      position: number;
      minimumFamilies: number;
      minimumSessions: number;
      minimumCorrectness: number;
    };
    traversal: string;
    evidenceStatus: string;
    evidence: Knowledge | null;
    availableFamilies: number;
  }[];
};
export type Activity = {
  id: string;
  kind: string;
  receivedAt: string;
  attemptId: string | null;
  sessionId: string | null;
};
export const exerciseTypes: ExerciseType[] = [
  "Flashcard",
  "Prediction",
  "MultipleChoice",
  "Ordering",
  "Matching",
  "BugSpotting",
];
