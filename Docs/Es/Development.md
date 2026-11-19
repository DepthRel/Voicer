# Desarrollo de Voicer

[English](../En/Development.md) · [Deutsch](../De/Development.md) · **Español** · [Français](../Fr/Development.md) · [Português](../Pt/Development.md) · [Русский](../Ru/Development.md)

## Compilar, probar, ejecutar

```bash
dotnet build Voicer.slnx                     # se esperan 0 avisos: las reglas del código son errores
dotnet test Voicer.slnx                      # xUnit 2 sobre VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

En VS Code, el perfil «Voicer» de `.vscode/launch.json` guarda los datos de la copia de depuración en
`.vscode/dev/data` (ignorado por git), así que un modelo descargado sobrevive a una compilación limpia. El proceso de
trabajo se puede probar por separado:

```bash
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker transcribe --model <modelo ggml> --audio <wav de 16 kHz> --device 0
```

## Reglas

Las reglas del código están en [AGENTS.md](../../AGENTS.md); la compilación y `Tests/Voicer.Conventions.Tests` las
hacen cumplir. `Tests/Voicer.Localization.Tests` comprueba que todos los paquetes tienen las mismas claves y marcadores,
que existe cada clave que usan el código y las vistas, y que no hay texto en ruso en las fuentes.

## Añadir un idioma

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` con todas las claves de `en.json` (se detecta solo).
2. La bandera: un bloque en `Scripts/Assets/generate-flags.ps1` y después ejecutarlo
   (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. El instalador: una línea en `[Languages]` y bloques en `[Messages]`, `[CustomMessages]` de `Scripts/App/voicer.iss`
   (UTF-8 con BOM), y `Scripts/Shared/License/intro.<code>.txt`.
4. Las notas de los paquetes: `Scripts/App/Notes/README.<code>.txt`; las entradas de menú en
   `Scripts/App/build-deb.sh` y `publish-common.sh`.
5. La documentación: `Docs/<Code>/` con los mismos archivos que `Docs/En` (ver [Documentación](#documentación)).
6. La página del proyecto: una entrada en `list` y `docs` de `Sources/Apps/Voicer.Pages/assets/languages.js`, su
   bandera `assets/flags/<code>.svg` y una tabla en `assets/site.js`.

## Documentación

Los documentos se guardan por idioma: `Docs/En` es el conjunto principal; `Docs/De`, `Docs/Es`, `Docs/Fr`, `Docs/Pt` y
`Docs/Ru` contienen el mismo conjunto en alemán, español, francés, portugués y ruso. La línea bajo el título de cada
documento enlaza el mismo documento en los demás idiomas.

- los archivos y sus nombres son los mismos en todos los idiomas; un cambio de sentido entra en todos los idiomas a la
  vez;
- los enlaces desde fuera (el README) llevan a `Docs/En`; la página del proyecto abre los documentos en el idioma
  elegido en ella.

## Añadir un modelo

Un registro por cuantización en `SpeechModelCatalog` (familia = nombre de la carpeta, cuantización, dirección HTTPS,
tamaño exacto, SHA-256), en el orden del catálogo: las familias de la más pesada a la más ligera, las cuantizaciones de
la más precisa a la más pequeña. El texto `Model.<familia>.Description` va en todos los paquetes; una cuantización
nueva necesita además `Quantization.<valor>.Name` y `.Description`. La licencia del modelo va en
`THIRD-PARTY-NOTICES.txt`.

Un modelo de voz es un registro en `VoiceModelCatalog`: su id (el nombre de la carpeta), los pesos y el proyector
(`mmproj`) de un GGUF publicado para llama.cpp, cada uno de una revisión fija con su tamaño exacto y su SHA-256, sus
idiomas (el predeterminado primero), las voces que trae, si habla con la voz de una muestra, si la necesita y si
funciona bien en el procesador. El texto `VoiceModel.<id>.Description` y `SpeechLanguage.<code>` de un idioma nuevo
van en todos los paquetes; la versión de llama.cpp en `LlamaCppPackage` debe admitir el modelo. Una versión nueva de
llama.cpp se fija allí con el tamaño y el SHA-256 de cada archivo comprimido.

## Paquetes y la página del proyecto

Ver [Scripts/README.md](../../Scripts/README.md). Un paquete solo lleva las bibliotecas nativas de su propia
plataforma; los modelos, FFmpeg y llama.cpp no van en él, el usuario los descarga desde los ajustes.

GitHub Actions: `ci.yml` compila y prueba en Windows y Linux con cada push a `main` y cada pull request;
`packages.yml` compila todos los paquetes a mano o para una etiqueta de versión y los adjunta a un borrador de versión;
`pages.yml` publica la página del proyecto (`Sources/Apps/Voicer.Pages`, ver su README) con cada cambio en ella en
`main`.
