# Sensei++ — Codex UI Implementation Handoff

**Date:** 30 September 2026\
**Selected foundation:** React + shadcn/ui using Base UI primitives.\
**Visual authority:** [SPA Design Guidelines](Sensei-SPA-Design-Guidelines.md), version 1.1, and [Visual Identity](Sensei-Visual-Identity-and-UI-Guidelines.md), version 1.2.

**Current baseline:** Coastal and Coastal Night with cobalt actions. The standalone demo validates the first reflection/gallery slice. The owner-authorized SPA adoption is recorded in the [implementation report](Sensei-SPA-Design-Implementation.md). This handoff remains guidance for subsequent implementation; it does not by itself authorize changes during a documentation-only task.

## 1. Assignment

Implement the agreed Sensei++ visual identity as a reusable, documented UI kit in the actual project environment, then apply it to the application's available web and responsive mobile surfaces.

The owner has selected shadcn/ui + Base UI. Proceed with that foundation. Read the visual guidelines and the supplied product brief before editing. The brief provides behavior and domain context; this assignment concerns the UI system and its application, not a new backend architecture research phase.

Inspect the repository, `AGENTS.md`, package manifests, existing components, routing, styling, assets, and tests. Preserve working product behavior and follow repository conventions. Use the existing React framework, router, data layer, and build setup where compatible. If the frontend is not React or another material incompatibility exists, identify the issue before undertaking a framework migration.

Work toward functioning components and screens. A prose proposal or another collection of image mockups is not sufficient for this implementation task.

## 2. Product and design intent

Sensei++ is a professional growth platform for developers who have already moved beyond introductory programming. It connects learning, reflection on engineering work, retained knowledge, experience records, and the ability to explain professional contributions.

Activities can include reviewing code, inspecting logs, decomposing systems, investigating performance, comparing approaches, revising theory, and practicing explanations. These activities share one visual identity. Do not turn a particular exercise format into the only way the product works.

The accepted identity is a restrained **Living Campus**: a welcoming academy with a coherent 2D illustration language and selective character guidance. It should feel capable, curious, comfortable, and personal.

Implement these visual qualities:

- Pale blue backgrounds, navy navigation and text, cobalt primary actions, neutral reading surfaces, and occasional warm cream guidance areas; use Coastal Night for the dark counterpart.
- Clean, readable modern sans typography; compatible monospace for technical material.
- Gently rounded geometry, restrained borders, purposeful surface grouping, and subtle elevation where layering needs it.
- Bright and welcoming casual presentation; quieter presentation for sustained technical work.
- Purposeful, limited 2D illustrations and understandable functional icons.

The identity must survive without scenic artwork. Long explanations, code, evidence, and journals need neutral, readable working surfaces. The concepts' saturated backgrounds and oversized headings can be refined for actual usability.

Avoid a generic untouched shadcn theme, formal serif casebook styling, heavy cartoon borders, clay-like 3D imagery, unexplained abstract corporate symbols, or landscape thumbnails on every card.

Character appearance, names, personality details, and asset specifications remain undecided. Support optional character/illustration assets without inventing those details. Existing approved assets may be accommodated; functional guidance must remain useful without artwork.

## 3. Set up the selected foundation

1. Inspect currently installed versions and existing component usage.
2. Consult current official shadcn and Base UI instructions for the project's framework.
3. Initialize or configure shadcn with the **Base UI** implementation. Do not assume an old CLI flag or registry path is still valid.
4. Verify that the relevant generated components use `@base-ui/react` primitives. Plain components such as a textarea may use native elements; that is expected.
5. Add only the components needed for the initial implementation. Use actual library components for complex interactions rather than imitating them with custom click handlers.
6. Commit a dependency lockfile using the project's package manager and document the selected versions.

Use TypeScript where supported by the repository. Tailwind is the default styling choice for shadcn components, with semantic CSS custom properties defining the Sensei design values. Preserve a compatible existing styling arrangement where it already meets the requirements.

Use one primary primitive system consistently. If the repository already uses another implementation, identify the affected components and plan a focused migration rather than maintaining duplicate dialog, select, and form systems across new screens.

## 4. Establish foundations and tokens

Use the SPA guidelines' selected palette and typography, spacing, radii, motion, and responsive defaults. Validate them in real screens rather than choosing a fresh visual foundation for each page. Extract shared tokens from the demo when adopting them in the SPA and have both consume one source. The guide distinguishes selected decisions from implementation gaps.

Keep tokens semantic and reusable. Cover at least:

| Group | Examples of roles |
| --- | --- |
| Color | Canvas, reading surface, supporting surface, primary/secondary text, border, primary action, selection, focus, success, attention, error, neutral state. |
| Typography | Page title, section title, body, label, metadata, code. |
| Layout | Spacing scale, content widths, control sizes, responsive density. |
| Shape and depth | Control/card/overlay radii, divider treatment, elevation, layering. |
| Motion | Feedback and transition durations, reduced-motion behavior. |

Check contrast for actual text and state combinations. Coastal actions use opaque `#315fc4` / `#274da4` fills with white labels; Coastal Night uses `#97b8ff` / `#aac5ff` with `#162846` labels. Follow the guide's stronger field-boundary and opaque-focus rules. Do not sample generated images and assume their colors are production-ready.

Casual/focused presentation should adjust density, background intensity, decoration, and movement within the same system. It is independent of light/dark theme and device. Adopt the selected light/night pair consistently when theming the SPA; additional demo palettes are experiments and should not become production choices by default.

## 5. Build the reusable UI kit

Adapt locally owned shadcn component source to the tokens. Keep upstream origins and license notices where applicable. Style shared component definitions rather than patching the same button differently on every page.

Start with buttons, labels, text inputs, textarea, tabs, accordion/collapsible content, dialogs, popovers, select/combobox where needed, status badges, alerts, progress, loading/skeleton states, and toasts for suitable transient messages.

Define applicable default, hover, keyboard-focus, pressed/selected, disabled, loading, invalid, and recovery states. Preserve the primitives' focus, keyboard, labeling, and overlay behavior while customizing appearance.

Compose product-level components such as:

- `PracticeCard` and `TopicRow`.
- `ContextPreview` and `ReflectionPanel`.
- `EvidenceRow` and `ConceptEvidencePanel`.
- `ExperienceEntryCard` and `EntryEditor`.
- `GuidancePanel` with optional illustration/character content.
- `SessionProgress`, `SaveStatus`, and semantic status labels.

These names are suggestions, not mandatory class or file names. Match the repository's organization. Keep product behavior outside the generic UI primitives.

## 6. Validate one complete screen first

Implement the work-reflection screen before spreading changes across the application. It exercises dense content and the important interaction types.

Include:

- The episode title and context/source version.
- Source summary and selected technical excerpt.
- A current question, written response, and save state.
- Optional contextual help presented through an accessible overlay or disclosure.
- A visible unresolved-question state.
- A continuation action and save/resume behavior already supported by the product.
- A recoverable feedback-error state that preserves the user's input.

On desktop, context and response may appear side by side. On mobile, allow context to expand or open on demand while keeping the question and answer accessible. Test long answers, narrow screens, keyboard appearance, dialog scrolling, and return of focus after dismissal.

Use actual product data and APIs where available. Otherwise use clearly identified fixtures behind a replaceable boundary. Do not invent a working assessment service, persistent backend, or successful integration to make a demo appear complete.

Refine the shared tokens and components from this screen, then continue implementation without requiring approval for each routine styling choice.

## 7. Apply the kit across available sections

| Surface | Implementation priorities |
| --- | --- |
| Home/discovery | A few clear next actions, concise continuation module, labeled topics, limited purposeful welcome artwork. |
| Learning/practice | Consistent controls and progress; task-appropriate code, logs, diagrams, or interactive tools. |
| Work reflection/completion | Distinguishable context, answer, feedback, assistance, uncertainty, and explicit acknowledgement. |
| Knowledge profile | Separate exposure and observed understanding; inspectable source evidence; neutral untested states. |
| Experience journal | Draft/approved status, provenance, comfortable editing, and user approval of substantive claims. |
| Presentation/rehearsal | Reuse approved experience with editable previews and honest missing-evidence states. |
| Settings | Clear controls and explanations of privacy, retention, integrations, and exports. |

Preserve existing workflows and align with the brief. Implement unavailable surfaces only within the project's agreed scope; report missing dependencies rather than filling pages with unrelated features.

Navigation labels in the concept images were provisional. Use the actual product structure and keep active location clear. Mobile layouts should adapt content meaningfully, rather than shrink desktop tables and multi-panel editors.

## 8. Preserve product distinctions

The UI must make these states understandable:

- User input versus generated feedback or suggested wording.
- First attempt versus assisted revision.
- Work exposure versus reasoning demonstrated in a particular scenario.
- Unknown, untested, disputed, and unresolved information.
- Input saved versus feedback pending or failed.
- Draft entry versus an entry approved by the user.
- Context-specific acknowledgement versus a claim that code is safe.
- Private material versus deliberately shared output.

Use text and structure alongside color. Do not add universal competence percentages, seniority rankings, commit-count productivity scores, mandatory streaks, or fabricated impact metrics.

## 9. Specialized tools

Add specialized libraries when an actual task requires them. Candidates include TanStack Table for substantial data-table behavior, Shiki for read-only highlighting, an editor for real editing requirements, and React Flow for interactive node diagrams.

These are optional, not an instruction to install all of them. Evaluate compatibility, license, mobile behavior, and bundle impact in the project. Restyle their visible surfaces using Sensei tokens. Avoid turning them into competing general UI systems.

## 10. Verify and document

Run the repository's relevant type, lint, build, and interaction checks. Add focused tests for meaningful behavior: dialog focus and dismissal, keyboard selection, form errors, preserved answers after failure, state distinctions, and narrow-layout behavior.

Inspect rendered screens at representative phone, tablet, and desktop widths, including a narrow phone view. Capture screenshots for the completed surfaces and inspect overflow, readability, hierarchy, and illustration footprint. Verify keyboard navigation, text enlargement, reduced motion, and key accessibility semantics on the implementation.

Provide a component gallery using Storybook if present; otherwise choose a lightweight development gallery or add Storybook if it materially helps maintain the kit. Include interactive states and dense examples. Use the gallery to demonstrate actual shared components, not parallel mock implementations.

Document final token values, component usage, responsive rules, semantic states, illustration-use guidance, and the actual dependency versions. Character specifics remain deferred. Update the project's design documentation to reflect final implementation choices and any justified deviations.

## 11. Definition of done

- The project uses the selected shadcn + Base UI foundation for applicable common controls.
- Shared tokens and components express the approved identity across available sections.
- Desktop and mobile layouts are usable with realistic content and interaction states.
- The reflection validation screen uses actual selected primitives and has been exercised in the proper environment.
- Illustration is purposeful and optional; character specifics have not been invented.
- Important product distinctions remain visible and editable where required.
- Relevant checks pass, screenshots have been inspected, and outstanding limitations are reported accurately.
- The UI kit has working examples and documentation sufficient for another developer or agent to extend it consistently.

Finish with a concise report of what was implemented, what was verified, any remaining gaps, and the relevant file paths. Keep screenshots and the kit documentation available for review.

## Official starting points

- [shadcn/ui documentation](https://ui.shadcn.com/docs)
- [shadcn Base UI dialog](https://ui.shadcn.com/docs/components/base/dialog)
- [Base UI quick start](https://base-ui.com/react/overview/quick-start)
- [Base UI styling](https://base-ui.com/react/handbook/styling)
- [Base UI accessibility](https://base-ui.com/react/overview/accessibility)
