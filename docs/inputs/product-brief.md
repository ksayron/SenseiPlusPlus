# Sensei++ — Product brief and architecture research assignment

## 0. Instructions to Codex

You are helping design **Sensei++**, a graduation project: a web application for the professional development of software developers.

Your first assignment is to research and propose a practical technical architecture and implementation plan **from the product requirements in this document**. Do not immediately scaffold an application, select an elaborate stack, or reduce the product to a generic chatbot, flashcard application, résumé builder, or code-review bot.

Read the complete brief before making architecture decisions. If working inside an existing repository, inspect its instructions and current state before proposing changes. This document describes the intended product; it does not assert that any component already exists.

Deliver an opinionated, implementable design. Explain how the chosen design serves the workflows, what it costs to operate and maintain, and which uncertainties need a small technical experiment. Recommend defaults for reversible decisions rather than asking the creator to choose every library.

### Authority and interpretation

- **Product commitments** are expressed as “must” or “required.” Preserve them unless the creator explicitly changes them.
- **Proposed graduation scope** is a concrete starting point for planning, not an assertion that every detail has already been approved.
- **Examples** illustrate behavior and are not mandatory schemas, screens, or exact prompts.
- **Research questions** are intentionally undecided. Evaluate them instead of treating them as chosen technologies.
- Where a constraint is unknown, identify it and make a clearly labeled working assumption. Do not invent a submission date, budget, institutional requirement, deployment target, or approved access to employer data.
- Do not require a commercial validation program before architecture work. The creator needs to build and defend a useful graduation project.
- Stop the initial assignment at the research/design deliverables in Section 20. Production implementation is a subsequent phase. Small feasibility experiments may be proposed; do not install integrations, upload private repositories, incur paid usage, or deploy services merely to investigate them without appropriate authorization.

## 1. Project context

The creator is a software engineering student approaching graduation and a working full-stack C#/.NET developer. They actively use AI coding tools and agents. The idea comes from three connected observations:

1. The creator and peers sometimes accept AI-assisted changes without deeply understanding the implementation, especially when a lead does not find a problem.
2. During a meetup, a CTO asked whether automated reviews could run before committing. That conversation suggested an additional opportunity: help the author understand and consciously approve what they are submitting, alongside checks on the code itself.
3. Developers struggle to reconstruct their achievements and explain mechanisms they use every day when preparing for interviews, performance reviews, or a new role.

These observations justify a practical project and exploratory evaluation. They are not proof of market size, commercial retention, or a causal learning benefit.

### Graduation-project constraints

- Assume one primary developer assisted by coding agents, unless told otherwise.
- Prioritize a complete, understandable, demonstrable product over infrastructure breadth.
- Keep operating cost and maintenance effort modest; exact hosting and AI budgets are not yet specified.
- The creator's strongest expertise is C#/.NET. Treat it as the strong default candidate for backend work; explain any recommendation to depart from it. This brief does not freeze a framework version or frontend stack.
- Exact submission date, weekly availability, university rubric, and required client platforms remain unknown. Plan in milestones and dependencies before assigning calendar dates.
- Research studies, large participant recruitment, enterprise procurement, and commercial traction are not prerequisites for success.
- The project may later become a public passion project or product, so avoid disposable design choices that would make ordinary deployment and maintenance unnecessarily difficult.

## 2. Product definition

**Sensei++ helps developers turn learning and everyday engineering work into retained understanding, visible growth, and credible accounts of their experience.**

Its identity is a mentor combining a teacher and a senior engineering colleague:

- A learner can study, explain, practice, and improve without having a job or a repository to connect.
- A working developer can reflect on real changes, investigate gaps, retain useful knowledge, and capture professional experience.
- A developer preparing for an interview or review can reuse their existing learning and experience history.

The application connects three dimensions:

| Dimension | User question | Product responsibility |
| --- | --- | --- |
| Learn | What do I understand, and what should I practice? | Teach when needed, elicit explanations, provide feedback, and schedule useful follow-up. |
| Work | What did I do, why, and what did I learn? | Capture meaningful episodes, reasoning, uncertainty, and outcomes with low friction. |
| Prove | Can I explain my reasoning and contribution when needed? | Help rehearse and present evidence grounded in the user's real history. |

“Prove” means demonstrate and communicate within a stated context. It does not mean an accredited certificate, a universal competence score, or verified employment history.

### Central loop

An engineering episode or learning scenario supplies context. The user explains their reasoning. Feedback reveals a gap or reinforces a sound understanding. Relevant teaching and practice address the gap. A later attempt checks retention or transfer. An approved record preserves what was learned or accomplished.

The components should share context so that the user does not repeatedly describe the same work to unrelated features.

## 3. The distinctive problem: code quality and author understanding

Conventional review asks whether a change is correct, maintainable, and appropriate. Sensei++ additionally asks whether the developer can explain the important behavior, decisions, assumptions, and failure modes.

These are related but distinct:

| Observation | What it establishes | What it does not establish |
| --- | --- | --- |
| Tests pass | Specified tests succeeded in the tested environment | Complete correctness or author understanding |
| A reviewer approves | A reviewer accepted the change under their process | That the author independently understands every mechanism |
| AI provides an explanation | An explanation was produced | That the developer can reproduce or apply the reasoning |
| Developer answers contextual questions | Evidence of understanding for those questions and conditions | Full understanding of the whole system |
| Developer acknowledges a reviewed change | An explicit recorded decision about a particular version | Guaranteed safety, independent reasoning, or formal certification |

Sensei++ should provide an **additional layer of assurance through observable explanation and explicit acknowledgement**. It must not claim to certify full understanding, prevent all mistakes, or replace tests and human review.

The product must distinguish:

- static or automated code observations;
- the developer's explanation;
- model-generated feedback;
- open questions and disputed feedback;
- the developer's final acknowledgement.

Acknowledgement is a user action. It must never be created automatically because a model assigned a high score.

## 4. Users and first scope

The first domain should favor C#/.NET/backend concepts because the creator can develop and judge examples in that area. Exact topic coverage is a scope decision for the implementation plan.

### Required user paths

**Learning path:** available without employer data, Git access, or professional experience. Users choose a topic or scenario, receive teaching when necessary, attempt exercises, inspect feedback, and revisit useful material.

**Work path:** users choose a meaningful change, investigation, incident, design decision, or other engineering episode. They supply context, reflect, capture experience, and receive related practice.

**Presentation path:** users revisit approved work entries and learning history to explain their decisions or contribution. Rich interview preparation may be staged, but reusing an experience entry must be possible in the graduation version.

These are modes within one product, not mutually exclusive account types. A student may work on a personal project; an experienced developer may be a beginner in a new topic.

### Primary initial evaluation group

Working junior-to-mid-level developers who use AI assistance are a practical starting group. Include a few learner scenarios to verify that the product does not require employment. This focus should not hard-code assumptions about seniority or workplace access into the domain model.

## 5. Goals and non-goals

### Product goals

1. Help users explain the mechanisms and decisions relevant to their work or study.
2. Reveal specific uncertainties before they become forgotten context.
3. Make active practice and later recall easier to sustain.
4. Preserve meaningful experience without demanding a separate elaborate journaling habit.
5. Turn approved experience into useful review notes and career stories.
6. Give users an honest picture of observed understanding, exposure, and unanswered questions.
7. Support personal development without becoming employee surveillance.

### Non-goals for the initial implementation

- A general-purpose coding agent or full automated code-review platform.
- A guarantee that a submitted change is safe to merge.
- A comprehensive curriculum across all programming languages.
- A replacement for technical documentation, university teaching, or a human mentor.
- A job board, recruitment platform, or complete performance-management system.
- Public leaderboards, universal seniority rankings, or commit-count productivity scores.
- Biometric proctoring, detection of every AI-assisted answer, or adversarial examinations.
- Fine-tuning a proprietary foundation model as a prerequisite.
- Enterprise analytics, multiple native clients, or large-scale infrastructure merely for architectural sophistication.

## 6. Main workflow: reflect on real work

### 6.1 Select an episode and provide context

The user starts from a meaningful event, not necessarily a commit. Examples include a bug investigation, feature, refactor, database optimization, design discussion, code review, or mentoring contribution.

Initial inputs may include:

- a manually written or dictated-and-transcribed description;
- a selected code snippet or pasted diff;
- task purpose and constraints;
- technologies or concepts, suggested by the system but correctable;
- what the user did personally versus what a teammate or AI produced;
- a source reference, when available and permitted.

Manual text and selected snippets are sufficient to establish the core flow. Voice input and automatic imports are options to research, not baseline requirements.

The user must be able to inspect what context will be sent to an external AI provider. The app must remain useful when they cannot share employer code.

### 6.2 Establish a shared understanding of the task

The application produces or requests a concise description of the intent, relevant decisions, and missing context. The user can correct it before assessment.

Do not infer business purpose solely from a diff. Ask targeted questions when a missing assumption changes the interpretation. If context is insufficient, say so rather than producing a confident review.

### 6.3 Conduct a short reflective conversation

Ask a small set of questions tied to actual decisions. Possible questions:

- Why did you choose this approach?
- What alternative did you consider, and what made it less suitable here?
- What happens under concurrent execution or partial failure?
- What does this dependency lifetime assume?
- What makes retrying safe in this case?
- What evidence supports your conclusion?
- What should a reviewer pay particular attention to?

The session should adapt to the answers, but have a clear stopping point. The goal is useful reasoning, not a long interrogation. The user can pause, skip, ask for help, challenge feedback, or mark a question irrelevant.

### 6.4 Show feedback and unresolved issues

Separate correct reasoning, missing information, possible misconceptions, and contextual trade-offs. Offer explanation or references when needed. Let the user revise an answer without erasing the distinction between their first attempt and assisted correction.

Model uncertainty and missing context are valid outcomes. A failed provider call is not a failed learning attempt.

### 6.5 Record informed acknowledgement

A completion view should summarize:

- what change or episode was discussed;
- which important topics were checked;
- what the user explained and where they needed help;
- unresolved risks, unknowns, or disputed feedback;
- the version of the supplied context;
- the user's explicit decision to acknowledge, return later, or leave the session incomplete.

If the underlying diff changes materially, an old acknowledgement must remain associated with the old version. It must not silently approve new code. Research an appropriate version/fingerprint and recheck approach.

Acknowledgement can coexist with documented uncertainty; it must not imply that the model validated the entire system. If a team later wants a gate, distinguish acknowledgement of review from evidence satisfying that team's separate acceptance criteria.

### 6.6 Reuse the session

With user review, produce:

1. A concise explanation or reviewer handoff note.
2. A draft experience entry.
3. One or more relevant concepts or gaps for later practice.

These artifacts share provenance but have separate approval and sharing states. Completing a reflection must not automatically publish private answers or promote inferred claims into confirmed achievements.

## 7. Workflow: learn and maintain knowledge

The learning experience must be useful independently of the work workflow.

### Entry points

- Choose a technology, mechanism, or curated scenario.
- Revisit a gap discovered during work reflection.
- Follow up on an earlier exercise.
- Refresh knowledge for a review or interview.

### Learning behavior

Use several ways to exercise a concept:

| Exercise | Intended signal |
| --- | --- |
| Recall | Retrieve a relevant fact or mechanism without rereading it first. |
| Explain | Connect steps and causes in the user's own words. |
| Compare | Explain when two approaches differ and why that matters. |
| Apply | Use a mechanism in a concrete situation. |
| Diagnose | Identify plausible causes and useful evidence in a failure scenario. |
| Challenge | Defend or revise a decision after a changed assumption. |
| Transfer | Apply the underlying reasoning to a new context. |

Do not implement this as a mandatory seven-step ceremony for every concept. Choose a suitable exercise and adapt the next step.

For unfamiliar material, teach before repeatedly testing. For recall, give the user an opportunity to answer before showing the explanation. After a wrong answer, provide a route to understanding rather than merely lowering a score.

Plan for short sessions, roughly 5–15 minutes, without making a daily streak obligatory. Missed sessions must not create an intimidating backlog or suggest professional failure.

### Retention and scheduling

Research scheduling for explanations and scenarios, not only binary flashcard ratings. Keep these distinct:

- time since last use or attempt;
- observed performance;
- whether hints or explanations were used;
- the model's confidence in its assessment;
- the user's own confidence;
- the importance and current relevance of a concept.

Represent a need for refresh without pretending that elapsed time precisely measures a person's lost competence. An untested topic is unknown, not failed.

## 8. Workflow: experience journal and career readiness

### Capture meaningful experience

A journal entry may originate from reflection or be created directly. Useful fields conceptually include:

- context/problem and constraints;
- the user's role and contribution;
- investigation, decisions, alternatives, and actions;
- outcome, with measured versus unmeasured impact distinguished;
- technologies and concepts;
- what was learned and what remains uncertain;
- supporting references, when allowed;
- whether the event concerns employment, coursework, a personal project, or another setting.

The final schema is for Codex to propose. Keep initial capture lightweight; not every field must be compulsory.

A difficult investigation ending in a one-line change can be more meaningful than a large routine diff. Non-code work must be supported. Never equate lines changed, commits, or time tracked with professional value.

### Preserve factual honesty

AI may propose wording and tags, but the user approves substantive claims. It must not invent measurements, business impact, responsibilities, or employment history. “Outcome not yet known” is a legitimate state that can be updated later.

AI-assisted or team-produced work is not automatically the user's individual accomplishment. Help describe their actual contribution without making AI use a moral judgment.

### Reuse approved entries

The graduation version should allow users to find and edit entries and turn one into a concise explanation of their work. Possible outputs include a reviewer note, interview story, STAR-style draft, CV bullet candidate, or performance-review note. Start with a limited subset instead of several full editors.

The broader vision includes vacancy analysis, a focused “interview in 14 days” plan, and practice questions about personal experience. These are later capabilities unless they fit without weakening the central loop.

Keep presentation coaching separate from factual approval. A more polished story does not become better evidence simply because it sounds confident.

## 9. Knowledge and experience profile

The profile must avoid collapsing all signals into a single percentage per technology.

| Signal | Example | Interpretation |
| --- | --- | --- |
| Self-declared familiarity | User says they know RabbitMQ | User claim, not independent assessment |
| Exposure | RabbitMQ appears in an approved work entry | Relevant experience, with stated provenance |
| Recency | A messaging task occurred recently | Recent contact, not necessarily mastery |
| Conceptual explanation | User explained acknowledgements in a session | Evidence about a particular mechanism |
| Scenario performance | User reasoned through a failure boundary | Evidence under those conditions |
| Assisted correction | User solved the case after a hint | Learning progress, distinct from unaided success |
| Experience evidence | User described their role in improving retries | Contribution claim grounded in the entry |

Show concrete observations and gaps rather than an opaque “developer level.” Users must be able to inspect the origin of claims and correct inaccurate classifications.

The profile should help answer: What have I used? What can I explain? What needs refreshing? What can I credibly discuss? What should I learn next?

## 10. Learning, assessment, and AI quality requirements

AI is a means of contextualizing instruction and feedback. It is not an authoritative examiner.

Required distinctions:

- User-provided facts versus model inferences.
- Curated technical guidance versus generated explanations.
- First attempts versus hints, revisions, and retries.
- Technical correctness versus clarity of communication.
- Misconceptions versus valid alternative solutions or missing assumptions.
- A system error versus an unsuccessful answer.

Technical feedback should evaluate reasoning under the stated conditions. Do not punish imperfect English as a technical failure. Research language support and whether explanation quality and technical assessment need separate handling.

Provide an “I disagree / context is missing” route. Preserve corrected assessments and their provenance; do not silently rewrite historical evidence after a prompt or model change.

Research structured output validation, bounded conversations, reference grounding, prompt/model versioning, evaluation examples, and when human-reviewed material is needed. Start with a curated conceptual core rather than trusting arbitrary generated content across every technology.

Code, comments, commit messages, and imported documents are untrusted task data. Embedded instructions must not be allowed to change system behavior, grading rules, access rights, or what is shared. The product should not need to execute submitted code to deliver its core learning flow. If execution is proposed, justify the additional isolation and operational requirements separately.

Users may use another AI tool to answer. Sensei++ cannot reliably prove independent thinking. Encourage honest practice, label assisted attempts where disclosed, and avoid high-stakes claims that would require proctoring.

## 11. Privacy, ownership, and team boundaries

The personal product must remain useful independently of an employer.

### Individual experience

- Learning attempts, mistakes, confidence, and reflections are private by default.
- Users decide which generated artifacts to save, export, or share.
- Explain what source material is retained and what is sent to a model provider.
- Support sanitized descriptions and selected excerpts; a repository connection is optional.
- Do not log raw source code, secrets, or sensitive answers as ordinary application telemetry.
- Provide understandable deletion and export behavior; identify how derived summaries and links behave when a source is removed.

### Employer-funded use and future teams

An employer paying for an account must not implicitly gain access to private failed answers. Sharing a reviewer note must not share the whole learning session.

Compatible possibilities include funded seats, curated team learning material, voluntary shared goals, and deliberately shared summaries. Aggregated skill trends are a future research question: small teams and rare topics can make people identifiable even without names.

Do not build manager rankings, individual failure dashboards, covert monitoring, or automated promotion recommendations.

Personal ownership of learning history does not grant permission to export company intellectual property. Distinguish portable personal learning from restricted source context. Codex should propose practical boundaries without claiming legal compliance from architecture alone.

## 12. Integration and pre-commit direction

The CTO's pre-commit question is meaningful context, but the exact integration is not decided.

Research these alternatives against the same product workflow:

| Entry mechanism | Questions to resolve |
| --- | --- |
| Web form with text/snippet/diff | Simplest complete path; how much context is enough? |
| Local CLI/helper | Can it select and sanitize context, open/resume a session, and minimize permissions? |
| Optional Git hook | Which diff is checked, how are latency/offline use handled, and what does bypass mean? |
| IDE action | Does proximity justify extension maintenance for the graduation scope? |
| Pull-request integration | Is post-commit timing acceptable, and what employer permissions are required? |

Preserve a distinction between **before commit**, **before push**, **before opening a PR**, and **before merge**. They are different events with different available context. Do not describe a PR bot as a pre-commit solution.

The initial experience should be developer-triggered and usable without installing a blocking hook. Research an optional integration as a separate milestone if it materially strengthens the demonstration.

If gating is considered, answer: what is being gated, who configures it, what evidence is required, how changes invalidate evidence, what happens during provider failure, and how a user proceeds with unresolved issues. A local hook can be bypassed; it is not tamper-proof proof of comprehension. Never silently commit, push, publish, or alter code as a side effect of acknowledging understanding.

## 13. Proposed graduation scope

This table is the starting scope to refine during architecture research. Preserve the connected vision while cutting breadth.

| Area | Proposed graduation baseline | Later or optional |
| --- | --- | --- |
| Accounts and personal data | A coherent private-user experience with appropriate access boundaries | Organization administration and complex roles |
| Work input | Manual episode plus selected snippet/diff | Continuous repository ingestion and many integrations |
| Reflection | Context correction, focused questions, feedback, pause/resume, explicit completion | Autonomous broad repository analysis |
| Acknowledgement | User action attached to a specific context version, with unresolved issues visible | Policy-driven organization gates |
| Learning | Curated initial domain, explanation/scenario exercises, feedback, later practice | Comprehensive curricula and all languages |
| Experience journal | Manual and reflection-derived drafts; approve, edit, search, and reuse | Automatic workstream mining and annual Wrapped |
| Profile | Evidence-linked exposure, attempts, gaps, and recency | Universal skill graphs and cross-user benchmarking |
| Prove | Rehearse or export a useful approved experience account | Full mock interview suite, vacancy analysis, CV management |
| Integrations | Core flow independent of integrations | One CLI/hook/IDE/PR integration after feasibility review |
| Evaluation | Demonstrable workflows, technical quality checks, small peer feedback | Large studies and commercial retention proof |

The graduation project should demonstrate a real vertical flow, not six disconnected placeholder pages. If time becomes tight, reduce domains, output formats, and integration breadth before removing the link between work, learning, and experience.

## 14. Product surfaces to account for

These are responsibilities to support, not a fixed navigation design:

- Onboarding that identifies goals and starting familiarity without a long exam.
- A home view with a small number of useful next actions.
- An episode/context editor with source-preview and correction.
- A reflection workspace with questions, answers, help, and clear progress.
- A completion view with checked topics, unresolved issues, and acknowledgement.
- A learning session that works without a work episode.
- A manageable follow-up queue.
- An experience journal with editing and provenance.
- A profile with inspectable evidence rather than opaque scores.
- An experience-rehearsal or output preview.
- Settings for privacy, retained data, integrations, and exports.

Notifications should be optional and proportionate. Product language should resemble a helpful mentor, not an auditor accusing the user of incompetence.

## 15. Conceptual information and lifecycle requirements

Do not translate this section directly into one database table per noun. Use it to reason about boundaries and relationships.

Relevant concepts include user, goal, topic/concept, work episode, source snapshot, reflection session, question, attempt, hint, feedback, dispute/correction, acknowledgement, practice recommendation, scheduled review, experience entry, and generated presentation artifact.

Key relationships:

- A work episode can have several snapshots and sessions.
- A learning session can exist without a work episode.
- One episode can touch several concepts; the same concept can recur across episodes.
- A single answer may supply evidence about a narrow aspect of a concept, not the entire technology.
- A recommendation may refer to evidence from work or standalone learning.
- A generated experience entry begins as a draft until approved by the user.
- An export reflects a particular approved version rather than a silently changing draft.
- Acknowledgement refers to the context and session that actually occurred.

Codex should propose lifecycle models for at least sessions, AI jobs, feedback corrections, acknowledgement currency, experience drafts, and scheduled practice.

Required behavior includes resuming interrupted work, avoiding duplicate artifacts after retries, handling outdated context, and not losing a user's answer when generation fails. Separate durable user input from replaceable generated suggestions.

## 16. Representative end-to-end scenarios

### A. AI-assisted background-processing change

A developer supplies a selected diff and explains its purpose. The system asks about a relevant dependency lifetime and a failure boundary. The developer realizes one assumption is unverified, asks for clarification, and records a question for the lead. They save an experience draft and later practice a related scenario.

Success: uncertainty is useful output; the app does not claim the change is certified or safe merely because the session ended.

### B. Difficult investigation, one-line fix

A developer describes hours spent narrowing down an intermittent bug. The source diff is tiny. The application captures the investigation and reasoning, asks what evidence distinguished competing causes, and drafts a contribution summary without inventing numerical impact.

Success: the episode's value is represented without using diff size as a proxy.

### C. Student learning a messaging mechanism

A student has no connected work. They choose a curated scenario, receive an explanation, answer a question, inspect feedback, and encounter a changed scenario later. A personal-project entry can be added but is never labeled employment experience.

Success: the learning path is substantive and independent.

### D. Preparing to explain past experience

A user selects an approved reliability-related entry. Sensei++ asks why the approach was chosen and helps shape a concise account of context, contribution, and outcome. Missing impact evidence stays missing rather than becoming a fabricated achievement.

Success: the output is grounded in the user's history and remains editable before export.

### E. Context changes after acknowledgement

A developer acknowledges one diff, then modifies relevant code. A new input is recognized as a different context version. Previous evidence remains visible but is not presented as approval of the new change.

Success: version-specific evidence stays honest.

### F. Provider outage or incorrect feedback

A user submits an answer, but the provider times out. The answer is retained and feedback can be retried. In another session, the user disputes feedback because the app missed a constraint. The correction is recorded without erasing the original attempt or treating disagreement as failure.

Success: the workflow remains trustworthy under ordinary faults.

## 17. Acceptance criteria for the core demonstration

The architecture and implementation plan must make the following demonstrable:

1. A user can complete a meaningful learning session without supplying workplace material.
2. A user can submit a work episode and inspect/correct the application's interpretation.
3. Reflection questions are grounded in the supplied context or explicitly labeled as general follow-ups.
4. Answers, hints, feedback, and revisions remain distinguishable.
5. A user can leave uncertainty unresolved without receiving a false full-understanding claim.
6. An acknowledgement requires an explicit action and identifies the relevant context version.
7. One work episode can produce an approved experience entry and a later learning activity without re-entering the whole story.
8. A later scenario can test related reasoning rather than merely repeat the same answer verbatim.
9. Exposure and observed understanding appear as separate signals with inspectable provenance.
10. The application does not fabricate impact or treat AI-generated text as a confirmed user claim.
11. Private attempts are not exposed through exported notes or another user's account.
12. An interrupted session or failed AI call can be recovered without losing input or duplicating achievements.
13. A user can reuse an approved entry to prepare a clear explanation of their work.

These establish implemented behavior. They do not establish a causal long-term learning effect.

## 18. Practical evaluation for a graduation project

Propose an evaluation the creator can actually run:

- Functional walkthroughs against the scenarios and acceptance criteria.
- A small collection of reviewed examples covering correct reasoning, misconceptions, valid alternatives, insufficient context, and hostile instructions inside source material.
- Technical review of sample feedback by a lead or experienced peer where available.
- A few peers trying the workflow and reporting relevance, clarity, burden, and useful outcomes.
- An optional later unaided explanation or changed scenario to illustrate retention, reported descriptively with the sample and limitations.
- Operational checks for data isolation, job recovery, generation cost, and responsiveness.

Do not mandate statistically powered experiments, a paid pilot, or a research lab. Do not invent university requirements. Ask about the actual assessment rubric when it would materially change the deliverables.

Useful measurements include time to useful output, irrelevant-question frequency, substantive feedback errors, session abandonment reasons, saved entries reused, and whether follow-up scenarios expose a meaningful gap. Question count, XP, and generated output volume are not sufficient measures of value.

## 19. Architecture research questions

Research current official documentation for technical claims, supported versions, licensing, provider data handling, and prices. Date the findings and separate verified facts from estimates. This brief intentionally does not choose the stack in advance.

### A. Overall application shape

- What is the simplest deployable architecture that supports the full loop and clear internal boundaries?
- Is a modular monolith appropriate? What would justify a separate process or service now?
- Where should identity, learning content, sessions, evidence, journals, and AI orchestration be separated logically?
- How can the creator understand and defend the design without infrastructure overwhelming the thesis?

### B. Backend and frontend

- Evaluate a C#/.NET backend first, accounting for existing expertise.
- Recommend a frontend approach based on the editor, conversational sessions, structured profile, and interactive learning needs.
- Compare only credible alternatives and give a recommendation rather than an unranked framework catalog.
- Determine whether responsive web is sufficient for the graduation version. Do not assume native mobile clients are required.

### C. Persistence and provenance

- Which data requires relational integrity, flexible structured storage, full-text search, file storage, or historical versioning?
- How will snapshots, attempts, feedback revisions, and approved claims retain their relationships?
- How will deletion, export, retention, and removal of sensitive source material affect derived artifacts?
- Is semantic/vector retrieval actually needed in the first scope? What simpler alternative works until it is?

### D. AI pipeline and pedagogical behavior

- Which steps use deterministic logic, curated content, or model generation?
- Should context extraction, question generation, assessment, and summarization be separate operations?
- How are outputs validated before becoming persisted facts or user-visible judgments?
- What provider/model strategy balances cost, quality, latency, language support, and data sensitivity?
- How are context limits, reference retrieval, prompt injection, retries, prompt versions, and evaluation examples handled?
- What practical safeguards prevent the system from grading its own unsupported explanation as truth?

### E. Long-running work and reliability

- Which operations are synchronous, streamed, or background jobs?
- How do sessions behave during disconnects, cancellations, timeouts, partial completion, or provider rate limits?
- Where are deduplication and idempotency necessary?
- Can the product limit per-user cost and concurrency without losing submitted work?

### F. Knowledge model and scheduling

- How should concepts and relationships be represented without building a universal ontology?
- What scheduling approach works for open-ended responses and scenario variants?
- What evidence updates the profile, and what remains unknown?
- How are assisted responses, confidence, disputed grades, stale evidence, and prerequisite gaps treated?

### G. Source intake and optional integration

- How much context is necessary for useful questions, and how can users select it safely?
- Can a first integration operate on local selected diffs without broad repository access?
- How are staged/unstaged changes and source versions represented if Git is involved?
- Which integration gives the strongest graduation demonstration for the least maintenance burden?
- What is the explicit behavior when a hook/helper cannot reach the server or its acknowledgement is stale?

### H. Security and privacy

- How are users authenticated and their private records isolated?
- What data reaches providers, application logs, analytics, backups, and exports?
- How are credentials for optional integrations scoped, stored, revoked, and excluded from model context?
- What are realistic protections for untrusted imported text and accidental sensitive-data submission?
- Which privacy promises can actually be supported, and which must not be made?

### I. Deployment, cost, and operations

- Propose modest development and hosted-demo setups, including necessary managed services or their absence.
- Estimate low-use and moderate-use monthly costs using explicit assumptions about active users, sessions, calls, token volume, storage, and hosting.
- Include cost sensitivity to context size and follow-up count. Do not present fabricated precision or stale prices as verified.
- Define minimal monitoring, backups, recovery, configuration, and provider-failure handling.

### J. Testing and implementation sequence

- Which tests protect real risks: access isolation, version-bound acknowledgement, factual approval, job retries, and assessment quality?
- How can most development run with deterministic AI fixtures while selected evaluations use real models?
- What is the smallest vertical slice that proves the product loop before integration breadth?
- Which technical uncertainties need a short spike, what question will each answer, and what decision follows from the result?

## 20. Required outputs from Codex's first research phase

Produce a connected design package, preferably Markdown with concise diagrams where useful. Match the existing repository's documentation conventions if any; otherwise propose a small `docs/` structure.

1. **Architecture recommendation:** components, boundaries, main data flows, deployment shape, and rationale tied to requirements.
2. **Decision comparison:** a short list of material alternatives, trade-offs, recommended choices, and reasons to defer complexity. Include versions and sources where relevant.
3. **Domain and lifecycle design:** conceptual entities, relationships, state transitions, provenance, and invariants. Identify what is durable fact versus derived suggestion.
4. **AI and learning design:** generation/assessment pipeline, curated content strategy, reference grounding, scheduling approach, evaluation and correction behavior.
5. **User-flow and interface outline:** how the learning-only path and integrated work path are both complete; key boundaries between client and server.
6. **Integration recommendation:** web-only baseline plus a reasoned decision on whether and when to add a local helper, hook, IDE action, or PR integration.
7. **Security/privacy design:** realistic trust boundaries, external data flows, user isolation, sharing, deletion/export, and limitations.
8. **Cost and operating model:** transparent assumptions, verified price sources, expected variability, and development/demo operating requirements.
9. **Implementation roadmap:** milestones, vertical slices, dependencies, a definition of done for each, and optional scope cuts. Avoid unsupported calendar commitments.
10. **Open questions and feasibility spikes:** only decisions whose answers could materially change the architecture or schedule. Give recommended temporary assumptions for the rest.

Map major product requirements and the scenarios in this brief to the proposed design and milestones so omissions are visible. Do not manufacture traceability paperwork that adds no implementation value.

Your first response should show that you understand the product, identify a small number of consequential unknowns, and proceed with research under explicit reasonable assumptions. Do not ask whether the creator should abandon the idea because commercial demand has not been proven.

## 21. Reference and final framing

The earlier [Sensei++ product-strategy document](https://docs.google.com/document/d/1XNrYg3s-CQd2otI8N9pVUURQpfGHjhNyk1EMR6MqwVE) contains supporting market discussion. Access to it is not required to understand this brief. Its startup-validation recommendations are superseded for project planning by the graduation-project clarification captured here.

The core ambition is a healthy environment for growth and productive engineering: developers understand more of what they build, preserve lessons and achievements, and become better able to explain their decisions. The application should make that process easier and more concrete, while remaining honest about what it has and has not assessed.

**Design for a complete, credible graduation project first, with room to grow. Preserve the learning purpose when deciding what to automate.**
