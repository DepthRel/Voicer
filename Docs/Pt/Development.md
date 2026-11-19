# Desenvolvimento do Voicer

[English](../En/Development.md) · [Deutsch](../De/Development.md) · [Español](../Es/Development.md) · [Français](../Fr/Development.md) · **Português** · [Русский](../Ru/Development.md)

## Compilar, testar, executar

```bash
dotnet build Voicer.slnx                     # esperam-se 0 avisos: as regras do código são erros
dotnet test Voicer.slnx                      # xUnit 2 sobre VSTest
dotnet run --project Sources/Apps/Voicer.UI -- --data .vscode/dev/data
```

No VS Code, o perfil «Voicer» de `.vscode/launch.json` guarda os dados da cópia de depuração em `.vscode/dev/data`
(ignorado pelo git), por isso um modelo transferido sobrevive a uma compilação limpa. O processo de trabalho pode ser
experimentado sozinho:

```bash
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker devices
dotnet Sources/Apps/Voicer.UI/bin/Debug/net10.0/Voicer.UI.dll --worker transcribe --model <modelo ggml> --audio <wav de 16 kHz> --device 0
```

## Regras

As regras do código estão em [AGENTS.md](../../AGENTS.md); a compilação e `Tests/Voicer.Conventions.Tests`
impõem-nas. `Tests/Voicer.Localization.Tests` verifica que todos os pacotes têm as mesmas chaves e marcadores, que
existe cada chave usada pelo código e pelas vistas, e que não há texto em russo nas fontes.

## Adicionar um idioma

1. `Sources/Core/Voicer.Localization/Languages/<code>.json` com todas as chaves de `en.json` (é detetado sozinho).
2. A bandeira: um bloco em `Scripts/Assets/generate-flags.ps1` e depois executá-lo
   (`Sources/Apps/Voicer.UI/Assets/Flags/<code>.png`).
3. O instalador: uma linha em `[Languages]` e blocos em `[Messages]`, `[CustomMessages]` de `Scripts/App/voicer.iss`
   (UTF-8 com BOM), e `Scripts/Shared/License/intro.<code>.txt`.
4. As notas dos pacotes: `Scripts/App/Notes/README.<code>.txt`; as entradas de menu em `Scripts/App/build-deb.sh` e
   `publish-common.sh`.
5. A documentação: `Docs/<Code>/` com os mesmos ficheiros que `Docs/En` (ver [Documentação](#documentação)).
6. A página do projeto: uma entrada em `list` e `docs` de `Sources/Apps/Voicer.Pages/assets/languages.js`, a sua
   bandeira `assets/flags/<code>.svg` e uma tabela em `assets/site.js`.

## Documentação

Os documentos estão organizados por idioma: `Docs/En` é o conjunto principal; `Docs/De`, `Docs/Es`, `Docs/Fr`,
`Docs/Pt` e `Docs/Ru` têm o mesmo conjunto em alemão, espanhol, francês, português e russo. A linha sob o título de
cada documento liga ao mesmo documento nos outros idiomas.

- os ficheiros e os seus nomes são os mesmos em todos os idiomas; uma mudança de sentido entra em todos os idiomas ao
  mesmo tempo;
- as ligações de fora (o README) levam a `Docs/En`; a página do projeto abre os documentos no idioma escolhido nela.

## Adicionar um modelo

Um registo por quantização em `SpeechModelCatalog` (família = nome da pasta, quantização, endereço HTTPS, tamanho
exato, SHA-256), pela ordem do catálogo: as famílias da mais pesada à mais leve, as quantizações da mais precisa à
mais pequena. O texto `Model.<família>.Description` vai para todos os pacotes; uma quantização nova precisa ainda de
`Quantization.<valor>.Name` e `.Description`. A licença do modelo vai para `THIRD-PARTY-NOTICES.txt`.

Um modelo de voz é um registo em `VoiceModelCatalog`: o seu id (o nome da pasta), os pesos e o projetor (`mmproj`) de
um GGUF publicado para o llama.cpp, cada um de uma revisão fixa com o seu tamanho exato e SHA-256, os seus idiomas (o
predefinido primeiro), as vozes que traz, se fala com a voz de uma amostra, se precisa de uma e se funciona bem no
processador. O texto `VoiceModel.<id>.Description` e `SpeechLanguage.<code>` de um idioma novo vão para todos os
pacotes; a versão do llama.cpp em `LlamaCppPackage` tem de suportar o modelo. Uma versão nova do llama.cpp é fixada
aí com o tamanho e o SHA-256 de cada arquivo.

## Pacotes e a página do projeto

Ver [Scripts/README.md](../../Scripts/README.md). Um pacote só leva as bibliotecas nativas da sua própria plataforma;
os modelos, o FFmpeg e o llama.cpp não vão nele, o utilizador transfere-os a partir das definições.

GitHub Actions: `ci.yml` compila e testa em Windows e Linux a cada push para `main` e a cada pull request;
`packages.yml` compila todos os pacotes à mão ou para uma etiqueta de versão e anexa-os a um rascunho de versão;
`pages.yml` publica a página do projeto (`Sources/Apps/Voicer.Pages`, ver o seu README) a cada alteração nela em
`main`.
