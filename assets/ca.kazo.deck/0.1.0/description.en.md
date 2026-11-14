# Kazo Deck

A presentation editor for Kazo OS. Kazo Deck is based on
[DeckCraft](https://github.com/storytold/deckcraft) by the ArtCraft team, an open-source
presentation editor written in Rust, running here in a Kazo OS window.

## Make presentations

- Slide masters and layouts, themes with colour and font schemes, sections, comments, notes,
  headers and footers, any slide size.
- Over 150 shapes with adjust handles, connectors, freeform drawing, merge shapes, gradients,
  shadows, glow and reflections.
- Formatted text on the slide: fonts, paragraphs, bullets and numbering, columns, autofit, links.
- Pictures, tables, charts, icons, audio and video.
- Transitions (Morph included), entrance, emphasis and exit animations, and motion paths.
- Starter templates to begin a presentation in seconds.

## Present

- F5 starts the slide show from the beginning, Shift+F5 from the current slide, and so does the
  "Present" button: the presentation goes full screen in the browser. Esc, or the end of the last
  slide, brings you back to the window.
- If the browser refuses full screen, the slide show fills the window.

## Your Kazo OS files

- Double-click a `.pptx`, `.ppsx`, `.potx` or `.deckcraft` file in the File Explorer: it opens in
  Kazo Deck. `.txt`, `.md` and `.rtf` files offer it under "Open with" (an outline becomes slides).
- One presentation per window: opening another file, or Ctrl+N, opens a new window.
- Save writes back to the open file, in its format. Save As, and the first save of a new
  presentation, go through the Kazo OS save dialog.
- Export (PowerPoint, PDF, pictures, animated GIF, outline) goes through the Kazo OS "Save a copy"
  dialog: the presentation stays bound to its file. The pictures of every slide go to the folder
  chosen for the first one. Nothing ends up in the browser's downloads.
- Closing the window asks before losing changes.
- Kazo Deck follows the Kazo OS light or dark theme.

## Known limits

- **DeckCraft is in alpha.** Most presentations open and save well, but keep a copy of the ones
  that matter.
- DeckCraft's interface is in English.
- Kazo OS opens files up to 128 MiB.
- DeckCraft in the browser embeds two fonts only: Inter and BIZ UDPGothic (Japanese). A
  presentation written with another font (Calibri, Arial, Lato…) shows in Inter, and its layout may
  move a little; the file keeps its fonts. Chinese, Korean, Arabic, Hebrew or Thai text may show as
  boxes.
- The presenter view (notes and next slide on another screen) is off for now.
- The window stays on the screen it opened on: moving it to another screen would reload DeckCraft
  and lose the open presentation.
- DeckCraft draws with WebGPU or WebGL 2: a browser or computer with neither shows an error screen
  instead of the slides.
- The download is about 15 MB (about 40 MB once installed), and each window loads its own copy of
  DeckCraft.
- Requires Kazo OS 3.3.4 or later.

## License

Kazo Deck is free software under the MIT license. DeckCraft is under the MIT or Apache-2.0 license;
its notices, the licenses of the material it carries and those of the fonts (SIL Open Font License
1.1) ship with the app (see `THIRD-PARTY-NOTICES.md` in the repository). The ArtCraft name and
logos are trademarks of the ArtCraft team and are not used by Kazo Deck.
