# Architecture de Voicer

[English](../En/Architecture.md) · [Deutsch](../De/Architecture.md) · [Español](../Es/Architecture.md) · **Français** · [Português](../Pt/Architecture.md) · [Русский](../Ru/Architecture.md)

Voicer transforme en texte la parole des fichiers audio et vidéo, et un texte en parole, sur l'ordinateur de
l'utilisateur. Les onglets sont Transcription, Synthèse vocale, Historique et Réglages.

## Projets

| Projet | Responsable de |
|---|---|
| `Sources/Core/Voicer.Localization` | les packs de langue (`Languages/<code>.json`), `ILocalizer`, `LocalizedError` |
| `Sources/Core/Voicer.Core` | les dossiers de données (`AppPaths`), le catalogue des modèles et leur stockage, les périphériques de calcul, les transcriptions et leur export en Markdown, l'historique des reconnaissances (`ITranscriptHistory`), les formats multimédias, le protocole du processus de travail |
| `Sources/Infrastructure/Voicer.Downloads` | les téléchargements en HTTPS qui reprennent après une coupure et sont vérifiés par taille et SHA-256 ; l'installateur des modèles |
| `Sources/Infrastructure/Voicer.Media` | FFmpeg : le trouver, installer le build figé sous Windows, extraire le son de n'importe quel fichier en WAV 16 kHz mono, le compresser en Opus pour l'historique, décoder un son conservé pour le lecteur |
| `Sources/Infrastructure/Voicer.Storage` | la base de données du dossier de données (SQLite, `Data/voicer.db`) : l'historique des reconnaissances et leur son |
| `Sources/Infrastructure/Voicer.LanguageModels` | le client des serveurs de modèles de langage dotés de l'API compatible OpenAI (LM Studio, Ollama, llama.cpp, Jan) : la liste des modèles, les réponses en streaming |
| `Sources/Infrastructure/Voicer.Audio` | le lecteur d'un son conservé : `AudioPlayer` au-dessus du décodeur FFmpeg et du périphérique audio (OpenAL Soft) |
| `Sources/Speech/Voicer.Speech.Whisper` | Whisper via Whisper.net : la liste des périphériques, la reconnaissance, l'hôte du processus de travail |
| `Sources/Speech/Voicer.Speech.Llama` | la synthèse vocale avec llama.cpp : son build figé et son installation, les arguments de llama-tts et le synthétiseur qui le lance pour chaque morceau de texte |
| `Sources/Apps/Voicer.UI` | l'application Avalonia (MVVM avec CommunityToolkit.Mvvm) ; le même programme est le processus de travail |
| `Sources/Apps/Voicer.Pages` | la page du projet : des fichiers statiques publiés par GitHub Pages |

Les dépendances pointent vers l'intérieur : le noyau ne connaît ni l'interface, ni le réseau, ni le moteur.

## Une reconnaissance

```
fichier (mp3, mp4, mkv, …)
  └─ FfmpegAudioExtractor ── ffmpeg ──► Data/Temp/<guid>.wav (16 kHz, mono, 16 bits)
       └─ ProcessWorkerClient ── Voicer.UI --worker --parent <pid> transcribe --model … --audio … --device <n>
            └─ WorkerHost ─ WhisperSpeechEngine (Whisper.net, whisper.cpp)
                 stdout : un objet JSON par ligne : ModelLoaded, Progress, Segment…, Done | Error
       ◄─ Transcript (segments, langue détectée) ── le WAV est supprimé
```

`TranscriptionRunner` conduit les étapes ; `TranscriptionViewModel` affiche l'étape et sa progression, conserve le
texte dans l'historique et l'ouvre dans la fenêtre de l'entrée, qui le relit depuis la base de données et l'exporte en
Markdown (`MarkdownTranscriptWriter`).

`WhisperSpeechEngine` décode chaque fenêtre de 30 secondes du son séparément (`n_max_text_ctx = 0`). Par défaut,
whisper.cpp donne à chaque fenêtre le texte des précédentes comme prompt, et `no_context` n'efface ce texte qu'avant la
première fenêtre : une phrase que le modèle a répétée une fois, ou inventée sur un silence, revient alors dans chaque
fenêtre suivante jusqu'à la fin d'un long fichier et chasse la vraie parole. Les fenêtres sont décodées par recherche
en faisceau de largeur 5, comme le fait `whisper-cli`, avec le repli sur une température plus élevée (pas de 0,2) pour
une fenêtre qui sort répétitive (entropie inférieure à 2,4) ou incertaine (log-probabilité moyenne inférieure à -1).
`RepetitionGuard` écarte un morceau de trois mots ou plus qui répète mot pour mot l'un des trois derniers morceaux.

Avant le modèle, un détecteur de parole trouve où quelqu'un parle : Silero VAD 6.2.0 au format ggml
(`SpeechDetectorModel`, 0,9 Mo, figé comme tout téléchargement, conservé dans `Data/Models/silero-vad`), exécuté par
whisper.cpp sur le processeur. `SpeechMap` relie les plages de parole avec des pauses de 0,3 s, le modèle ne reconnaît
qu'elles, et les temps de chaque morceau sont ramenés à ceux du fichier. Le silence, la musique et le bruit
n'atteignent jamais le modèle, qui n'invente donc rien pour eux (Whisper écrit « Thank you. » sur un silence), et une
longue pause ne coûte pas de temps. Un fichier sans parole donne un texte vide. Le détecteur arrive avec le premier
modèle Whisper ; un modèle téléchargé avant le reçoit à la première reconnaissance (`SpeechDetectorSource`). S'il ne
peut pas être téléchargé, tout le son est reconnu et la page le signale.

## Pourquoi un processus de travail

whisper.cpp tourne en code natif, sur les pilotes de la carte graphique. Un pilote qui échoue (sur la machine de
développement, un GPU AMD intégré plante dans Vulkan pendant le chargement d'un modèle) met fin à tout le processus.
Le moteur ne tourne donc jamais dans la fenêtre : le programme se relance lui-même avec `--worker` pour chaque commande,
lit ses lignes JSON et transforme un échec en message (`WorkerSession`). Le processus de travail suit l'identifiant de
processus donné avec `--parent` et s'arrête quand l'application se termine. Annuler une reconnaissance met fin au
processus de travail immédiatement.

## Périphériques

`GgmlDeviceProbe` demande à la bibliothèque ggml du runtime chargé par Whisper.net (`ggml_backend_dev_*`) ses
périphériques, avec leur mémoire libre et totale. whisper.cpp numérote les cartes graphiques (dédiées et intégrées)
dans l'ordre de ce registre, de sorte que l'index affiché est exactement le `GpuDevice` que prend le moteur ; le
processeur est `-1`. Les runtimes sont celui du CPU (avec Metal sur Apple silicon) et Vulkan (Windows et Linux,
n'importe quelle carte graphique). Une carte dédiée est présélectionnée, puis une intégrée, puis le processeur. Si le
processus de travail ne peut pas lister les périphériques, le processeur est proposé seul.

## Modèles et outils

Le catalogue (`SpeechModelCatalog`) contient les modèles Whisper multilingues éprouvés dans les fichiers ggml de
whisper.cpp, du plus lourd au plus léger : large-v3, large-v3-turbo, medium et small (tiny et base perdent trop de
précision). Chaque modèle existe dans les quantifications que whisper.cpp publie pour lui (`ModelQuantization`) : F16
(pleine précision), Q8 et Q5 ; large-v3 n'a pas de Q8. Chaque fichier est figé par taille et SHA-256. Les réglages
montrent une carte par modèle avec un choix de quantification ; un fichier est téléchargé dans le dossier de sa
famille, `Data/Models/<family>`, si bien que plusieurs quantifications d'un même modèle cohabitent, et la
reconnaissance liste chaque fichier téléchargé sous la forme `<nom> <quantification>`. La partie reçue attend dans
`<file>.part`, de sorte qu'un téléchargement interrompu reprend, et le fichier n'est utilisé qu'une fois sa somme de
contrôle vérifiée. FFmpeg s'installe de la même façon sous Windows (le build essentials 9.0.2 de Gyan dans
`Data/Tools/FFmpeg`) ; Linux et macOS utilisent le FFmpeg du système, trouvé dans `PATH` et aux emplacements habituels
des gestionnaires de paquets.

Les réglages gardent les modèles à l'écart : une ligne par catalogue indique combien de modèles sont téléchargés, et
son bouton ouvre une fenêtre avec une carte par modèle (`ModelCatalogViewModel`, `ModelsWindow`). Un modèle téléchargé
a une carte éclairée marquée comme reçue ; un modèle qui tourne bien sur le seul processeur (Whisper Small, Kokoro)
l'affiche comme un avantage. Les modèles de voix et llama.cpp sont décrits dans [Synthèse vocale](#synthèse-vocale).

## Historique

Chaque texte reconnu va dans la base SQLite du dossier de données, `Data/voicer.db` (`SqliteTranscriptHistory` via
Microsoft.Data.Sqlite) : une ligne de `records` par reconnaissance (fichier, modèle, périphérique, langue, heure avec
son décalage UTC, durée, le début du texte) et ses fragments dans `segments`. La version du schéma est conservée dans
`PRAGMA user_version` ; un fichier d'une version plus récente est refusé. Une entrée sait comment elle a été faite
(`kind`, version de schéma 5) : une reconnaissance ou une voix générée ; l'onglet Historique marque ainsi chaque entrée
et les liste de la plus récente à la plus ancienne ; une entrée s'ouvre dans sa fenêtre, lue depuis la base, et peut
être supprimée après confirmation. La suppression est physique : `secure_delete` écrase les lignes supprimées et
`auto_vacuum = INCREMENTAL` rend les pages libérées au système de fichiers, de sorte que le texte n'est plus dans le
fichier. Le stockage formule ses échecs comme des `IOException` avec un texte localisé ; les pages les affichent.

Après une reconnaissance, `RecordKeeper` compresse la première piste son du fichier avec FFmpeg en Opus (mono, 48 kHz,
32 kbit/s, environ 14 Mo par heure, `Data/Temp/Audio`) et la conserve avec le texte dans une seule transaction : la
table `audio` (version de schéma 2), lue et écrite en flux avec `SqliteBlob`, jamais entière en mémoire. Un son qui ne
peut pas être compressé (sans `libopus` dans le FFmpeg du système, par exemple) laisse le texte conservé sans lui et une
note sur la page. La durée d'une entrée avec son est la durée du son.

La fenêtre d'une entrée a le lecteur en haut et le texte en dessous. `RecordViewer` écrit le son dans un fichier
temporaire tant que la fenêtre est ouverte. `AudioPlayer` lance FFmpeg (`FfmpegPcmDecoder`) à partir du moment choisi
à la vitesse choisie (le filtre `atempo` garde la hauteur de la voix ; de deux fois plus lent à deux fois plus rapide)
et passe ses échantillons bruts (16 bits mono 48 kHz) à OpenAL Soft (`OpenAlOutput` : une source avec quatre tampons
de 100 ms) ; ses quelques fonctions sont liées par `OpenAlNative`, la bibliothèque native vient du paquet
`Silk.NET.OpenAL.Soft.Native`. Un saut ou une nouvelle vitesse lance une nouvelle exécution du décodeur ; la position
est le début de l'exécution plus ce que le périphérique a joué, multiplié par la vitesse. Les boutons sautent de dix
secondes en arrière et en avant ; le curseur est appliqué au rafraîchissement suivant de la fenêtre (cinq fois par
seconde), de sorte que le faire glisser ne lance pas FFmpeg pour chaque pixel.

## Résumés

Un résumé est écrit par un modèle de langage sur un serveur que fait tourner l'utilisateur : LM Studio, Ollama,
llama.cpp (`llama-server`), Jan ou tout autre doté de l'API compatible OpenAI. Les réglages conservent son adresse, une
clé facultative, le modèle et le contexte avec lequel il est chargé (`summary.*` dans `Data/settings.json`) ; les
modèles sont listés par `GET models`. Le moteur vocal n'est alors plus en mémoire : son processus de travail s'est
terminé avec la reconnaissance.

`Summarizer` (Core) adapte le texte au contexte : un texte qui tient est résumé en une requête ; un texte plus long est
découpé par `TextChunker` (lignes, puis phrases, puis mots) en parties d'environ 2,5 caractères par jeton, chaque partie
donne des notes, et les notes sont réunies en résumé (d'abord par groupes quand elles sont trop longues ensemble, trois
tours au plus). Les instructions viennent du pack de langue (`Summary.Prompt.*`), de sorte que le résumé est dans la
langue de l'interface. `ReasoningFilter` retire le `<think>…</think>` des modèles qui raisonnent.
`OpenAiCompatibleClient` reçoit la réponse en flux (`stream: true`, server-sent events ; un serveur qui répond d'un
bloc est compris aussi) et formule ses échecs : un serveur qui ne répond pas, un refus avec son code et son message,
une réponse illisible.

Le résumé est conservé avec son entrée (`summaries`, version de schéma 3 : texte, modèle, heure) et remplacé par un
nouveau ; il part avec l'entrée. La page Transcription propose « Résumer le texte » après une reconnaissance et ouvre la
fenêtre de l'entrée quand il est prêt ; la fenêtre affiche le résumé sous le texte, l'écrit (à nouveau) et l'affiche au
fur et à mesure qu'il arrive. L'export en Markdown place le résumé avant le texte.

## Recherche et questions

L'onglet Historique cherche dans les entrées et répond à des questions à leur sujet (`Core/Search`). Quand une entrée
est conservée, ses fragments sont regroupés en passages d'environ 600 caractères avec leurs moments
(`PassageBuilder`) : l'unité que trouve la recherche, que lit le modèle et vers laquelle saute le lecteur (version de
schéma 4 : `passages` ; les entrées conservées avant sont indexées lors de la mise à niveau).

- **Par mots** : l'index FTS5 `passages_fts` (`unicode61`, diacritiques supprimés, classé par BM25). `FullTextQuery`
  transforme ce que l'utilisateur a tapé en requête sûre : les mots entre guillemets forment une expression ; les
  autres mots sont cherchés par leur début sans la ou les deux dernières lettres, de sorte que d'autres formes
  correspondent (*договоров* trouve *договор*) ; n'importe quel mot suffit, et davantage de mots classent plus haut.
  Seuls les lettres et les chiffres atteignent l'index.
- **Par le sens** : un modèle d'embeddings sur le même serveur (`POST embeddings`), choisi dans les réglages à côté du
  modèle des résumés ; un modèle multilingue (bge-m3, multilingual-e5) pour une parole qui n'est pas en anglais. Avant
  une recherche, chaque passage sans vecteur de ce modèle en reçoit un, seize par requête (l'indexation est affichée) ;
  les vecteurs sont normalisés et conservés par modèle dans `embeddings`, de sorte que changer de modèle réindexe sans
  perdre les anciens. Les plus proches sont trouvés par cosinus en mémoire : des milliers de passages prennent des
  millisecondes et aucune extension native n'est nécessaire. `EmbeddingText` ajoute les préfixes qu'attendent
  certaines familles (`query:`/`passage:` de e5, `search_query:`/`search_document:` de nomic).
- **Fusion** : les deux listes sont fusionnées par Reciprocal Rank Fusion (k = 60), comme la recherche hybride
  d'Elasticsearch. Sans modèle d'embeddings, la recherche se fait par mots seulement.
- **Questions** (génération augmentée par récupération, `RecordQuestions`) : les meilleurs passages qui tiennent dans le
  contexte sont donnés au modèle des résumés, numérotés avec leur fichier, leur date et leur heure ; les instructions du
  pack de langue lui disent de répondre uniquement à partir d'eux et de les citer comme [1], [2]. La réponse arrive en
  flux ; ses sources sont affichées en dessous. Rien de trouvé, pas de requête.

Un passage trouvé ouvre son entrée avec le lecteur au début du passage. Les passages, leur index (FTS5
`secure-delete`) et leurs vecteurs partent avec l'entrée et ne restent pas dans le fichier.

Certains serveurs placent toute la réponse d'un modèle dans `reasoning_content` quand le modèle ne ferme jamais son
raisonnement ; `OpenAiCompatibleClient` la prend alors plutôt que de ne rien renvoyer.

## Synthèse vocale

L'onglet Synthèse vocale lit un texte avec un modèle de voix libre sur cet ordinateur, pour une narration allant
jusqu'à plusieurs dizaines de minutes. Les modèles (`VoiceModelCatalog`) sont les fichiers GGUF que le projet llama.cpp
convertit et publie lui-même (ggml-org sur Hugging Face), chacun figé par taille et SHA-256 à une révision fixe de son
dépôt : Qwen3-TTS 1.7B Base (Apache-2.0, dix langues, la voix d'un échantillon), Chatterbox Turbo (MIT, anglais, une
voix intégrée ou un échantillon), Kokoro 82M (Apache-2.0, anglais, 28 voix fournies, rapide sur le processeur) et
Chatterbox Multilingual (MIT, 23 langues). Un modèle, ce sont deux fichiers, ses poids et son projecteur (`mmproj` : le
codec audio, les voix, l'encodeur du locuteur), téléchargés dans `Data/Models/<id>`.

Ils tournent dans llama.cpp (MIT). L'application en installe une version figée (`LlamaCppPackage`) dans
`Data/Tools/LlamaCpp/<release>` : le build Vulkan sous Windows et Linux x64, le build Metal sous macOS arm64 ; l'archive
est décompressée à côté, chaque entrée est vérifiée pour rester dans le dossier, et le tout est mis en place d'un bloc.
Un llama.cpp du système n'est pas pris : les modèles ont besoin de la version avec laquelle ils ont été vérifiés.

```
texte
  └─ SpeechScript : une ligne est un paragraphe, un paragraphe est coupé entre les phrases en morceaux de 250 caractères au plus
       ├─ échantillon (facultatif) : FfmpegVoiceSampleExtractor ── 30 premières s, mono, WAV 24 kHz
       └─ pour chaque morceau : LlamaTtsSynthesizer ── llama-tts -m … -mm … -p <morceau> --output part-N.wav
                     [--tts-lang] [--tts-voice] [--tts-speaker-file] [--device VulkanN | none]
  └─ WaveJoiner : les échantillons copiés tels quels, 250 ms de silence entre les phrases, 650 ms entre les paragraphes
       ◄─ Data/Temp/Generation/<guid>/<premiers mots>.wav + un fragment par morceau avec ses moments
```

Chaque morceau est lu par un processus à part, lancé sans shell : le modèle est chargé, parle et quitte la mémoire avec
le processus, de sorte qu'il n'en reste rien après la synthèse, et un échec natif d'un pilote vidéo ne met fin qu'à ce
processus ; ses dernières lignes de journal formulent l'échec. Annuler met fin au processus immédiatement et supprime
le dossier de la synthèse. Le périphérique est celui de la reconnaissance : l'index d'une carte graphique parmi les GPU
de ggml est le `VulkanN` de llama.cpp ; le processeur reçoit `--device none -ngl 0` ; sous macOS, Metal prend son
périphérique lui-même.

Le résultat se joue sur la page (le lecteur de l'historique) et s'enregistre en WAV là où l'utilisateur le choisit ; il
va dans l'historique comme une reconnaissance, son son compressé en Opus, marqué comme synthèse. Le modèle, la langue,
la voix et l'échantillon de la dernière synthèse sont mémorisés (`generation.*` dans `Data/settings.json`), de sorte
qu'une série d'enregistrements garde une même voix.

## Dossier des données

`AppPaths` choisit le dossier des données : `--data <folder>` ; sinon `Data` à côté du marqueur `voicer.portable` d'un
paquet ; sinon `Data` à côté du programme ; et quand ce dossier n'est pas accessible en écriture (Program Files,
`/opt`), le profil de l'utilisateur (`%LOCALAPPDATA%\Voicer\Data`, `~/.local/share/Voicer/Data`).

Un échec de la fenêtre que rien ne prévoyait ne met pas fin à l'application : `App` l'intercepte sur le dispatcher,
l'écrit dans `Data/errors.log` (`ErrorLog` : le moment, le type, le message et la pile ; le fichier recommence au-delà
de 1 Mo) et affiche un avis avec le chemin du journal, un seul à la fois.

## Interface

Un seul thème sombre : la palette de RCynQ avec plus de lumière : l'accent luit autour des boutons principaux, de
l'onglet choisi, des barres de progression et d'un fichier glissé au-dessus de la zone de dépôt. La langue est choisie
au premier lancement d'après la langue du système et mémorisée ; en changer reformule chaque écran aussitôt
(`LocalizationSource`, `LocalizedViewModel`).
