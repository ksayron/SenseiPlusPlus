# Sensei++ UI demo

**Date:** 30 September 2026. **Scope:** standalone reflection demo and shared-component gallery. This is the first validation slice of the UI handoff, not a migration of all application screens.

For future SPA design, use the [SPA Design Guidelines](Sensei-SPA-Design-Guidelines.md). Coastal and Coastal Night with cobalt actions are now the selected design baseline; other palettes remain exploration examples. The guidelines also set larger supporting-text defaults and stronger field/focus requirements for future screens than some compact demo controls currently use.

## Run and explore

From `client`, run `npm install` and `npm run dev`, then open `http://localhost:5173/ui-demo.html`. The existing application remains at `/`. Both entry pages are included by `npm run build`. No backend is needed for the demo.

The reflection studio uses a sanitized fixture about a retry boundary in a C# background worker. Write an answer, add an unresolved question, open a hint, and request sample feedback. Enable **Try a feedback timeout** to exercise recovery. **Retry feedback** restores the fixture response and preserves the submitted answer. **Review & acknowledge** records an explicit acknowledgement of `snapshot-03`; editing the response or unresolved question invalidates it.

Answers, first submitted attempts, unresolved questions, assistance, and acknowledgement are stored in the browser under `sensei-ui-demo:reflection:snapshot-03:v1`. Reloading resumes that draft. Storage errors are reported without clearing the answer. This is device-local demo persistence, not server persistence or general offline synchronization. Feedback is a fixed fixture prompt and does not assess the answer. Nothing is sent to an AI provider. No experience entry is approved or published.

The component gallery demonstrates the same controls used in the studio, including action hierarchy, status labels, inputs, a dialog, a disclosure, a switch, activity progress, and an alert. Gallery state is ephemeral. Character assets and identities remain deferred.

## Color theme previews

The palette button in the top bar opens a Base UI dialog with six live previews, grouped into three light and three dark choices. Coastal is the default, following the owner's preference for blue tones over green.

| Mode | Theme | Atmosphere | Action |
| --- | --- | --- | --- |
| Light | Coastal | Airy blue and midnight ink | Cobalt `#315fc4` |
| Light | Daybreak | Warm ivory and navy | Coral `#ad493a` |
| Light | Porcelain | Cool white and slate | Blue `#355faf` |
| Dark | Coastal Night | Midnight blue | Soft cobalt `#97b8ff` |
| Dark | Ember Night | Warm charcoal | Peach `#f0ac96` |
| Dark | Slate Night | Graphite and silver | Blue `#a6c5fb` |

Coastal and Coastal Night are the selected web design direction; the other four choices are demo alternatives. `client/src/theme/palettes.ts` is the shared SPA/demo palette source; `client/src/demo/themes.ts` re-exports it. `ThemePicker.tsx` previews and selects them. The choice applies to the complete demo, including navigation, code, guidance, semantic states, overlays, and gallery swatches. Switching does not reset the reflection. The selection is stored separately under `sensei-ui-demo:color-theme:v1`; invalid or unavailable storage falls back to Coastal. Retired light palettes also fall back to Coastal; the old `dusk` preference maps to Coastal Night. If storage cannot be written, a selection still works for the current visit. The demo entry applies the saved palette before mounting React. Green-led demo variants have been retired; saved/feedback status surfaces now use explicit labels with restrained blue tones.

Eight text pairings per palette were checked: primary action, reading surface, muted text on canvas, guidance, attention, success, error, and muted navigation labels. The lowest ratio per palette is 5.34:1 for Coastal, 5.20:1 for Daybreak, 5.25:1 for Porcelain, 7.44:1 for Coastal Night, 7.60:1 for Ember Night, and 7.25:1 for Slate Night. This is a check of those pairings, not a full accessibility audit. State meanings remain explicit and are not repurposed as theme accents.

Coastal and Coastal Night use the owner's selected cobalt direction. Their primary hover fills are opaque `#274da4` and `#aac5ff`, giving label contrast of 7.82:1 and 8.51:1 respectively. Coastal uses white action labels; Coastal Night uses `#162846`. Both navigation rails use `#97b8ff` for active accents. Other palettes retain their existing hover treatment. Warm guidance and attention surfaces keep their semantic roles.

The browser suite now includes palette preview, selected-state announcement, keyboard dismissal and focus return, draft preservation, theme persistence after reload, matching gallery values, and horizontal overflow at desktop/tablet/phone widths. Theme screenshots use `*-theme-*.png` in the screenshot directory. A theme selector is available in both demo sections; phone layouts use the palette icon when space is tight.

## Foundation and ownership

Generated with shadcn CLI **4.21.0**, registry style **base-nova**. Actual runtime dependency versions are pinned by `client/package-lock.json`:

| Dependency | Installed version |
| --- | --- |
| `@base-ui/react` | 1.8.0 |
| `tailwindcss` / `@tailwindcss/vite` | 4.3.3 |
| `class-variance-authority` | 0.7.1 |
| `cn` | 0.4.0 |
| `tw-animate-css` | 1.4.0 |
| `@fontsource/plus-jakarta-sans` / `@fontsource/dm-mono` | 5.3.0 |

`client/components.json` selects Base UI. Locally owned source lives in `client/src/components/ui`. Applicable controls import `@base-ui/react`; textarea and alerts use native semantic elements. Imports use the existing Vite framework with a new `@/` alias. Variant helpers remain internal to their component files to preserve Fast Refresh conventions. Review registry updates deliberately; do not overwrite local adaptation without checking the diff.

`src/theme/foundation.css` defines shared semantic colors, foundation tokens and Tailwind mappings; `src/demo/theme.css` imports it. `src/demo/demo.css` holds product composition and responsive rules. The demo's HTML entry isolates its styles from the existing application. The application now consumes this foundation; see the [SPA implementation report](Sensei-SPA-Design-Implementation.md) for scope and verification. Do not create a parallel set of controls when extending this demo.

Fonts are bundled locally: Plus Jakarta Sans 400/500/600/700 and DM Mono 400, Latin subsets. Broader multilingual coverage requires adding the appropriate subsets. Upstream MIT and font OFL notices are included in `client/public/licenses` and copied into the built artifact.

Official sources: [Vite setup](https://ui.shadcn.com/docs/installation/vite), [shadcn CLI](https://ui.shadcn.com/docs/cli), [Base UI dialog composition](https://ui.shadcn.com/docs/components/base/dialog), [Base UI tabs](https://base-ui.com/react/components/tabs).

## Initial design decisions

These are current default Coastal implementation values, subject to refinement. Other palettes use the corresponding semantic roles in `themes.ts`.

| Role | Value |
| --- | --- |
| Canvas / reading surface | `#eef3fa` / `#ffffff` |
| Strong text and navigation | `#253b59` |
| Primary action / label | `#315fc4` / `#ffffff` |
| Primary hover | `#274da4` |
| Secondary surface / text | `#e3eaf5` / `#355072` |
| Muted text | `#52657f` |
| Border / input border | `#d9e2ef` / `#9cacbf` |
| Focus | `#416da7` |
| Guidance surface / text | `#fff3de` / `#725a33` |
| Attention surface / text | `#fbefd8` / `#71521d` |
| Success surface / text | `#e1eaf8` / `#345682` |
| Error surface / text | `#ffe7e0` / `#a73229` |

Computed default Coastal contrast ratios: primary button 5.89:1; body on white 11.36:1; muted text on canvas 5.34:1; attention 6.30:1; success 6.19:1; error 5.67:1; guidance 5.93:1. These checks cover the listed text pairings, not a complete accessibility certification.

Font sizes use rem units for supporting text, controls, and technical content. Most supporting text starts at 11px equivalent; body/context text uses 12–14px; the question uses 19px; page titles use 25–36px. Code retains horizontal scrolling. Geometry uses an 8px control radius and 10–12px panel radii. Layout gaps predominantly use 8, 12, 16, 20, 24, and 32px. Overlay transitions use the supplied 100ms shadcn treatment; reduced-motion preferences suppress animations and transitions.

Desktop uses a 230px navigation rail and adjacent source/response panels. At 1150px, spacing and rail width decrease. At 850px, navigation moves above the content. At 650px, source context becomes an expandable disclosure, writing remains full-width, primary actions become at least 44px high, and supporting panels stack. Input text is 16px on narrow phones. Source context is never replaced by a shrunk desktop screenshot.

## Verification

Run `npm run lint`, `npm run build`, `npm run test`, and `npm run test:demo` from `client`. The demo test configuration uses installed Microsoft Edge and a dedicated Vite server on port 5174; it does not start the .NET backend. Workers may require unrestricted process execution on Windows.

Browser checks cover 1440px desktop, 768px tablet, and 360px phone widths: rendering and horizontal overflow, gallery control interaction, saved-answer reload, feedback failure/retry, modal focus containment and Escape dismissal, focus return, assistance labeling, empty-answer validation, explicit version-specific acknowledgement and invalidation, keyboard tabs, reduced-motion preference, and enlarged text. Existing unit tests also pass. Screenshots for reflection, gallery, and recovery live in `docs/ui/demo-screenshots`.

Remaining scope: backend integration, feedback assessment, other application sections, a complete production UI kit, character artwork, broader language font coverage, and real mobile keyboard/device validation. The browser viewport checks do not substitute for the last item.
