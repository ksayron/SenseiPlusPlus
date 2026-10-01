# Learning experience: screens, interactions and recovery

**Decision date: 24 September 2026. Status: agreed Phase 1 experience, planned rather than implemented.**

This is the interaction companion to the [learning architecture](09-learning-system.md). [Delivery and verification](11-learning-delivery-plan.md) defines acceptance checks. The audience is experienced developers. C# is fixture content only; no screen, navigation item, or interaction may depend on a particular technology name.

## 1. Information architecture

Keep the existing application's non-learning destinations intact. The current workbench uses Today, Learn, Reflect, Evidence, and Experience; this design changes the learning surface and its entry points without requiring a product-wide rename.

Learn becomes a learner home with Daily practice, Explore topics, Continue, active goals, active roadmaps, and recent activity. Concept CRUD remains a secondary library-management concern rather than the main way to use the product. A learner should be able to train without manually creating evidence records.

Use directly addressable learning routes. These are proposed browser paths, not currently implemented routes:

| Route | Screen | Navigation behavior |
| --- | --- | --- |
| `/learn` | Learning home | Daily practice, continue, goals, roadmaps and recent activity |
| `/learn/topics` | Topic browser | Search, parent/child browsing and availability; query state retained on return |
| `/learn/topics/:conceptId?section=overview` | Topic workspace | Overview is default; material, practice and activity are separately addressable sections |
| `/learn/session-setup` | Session setup | Scope and originating topic/roadmap context carried in URL state |
| `/learn/sessions/:sessionId` | Active/paused session | Server state selects the current item; local rendering cannot invent progress |
| `/learn/sessions/:sessionId/summary` | Full or partial summary | Links to owned attempt feedback and originating topic/roadmap |
| `/learn/roadmaps/:versionId` | Roadmap workspace | Stage navigation plus separate traversal/evidence coverage |

Browser Back and explicit navigation use the same draft-save rules. Preserve the originating topic section, roadmap stage, or browse query as an allowlisted return context; do not accept arbitrary redirect URLs.

### Learning home

**Daily practice** starts setup for a mixed session. “Daily” does not mean one session per calendar day and does not imply a streak, overdue debt, or failure for absence. The learner selects a count and sees estimated duration.

**Explore topics** is always available, including before goal creation. With no history, say “No learning activity yet” and offer content browsing. Do not label the user a beginner or show 0% knowledge.

**Continue** shows the ordinary active/paused session with saved position. If none exists, it can return the learner to an active roadmap's last visited stage. An active session takes precedence over suggesting another daily session.

**Goals** allow choosing concept(s) or a roadmap and a brief intention. Archive removes a goal from future recommendations without deleting history. There is no priority-tuning dashboard in Phase 1; richer priorities belong to Phase 2.

The home may show recent practice facts and roadmap links. It does not expose a detailed “Why recommended?” explanation system before Phase 2, or teaser controls for unimplemented offline/exam features.

## 2. Topic browser and workspace

### Browser

Provide search by topic name/key and hierarchical browsing. A flat fallback is acceptable for concepts with no containment relationship. Display name, concise scope, content locale, and whether material/practice is available. A concept can appear in multiple contexts but always opens the same canonical identity.

Prerequisites and related concepts are navigation links, not locked gates. Long technology hierarchies should remain usable as lists/breadcrumbs; a graph canvas is not required.

### Overview

Show scope, child concepts, prerequisite suggestions, related topics, and applicable roadmap stages. Provide Read material and Practice actions. A small evidence summary describes observed activity without presenting a technology mastery number.

Examples of suitable wording are “No objective attempts yet,” “3 exercise families practiced,” and “2 assisted answers.” Do not turn reading time, number of opened pages, or a self-declaration into assessed knowledge.

### Material

Render an ordered reading view with headings, paragraphs, lists, code examples, diagrams/images with alternative text, and external reference links. Code is readable and copyable but not executable. Distinguish the material's language from the user's UI language; show an explicit fallback notice when English content is used.

Material can be opened before practice or during an exercise. Normal topic reading can record an unscored material-view fact; fetching content is a read and must not itself award or persist an assessment. During an active item, the learner uses its Reference action; save the assistance fact before opening that item-scoped reference view. Assistance records describe observed use of product controls, not proof that no external help was used.

If a material asset fails, keep available text readable and identify the unavailable asset. Missing material does not disable unrelated exercises. Unsafe/unsupported content is not rendered as executable HTML.

### Practice

Show session count choices 3, 5 and 10, defaulting to 5. Exercise-type filters are optional and default to all supported types. Count and duration reflect the selected scope and filters.

The topic scope contains the selected concept and its containment descendants. The interface states that scope; it does not silently add related technologies to fill a session.

Before starting, show the actual eligible count. Example: “4 exercises available for these filters. Start 4 exercises.” The final response may report a smaller count if availability changed after preview; show that change before the learner answers. Zero eligible exercises produces a helpful empty state with clear options to clear filters or return to browsing.

### Activity

Display chronological saved activity with filters for objective attempts, self-review, and assistance. Each entry links to its original item/version and feedback. Show retry lineage rather than overwriting the first answer.

Keep active, disputed, superseded and withdrawn evidence distinguishable. A withdrawn observation stays inspectable where retained but is labeled excluded from estimates/coverage. A note is displayed as the learner's writing, not an assessed explanation.

## 3. Session setup and lifecycle

### Setup

The learner chooses mixed daily practice, the topic they came from, or a roadmap stage. Preserve that context on summary and exit. Selection uses available content and the server policy; the UI contains no subject-specific recommendation rules.

Estimated time is informational. There is no countdown or speed bonus. A ten-item session may take longer than ten minutes; the interface must not imply otherwise.

If an ordinary session is already Active or Paused, present:

- **Resume existing** with its topic/count and current progress.
- **End existing and start new**, explaining that submitted answers remain saved and unfinished items stay unassessed.

Ending the old session and failing to create a new one does not lose completed work. Present the old partial summary and retry setup. A duplicate start request reuses its operation ID and does not create a second session.

### Lifecycle display

| State | Learner sees | Available action |
| --- | --- | --- |
| Active | Current prompt or current item's feedback; item position and save state | Answer, hint/reference, skip, pause or end |
| Paused | Saved topic/count/position and timestamp | Resume or end early |
| Completed | Final feedback until Continue, then summary | Inspect feedback, return to topic/roadmap, start another session |
| EndedEarly | Partial summary with unfinished items identified | Inspect completed work or start a new session |

Terminal item outcomes are Answered, SelfReviewed and Skipped. “Completed” describes session traversal, not correctness. A session can complete with assisted or skipped work.

No automatic timeout abandons a paused session. Auto-advance must not hide feedback. Submission preserves the current presentation position; Continue explicitly advances it, so refresh before Continue restores the result being reviewed. After the last submission, the server can mark the session complete while the client still displays that result until Continue opens the summary.

## 4. Common exercise shell

Every format uses the same shell: topic link, item position, prompt and supporting content, format-specific answer control, optional written note, hint/reference actions, Submit, Skip, pause/exit, and save state.

Keep the answer visually prominent. On narrow screens stack content and controls; long code blocks may scroll horizontally without forcing the entire page sideways. Reference content opens in an accessible panel or separate section with a clear return action.

Before submission, the item contains no answer key or explanation that reveals its key. Optional notes are labeled “Your reasoning — saved, not automatically assessed.”

### Flashcard

1. Read the prompt and recall privately; optional notes can be saved.
2. Select Reveal answer. Persist the reveal fact before displaying the reference answer.
3. Compare and choose Again, Partly, or Recalled.
4. Show self-review confirmation, then Continue.

Do not display a “correct” badge for a self-rating. If another positive review is blocked by the topic cooldown, use plain wording such as “Review saved. Another positive self-review contribution is available after [time].” The learning activity remains useful and visible even when it earns no contribution. Do not display XP or a gain meter.

### Prediction

Display the scenario/code and authored outcome choices. The learner selects one outcome and can add a note. The server compares the outcome ID, not the note's keywords. No code execution is required or implied.

### MultipleChoice

Clearly label single-choice or “Select all that apply.” Use radio controls for single choice and checkboxes for multiple choice. Require a valid selection before submission; a valid but incomplete accepted set is an incorrect answer, while malformed IDs are a validation problem.

### Ordering

Show a stable labeled list with Move up/Move down controls, usable by keyboard and touch. Optional drag interactions may complement these controls. Preserve focus on the moved item and announce its new position. Submit the complete ID order; feedback explains the accepted ordering without suggesting that an alternative authored valid order is wrong.

### Matching

Show each left item with a labeled counterpart selector. A one-to-one matching exercise prevents conflicting selections or clearly explains how to replace them. All mappings must be supplied before submission. Keyboard operation is mandatory; a line-drawing canvas is unnecessary.

### BugSpotting

Display declared selectable regions/findings with clear labels. “No issue” is mutually exclusive with selecting findings. It must be a genuinely possible correct authored answer in the fixture set, not a trap that is always wrong. Feedback separates the result from the explanation; it does not reward marking every region.

## 5. Feedback, assistance and summaries

### Immediate feedback

After an objective answer, show Correct or Incorrect, authored explanation, relevant answer/rationale, assistance used, and saved status. If feedback explains partially correct components, the result remains binary in Phase 1.

Keep the original answer visible. Do not clear optional notes when feedback arrives. The learner controls Continue; retry is an optional secondary action explicitly labeled practice after seeing the answer.

A retry creates a linked attempt with answer-aware assistance. Show its outcome in history but do not replace the initial assessment or increase distinct evidence coverage. Skipping is unscored and needs no justification.

### Assistance

Hints and reference use are separate recorded facts. Material consulted during the active item lowers the assessment weight as specified in the architecture but does not change a correct answer into an incorrect one.

If a hint/reveal request fails, preserve the pending action and retry the same operation ID. Do not reveal gated content while claiming the assistance was saved if the server did not acknowledge it. The learner can continue editing their answer during a recoverable error.

### Summary

Separate objective results, self-review, assisted answers, skipped items, and unvisited items. List concepts touched and provide feedback/material links. Counts of completed items and correct objective items use different labels and denominators.

Do not calculate a session mastery percentage from mixed objective/self-review/skipped work. A factual “3 of 4 objective answers correct” is permitted, with assistance visible. An early-ended session is a useful partial record, not a failed exam.

### Future exam review

Phase 1 has no exam toggle. Phase 3 can reuse the shell with EndOfSession feedback: submitted answers stay immutable; interim correctness, explanations, keys and scores stay hidden; finalization opens a review. Exam restrictions such as timing, hints and resume policy are separate decisions for that phase.

## 6. Roadmap workspace

Show the roadmap purpose, ordered stages, referenced concepts and a recommended next stage. Learners can enter any stage. Prerequisite warnings offer navigation to preparation rather than a disabled Start button.

For each stage, show traversal separately from evidence:

| Traversal | Meaning |
| --- | --- |
| Not visited | No recorded navigation through this enrollment |
| Visited | Opened the stage; no claim of understanding |
| Practiced | Started/completed relevant practice; detailed outcomes remain available |
| Skipped by you | Explicit navigation choice, never assessed knowledge |

| Evidence status | Meaning |
| --- | --- |
| No objective evidence | Unknown assessed state, not zero knowledge |
| More evidence needed | Show family/session/correctness requirements and current counts |
| Evidence requirement met | Link the qualifying observations; avoid the unqualified label Mastered |
| Insufficient exercise coverage | The available library cannot supply the required distinct families |

Completing relevant free practice can update evidence coverage even if the learner never visited that stage. Starting another roadmap shows the same evidence. Pausing enrollment stops its active guidance without removing shared knowledge or submitted work.

## 7. Saving and failures

### Draft state

Use a 750 ms idle debounce as the initial implementation default. Explicit pause/navigation flushes pending edits before leaving. Commands within one session are serialized so an assistance request, autosave and answer submission use the current session token in order.

| State | User-facing meaning | Required behavior |
| --- | --- | --- |
| Unsaved changes | Current text/selection differs from last acknowledged draft | Keep the input and schedule save |
| Saving | Request is in progress | Do not claim the server has accepted it yet |
| Saved | Latest draft generation acknowledged | Refresh can recover that generation |
| Save failed | Current page still has edits, server acceptance unavailable | Retry/copy; explain the last confirmed save |
| Conflict | Another tab changed the same session version | Preserve local input and offer inspect/reload/recovery |

Do not let a delayed acknowledgement of an older draft mark newer text as saved. A late draft response cannot regress a submitted item to editable state.

### Navigation and refresh

For deliberate navigation, attempt save first. If it fails, let the learner stay and retry or explicitly leave unsaved changes. Abrupt browser closure cannot be guaranteed to complete a network save; do not claim otherwise. Refresh recovers only acknowledged server drafts and committed attempts.

Phase 1 retains failed edits in the open page, not a durable offline database. It must not display “Saved on device” or “Waiting to sync” for data that has no such storage guarantee. The eventual Phase 4 UI introduces those states with actual local persistence.

### Submission failure and replay

On network failure, leave the item and input visible. Retry with the same operation ID so a lost successful response cannot create another answer or cooldown admission. Do not advance until the committed outcome is known.

On validation failure, explain the specific answer-control problem and restore focus there. On stale version, retain input and inspect current server state rather than blindly retrying with a new ETag. On an unavailable historical item, show a recoverable history state without substituting a different exercise under the same answer.

Account switching clears the previous owner's in-memory learning state before displaying another owner's data. Handle unsaved input explicitly. An item URL is never sufficient authorization.

## 8. Accessibility and acceptance journeys

Use semantic controls, visible keyboard focus, labels for every answer control, text alongside color, and live status announcements for saves and reordering. Restore focus when closing material/hint panels. Feedback should become discoverable by focus/announcement without moving the user unexpectedly to the next question.

Required walkthroughs include:

1. New learner explores an unfamiliar topic, opens optional material and completes a three-item session with no AI configured.
2. Experienced learner enters a roadmap's later stage, practices, and sees evidence without mandatory prerequisite completion.
3. Learner asks for ten exercises when four are available and starts an explicitly four-item session.
4. Learner uses a hint, submits a correct answer, inspects assisted feedback and retries without replacing the original result.
5. Learner reveals/rates flashcards repeatedly and sees history while the topic-level positive contribution stays limited.
6. Learner pauses with a saved draft, refreshes, resumes, then ends early with a truthful partial summary.
7. Submission response is lost; replay recovers exactly one accepted result.
8. Two tabs conflict; the losing tab retains its unsaved answer and never overwrites a submitted result.
9. Free practice changes roadmap evidence coverage without changing traversal into a false visited/completed claim.
10. Ordering and matching work on a narrow touchscreen and entirely with a keyboard.

These are future application acceptance journeys. This documentation change does not claim they have been implemented or passed.
