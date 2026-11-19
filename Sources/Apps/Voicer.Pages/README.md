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
| `updates.json` (after the first release) | The latest release. The "Download" buttons read it; without it they lead to the releases page. |

## First publication

1. Open the repository on GitHub: **Settings → Pages**.
2. Under **Build and deployment** choose **Source: GitHub Actions**.
3. Push a commit with this folder to `main` (or run the **Pages** workflow by hand on the **Actions** tab).
4. A couple of minutes later the page opens at <https://depthrel.github.io/Voicer/>. The "Deploy" badge of the README
   shows the state of the last publication.

## A new release

1. Push the version tag (or run `Scripts/build-release.ps1`), check the draft release with its packages and publish it.
2. Create or fill in `updates.json` (without it the buttons lead to the releases page):

   ```json
   {
     "windows": { "version": "X.Y.Z", "url": "https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-setup.exe",
                  "zip": "https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-win-x64.zip" },
     "linux":   { "version": "X.Y.Z", "url": "https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-linux-x64.tar.gz",
                  "deb": "https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-linux-x64.deb" },
     "macos":   { "version": "X.Y.Z", "url": "https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-osx-arm64.tar.gz",
                  "intel": "https://github.com/DepthRel/Voicer/releases/download/X.Y.Z/Voicer-X.Y.Z-osx-x64.tar.gz" }
   }
   ```
3. Commit it only after the release is published: an early `updates.json` sends every visitor to a 404.

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
