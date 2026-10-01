# Sensei++ — Learning System Architecture & Product Specification

## 1. Goal

Implement the learning subsystem of Sensei++.

The system is not intended to be another linear programming course platform and must not depend on AI for its core learning mechanics.

Sensei++ should maintain a continuously evolving model of what the developer:

* knows;
* has used in practice;
* understands conceptually;
* has difficulty explaining;
* has made mistakes with;
* has not touched for a long time;
* wants to learn;
* wants to repeat;
* may need to revisit based on work activity.

The learning system then uses this model to offer:

1. short daily/mobile practice;
2. free practice by skill/domain;
3. targeted repetition;
4. structured roadmaps for unfamiliar technologies or domains;
5. intensive web training;
6. diagnostic sessions;
7. preparation tracks;
8. future training types without changing the core architecture.

The user must never be forced into a single predefined learning path.

The system should support both:

> “I know what I want to practice.”

and:

> “I have no idea what I should practice — choose something useful for me.”

---

# 2. Core Product Principle

The learning system should be built around a **Knowledge Profile + Learning Graph + Exercise Engine**, not around courses.

Courses and roadmaps are only one possible projection over the graph.

The core abstraction is:

```text
User
    ↓
Knowledge Profile
    ↓
Learning Priorities
    ↓
Knowledge Graph
    ↓
Exercise Selection
    ↓
Learning Session
    ↓
Learning Events
    ↓
Updated Knowledge Profile
```

A roadmap should therefore not own user progress.

The user's actual progress belongs to the global knowledge profile.

For example:

```text
ASP.NET Core roadmap
RabbitMQ roadmap
Interview preparation
Commit review remediation
Daily practice
```

may all exercise the same knowledge node:

```text
Distributed Systems
└── Messaging
    └── RabbitMQ
        └── Delivery Semantics
            └── Idempotency
```

Improvement in one context must affect the user's global mastery of that concept.

---

# 3. AI Independence

AI must not be required for the core learning system.

The platform must work using:

* predefined concepts;
* relationships between concepts;
* authored exercises;
* exercise templates;
* deterministic scoring;
* spaced repetition;
* prerequisite graphs;
* mastery calculations;
* rule-based recommendations.

AI may later be introduced as an optional enhancement.

Possible AI features:

* generating additional explanations;
* adapting hints;
* analysing free-form answers;
* generating follow-up interview questions;
* generating exercises from user context;
* evaluating oral explanations;
* explaining mistakes differently;
* creating personalized examples.

However:

```text
AI unavailable
```

must never mean:

```text
learning unavailable
```

The application must remain fully usable without an AI provider.

---

# 4. Sources of Learning Signals

The system must be designed so that the number of information sources can grow over time.

Do not hardcode the recommendation logic around one source.

Initial sources include:

## 4.1 Explicit user intent

Examples:

```text
"I want to learn RabbitMQ."

"I want to repeat ASP.NET Core."

"I want to prepare for a .NET interview."

"I want to understand distributed systems."

"I keep forgetting async/await."

"I want to learn PostgreSQL indexes."
```

This should create explicit learning priorities.

---

## 4.2 Experience / exposure profile

Sensei++ may know that the user has:

```text
C#               high exposure
ASP.NET Core     high exposure
PostgreSQL       medium exposure
RabbitMQ         medium exposure
Kubernetes       very low exposure
Kafka            no exposure
```

Exposure and mastery must be separate concepts.

A developer may have:

```text
RabbitMQ exposure: HIGH
RabbitMQ mastery: MEDIUM
```

because they use RabbitMQ at work but do not fully understand delivery semantics.

---

## 4.3 Local agent / commit analysis reports

The Sensei local agent can report learning signals derived from development activity.

The server should receive knowledge-related findings rather than raw proprietary source code.

Example:

```text
Source:
Commit Review Agent

Observation:
Developer correctly implemented a RabbitMQ consumer.

Weakness:
Could not clearly explain why duplicate message delivery is possible.

Suggested concepts:
- at-least-once delivery
- acknowledgement
- idempotency

Confidence:
0.86
```

This should affect learning priorities.

It must NOT directly mark the user as "not knowing RabbitMQ".

Instead it generates evidence attached to specific knowledge nodes.

---

## 4.4 Learning history

Previous exercises generate additional evidence:

```text
correct answers;
wrong answers;
response time;
hint usage;
confidence;
repeated mistakes;
mastery decay;
recent successful application.
```

---

## 4.5 Future sources

The architecture must support additional signal providers.

Possible future examples:

```text
IDE activity
Pull request review results
Company learning requirements
Team lead assignments
Imported CV
Imported GitHub profile
Self-assessment
Interview results
Certification preparation
Project history
Work analytics
```

For this reason, use a generic concept such as:

```text
LearningSignal
```

rather than provider-specific fields in the KnowledgeProfile.

---

# 5. Knowledge Graph

The learning domain should be represented as a graph.

Example:

```text
Backend Development
|
+-- C#
|   |
|   +-- Language Fundamentals
|   +-- Memory
|   +-- LINQ
|   +-- async/await
|
+-- ASP.NET Core
|   |
|   +-- Middleware
|   +-- Dependency Injection
|   +-- Authentication
|   +-- Authorization
|
+-- Messaging
    |
    +-- RabbitMQ
        |
        +-- Exchanges
        +-- Queues
        +-- Routing
        +-- Acknowledgements
        +-- Prefetch
        +-- Delivery Semantics
            |
            +-- At-least-once delivery
            +-- Idempotency
```

Relationships may include:

```text
Parent
Prerequisite
RelatedTo
PartOf
AlternativeTo
OftenConfusedWith
AppliedBy
```

The graph must be reusable across:

* roadmaps;
* lessons;
* recommendations;
* analytics;
* agent reports;
* skill profiles.

---

# 6. User Knowledge Profile

Do not store only a single percentage per technology.

Knowledge should have multiple dimensions.

Example:

```text
RabbitMQ / Acknowledgements

Exposure:           0.82
Recall:             0.75
ConceptualModel:    0.54
Application:        0.69
Debugging:          0.42
Explanation:        0.38
Confidence:         0.71

LastPracticed:
2026-09-19

LastObservedAtWork:
2026-09-22
```

Suggested dimensions:

```text
Recall
ConceptualUnderstanding
Application
Debugging
Architecture
Explanation
```

Not every concept needs every dimension.

For example:

```text
HTTP Status Codes
```

may primarily use:

```text
Recall
Application
```

while:

```text
Distributed Transactions
```

may heavily use:

```text
ConceptualUnderstanding
Architecture
Debugging
Explanation
```

---

# 7. Evidence-Based Mastery

Knowledge profile values should be calculated from evidence.

Example evidence:

```text
ExerciseResult
AgentObservation
UserSelfAssessment
RoadmapCheckpoint
InterviewResult
ProjectChallengeResult
```

Each evidence item should include:

```text
ConceptId
Dimension
Result
Confidence
Difficulty
Timestamp
Source
```

Example:

```json
{
  "conceptId": "rabbitmq.idempotency",
  "dimension": "ConceptualUnderstanding",
  "score": 0.35,
  "confidence": 0.9,
  "difficulty": 0.6,
  "source": "AgentReport"
}
```

The initial mastery algorithm can be relatively simple.

Do not prematurely build a complex ML model.

Start with deterministic weighted evidence and decay.

---

# 8. Learning Priorities

Separate:

```text
Knowledge
```

from:

```text
LearningPriority
```

The user may know a technology well but still want to revise it.

Example:

```text
C# mastery = 0.86
C# learning priority = HIGH
```

because:

```text
user has an interview tomorrow.
```

Likewise:

```text
Kafka mastery = 0.05
Kafka learning priority = LOW
```

if the user currently does not care about Kafka.

Priority sources can include:

```text
ExplicitUserGoal
DetectedWeakness
KnowledgeDecay
RoadmapRequirement
AgentFinding
InterviewPreparation
PrerequisiteGap
```

---

# 9. Two Learning Modes Must Coexist

Sensei++ must support two equally important models.

## 9.1 Guided Learning

For users who do not know what to study.

Examples:

```text
Learn RabbitMQ
Learn ASP.NET Core
Prepare for .NET backend interview
Understand distributed systems
```

Sensei provides a roadmap.

Example:

```text
RabbitMQ Fundamentals

1. Messaging basics
2. Producer / Consumer
3. Queue
4. Exchange
5. Routing
6. ACK / NACK
7. Retry
8. Delivery guarantees
9. Idempotency
10. Debugging scenarios
```

This is especially important when:

```text
Exposure is very low
```

or:

```text
the user enters a new domain.
```

---

## 9.2 Free Practice

The user must also be able to ignore roadmaps entirely.

Examples:

```text
Practice something useful
Repeat weak areas
Repeat old knowledge
Practice C#
Practice debugging
Give me architecture exercises
Practice RabbitMQ
Surprise me
Prepare me for interviews
```

The system selects exercises dynamically.

This should feel closer to:

```text
"open Sensei and train"
```

than:

```text
"continue Course 17, Lesson 8."
```

---

# 10. Roadmaps

A Roadmap is a recommended sequence through the knowledge graph.

It must not duplicate the knowledge model.

Suggested structure:

```text
Roadmap
 ├── Stage
 │    ├── KnowledgeNode
 │    ├── KnowledgeNode
 │    └── Checkpoint
 ├── Stage
 └── FinalAssessment
```

Example:

```text
RabbitMQ Roadmap

Stage 1 — Foundations
    Messaging
    Queue
    Producer
    Consumer

Stage 2 — Routing
    Exchange
    Direct exchange
    Topic exchange

Stage 3 — Reliability
    ACK/NACK
    Prefetch
    Retry
    Dead Letter Queue

Stage 4 — Production
    Delivery guarantees
    Idempotency
    Monitoring
    Failure scenarios
```

Roadmaps can define:

```text
recommended order;
required prerequisites;
minimum mastery;
checkpoints;
recommended intensive exercises.
```

---

# 11. Roadmap Flexibility

A roadmap must not behave like a locked linear course.

If the user already knows:

```text
Queues
Exchanges
Routing
```

the roadmap may show:

```text
Already mastered
```

and allow skipping those sections.

Likewise, completing exercises outside the roadmap should advance roadmap progress.

Example:

```text
User completed RabbitMQ Idempotency Debugging Lab
```

before reaching the roadmap stage.

The roadmap should later recognise:

```text
Idempotency mastery already sufficient.
```

---

# 12. Mobile / Daily Learning

Mobile learning should target sessions of approximately:

```text
2–10 minutes.
```

Exercises should be lightweight to interact with but should test real understanding.

Initial exercise types:

```text
Flashcard
Prediction
Ordering
Matching
DiagramAssembly
BugSpotting
BestExplanation
Timeline
WhatChanges
ConstraintChoice
ConfidenceCheck
Reconstruction
OddOneOut
```

Examples:

### Prediction

```csharp
var task = GetValueAsync();

Console.WriteLine("A");

var result = await task;

Console.WriteLine("B");
```

Question:

```text
What happens and in what order?
```

---

### Ordering

```text
Arrange ASP.NET middleware:

Routing
Authentication
Authorization
Endpoints
```

---

### Bug Spotting

```text
Identify suspicious parts of this code.
```

Multiple answers may be correct.

Sometimes:

```text
"No issue"
```

must also be valid.

---

### Architecture Mini-Task

```text
API
Queue
Worker
Database
```

Arrange the components.

---

# 13. Daily Session Composition

A DailySession should not simply pick five random questions.

It should intentionally combine several learning dimensions.

Example:

```text
1 Recall
1 Prediction
1 Bug Spotting
1 Conceptual task
1 Weak-area exercise
```

Possible generation logic:

```text
40% current weak areas
25% spaced repetition
15% active roadmap
10% explicit interests
10% exploration
```

These percentages should be configurable, not hardcoded into UI logic.

---

# 14. Intensive Web Training

Web intensive training is intended for:

```text
30 minutes → several hours
```

and should evaluate engineering reasoning rather than trivia.

Initial intensive exercise types:

```text
CodeReasoning
DebuggingLab
ArchitectureScenario
ProgressiveProject
PRReview
IncidentSimulation
InterviewSimulation
ReverseEngineering
RefactoringChallenge
ProjectDefence
TeachIt
```

---

# 15. Code Reasoning

Example task:

```text
Read the code.

Predict:
- execution order;
- output;
- exception behaviour;
- memory behaviour;
- generated SQL;
- thread behaviour.
```

This should test mental models.

It is NOT meant to be LeetCode.

---

# 16. Debugging Lab

Example scenario:

```text
Payments are occasionally duplicated.
```

The user receives:

```text
logs;
code;
architecture;
metrics;
events.
```

They investigate possible causes:

```text
RabbitMQ redelivery
missing idempotency
transaction boundary
client retry
race condition
```

The system evaluates reasoning steps and conclusions.

---

# 17. Architecture Scenario

Provide a realistic system requirement.

Example:

```text
PDF generation takes 20 seconds.

Requirements:
- request must return quickly;
- processing must happen asynchronously;
- retry up to three times;
- duplicate jobs must be prevented;
- user can check status.
```

The user assembles:

```text
API
Queue
Worker
Storage
Database
```

Then introduce new constraints:

```text
Now load is 100x higher.

Now multiple workers exist.

Now jobs cannot be duplicated.

Now cancellation is required.
```

The exercise tests architecture evolution.

---

# 18. Progressive Project

A flagship intensive feature.

Example:

```text
ASP.NET Core Backend Challenge
```

Stages:

```text
Stage 1
Implement endpoint.

Stage 2
Add persistence.

Stage 3
Add authentication.

Stage 4
Add background processing.

Stage 5
Handle failures.

Stage 6
Scale the solution.
```

After implementation the developer must explain decisions.

Examples:

```text
Why is this service scoped?

Where is the transaction boundary?

What happens when this worker crashes?

Why did you choose a queue?

What happens under 10x load?
```

The system evaluates:

```text
implementation
+
understanding
```

not only whether tests pass.

---

# 19. PR Review Simulator

Give the developer a realistic pull request.

The user writes review comments.

Prepared findings may include:

```text
Critical
Important
Potential concern
Trade-off
Style
False positive
```

The goal is not simply:

```text
find every hidden bug
```

but to practice engineering review.

---

# 20. Incident Simulator

Example:

```text
14:32
API latency increased from 80 ms to 4 seconds.
```

Available information:

```text
logs
CPU
memory
DB pool
queue length
traces
```

The user chooses what to inspect.

The scenario behaves like a deterministic investigation tree.

Example:

```text
DB connection pool exhausted
        ↓
Long transactions
        ↓
External HTTP call inside transaction
```

---

# 21. Interview Simulator

AI should not be required.

Use authored question trees.

Example:

```text
What does async/await do?
        ↓
Does await create a new thread?
        ↓
What happens to the current thread during I/O?
        ↓
What is SynchronizationContext?
        ↓
How is ASP.NET Core different?
```

AI may later generate extra follow-up questions.

---

# 22. Project Defence

After complex exercises the developer explains their solution.

Prompts:

```text
Explain the architecture.

Explain three design decisions.

Name an important trade-off.

Describe the most likely failure mode.

What changes at 10x load?

What would you redesign with more time?
```

This directly supports Sensei++'s core goal:

> developers should understand the systems and code they approve.

---

# 23. Session Types

The application should support several user intents.

Suggested initial types:

```text
DailyPractice
WeakAreas
SpacedReview
TechnologyPractice
DomainPractice
RoadmapSession
InterviewPreparation
AgentRecommended
RandomPractice
IntensiveTraining
DiagnosticSession
```

These should all use the same Exercise Engine.

---

# 24. "What Should I Study?" Mode

This is an important product flow.

The user opens Sensei++ and chooses:

```text
Train me
```

The engine should select useful exercises using:

```text
learning priorities;
knowledge gaps;
knowledge decay;
recent agent findings;
roadmap goals;
recent repetition;
difficulty balance.
```

The user therefore does not need to manage their learning plan manually.

---

# 25. Offline-First Requirement

Offline support is an architectural requirement, not a future patch.

Mobile learning must remain usable without network access.

The user should be able to:

```text
open previously downloaded lessons;
complete exercises;
progress through downloaded roadmap sections;
change self-assessment/profile settings;
update learning goals;
generate learning events;
change knowledge state locally.
```

All of this must work offline.

---

# 26. Local Knowledge State

The client should maintain a local representation of relevant knowledge state.

Example:

```text
LocalKnowledgeProfile
LocalLearningPriorities
DownloadedRoadmaps
DownloadedExercises
LearningEventQueue
SyncMetadata
```

When the user completes an offline lesson:

```text
Exercise completed
        ↓
LearningEvent created locally
        ↓
Local mastery updated immediately
        ↓
UI reflects new state
        ↓
Event waits for synchronization
```

The application should not require server confirmation before showing progress.

---

# 27. Event-Based Synchronization

Prefer syncing learning events rather than blindly replacing the full profile.

Example event:

```json
{
  "eventId": "uuid",
  "deviceId": "uuid",
  "type": "ExerciseCompleted",
  "conceptId": "rabbitmq.idempotency",
  "dimension": "Debugging",
  "score": 0.8,
  "difficulty": 0.7,
  "occurredAt": "...",
  "clientSequence": 125
}
```

The server receives events and recalculates authoritative knowledge state.

This makes multi-device/offline synchronization easier than synchronizing mutable percentages directly.

---

# 28. Local Projection

The client still needs immediate knowledge updates.

Therefore:

```text
Server profile
+
unsynchronized local learning events
=
Local projected profile
```

After synchronization:

```text
Server recalculates profile
        ↓
Client receives authoritative snapshot
        ↓
Pending events become acknowledged
        ↓
Local projection converges
```

---

# 29. Multi-Device Behaviour

Offline behaviour must be visible and understandable to the user.

Example:

```text
Laptop:
RabbitMQ mastery 61%

Phone offline:
complete 4 exercises

Phone local profile:
RabbitMQ mastery 68%
Sync pending
```

Meanwhile:

```text
Web:
complete Debugging Lab

Server:
RabbitMQ mastery 66%
```

When the phone reconnects:

```text
phone events
+
server events
```

are merged.

The final state may become:

```text
RabbitMQ mastery 72%
```

The system should never simply choose:

```text
"latest device wins"
```

for learning activity.

Both devices produced valid evidence.

---

# 30. Sync UX

Synchronization state should be clearly visible.

Possible states:

```text
Synced
Offline
Changes waiting to sync
Syncing
Sync failed
```

Example UI:

```text
Offline mode

7 learning activities saved on this device.
They will sync when connection is restored.
```

For the knowledge profile:

```text
RabbitMQ
68%

↑ 4 offline exercises not yet synchronized
```

The user should understand where the number comes from.

This transparency is particularly important because Sensei++ presents itself as a knowledge tracking system.

---

# 31. What Is Downloaded for Offline Use?

The client should not need the entire global exercise database.

Support downloadable Learning Packs.

Example:

```text
Daily practice pack
RabbitMQ roadmap
ASP.NET Core revision
Interview preparation
```

A pack may contain:

```text
KnowledgeNode metadata
Exercises
Hints
Explanations
Roadmap structure
Prerequisite metadata
Assets
Scoring rules
```

The user can explicitly download a roadmap.

The application can also automatically cache upcoming exercises.

---

# 32. Offline Roadmaps

Downloaded roadmaps must remain functional offline.

Example:

```text
RabbitMQ roadmap downloaded
```

The user can:

```text
complete stages;
unlock locally available stages;
view progress;
repeat exercises;
take checkpoints.
```

Progress synchronizes later.

If a stage requires server-only content:

```text
AI interview
cloud-generated project
company-specific content
```

mark this explicitly.

Do not make the entire roadmap unavailable.

---

# 33. Offline Profile Changes

User changes should also use sync events.

Examples:

```text
LearningGoalCreated
LearningGoalRemoved
SelfAssessmentChanged
RoadmapStarted
RoadmapPaused
InterestAdded
InterestRemoved
```

These changes occur locally and synchronize later.

---

# 34. Conflict Resolution

Different data categories require different merge strategies.

Do not implement one global Last-Write-Wins rule.

Recommended behaviour:

### Learning activity

Append-only event merge.

```text
keep both
```

### Knowledge evidence

Derived from merged learning events.

### Goals

Usually merge by identifier.

### Roadmap progress

Derived from completed learning events.

### User preference

Potentially last-write-wins.

### Deleted items

Use tombstones if required.

---

# 35. Recommended Domain Model

Initial conceptual model:

```text
User

KnowledgeNode
KnowledgeRelation

KnowledgeProfile
KnowledgeState
KnowledgeDimension

LearningSignal
KnowledgeEvidence
LearningPriority

Exercise
ExerciseVariant
ExerciseConcept
ExerciseResult

LearningSession
LearningSessionItem

Roadmap
RoadmapStage
RoadmapNode

LearningGoal

LearningEvent

LearningPack

Device
SyncCursor
SyncBatch
```

Avoid prematurely coupling these entities to a specific UI.

---

# 36. Exercise Metadata

Each exercise should describe what it trains.

Example:

```json
{
  "type": "BugSpotting",
  "concepts": [
    "efcore.async-query",
    "efcore.tracking"
  ],
  "dimensions": [
    "Debugging",
    "ConceptualUnderstanding"
  ],
  "difficulty": 0.45,
  "estimatedSeconds": 90
}
```

This allows the recommendation engine to select exercises intelligently.

---

# 37. Recommendation Engine V1

Do not start with ML.

Implement a rule-based scoring function.

Example conceptual formula:

```text
ExercisePriority =
    WeaknessWeight
  + GoalWeight
  + DecayWeight
  + AgentSignalWeight
  + RoadmapWeight
  + VarietyWeight
  - RecentRepetitionPenalty
  - DifficultyMismatchPenalty
```

The weights should be configurable.

The architecture must allow replacement of this engine later.

---

# 38. Difficulty Adaptation

Difficulty may be chosen using recent evidence.

Example:

```text
Mastery < 0.30
    foundational exercises

0.30–0.60
    applied exercises

0.60–0.80
    debugging / explanation

> 0.80
    architecture / edge cases / teaching
```

Avoid interpreting high mastery as:

```text
"nothing left to learn."
```

Instead use more difficult exercise dimensions.

---

# 39. Discovery Mode

When the user says:

```text
"I want to learn Kafka"
```

but has no exposure data, Sensei should not immediately assume the user is a complete beginner.

Run a lightweight discovery/diagnostic session.

Example:

```text
Messaging fundamentals
Partitions
Consumer groups
Offsets
Delivery semantics
Replication
```

The result determines where the roadmap should begin.

---

# 40. Freedom of Navigation

The UI should expose both:

```text
Recommended
```

and:

```text
Explore
```

Example learning home:

```text
Continue learning
    RabbitMQ roadmap

Recommended for you
    ACK/NACK review
    async/await prediction

Quick practice
    Debugging
    Architecture
    C#
    Random

Explore knowledge
    Backend
    Databases
    Messaging
    Distributed systems
```

Roadmaps guide.

They do not imprison.

---

# 41. Learning History

The user should be able to understand why Sensei recommends something.

Example:

```text
Why am I seeing this?

RabbitMQ / Idempotency

• commit analysis found uncertainty here 3 days ago;
• you answered 2/4 related exercises correctly;
• last successful review was 21 days ago.
```

This should be possible without AI.

---

# 42. Transparency

Sensei++ should avoid opaque claims like:

```text
"You know RabbitMQ 63%."
```

Instead, users should be able to inspect:

```text
RabbitMQ — 63%

Conceptual understanding  72%
Application               68%
Debugging                 48%
Explanation               41%

Recent evidence:
✓ routing exercise
✓ architecture scenario
✗ duplicate delivery question
△ agent report: explanation difficulty
```

This also makes offline changes understandable.

---

# 43. Important Separation of Concerns

Keep these concepts separate:

```text
KnowledgeGraph
UserKnowledgeProfile
LearningPriority
Roadmap
Exercise
LearningSession
LearningEvent
RecommendationEngine
SyncEngine
```

In particular:

```text
Roadmap != KnowledgeProfile

ExerciseResult != Mastery

AgentFinding != Mastery

Exposure != Mastery

Recommendation != Roadmap
```

These distinctions are essential for long-term extensibility.

---

# 44. Backend vs Client Responsibilities

## Backend

Responsible for:

```text
canonical knowledge graph;
exercise repository;
roadmaps;
authoritative knowledge projection;
learning signal ingestion;
recommendation generation;
learning pack creation;
event synchronization;
cross-device state.
```

## Client

Responsible for:

```text
offline exercise execution;
local event storage;
local mastery projection;
downloaded learning packs;
session state;
sync queue;
offline UI;
temporary recommendation generation from cached content.
```

---

# 45. Offline Recommendation Fallback

When offline, the client should still be able to build practice sessions.

Use cached data:

```text
local knowledge projection;
downloaded exercises;
cached priorities;
roadmap state;
spaced repetition schedule.
```

Recommendation quality may be slightly reduced offline, but practice must remain possible.

---

# 46. Suggested Architecture Direction

Prefer an event-oriented learning subsystem.

Conceptually:

```text
                         ┌─────────────┐
                         │ User Goals  │
                         └──────┬──────┘
                                │
                         ┌──────▼──────┐
                         │ Agent Data  │
                         └──────┬──────┘
                                │
                         ┌──────▼──────┐
                         │ Signals     │
                         └──────┬──────┘
                                │
                    ┌───────────▼───────────┐
                    │ Knowledge Projection  │
                    └───────────┬───────────┘
                                │
                 ┌──────────────▼──────────────┐
                 │ Recommendation / Roadmaps   │
                 └──────────────┬──────────────┘
                                │
                      ┌─────────▼─────────┐
                      │ Exercise Engine   │
                      └─────────┬─────────┘
                                │
                      ┌─────────▼─────────┐
                      │ Learning Events   │
                      └─────────┬─────────┘
                                │
                     updates knowledge
```

Offline clients maintain their own temporary projection from locally generated learning events.

---

# 47. MVP Scope

Do not attempt to implement the entire vision immediately.

## Phase 1

Implement:

```text
KnowledgeNode
KnowledgeGraph
KnowledgeProfile
KnowledgeEvidence
LearningGoal

Exercise
ExerciseResult

DailyPracticeSession

basic deterministic mastery calculation
basic recommendation scoring

manual topic selection

one roadmap

mobile-friendly exercise execution
```

Exercise types:

```text
Flashcard
Prediction
MultipleChoice
Ordering
Matching
BugSpotting
```

---

# 48. Phase 2

Add:

```text
Spaced repetition
Knowledge decay
Multiple mastery dimensions
Roadmap diagnostics
Weak-area sessions
Learning priorities
Agent learning signals
Recommendation explanations
```

---

# 49. Phase 3

Add intensive web exercises:

```text
CodeReasoning
DebuggingLab
ArchitectureScenario
PRReview
InterviewSimulation
ProjectDefence
```

---

# 50. Phase 4

Add full offline-first support:

```text
Learning Packs
Local learning events
Local knowledge projection
Sync protocol
Multi-device reconciliation
Offline roadmaps
Offline recommendations
Visible synchronization UX
```

However:

**data models and APIs created in earlier phases must already assume eventual offline/event synchronization.**

Do not create an architecture that requires rewriting the learning model to add offline support.

---

# 51. Phase 5

Add advanced integrations:

```text
Local Agent
Commit reports
Corporate learning signals
Team learning plans
Company knowledge domains
IDE integrations
Project-derived practice
```

---

# 52. Phase 6 — Optional AI Layer

AI can enhance:

```text
free-form explanation evaluation;
adaptive hints;
custom follow-up questions;
exercise generation;
project defence;
voice interviews;
personalized explanations.
```

AI output should preferably result in normal platform objects:

```text
Exercise
LearningSignal
Hint
Explanation
```

rather than creating an entirely separate AI-only learning subsystem.

---

# 53. Primary Product Philosophy

Sensei++ should not answer only:

> What course is the user currently taking?

It should answer:

> What does this developer appear to understand?

> Where is their understanding weak?

> What have they actually encountered?

> What do they want to learn?

> What should probably be revisited?

> Which type of exercise would best test that knowledge?

And then allow the developer either to:

```text
follow a recommended path
```

or:

```text
practice whatever they want.
```

Both workflows are first-class.

---

# 54. Final UX Mental Model

The application should feel like the developer owns a persistent map of their engineering knowledge.

Sensei observes evidence from:

```text
learning
work
self-defined goals
future integrations
```

and continuously updates that map.

From the same map the user can:

```text
learn something new;
repeat old knowledge;
repair a detected weakness;
prepare for an interview;
follow a roadmap;
do a five-minute mobile session;
complete a two-hour intensive lab;
practice offline;
continue on another device later.
```

The learning system should therefore be implemented as a reusable platform subsystem rather than as a traditional course module.
