# Sensei++ SPA Design Guidelines

**Version:** 1.1\
**Date:** 1 October 2026\
**Status:** Selected design baseline, applied to the existing React SPA.\
**Direction:** Living Campus, expressed through Coastal and Coastal Night with cobalt actions.

## 1. Scope and authority

Use this guide when designing or implementing Sensei++ web application screens. It formalizes the owner's selected demo direction: pale blue working environments, navy anchors, cobalt primary actions, readable panels, restrained rounded geometry, and optional purposeful 2D illustration.

The palette and component foundation are settled decisions. Typography, dimensions, and interaction rules below govern SPA work; validate their application with real screen content. The [implementation report](Sensei-SPA-Design-Implementation.md) records the migrated routes and verification limits. This guide does not claim that every screen or demo control has passed a full accessibility audit.

Read the documents in this order:

1. Product requirements and the relevant learning/domain documents govern behavior and scope.
2. This guide governs concrete SPA appearance, component usage, and responsive interaction. It supersedes the earlier teal/coral color direction for web screens.
3. [Visual identity](Sensei-Visual-Identity-and-UI-Guidelines.md) governs product personality, illustration, and character use.
4. [Implementation handoff](Sensei-Codex-UI-Implementation-Handoff.md) describes the implementation workflow.
5. [Demo notes](Sensei-UI-Demo.md) describe the working reference and its verification limits.

Coastal is the default light appearance; Coastal Night is its dark counterpart. Daybreak, Porcelain, Ember Night, and Slate Night remain demo experiments, not additional SPA design defaults. Native applications and character production require their own later specifications.

## 2. Design principles

| Principle | Application |
| --- | --- |
| A welcoming professional academy | Warm, direct language; quiet blue surfaces; modest character rather than decorative spectacle. |
| The current task comes first | Give the response, source, or evidence most of the working area. Supporting guidance recedes. |
| One clear next step | One dominant action per task group; supporting controls use quieter treatments. |
| Evidence stays inspectable | Preserve source links, context versions, assistance, uncertainty, and provenance. |
| Meaning survives without color | Use labels, structure, and icons for state distinctions. |
| One system across routes | Reuse shared tokens and controls; task layouts can vary. |

Focused work and casual discovery use the same foundations. Discovery may add a small welcome illustration and more open spacing; code, editors, tables, and evidence views reduce decoration. Presentation intensity is independent of light/dark appearance.

## 3. Color system

Use semantic CSS custom properties. Page and component CSS consume roles rather than embedding hex values or general Tailwind color literals. Surface/foreground pairs must travel together. This follows [shadcn's token convention](https://ui.shadcn.com/docs/theming).

### Selected palette

The table records the current selected demo values. `primary-hover` is an explicit opaque action state. Derived foreground roles use `foreground`; popovers use `card`.

| Token | Coastal | Coastal Night | Usage |
| --- | --- | --- | --- |
| `background` | `#eef3fa` | `#121c2d` | Page canvas |
| `foreground` | `#253b59` | `#e3ebf8` | Primary reading text |
| `card` | `#ffffff` | `#1c2940` | Reading/writing panels |
| `card-foreground` | `#253b59` | `#e3ebf8` | Panel content |
| `popover` | `#ffffff` | `#1c2940` | Dialog/menu surface |
| `popover-foreground` | `#253b59` | `#e3ebf8` | Overlay content |
| `primary` | `#315fc4` | `#97b8ff` | Dominant action fill |
| `primary-hover` | `#274da4` | `#aac5ff` | Dominant action hover |
| `primary-foreground` | `#ffffff` | `#162846` | Action label/icon |
| `secondary` | `#e3eaf5` | `#283954` | Supporting control fill |
| `secondary-foreground` | `#355072` | `#d6e3f6` | Supporting control content |
| `muted` | `#f0f4fa` | `#23324a` | Quiet supporting surface |
| `muted-foreground` | `#52657f` | `#b0c0d9` | Supporting text |
| `accent` | `#dce7f6` | `#304565` | Subtle selection/highlight |
| `accent-foreground` | `#253b59` | `#e3ebf8` | Highlight content |
| `border` | `#d9e2ef` | `#3c506d` | Dividers and grouping |
| `input` | `#9cacbf` | `#7087a8` | Demo field boundary; see control rule below |
| `ring` | `#416da7` | `#8bb5f0` | Keyboard focus and strong control boundary |
| `sidebar` | `#253b59` | `#0d1524` | Navigation rail |
| `sidebar-foreground` | `#f1f5fc` | `#edf3ff` | Active/strong navigation text |
| `sidebar-muted` | `#bccde1` | `#b7c8e3` | Other navigation text |
| `sidebar-accent` | `#97b8ff` | `#97b8ff` | Active marker and small brand accent |
| `guidance` | `#fff3de` | `#383229` | Curated contextual explanation |
| `guidance-foreground` | `#725a33` | `#ebd5b0` | Guidance content |
| `attention-surface` | `#fbefd8` | `#423929` | Unresolved/review-needed state |
| `attention-foreground` | `#71521d` | `#f0d39b` | Attention content |
| `success-surface` | `#e1eaf8` | `#263b58` | Recorded/saved/completed state |
| `success-foreground` | `#345682` | `#bfd5f5` | Success content |
| `error-surface` | `#ffe7e0` | `#482e2d` | Error explanation |
| `error-foreground` | `#a73229` | `#ffc4b9` | Error content |
| `destructive` | `#a73229` | `#ffb2a7` | Destructive action emphasis |

`sidebar-accent` is a local demo marker/text token, not the background token of shadcn's standard Sidebar component. If adding that component, map marker/text and selected surfaces into its separate roles; do not feed a pale blue marker into an unlabeled selected background.

### Color usage rules

- Cobalt marks primary actions. Avoid yellow, ochre, amber, or green as the normal action accent. Warm cream remains available for guidance and attention.
- Keep panels predominantly neutral. Large solid cobalt areas belong to deliberate brand moments, not ordinary reading surfaces.
- Use `accent` for selected technical lines or subtle local selection. Add a marker, check, or explicit selected state when selection matters.
- Use the same action label/icon foreground in default and hover states. Do not fade Coastal primary fills with opacity; use the opaque hover colors above.
- Reserve error/destructive roles for failures and destructive operations. An unknown concept or unanswered question is not an error.
- Success uses restrained blue with explicit text. Cobalt by itself does not mean saved, correct, approved, or complete.
- Underline inline links or provide another persistent non-color cue. Choose a readable text pairing; a blue label alone is insufficient to distinguish a link from adjacent prose.

The light demo `input` boundary is only **2.32:1 against white**. For a new SPA field whose boundary is needed to identify the control, use the stronger `ring` color for the default boundary, or establish another verified control treatment. Do not copy the pale `border` or `input` value as the sole identifying cue. The light `ring` is 5.29:1 against white; the night `input` is 3.97:1 against `card`.

### Theme behavior

Store the user's explicit light/night choice separately from drafts. Apply the resolved appearance before the first meaningful render, set native `color-scheme`, and theme portals, code surfaces, alerts, and native controls as well as the page canvas. Theme changes must preserve editor contents, route, and task state.

Use one theme source and one provider/initializer when adopting these values in the SPA. The demo currently uses `data-theme` and inline variables; its dark appearance is not evidence that every library `dark:` class is active. If components rely on `.dark`, synchronize that selector with the resolved appearance. A system-following preference is an optional later product decision; these guidelines do not claim that the demo implements it.

## 4. Typography

Use **Plus Jakarta Sans** for interface text and **DM Mono** for code, logs, and technical identifiers. Keep existing license notices. The demo bundles Latin subsets; add and verify the required language coverage before enabling Russian or other scripts. Use `'Segoe UI', sans-serif` and `Consolas, monospace` as practical fallbacks.

The following are SPA defaults, expressed in rem at a 16px browser baseline. They intentionally raise the demo's many 11px annotations; that compact demo scale is not a requirement for future screens.

| Role | Size | Weight | Line height |
| --- | --- | --- | --- |
| Page title | 1.75–2.25rem (28–36px) | 600 | 1.25 |
| Section title | 1.25rem (20px) | 600 | 1.4 |
| Task question | 1.1875rem (19px) | 600 | 1.55 |
| Body / editor | 0.875–1rem (14–16px) | 400 | 1.6–1.8 |
| Control / field label | 0.875rem (14px) | 500–600 | 1.4 |
| Metadata / supporting note | 0.75rem (12px) | 400–500 | 1.5–1.7 |
| Code / logs | 0.8125–0.875rem (13–14px) | 400 | 1.7 |

Use 700 selectively for the brand or short strong emphasis. Keep sentence case, one route-level `h1`, and a meaningful heading hierarchy. Short eyebrow labels may use uppercase and modest letter spacing; instructions and paragraph text do not.

Keep prose near 60–75 characters per line where possible. Long text wraps; technical blocks scroll horizontally when needed. Never shrink code or essential labels to make columns fit. Phone editable fields use at least 1rem. Preserve browser text scaling; do not lower the root font size.

## 5. Spacing, geometry, and surfaces

Use the spacing sequence **4, 8, 12, 16, 20, 24, 32, 40, 48px**, represented with rem tokens. Use 4–8px within compact controls, 12–16px within a content group, 20–24px between related panels, and 32–48px between major sections. Introduce another value only for a functional layout constraint.

| Foundation | Default |
| --- | --- |
| Control radius | 8px |
| Content panel radius | 10–12px |
| Overlay radius | 12px |
| Divider / panel border | 1px using `border` |
| Desktop page padding | 32–40px |
| Tablet page padding | 24px |
| Phone page padding | 16–20px |
| Standard action height | 36–40px on fine pointers |
| Touch action target | At least 44 × 44px, including icon actions |

A compact 32px control is appropriate only for a secondary dense desktop tool. Do not copy the generated 24/28/32px sizes into touch actions unchanged. Keep panels flat; use elevation for menus and overlays where the layer change needs to be visible. Avoid nesting cards around every row.

Use an occasional arch silhouette in a brand mark or relevant illustration. Ordinary inputs, navigation rows, and buttons remain familiar rounded rectangles. Shadows, charts, and illustrations requiring further color roles must receive named shared tokens instead of page-specific constants.

## 6. SPA shell and responsive layouts

Use semantic landmarks: a labeled navigation area, a top/context bar where useful, and one main content area with a skip link. Reuse a stable shell across routes. Route navigation uses links and `aria-current="page"`; local content tabs use Base UI Tabs. The demo's two navigation tabs are a gallery convenience, not a routing specification.

Persist meaningful route state in URLs when existing product behavior supports it. Browser Back, direct links, and reload should identify the same destination. After route navigation, update the document title and move focus to the main heading or another appropriate content target. Local tab changes retain tab semantics; theme changes do not move focus.

Use these demo-derived responsive thresholds as starting points, then check real content:

| Width | Composition |
| --- | --- |
| Above 1150px | Approximately 230px navigation rail; adjacent source and task panels where useful. |
| 851–1150px | Approximately 200px rail; reduced page padding; supporting panels may stack. |
| 651–850px | Navigation becomes a compact top area or accessible menu; remove the permanent rail. |
| Up to 650px | One content column; source details may become a labeled disclosure; primary task remains directly available. |
| Above 1550px | Increase breathing room within a bounded content area, not text sizes or the number of competing panels. |

Use `minmax(0, 1fr)` and `min-width: 0` for working columns. Bound general work areas around 1440px; reading-only pages should be narrower. These widths are starting constraints, not fixed viewport requirements.

At 320px and enlarged text, wrap actions and navigation labels. Hide only decoration or genuinely redundant metadata. Never hide source access, save/error status, approval meaning, or the current task to fit the layout. Do not horizontally scroll the entire page. Code and genuinely two-dimensional data may have localized scrolling with accessible labels.

Recompose evidence tables into labeled rows/cards when that preserves comparison. If a table needs column relationships, keep semantic headers and controlled local scrolling. Sticky actions must not cover content, focus, or validation messages; verify mobile keyboard and safe-area behavior on real devices.

## 7. Shared components and interaction states

Use the selected **React + shadcn/ui Base UI** foundation. Applicable complex controls use `@base-ui/react`; native inputs and textarea remain appropriate. Shared source lives under `client/src/components/ui`. Product compositions own domain logic. Preserve primitive keyboard, selection, labeling, and overlay behavior while applying visual tokens. See [Base UI accessibility guidance](https://base-ui.com/react/overview/accessibility).

| Family | Usage and states |
| --- | --- |
| Primary button | One dominant action per task group; cobalt fill; opaque hover; labeled loading; visible focus; no repeated submission while pending. |
| Secondary / outline / ghost | Supporting actions, dismissal, hints, and low-emphasis tools. Avoid giving each control a filled primary treatment. |
| Destructive action | Explicit verb and consequence; confirmation when the product requires it; error-colored treatment rather than ordinary cobalt emphasis. |
| Input / textarea | Persistent label, optional hint, default boundary, focus, invalid message association, and meaningful read-only/disabled state. Placeholder is supplementary. |
| Navigation / tabs | Clear current/selected state with text and marker; correct route-link versus local-tab semantics. |
| Dialog / popover | Named surface, predictable focus, keyboard dismissal where appropriate, accessible close control, and focus return. |
| Disclosure | Labeled trigger, expanded state, keyboard operation; collapsed material must remain discoverable. |
| Status / alert | Explicit short meaning; persistent inline message for important failure; transient toast only for supplementary confirmation. |
| Progress / loading | Known activity position or labeled wait. Avoid presenting activity completion as competence or approval. |
| Table / evidence row | Inspectable source, clear headers and provenance; readable empty/unknown/loading/error states. |

Use applicable default, hover, focus-visible, pressed/selected, disabled, pending, invalid, and recovery states. A pressed action may use a subtle 1px displacement; selection requires a persistent marker. Do not invent state combinations a component does not support.

Loading actions keep their label and stable width, add an indicator when useful, and expose busy state. Explain unavailable actions when the reason is useful; disabled controls cannot be the only route to that explanation. Failed operations preserve editable/submitted content and offer a clear recovery path.

Modals keep focus inside while open, make background content unavailable, and return focus appropriately on dismissal. Follow the [WAI-ARIA modal dialog pattern](https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/). Avoid custom overlay implementations where the primitive already supplies this behavior.

## 8. Product states and writing

| State | Presentation |
| --- | --- |
| Input saved | Quiet status with exact persistence meaning, such as device-local or server-saved. |
| Feedback pending | Separate busy state; saving is not waiting for assessment. |
| Feedback unavailable | Error explanation and retry; user text remains intact. |
| Assisted attempt | Explicit assistance label near the relevant attempt/evidence. |
| Unknown / untested | Neutral label; no failure implication. |
| Unresolved question | Attention treatment and editable question. |
| Draft / suggested wording | Explicit draft/source label; editable; distinct from approved claims. |
| Approved / acknowledged | Explicit user action and scope/version where relevant. |
| Private / shared | Label at the point where the distinction affects the action. |

Write concise, direct labels describing the action: “Save draft”, “Retry feedback”, or “Acknowledge reflection”. Distinguish curated guidance, deterministic results, model suggestions, self-review, and user input. Avoid invented impact metrics, universal mastery percentages, seniority ranks, or claims that one exercise certifies capability. Never turn a successful model response into silent approval.

Empty states explain what is missing and provide the next available action. Error messages identify the failed operation and recovery option without describing provider failure as a learning outcome. Reserve `role="alert"` for timely errors; use a polite live region for meaningful save/loading updates rather than announcing every keystroke.

## 9. Icons, illustration, technical content, and motion

Use the existing **Lucide** icon family consistently. Typical control icons are 16–20px with the standard stroke; icon-only controls have accessible names and adequate targets. Topic illustrations are separate from functional icons.

Use purposeful, limited 2D artwork to establish context, clarify content, or communicate a meaningful next step. Provide alternatives for informative images; hide purely decorative assets from assistive navigation. Character appearance, names, personalities, and animation assets remain deferred. A screen must work without character artwork.

Code and logs use monospace, readable syntax tokens for both appearances, preserved whitespace, and intentional highlights. Do not imply an edited or selected line merely through decoration. An interactive editor or diagram library is added only when the actual task needs it and adopts shared surfaces, labels, focus, and state roles.

Use 100ms transitions for small controls/overlays, and up to 200ms for meaningful disclosure or layout continuity. Prefer color/opacity transitions over movement during focused work. Respect `prefers-reduced-motion`; loading and progress meaning remains available in text. Avoid continuous decorative motion beside editors and code.

## 10. Accessibility and verification

Target WCAG 2.2 AA on each implemented screen. Normal text, including placeholders and hover text, needs at least **4.5:1**; qualifying large text needs **3:1**. See [W3C text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html). Ratios below are computed sRGB label/fill checks, not an accessibility certification.

| Action pairing | Default | Hover |
| --- | --- | --- |
| Coastal white label | 5.89:1 | 7.82:1 |
| Coastal Night navy label | 7.44:1 | 8.51:1 |

Identifying control details, meaningful state markers, and relevant graphical objects need **3:1 against adjacent colors** under [W3C non-text contrast guidance](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html). Quiet grouping dividers need not serve as control boundaries. A labeled button does not automatically require its entire fill to contrast 3:1 with its surroundings; check the actual information needed to identify it.

Use a visible, unobscured focus indicator. Prefer a 2–3px opaque `ring` with a small offset, and verify it on its actual adjacent surfaces. The demo's translucent generated focus rings are not a prevalidated production focus specification.

The product's touch target default is **44 × 44px**. WCAG 2.2 AA's [target-size criterion](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) uses 24 × 24 CSS pixels with exceptions; the larger product default is a usability choice. Verify keyboard operation, semantic labels, text enlargement, reflow, and reduced motion as well as color.

For each new or migrated screen, review:

- Both selected appearances with real long text, empty data, loading, invalid input, and recoverable error states.
- Representative desktop/tablet/phone widths, a 320px layout, and 200% text enlargement.
- Keyboard route/tab navigation, focus visibility, modal containment/dismissal/return, and screen-reader names/state announcements.
- Every new text/background pairing, identifying field boundary, and selected/focus state after compositing.
- Draft preservation through theme changes, route changes according to product policy, and failed operations.
- Source, assistance, unknown, draft, and approval distinctions.

Keep screenshots and meaningful interaction checks with the component gallery. Run the relevant lint/build/interaction checks when implementation changes. Documentation checks alone do not verify a screen.

## 11. Adoption and maintenance

The standalone demo is the visual reference. Shared colors live in `client/src/theme/palettes.ts` and mappings/foundations in `client/src/theme/foundation.css`; the demo's former theme files are compatibility entries. Demo compositions live in `client/src/demo/demo.css`. Reference screenshots are [Coastal desktop](demo-screenshots/desktop-theme-coastal.png) and [Coastal Night desktop](demo-screenshots/desktop-theme-coastal-night.png). The [implementation report](Sensei-SPA-Design-Implementation.md) links current SPA references. Earlier theme screenshots are exploration history, not competing defaults.

When adopting the system in the SPA:

1. Extract the two selected palettes and shared foundation tokens into the application's common theme layer. Have the demo consume that source too; avoid a second handwritten palette.
2. Apply one theme initializer/provider and synchronize all selectors required by adopted components. Keep preference storage separate from task data.
3. Reuse existing locally owned shared controls. Implement the typography defaults, identifying field boundaries, and opaque focus rules in shared styles before spreading them across routes.
4. Validate one complete source/task/evidence screen in both appearances, including recovery and long content.
5. Extend to other authorized product routes using the same shell and foundations; add component states to the gallery as they are needed.
6. Update this guide, implementation tokens, and reference screenshots together when a design decision changes.

The current SPA and demo checks are recorded in the implementation report and demo notes. Real mobile keyboards, expanded language coverage, full screen-reader review, production content and character assets require further verification or implementation. The baseline can be used without treating those outstanding items as complete.
