# Sensei++ SPA design implementation

**Date:** 1 October 2026\
**Baseline:** [SPA Design Guidelines](Sensei-SPA-Design-Guidelines.md), Coastal / Coastal Night.

## Applied scope

The existing React application now uses the selected Living Campus direction across Today, Learn, Reflect, Evidence and Experience. The learning library, topic material, roadmap, session setup, exercise player, saved feedback and summary share the same foundations. Existing API contracts and learning-command behavior are preserved.

The shell has a navy navigation rail, pale blue canvas, neutral reading surfaces, cobalt actions and cream guidance. It uses Plus Jakarta Sans for the interface and DM Mono for technical material. Fonts are bundled locally, with Segoe UI and Consolas fallbacks; basic Russian glyphs are handled by the fallback fonts, not a newly claimed Jakarta Cyrillic subset. Broader multilingual font work remains separate.

The navigation becomes a wrapping top area at 850px. At 650px, task layouts use one column and editable fields use 16px text. Controls use 40px desktop and at least 44px touch targets. Long technical content and the semantic evidence table scroll within labeled, keyboard-accessible regions.

## Shared implementation

| Source | Responsibility |
| --- | --- |
| `client/src/theme/palettes.ts` | Single palette registry and theme application, including opaque hover colors, strong control boundaries, code, overlays, native color-scheme and synchronized `.dark`. |
| `client/src/theme/appearance.ts` | Only Coastal and Coastal Night are selectable in the SPA; preference is separate from drafts under `sensei:appearance:v1`. |
| `client/src/theme/foundation.css` | Shared semantic Tailwind mappings, fonts, spacing scale, radii, content width and motion tokens. |
| `client/src/styles.css` | Application shell and dashboard, reflection, evidence, journal and form compositions. |
| `client/src/learning/learning.css` | Topic, material, roadmap, exercise and feedback compositions. |
| `client/src/components/ui` | Existing shadcn Base UI controls, now consumed by the application. |
| `client/src/demo/themes.ts`, `client/src/demo/theme.css` | Compatibility entries consuming the common theme layer. Experimental palettes remain confined to demo selection. |

Theme resolution runs before React mounts. Changing appearance preserves the route, goal input, exercise answer and acknowledged save state. Portaled forms inherit the same tokens. The demo's preference remains independent.

Main navigation uses route links with `aria-current`; the shell provides a skip link and focuses the main heading after client-side route navigation. Topic sections use Base UI tabs with keyboard behavior, connected panels and the existing URL query state. Form dialogs use shared Base UI Dialog; the unsaved-session recovery dialog uses Base UI AlertDialog. Important mutation errors remain inline and preserve the form contents. Saved, draft, approved, assisted and unknown states retain explicit labels.

The former decorative search field now links to the functioning topic library. Static settings and user decorations no longer suggest unavailable actions. Continuous decorative motion and the capped knowledge-signal bars were removed; observable evidence counts remain explicit.

## Verification

| Check | Result |
| --- | --- |
| `npm run build` | Passed: TypeScript and production application/demo bundles. |
| `npm run lint` | Passed. |
| `npm test` | 7 tests passed. |
| Existing learning Playwright suite, Edge | 36 scenarios passed across desktop and narrow layouts with the local PostgreSQL/API. Theme preservation and the new unsaved-work dialog were additionally rechecked after their final changes. |
| `npm run test:ui` | 18 checks passed at 1440px, 768px and 320px. |
| Demo Playwright suite | 21 checks passed after adopting the common theme source. |

The UI suite uses deterministic intercepted API responses to inspect all five routes in both appearances. It checks overflow, 200% text, reduced motion, theme persistence and input preservation, modal focus containment/return, failed-save recovery and keyboard-controlled topic tabs. It measures the actual primary-action default/hover label contrast (at least 4.5:1) and identifying field boundary contrast (at least 3:1) in both appearances. The learning suite uses the real backend to check all six exercise formats, autosave, pause/resume, lost responses, conflicts and evidence-related flows.

Screenshots under `spa-screenshots/` record populated fixture-based pages at desktop/tablet/phone widths, plus real saved exercise input at desktop/narrow widths. Representative screenshots were visually inspected; these checks do not establish a full screen-reader or WCAG audit. Real mobile keyboards, device safe areas and expanded language coverage remain unverified.

## Visual references

- [Today — Coastal desktop](spa-screenshots/desktop-coastal-today.png)
- [Today — Coastal Night desktop](spa-screenshots/desktop-coastal-night-today.png)
- [Material — Coastal Night phone, 320px](spa-screenshots/phone-coastal-night-material.png)
- [Experience — Coastal phone, 320px](spa-screenshots/phone-coastal-experience.png)
- [Saved exercise input — Coastal Night desktop](spa-screenshots/session-desktop-coastal-night.png)

The [standalone gallery](Sensei-UI-Demo.md) remains available for component states and experimental palettes. Product screens use the selected light/night pair.
