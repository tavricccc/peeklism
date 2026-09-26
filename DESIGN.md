---
name: Peeklism
description: A focused visual system for the Peeklism website
colors:
  page: "#10141b"
  surface: "#171d27"
  surface-raised: "#1d2735"
  text: "#f1f5f9"
  muted: "#abb8c9"
  line: "#344052"
  blue: "#6a9dff"
  blue-strong: "#477fed"
  blue-ink: "#071b40"
  light-page: "#e9eef4"
  light-text: "#152231"
typography:
  display:
    fontFamily: "Peeklism Sans, Noto Sans TC, Microsoft JhengHei, sans-serif"
    fontSize: "clamp(2.9rem, 4.6vw, 5.75rem)"
    fontWeight: 560
    lineHeight: 1.17
    letterSpacing: "-0.035em"
  body:
    fontFamily: "Peeklism Sans, Noto Sans TC, Microsoft JhengHei, sans-serif"
    fontSize: "16px"
    fontWeight: 400
    lineHeight: 1.65
rounded:
  control: "8px"
  media: "9px"
  shell: "14px"
spacing:
  small: "16px"
  medium: "24px"
  large: "48px"
components:
  button-primary:
    backgroundColor: "{colors.blue}"
    textColor: "{colors.blue-ink}"
    rounded: "{rounded.control}"
    padding: "10px 22px"
    height: "52px"
  button-primary-hover:
    backgroundColor: "{colors.blue-strong}"
    textColor: "{colors.text}"
    rounded: "{rounded.control}"
    padding: "10px 22px"
    height: "52px"
---

# Design System: Peeklism

## Overview

The website places actual application screenshots on a graphite workspace. The native preview window stays bright and legible against its surroundings. Cobalt comes from the existing eye logo and marks selection, actions, and the image backplate. The page reads as an inspection surface with a compact header, open spacing, and a working file selection demonstration.

## Colors

The default dark scheme uses `page`, `surface`, and `surface-raised` for depth. Text uses `text` and `muted`; borders use `line`. `blue` is the single action and selection color. A light scheme keeps the same blue relationship with `light-page` and `light-text` as its base.

## Typography

`Peeklism Sans` is a subset of Noto Sans TC served locally as a variable WOFF2 font. The hero uses the display size above with a heavier cobalt phrase. Section headings use `clamp(2.3rem, 3.25vw, 4.25rem)` at weight 770 and line-height 1.3. Body text stays near 16px, with a slightly larger introductory line and muted supporting copy.

## Layout

The content container is at most 1336px wide, with 48px side margins on desktop, 32px below 1100px, and 16px below 600px. The hero, focus explanation, surface status, and installer are split layouts at desktop sizes; each becomes one column below 800px. The file demonstration becomes one column below 800px and switches to stacked file rows below 600px. Sections use 96-160px vertical space on desktop and 76px on phones.

## Elevation & Depth

Most content uses tonal surfaces and dividers. The actual application screenshots carry the one substantial shadow: `0 20px 60px -24px rgba(2, 9, 22, .75)` in dark mode. The cobalt block behind the hero screenshot is a solid plane, not a glow.

## Shapes

Controls use 8px corners, screenshots 9px, and the demo shell 14px. File selections have a blue border and tinted fill. Long content lists use single dividers rather than separate cards.

## Components

Primary buttons are blue with dark text, 52px tall, and move up 2px on hover. Text actions use an underline border. All interactive elements receive a visible 3px focus outline. The demonstration's Space control has a filled keycap; file buttons expose their selected state with `aria-pressed` and remain operable by arrow keys and Space. The header stays one line on desktop and removes its section navigation below 800px while retaining the install action.

## Do's and Don'ts

- **Do** show actual Peeklism screenshots when describing the application.
- **Do** distinguish the browser demonstration from the installed product.
- **Do** keep blue attached to selection and action across both color schemes.
- **Don't** imply that file-open dialog preview works yet.
- **Don't** turn technical explanations into generic feature cards.
