# Sensei++ — Visual Identity and UI Guidelines

**Version:** 1.2\
**Date:** 30 September 2026\
**Status:** Agreed visual identity; Coastal/cobalt SPA baseline formalized in the companion design guidelines.

## 1. Purpose and authority

This document records the visual identity agreed through the Sensei++ design exploration. It gives designers and developers a common direction for the web application and mobile experience, including casual practice, focused technical work, knowledge profiles, and experience records.

The accepted direction is a restrained **Living Campus**: an inviting professional academy expressed through clean interfaces, a coherent 2D illustration language, and selective character guidance. The academy is a sense of place and personality carried by the product, rather than a requirement to depict a physical campus on every screen.

The visual direction is approved. The [SPA Design Guidelines](Sensei-SPA-Design-Guidelines.md) specify the selected Coastal and Coastal Night palettes with cobalt actions, font families, and defaults for typography, dimensions, responsive layout, and interaction states. Use that document for concrete web design decisions. Character production rules, native layouts, and additional component APIs remain open. A written specification does not establish that every application screen has been implemented or verified.

Product requirements remain authoritative for behavior. Concept images illustrate appearance; their navigation labels, sample records, counts, technical examples, and incidental controls are not a finalized functional specification.

## 2. Audience and product character

Sensei++ serves developers who have moved beyond introductory programming and want to develop as capable professionals. They may be working juniors, middle-level developers, or more experienced engineers expanding into unfamiliar technologies and responsibilities. A repository connection or current employment is not required.

The platform supports practical growth through system and goal decomposition, debugging, performance improvement, code review, theory revision, reflection on real work, and communication of professional experience.

Its personality is **welcoming, capable, curious, and humble**. The interface respects existing experience, makes uncertainty comfortable to express, and recognizes the value of evidence, constraints, and trade-offs. It should make substantial technical work approachable while allowing users to concentrate.

Professional growth should be represented through concrete capabilities and evidence. Product language should avoid presenting technical opinions as universal truths or implying that a user has achieved a professional rank from a single exercise.

## 3. Visual principles

| Principle | Design consequence |
| --- | --- |
| Welcome the user | Bright, comfortable casual screens; understandable choices; approachable language. |
| Respect concentration | Calm working surfaces, readable technical content, restrained movement, and purposeful decoration. |
| Make the academy recognizable | Reuse a consistent palette, typography, shapes, icon family, and 2D illustration language. |
| Give illustrations a purpose | Use artwork to establish context, clarify content, or communicate feedback. |
| Make meaning readable | Pair unfamiliar visual concepts with text; preserve labels for important states and actions. |
| Respect engineering judgment | Show constraints, evidence, alternatives, and unresolved questions clearly. |
| Keep the system adaptable | Support different tasks with one visual identity and reusable components. |

## 4. Shared identity across web and mobile

Web and mobile belong to the same academy. They share color roles, typography, component geometry, iconography, state language, and illustration treatment.

Layouts should adapt to the task and available space. A desktop view may show source context beside a response editor, while mobile reveals context on demand and gives the response most of the available space. A desktop evidence table may become labeled concept cards on mobile.

The visual identity must accommodate code inspection, log analysis, system diagrams, interactive exercises, explanations, profiles, and journals. These are compatible activities, not competing visual styles. A task-specific interface may rearrange components or introduce specialized tools while retaining the shared foundations.

## 5. Casual and focused presentation

Presentation intensity is independent of device and theme. Mobile can support focused work, and the web application can support casual practice. Coastal and Coastal Night are the selected light and dark web appearances.

| Aspect | Casual presentation | Focused presentation |
| --- | --- | --- |
| Surface | Bright, softly tinted, welcoming. | Predominantly neutral and quiet around dense content. |
| Accent | More visible in discovery, selected topics, and invitations to continue. | Reserved for actions, selections, progress, and meaningful states. |
| Illustration | A small contextual welcome scene or relevant topic artwork may appear. | Small contextual cues; illustrations recede during sustained work. |
| Character | May introduce an activity or respond to a meaningful outcome. | Appears selectively with a relevant question, hint, or explanation. |
| Density | Comfortable browsing and touch interaction. | Efficient grouping of technical information without sacrificing readability. |
| Motion | Brief expressive feedback where useful. | Minimal movement and predictable transitions. |

A focused screen should not lose its identity when illustrations disappear. Typography, geometry, navigation, semantic colors, and component styling must carry the brand independently.

## 6. Color direction

The selected web palette combines pale blue surfaces, navy anchors, cobalt primary actions, white light-mode content panels, midnight blue dark-mode panels, and occasional warm cream supporting areas. This Coastal direction replaces the earlier aqua/teal/coral exploration. Exact values and state pairings are recorded in the [SPA Design Guidelines](Sensei-SPA-Design-Guidelines.md#3-color-system).

| Color role | Intended use |
| --- | --- |
| Pale sky / blue | Welcoming page backgrounds, restrained selection tints, and some casual modules. |
| Navy / midnight blue | Navigation anchors, strong light-mode text, and dark-mode working surfaces. |
| Cobalt / soft cobalt | Primary actions in light/night appearance and deliberate active emphasis. |
| White / neutral panel | Reading, writing, source material, tables, and sustained work; dark mode uses the corresponding quiet blue panel. |
| Warm cream | Occasional supporting explanation or contextual guidance. |
| Semantic state colors | Distinguishable success, attention, error, and neutral states with explicit text. |

Tint should support hierarchy rather than cover every surface. Long-form text and intensive work should generally sit on quiet panels. Cobalt must retain a clear action role; spreading it over many competing elements would weaken that hierarchy. Yellow/amber and green are not normal action accents in the selected web direction.

Selection, completion, saved input, uncertainty, approval, and error have different meanings. They must not be assigned the same visual treatment simply because one color is convenient. Color is accompanied by labels, icons, or structure wherever the distinction matters.

Use the selected semantic palette and checked action pairings in the SPA guidelines. Verify additional pairings and composited states on each implemented screen. Generated images are visual references, not reliable sources of final color values.

## 7. Typography

Use clean, readable modern sans-serif typography with open letterforms. Headings may carry warmth and confidence, while body text and controls remain straightforward. Use a compatible monospace face for code, logs, and technical identifiers.

The preferred direction comes from the softer, more readable typography explored in variant B. Avoid excessively heavy or inflated lettering, elaborate technical display faces, and formal serif document styling.

Establish explicit roles for page titles, section headings, body text, controls, metadata, and code. Reuse these roles across screens rather than creating a unique text style for every module.

Use sentence case for normal interface text. Short labels may use a different treatment sparingly. Preserve comfortable line length and line spacing for explanations and journal entries. Technical content should remain legible without forcing users to zoom.

The selected web families are Plus Jakarta Sans and DM Mono. The SPA guidelines define the default type scale and fallbacks. Multilingual coverage and screen-level readability still require validation; the current demo bundles Latin subsets.

## 8. Geometry, surfaces, and hierarchy

Use gently rounded rectangles, soft transitions between surfaces, and restrained borders. The interface should feel approachable without turning every control into a pill.

Cards and panels exist to group related content, distinguish working areas, or express a clear interactive unit. Use spacing and typography when a border or container adds little value. Subtle dividers can organize tables and dense material.

Selected controls should be unmistakable. Use a combination of surface tint, icon, label, or border emphasis appropriate to the component. Elevation is reserved for overlays, menus, and other meaningful layer changes; ordinary content does not need pronounced shadows.

An occasional arch-like silhouette may reinforce the academy identity in an illustration or larger composition. It should not become a compulsory ornament or distort familiar controls.

Hierarchy should remain visible when artwork is removed. Avoid giving every panel equal weight: the current task, continuation action, source material, and supporting information have different priorities.

## 9. Illustration and character use

Use a coherent **2D illustration language** with clear shapes, limited detail, restrained shading, and good readability at the intended size. Illustration should feel connected to the interface rather than imported from unrelated stock collections.

Every illustration must serve at least one purpose:

1. **Establish context:** introduce the academy or the activity in a compact welcome or transition scene.
2. **Clarify content:** reinforce a labeled topic, mechanism, or example through understandable imagery.
3. **Communicate feedback:** help explain a result, uncertainty, useful next step, or meaningful achievement.

Topic illustrations should use recognizable subject matter and remain paired with descriptive text. A diagram can accompany decomposition, a code window and timer can accompany performance investigation, and an annotated change can accompany code review. Artwork must not become a symbolic puzzle the user needs to decipher.

Characters form part of the identity and may act as mentors, peers, or participants in learning situations. Use them where they contribute guidance, a question, an explanation, or contextual feedback. Character appearance, names, personalities, and production specifications are intentionally deferred.

Large character or environment artwork must not displace the exercise, explanation, or editing area. A character is not required on every screen. Avoid repeated landscape thumbnails, decorative scenes inside routine cards, and scenic backgrounds beneath dense text.

The accepted direction favors 2D artwork. Clay-like 3D objects, commercial product-display imagery, and unexplained abstract corporate graphics are outside this visual direction.

## 10. Icons and technical content

Functional icons should belong to a consistent family with compatible stroke, proportions, optical size, and detail. Keep navigation and action icons simple. Larger topic illustrations are a separate asset category and may be richer while sharing the same visual language.

Code and log panels prioritize syntax readability, line structure, selection, and useful annotations. They should use appropriate horizontal scrolling or expansion rather than shrinking content until it is unreadable. A contrasting code surface is acceptable if it supports reading and fits the palette.

Highlights must have an identifiable purpose, such as a selected line, evidence under discussion, or changed code. Diagrams and simulation elements may use specialized symbols, but those symbols must be labeled where their meaning is not obvious.

## 11. Component families

The future UI kit should define appearance, behavior, states, and usage guidance for these families.

| Family | Visual and usage direction |
| --- | --- |
| Navigation | Clear current location; consistent labels; navy desktop anchors and compact mobile treatment where appropriate. |
| Buttons and links | Cobalt primary action; quieter secondary and text actions; predictable hierarchy within a task. |
| Inputs and editors | Neutral reading/writing surfaces; visible focus; clear labels; distinguish saved input from evaluated content. |
| Cards and rows | Purposeful grouping; concise hierarchy; small relevant illustrations only where useful. |
| Tabs and filters | Easy-to-scan labels and clear selection; no reliance on tint alone. |
| Status labels | Explicit meaning for draft, approval, assistance, uncertainty, recency, and errors. |
| Progress indicators | Show position or completion within a known activity; avoid implying a universal competence score. |
| Tables and evidence lists | Inspectable sources, readable columns, neutral unknown states, responsive alternatives. |
| Guidance and feedback | Compact contextual panels; distinguish user statements, curated guidance, and generated suggestions. |
| Overlays | Clear layering, accessible dismissal, preserved task context, and stable focus behavior. |
| Technical work areas | Code, logs, diagrams, comparisons, or simulations styled from the same foundations. |

At minimum, each interactive component needs its applicable default, hover, focus, active/selected, disabled, loading, and error states. Avoid unnecessary states that do not belong to the component's behavior.

## 12. Applying the identity to product sections

| Section | Application of the visual direction |
| --- | --- |
| Home and discovery | Bright surfaces, a small number of next actions, selective welcome illustration, and understandable topic imagery. |
| Learning paths and practice | Clear activity hierarchy and progress; specialized exercise tools can vary while sharing component styling. |
| Work reflection | Quiet context and response areas; compact guidance; visible unresolved questions and save state. |
| Knowledge profile | Readable evidence and source links; distinguish work exposure, practice observations, assistance, confidence, and unknowns. |
| Experience journal | Comfortable editing, clear draft/approved status, provenance, and separation of suggested wording from approved claims. |
| Presentation and rehearsal | Legible accounts grounded in approved experience; previews and revision controls follow the same hierarchy. |
| Settings | Direct labels, predictable controls, and clear descriptions of privacy, retained data, and integrations. |

These mappings describe visual treatment, not a frozen navigation structure. The brief's product requirements and later usability work determine final information architecture.

## 13. State, feedback, and trust

The interface must preserve distinctions that matter to the product:

- The user's explanation and generated feedback.
- A first attempt and an assisted revision.
- Work exposure and demonstrated reasoning about a specific concept.
- An unresolved question and an unsuccessful attempt.
- A provider error and a learning outcome.
- A draft suggestion and a user-approved experience claim.
- A recorded acknowledgement and a claim that a change is safe.

Unknown or untested topics use neutral language and visual treatment. Missing information should not resemble an error in the user's competence. Feedback should offer a clear next step and a route to revise, ask for explanation, or dispute an interpretation.

Acknowledgement and approval remain explicit user actions. Their controls must describe what is being acknowledged or approved. A positive model response must not silently convert a draft into an approved record.

Loading and recovery states should preserve submitted text and explain what is happening. A saved answer, pending feedback, and completed review are separate states. Private and shared records should be distinguishable at the point where that distinction affects the user's action.

## 14. Motion, responsive behavior, and accessibility

Motion should reinforce state changes, continuity, or meaningful feedback. Use short, restrained transitions. Character animation is selective; continuous movement beside code or long text should be avoided. Support reduced motion and ensure essential meaning remains available without animation.

Responsive design should prioritize content rather than reproduce desktop geometry. Stack or reveal panels according to relevance; transform tables into labeled rows or cards where needed; keep primary actions accessible without obscuring content. Consider keyboard appearance, safe areas, and scrolling when placing mobile actions.

Provide readable contrast, visible keyboard focus, descriptive control labels, comfortable touch targets, keyboard access, and useful screen-reader structure. Status must remain understandable without color. Allow text enlargement and respect motion preferences. Relevant illustrations need useful alternatives; decorative illustrations should not add noise to assistive navigation.

Accessibility and real device behavior must be verified on implemented components. Attractive concept images do not establish either.

## 15. Development handoff and future UI kit

Translate the agreed identity into a small, maintainable system. Shared values should be expressed through tokens rather than independently recreated on each page.

The first token groups should cover:

- Color roles: canvas, surface, text, border, action, selection, focus, and semantic states.
- Typography roles: page title, section heading, body, label, metadata, and code.
- Spacing, layout widths, and responsive density.
- Shape, border, elevation, and layering.
- Motion and interaction timing.

Use semantic names such as `surface.reading`, `text.primary`, and `action.primary`, rather than letting page-specific styling become the source of truth. Exact names and values should match the implementation once selected.

The complete UI kit should include a foundations specification, reusable components with states, responsive examples, icon and illustration rules, character-use guidance, writing guidance, accessibility checks, and working component demonstrations. Include representative screens with dense content and recovery states, not only ideal home pages.

The SPA guidelines establish selected colors and default dimensions and breakpoints. Continue validating long text, technical content, narrow displays, keyboard use, and semantic state distinctions when applying them to new screens. Illustration production and a complete component catalog remain later work.

## 16. Review checklist

A design is consistent with this direction when:

- It feels like the same welcoming professional academy on web and mobile.
- Dense technical work remains comfortable to read and use.
- Typography is clear and component borders remain restrained.
- Illustration is 2D, coherent, limited, and purposeful.
- Character presence contributes to the situation without consuming working space.
- Labels explain topic imagery and important states.
- The current action is visually clear without many competing primary controls.
- Evidence, uncertainty, assistance, drafts, and approval remain distinguishable.
- Removing decorative artwork leaves a coherent interface.
- The screen can be built from shared foundations and reusable components.

**Recorded decision:** proceed with the restrained Living Campus identity and develop the detailed UI kit from this baseline. Character specifics remain open.

## 17. Selected React component foundation

**Decision recorded:** use shadcn/ui with Base UI as the foundation of the React web UI, including responsive mobile web. This selection does not decide a future React Native implementation.

Use shadcn's editable component source for common controls and Base UI for underlying interaction behavior. Apply the Sensei identity through shared semantic tokens and locally maintained component styles. Treat the supplied default styling as a starting point for adaptation.

The UI should have three layers:

1. **Interaction primitives:** Base UI handles applicable focus, keyboard, overlay, selection, and related behavior.
2. **Sensei components:** locally maintained shadcn-derived buttons, inputs, tabs, dialogs, and other common controls share the visual foundations and states.
3. **Product components:** reflection panels, context previews, evidence rows, practice cards, and guidance compose the shared controls.

Choose the Base UI implementation when adding shadcn components. Keep imports and component conventions consistent. Record upstream origins for copied source and review updates deliberately. Page-specific changes should not create parallel button, form, or overlay systems.

CSS custom properties should carry semantic tokens. Tailwind may support layout and state styling around those tokens; its default utility palette is not the brand specification. Use the SPA guidelines for selected fonts, palette values, and numeric design defaults.

Specialized tools can be introduced when a task requires them, such as a headless table utility, code highlighter, or diagram canvas. They should adopt the same tokens and should not become a second general-purpose UI system.

### First implementation check

Validate a reflection screen at desktop and mobile widths. Include source context, a long written response, contextual help, a dialog, keyboard interaction, save state, and a recoverable feedback error. Verify that submitted text survives failure and that the distinction between saved input and received feedback remains visible.

The standalone reflection/gallery demo provides the first implementation reference; [demo notes](Sensei-UI-Demo.md) record its checks and limits. The companion implementation handoff describes the broader workflow. A migration of the main SPA and complete production accessibility validation are not claimed.

### Official references

- [shadcn/ui introduction](https://ui.shadcn.com/docs)
- [shadcn Base UI dialog](https://ui.shadcn.com/docs/components/base/dialog)
- [Base UI quick start](https://base-ui.com/react/overview/quick-start)
- [Base UI accessibility guidance](https://base-ui.com/react/overview/accessibility)
