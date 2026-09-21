# Architecture Direction

This section describes the current architectural direction of Sensei++.

It is not a final implementation specification.

Codex should treat these decisions as strong design constraints and use them when researching and proposing the detailed architecture, while still pointing out problems or better alternatives where justified.

---

## 1. Product architecture, not one giant UI

Sensei++ should not be thought of as one application screen containing every feature.

The product may eventually have several user-facing clients serving different contexts:

- personal developer web application;
- mobile/personal learning client;
- company/team web interface;
- IDE integration;
- local developer agent / CLI;
- possibly integrations with Git providers and other developer tools.

These applications should share the same underlying professional-development model and backend services where appropriate.

However, they do not need to expose the same functionality.

For example:

### Personal application

Main concerns:

- learning;
- knowledge retention;
- skill profile;
- professional journal;
- achievements;
- interview preparation;
- career stories;
- personal growth history.

### Team / lead application

Possible concerns:

- aggregated competency trends;
- recurring team knowledge gaps;
- shared learning initiatives;
- intentionally shared development information;
- organization-level configuration.

It must NOT simply expose all private developer learning information to managers.

### Local developer tooling

Possible future concerns:

- understanding repository context;
- Git integration;
- work reflection;
- code-related questioning;
- pre-review reflection;
- creation of sanitized professional-development events.

These are different interaction environments built around the same broader product.

---

# 2. Initial system shape

For the initial implementation, prefer a relatively simple deployment architecture.

The current preferred direction is:

- one primary backend application;
- modular internal architecture;
- one primary relational database;
- web frontend;
- background processing where required;
- AI provider integration behind an abstraction;
- additional clients communicating through the same backend API.

Do NOT introduce distributed-system complexity merely because the product contains multiple conceptual domains.

In particular, microservices are NOT a requirement for the initial system.

A modular monolith is currently the preferred starting point unless research finds a strong reason otherwise.

---

# 3. Backend

The backend will most likely be implemented with:

- C#
- ASP.NET Core
- PostgreSQL

The backend should remain the authoritative server-side application for cloud data.

Potential infrastructure components may include:

- Hangfire for scheduled/background work;
- PostgreSQL for relational storage;
- pgvector or another vector mechanism if semantic retrieval becomes justified;
- SignalR only for genuinely realtime use cases;
- external AI providers through a common model abstraction.

These choices should be validated during architecture research rather than added automatically.

---

# 4. Modular domain structure

Although initially deployed as one backend, the system should be divided into clear functional modules.

Current candidate domains include:

## Identity

Users, authentication, accounts and access control.

## Organizations

Companies, teams, organization membership and organization-level permissions.

This module should remain optional for individual users.

## Professional Profile

Represents the user's evolving professional state.

This may include:

- technologies used;
- areas of knowledge;
- professional exposure;
- demonstrated understanding;
- experience history;
- knowledge confidence;
- evidence collected from other modules.

This is likely to become one of the central domains.

## Learning

Responsible for:

- learning sessions;
- questions;
- exercises;
- spaced repetition;
- review scheduling;
- learning history;
- adaptive exercise selection.

## Knowledge / Competency Model

Responsible for representing what the system currently believes about the user's knowledge.

Important distinction:

professional exposure != theoretical knowledge != demonstrated understanding.

The system should avoid reducing everything to a meaningless single "skill percentage".

Evidence may include:

- self-declared familiarity;
- learning performance;
- successful recall;
- explanation performance;
- scenario performance;
- real professional usage;
- recency.

## Experience / Career Journal

Responsible for professional events such as:

- completed tasks;
- difficult bugs;
- architectural decisions;
- improvements;
- incidents;
- achievements;
- technologies used;
- outcomes and impact.

These events may later become:

- CV bullet candidates;
- STAR stories;
- performance-review material;
- interview material.

## Interview / Career Preparation

Responsible for workflows such as:

- technical interview preparation;
- focused refresh plans;
- vacancy analysis;
- knowledge-gap analysis;
- interview questions based on personal experience;
- behavioral/STAR preparation.

It should consume information accumulated elsewhere rather than building a completely separate user profile.

## AI

AI should be an infrastructure capability used by multiple domains rather than the entire application architecture being centered around one LLM.

Possible responsibilities include:

- evaluating free-form answers;
- generating follow-up questions;
- extracting structured information from career entries;
- helping generate learning content;
- vacancy analysis;
- transforming professional events into interview stories.

The business domains should not depend directly on one specific model provider.

---

# 5. Shared professional model

One of the most important architectural ideas is that different workflows should contribute evidence to the same professional profile.

For example:

A user records:

> Investigated and fixed an intermittent concurrency issue caused by incorrect service lifetime.

This may create evidence for:

- debugging;
- concurrency;
- dependency injection;
- service lifetimes.

Later the learning system may discover:

- strong understanding of DI lifetimes;
- weak understanding of thread safety.

Later still, interview preparation may recognize the same event as a possible answer to:

> Tell me about a difficult bug you diagnosed.

The original professional event should not need to be recreated independently inside three different modules.

The architecture should therefore allow information to flow:

Work / Experience
        ↓
Professional Evidence
        ↓
Knowledge / Competency Model
        ↓
Learning Recommendations
        ↓
Career / Interview Preparation

This relationship is central to the product.

---

# 6. Event-oriented domain interaction

The system may benefit from representing important user activities as domain events.

Examples:

- ProfessionalEventCreated
- LearningSessionCompleted
- KnowledgeAssessmentCompleted
- AchievementConfirmed
- ReviewScheduled
- InterviewPlanCreated
- WorkReflectionCompleted

For example:

WorkReflectionCompleted
        |
        +--> update professional evidence
        |
        +--> update knowledge state
        |
        +--> suggest future learning
        |
        +--> possibly suggest a career achievement

Initially this does NOT require Kafka, RabbitMQ, or a distributed event architecture.

In-process application/domain events are acceptable.

An outbox mechanism may be introduced later for asynchronous operations where reliability matters.

The important architectural principle is decoupling domain reactions, not introducing infrastructure for its own sake.

---

# 7. Evidence-oriented competency model

The system should avoid treating skill tracking as:

Skill -> integer

For example:

RabbitMQ = 73%

is not enough.

Instead the system should conceptually retain evidence such as:

RabbitMQ
- declared familiarity;
- used professionally;
- last professional usage;
- recall history;
- explanation performance;
- scenario performance;
- related professional events;
- review history.

A confidence/mastery indicator may later be derived from these signals.

The raw evidence is more important than the final score.

This model should be investigated carefully because it influences much of the rest of the product.

---

# 8. Privacy boundary

An important future component is a developer-side local agent.

The architectural rule for this component should be:

SOURCE CODE AND PRIVATE REPOSITORY CONTEXT DO NOT NEED TO BE SENT TO THE SENSEI++ BACKEND.

The local component may eventually have access to:

- repository contents;
- Git diff;
- Git history;
- tests;
- build output;
- project documentation;
- architecture context.

It may use that context to conduct developer reflection such as:

- Why did you choose this solution?
- What can fail?
- What alternatives were considered?
- Can this execute concurrently?
- What assumptions does the implementation depend on?

However, the cloud backend should receive a sanitized professional-development event rather than the repository context itself.

Conceptually:

Repository
    ↓
Local analysis
    ↓
Developer reflection
    ↓
Sanitized professional event
    ↓
Sensei++ backend

Example cloud-side information:

- user investigated a concurrency problem;
- dependency injection lifetimes were involved;
- user demonstrated strong knowledge of DI;
- user had difficulty explaining thread safety;
- task required substantial debugging;
- user considers the task professionally significant.

The cloud does NOT need:

- class names;
- company domain objects;
- endpoints;
- source code;
- SQL queries;
- proprietary implementation details.

---

# 9. Local agent is a future complementary component

The local developer agent should NOT be required for the first usable version of Sensei++.

The web application should provide significant value independently.

Initial users may have no professional work environment at all.

For example, a student should still be able to use:

- learning;
- knowledge retention;
- skill mapping;
- personal projects;
- interview preparation;
- professional-development tracking.

The future local agent enriches the professional profile with information derived from real development activity.

It should not become the foundation without which the rest of the product cannot function.

---

# 10. Possible local architecture

If the local agent is implemented later, prefer separating its core functionality from the IDE UI.

Conceptually:

Git / Repository
       ↓
Sensei++ Local Agent
       ↓
Local analysis / LLM
       ↓
Developer reflection
       ↓
Sanitized event
       ↓
Sensei++ API

IDE extensions should ideally act as interfaces to the local agent rather than containing all core analysis logic themselves.

This would allow future support for:

- VS Code;
- Visual Studio;
- Rider / JetBrains IDEs;
- terminal workflows;
- CI/developer tooling integrations.

This is currently a direction, not an MVP requirement.

---

# 11. Individual-first architecture

Sensei++ should remain useful when:

- the user's employer does not use Sensei++;
- the user is a student;
- the user is unemployed;
- the user is changing jobs;
- the user changes companies.

The professional profile belongs conceptually to the individual.

Corporate functionality should be built on top of this model rather than making the user's identity permanently dependent on one employer.

This matters because one of the product's goals is to preserve professional growth across a developer's career.

---

# 12. Personal vs corporate data

Be careful when designing organization functionality.

We do NOT currently want a model where managers automatically see:

- every wrong learning answer;
- private interview preparation;
- all personal career entries;
- detailed individual weaknesses;
- private reflections.

Possible organization-visible information should be explicitly designed.

Examples of potentially acceptable team-level functionality:

- aggregated knowledge trends;
- opt-in shared competency evidence;
- organizational learning paths;
- team-wide recurring knowledge gaps;
- voluntarily shared pre-review summaries.

The product must avoid becoming employee surveillance software.

Trust is an important architectural and product constraint.

---

# 13. MVP architectural goal

Do not attempt to implement the entire ecosystem initially.

The first architecture should support a vertical slice such as:

User
    ↓
Professional profile
    ↓
Learning session
    ↓
Answer / assessment
    ↓
Knowledge evidence updated
    ↓
Personalized future review
    ↓
Professional event captured
    ↓
Event becomes useful during interview preparation

This is enough to validate the common underlying model.

The architecture should make future additions possible without requiring them now.

---

# 14. Key architectural question for research

When proposing the detailed architecture, investigate especially:

1. How should the professional competency/evidence model be represented?
2. What should be a domain entity versus derived information?
3. How should learning activity affect knowledge confidence?
4. How should professional experience affect the competency model without falsely assuming "used == understood"?
5. How should modules communicate inside the modular monolith?
6. What belongs in relational data versus semantic/vector retrieval?
7. Where is AI actually justified?
8. What should remain deterministic rather than LLM-driven?
9. How should AI evaluation uncertainty be represented?
10. How can privacy boundaries be enforced technically rather than merely promised?
11. What should be designed now to allow a future local agent?
12. Which architectural abstractions would be premature for the MVP?

Do not optimize for architecture sophistication.

Optimize for:

- clear domain boundaries;
- maintainability;
- privacy;
- extensibility;
- testability;
- the ability to validate the product incrementally.