# Voicer.Pages

The project page: <https://depthrel.github.io/Voicer/>. Plain static files with no build step, published by GitHub
Pages from this folder (`.github/workflows/pages.yml`) on every change of it in the `main` branch. The page is short
on purpose: what Voicer is, what it does, and the downloads for the three systems.

| File | What for |
|---|---|
| `index.html`, `assets/site.css`, `assets/site.js` | The page; the texts of the other languages are tables in `site.js`. |
| `assets/wave.svg` | The animated sound wave of the hero: bars beating like a heart, the colours of the logo flowing through them. Drawn by `Scripts/Assets/generate-wave.ps1` together with `Docs/images/banner.svg` of the README. |
| `assets/languages.js`, `assets/flags/*.svg` | The languages of the page (English, German, Spanish, French, Portuguese, Russian), the language picker with flags and the links to the documentation in the chosen language. |
| `assets/voicer.svg` | The logo, the same as `Sources/Apps/Voicer.UI/Assets/voicer.svg`. |
| `updates.json` | The current release (0.1.0): the version shown on the cards and the files the "Download" buttons lead to. The same files are written in the links of `index.html`, so the buttons work without scripts too. |

## A new release

The links lead to the files of a fixed version, `0.1.0` for now; they answer 404 until that release is published.

1. Push the version tag (or run `Scripts/build-release.ps1`), check the draft release with its packages and publish it.
2. Replace the old version with the new one in `updates.json` and in the download links of `index.html` (every
   address has the form `https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-<package>`).
3. Commit; the Pages workflow publishes the page.

## Rules of the page

- Nothing is loaded from other servers, nothing inline runs, and no visits are counted: the page pins what it may load
  with a `Content-Security-Policy` that allows only its own files.
- The English text is written right in the HTML (the page reads fine without scripts); the other languages are tables
  in `assets/site.js`, and a table must have every `data-t` key of `index.html`. Without scripts the language picker is
  hidden.
- The language is chosen like in the application: the one the visitor picked before (kept in `localStorage`),
  otherwise the first language of the browser the page speaks, otherwise English.
- The animations stop for visitors who ask for less motion (`prefers-reduced-motion`).
- A new language: one more entry in `list` and `docs` of `assets/languages.js`, its flag in `assets/flags/<code>.svg`
  (3:2), one more table in `assets/site.js`.

## Look at it locally

Any static server in this folder, for example `npx http-server . -p 8765`, then <http://127.0.0.1:8765/>. Opening
`index.html` as a file does not work: the security policy of the page allows only its own site.
