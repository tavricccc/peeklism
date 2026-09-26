# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

People working with files in Windows 11 File Explorer or on the desktop. Their immediate job is to inspect a selected item before deciding whether to open it. This audience description is inferred from the repository's README and current website.

## Product Purpose

Peeklism is a Traditional Chinese Windows preview utility. Select a file and press Space to see a quick preview in place; press Space again or Escape to dismiss it. For longer inspection, open the separate read-only viewer.

## Positioning

The preview window does not take keyboard focus from File Explorer. Arrow-key selection changes update the content while the preview stays in place, so the browsing flow continues.

## Operating Context

The app runs in the Windows system tray. Quick preview works in File Explorer and on the desktop. The application also offers a full viewer through Windows Open with, the preview, and the tray. The website is a static Traditional Chinese marketing and product information page.

## Capabilities and Constraints

- Quick preview supports images, video, audio, plain text, Markdown, PDF, and folders. Unknown formats still show file information.
- The full viewer supports image zoom, drag, and rotation; media controls; complete PDF reading; and Markdown and text reading. It also supports drag and drop, multiple selections, and fullscreen.
- File-open dialog preview remains unfinished. The latest README records a settings window as complete; the incumbent website's older status table says otherwise and should be corrected.
- The installer targets Windows 11 build 26100 or later on x64. It installs for the current user without administrator rights. The installer is unsigned. The repository and release download require authorized GitHub access while private.
- PDF and document viewing require Microsoft Edge WebView2 Runtime; some media formats depend on installed Windows decoders.

## Brand Commitments

Keep the Peeklism name, existing blue eye logo, Traditional Chinese voice, and actual application screenshots. The current site links to Downlism and Flowlism as related independent programs.

## Evidence on Hand

- `README.md` and `docs/releases/v0.6.0.md` provide current capability and release facts.
- `website/assets/preview-image.webp`, `preview-markdown.webp`, and `preview-text.webp` are actual application screenshots.
- The current website contains an interactive, explicitly simulated keyboard preview. Its demonstration data does not represent local user files.
- No customer testimonials, usage metrics, or public performance benchmarks are supplied.

## Product Principles

- Show a file quickly without interrupting the user's existing keyboard flow.
- Let the viewer take over when a quick glance is insufficient.
- Describe the app's current limits and installation requirements plainly.
