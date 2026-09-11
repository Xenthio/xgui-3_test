# Copilot Instructions

## Project Guidelines
- For XGUI theme remakes, use a historically accurate unskinned/base theme as the foundation (analogous to Windows Classic), and treat Aqua as a visual-style layer/override rather than the base control behavior and structure. NextStep should be the Mac base theme, with Aqua/Puma implemented as a separate visual style.
- Retain Cocoa as the shared macOS platform/base layer containing only cross-version structure, control contracts, selectors, and behavior. Version-specific themes (such as AquaPuma, Tiger/Aqua, and later macOS styles) should provide the visual metrics, colors, gradients, and version-specific overrides; Cocoa is not an unthemed renderer.

## Image Processing Guidelines
- Convert PXM image payloads from RGBA to BGRA before PNG packing for compatibility with rsrcdump.pack_png.
- PXM's separate mask is a click/hit mask, not an alpha channel; do not apply it to image alpha when exporting the visual PNG. Preserve the embedded RGBA alpha independently.