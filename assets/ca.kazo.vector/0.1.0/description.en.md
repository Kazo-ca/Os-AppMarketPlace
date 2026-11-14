# Kazo Vector

A vector drawing app for Kazo OS: logos, illustrations, icons, posters and diagrams. Kazo Vector is
based on [VectorCraft](https://github.com/storytold/vectorcraft) by the ArtCraft team, an
open-source vector illustration app written in Rust, running here in a Kazo OS window.

## Draw

- Pen, pencil, curvature and shape tools, with real Bézier anchor points and handles.
- Exact path operations (unite, subtract, intersect, divide) and a shape builder.
- Fills, strokes, gradients, swatches, transparency, clipping masks and live effects such as glows
  and shadows.
- Text on a line, in a frame or along a path, with character and paragraph styles.
- Several artboards per drawing, layers, groups, symbols, live blends, smart guides and rulers.
- Unlimited undo.

## Your Kazo OS files

- Double-click a `.svg`, `.svgz`, `.ai`, `.ait`, `.eps`, `.epsf`, `.ps`, `.dxf`, `.emf`, `.wmf`,
  `.vectorcraft` or `.vctemplate` file in the File Explorer: it opens in Kazo Vector. `.pdf` and
  Affinity `.afdesign` files offer it under "Open with".
- Several drawings per window, each in its own tab. File › Open uses the Kazo OS open dialog and
  can open several files at once. A file opened from the File Explorer gets its own window.
- Save writes back to the open file, in its format. Save As, and the first save of a new drawing,
  go through the Kazo OS save dialog.
- Save a Copy and Export (SVG, PDF, PNG, JPEG, WebP, Export for Screens) go through the Kazo OS
  "Save a copy" dialog: the drawing stays bound to its file. Nothing ends up in the browser's
  downloads.
- Closing the window asks before losing changes, and can save every modified drawing first.
- Kazo Vector follows the Kazo OS light or dark theme and its language.

## Known limits

- **VectorCraft is in alpha.** Most drawings open and save well, but keep a copy of the ones that
  matter.
- Kazo OS opens files up to 128 MiB. Very large or very complex drawings can be slow to open and
  draw, especially on a computer without a graphics card.
- No recovery after a crash for now: a drawing that was not saved is lost if the browser or the tab
  closes. Save often.
- VectorCraft's own settings (tool options, panels, preferences) are not kept from one window to
  the next.
- The first save of a new drawing shows two dialogs: VectorCraft's save options (name and format),
  then the Kazo OS save dialog (folder).
- Print uses the browser's print dialog.
- Kazo OS keeps Alt+Shift+Left/Right and Ctrl+Alt+Shift+Left/Right: in vertical text, shift the
  baseline from the Character panel instead. Alt+Space opens the window menu: release Alt before
  panning with Space.
- The window stays on the screen it opened on: moving it to another screen would reload
  VectorCraft and lose the open drawings.
- VectorCraft draws with WebGPU or WebGL 2: a browser or computer with neither shows an error screen
  instead of the drawing.
- The download is about 30 MB (about 72 MB once installed), and each window loads its own copy of
  VectorCraft.
- Requires Kazo OS 3.3.4 or later.

## License

Kazo Vector is free software under the MIT license. VectorCraft is under the MIT or Apache-2.0
license; its notices, the licenses of the material it carries and those of the fonts
(SIL Open Font License 1.1) ship with the app (see `THIRD-PARTY-NOTICES.md` in the repository). The ArtCraft name
and logos are trademarks of the ArtCraft team and are not used by Kazo Vector.
