# Développement de Voicer

[English](../En/Development.md) · [Deutsch](../De/Development.md) · [Español](../Es/Development.md) · **Français** · [Português](../Pt/Development.md) · [Русский](../Ru/Development.md)

## Compiler, tester, lancer

```bash
dotnet build Voicer.slnx                     # 0 avertissement attendu : les règles du code sont des erreurs
dotnet test Voicer.slnx                      # xUnit 2 sur VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

Dans VS Code, le profil « Voicer » de `.vscode/launch.json` garde les données de la copie de débogage dans
`.vscode/dev/data` (ignoré par git) : un modèle téléchargé survit ainsi à une compilation propre. Le processus de
travail peut être essayé seul :

```bash
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker transcribe --model <modèle ggml> --audio <wav 16 kHz> --device 0
```

## Règles

Les règles du code sont dans [AGENTS.md](../../AGENTS.md) ; la compilation et `Tests/Voicer.Conventions.Tests` les
font respecter. `Tests/Voicer.Localization.Tests` vérifie que chaque pack a les mêmes clés et les mêmes emplacements,
que chaque clé utilisée par le code et les vues existe, et qu'aucun texte russe ne se trouve dans les sources.

## Ajouter une langue

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` avec toutes les clés de `en.json` (il est pris en compte
   tout seul).
2. Le drapeau : un bloc dans `Scripts/Assets/generate-flags.ps1`, puis lancer le script
   (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. L'installateur : une ligne dans `[Languages]` et des blocs dans `[Messages]`, `[CustomMessages]` de
   `Scripts/App/voicer.iss` (UTF-8 avec BOM), ainsi que `Scripts/Shared/License/intro.<code>.txt`.
4. Les notes des paquets : `Scripts/App/Notes/README.<code>.txt` ; les entrées de menu dans `Scripts/App/build-deb.sh`
   et `publish-common.sh`.
5. La documentation : `Docs/<Code>/` avec les mêmes fichiers que `Docs/En` (voir [Documentation](#documentation)).
6. La page du projet : une entrée dans `list` et `docs` de `Sources/Apps/Voicer.Pages/assets/languages.js`, son drapeau
   `assets/flags/<code>.svg` et une table dans `assets/site.js`.

## Documentation

Les documents sont rangés par langue : `Docs/En` est l'ensemble principal ; `Docs/De`, `Docs/Es`, `Docs/Fr`, `Docs/Pt`
et `Docs/Ru` contiennent le même ensemble en allemand, espagnol, français, portugais et russe. La ligne sous le titre de
chaque document renvoie au même document dans les autres langues.

- les fichiers et leurs noms sont les mêmes dans toutes les langues ; un changement de sens va dans toutes les langues
  à la fois ;
- les liens venant de l'extérieur (le README) mènent à `Docs/En` ; la page du projet ouvre les documents dans la langue
  choisie sur elle.

## Ajouter un modèle

Un enregistrement par quantification dans `SpeechModelCatalog` (famille = nom du dossier, quantification, adresse
HTTPS, taille exacte, SHA-256), dans l'ordre du catalogue : les familles de la plus lourde à la plus légère, les
quantifications de la plus précise à la plus petite. Le texte `Model.<famille>.Description` va dans chaque pack ; une
nouvelle quantification a aussi besoin de `Quantization.<valeur>.Name` et `.Description`. La licence du modèle va dans
`THIRD-PARTY-NOTICES.txt`.

Un modèle de voix est un enregistrement dans `VoiceModelCatalog` : son id (le nom du dossier), les poids et le
projecteur (`mmproj`) d'un GGUF publié pour llama.cpp, chacun d'une révision fixe avec sa taille exacte et son SHA-256,
ses langues (celle par défaut d'abord), les voix qu'il fournit, s'il parle avec la voix d'un échantillon, s'il en a
besoin et s'il tourne bien sur le processeur. Le texte `VoiceModel.<id>.Description` et `SpeechLanguage.<code>` d'une
nouvelle langue vont dans chaque pack ; la version de llama.cpp dans `LlamaCppPackage` doit prendre en charge le
modèle. Une nouvelle version de llama.cpp y est fixée avec la taille et le SHA-256 de chaque archive.

## Paquets et la page du projet

Voir [Scripts/README.md](../../Scripts/README.md). Un paquet ne contient que les bibliothèques natives de sa propre
plateforme ; les modèles, FFmpeg et llama.cpp n'y sont pas, l'utilisateur les télécharge depuis les réglages.

GitHub Actions : `ci.yml` compile et teste sous Windows et Linux à chaque push sur `main` et à chaque pull request ;
`packages.yml` construit tous les paquets à la main ou pour une étiquette de version et les joint à un brouillon de
version ; `pages.yml` publie la page du projet (`Sources/Apps/Voicer.Pages`, voir son README) à chacune de ses
modifications dans `main`.
