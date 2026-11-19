// The texts of the page in every language but English (English is written in index.html), the language switch and the
// download buttons. A table must have every data-t key of index.html, plus page.title, nav.language, dl.version and
// dl.for.
(function () {
  'use strict';

  var languages = window.VoicerLanguages;

  var texts = {
    de: {
      'page.title': 'Voicer: Sprache zu Text und Text zu Sprache auf Ihrem Computer',
      'nav.language': 'Sprache',
      'nav.features': 'Funktionen',
      'nav.download': 'Download',
      'hero.title': 'Sprache zu Text und zurück.<br><em>Auf Ihrem eigenen Computer.</em>',
      'hero.lead': 'Voicer verwandelt die Sprache von Audio- und Videodateien in Text und einen Text in natürliche Sprache. Whisper und freie Sprachmodelle laufen auf Ihrer Grafikkarte oder Ihrem Prozessor: keine Cloud, kein Konto, nichts verlässt den Rechner.',
      'hero.download': 'Voicer herunterladen',
      'hero.features': 'Was es kann',
      'hero.platforms': 'Frei und quelloffen · Windows · Linux · macOS',
      'features.title': 'Was Voicer macht',
      'f.stt.title': 'Sprache zu Text',
      'f.stt.text': 'Ziehen Sie eine MP3-, WAV-, MP4-, MKV- oder andere Mediendatei hinein und erhalten Sie ihren Text, in rund hundert Sprachen. Export nach Markdown.',
      'f.tts.title': 'Text zu Sprache',
      'f.tts.text': 'Fügen Sie einen Text ein, wählen Sie eine Stimme oder geben Sie eine Probe Ihrer eigenen, und erhalten Sie eine Lesung von bis zu mehreren Dutzend Minuten.',
      'f.history.title': 'Verlauf und Suche',
      'f.history.text': 'Jeder Text und jede Lesung wird mit ihrem Ton gespeichert. Finden Sie einen Eintrag nach Wörtern oder nach Bedeutung und hören Sie ab genau dieser Stelle.',
      'f.ask.title': 'Zusammenfassungen und Fragen',
      'f.ask.text': 'Ein Sprachmodell auf Ihrem eigenen Server fasst eine lange Besprechung zusammen und beantwortet Fragen zu Ihren Einträgen, mit Quellenangaben.',
      'f.device.title': 'Ihre Grafikkarte oder Ihr Prozessor',
      'f.device.text': 'Vulkan unter Windows und Linux, Metal auf Apple Silicon. Keine Grafikkarte? Die leichten Modelle laufen gut auf dem Prozessor.',
      'f.private.title': 'Privat von Grund auf',
      'f.private.text': 'Aufnahmen verlassen nie den Computer. Modelle werden einmal heruntergeladen, per SHA-256 geprüft und neben dem Programm aufbewahrt.',
      'dl.title': 'Download',
      'dl.version': 'Version ',
      'dl.for': 'Download für ',
      'dl.button': 'Herunterladen',
      'dl.windows.text': 'Windows 10 oder neuer, x64. Der Installer braucht keine Administratorrechte.',
      'dl.windows.other': 'oder das portable .zip-Archiv',
      'dl.linux.text': 'x64. Ein .deb-Paket für Debian und Ubuntu; FFmpeg aus Ihrer Paketverwaltung.',
      'dl.linux.other': 'oder das portable .tar.gz-Archiv',
      'dl.macos.text': 'Apple Silicon und Intel; FFmpeg aus Homebrew. Die Sprachausgabe braucht Apple Silicon.',
      'dl.macos.other': 'oder für Macs mit Intel-Prozessor',
      'foot.architecture': 'Architektur',
      'foot.source': 'Quellcode',
      'foot.releases': 'Alle Versionen'
    },
    es: {
      'page.title': 'Voicer: de voz a texto y de texto a voz en tu equipo',
      'nav.language': 'Idioma',
      'nav.features': 'Funciones',
      'nav.download': 'Descargar',
      'hero.title': 'De voz a texto y de vuelta.<br><em>En tu propio equipo.</em>',
      'hero.lead': 'Voicer convierte en texto el habla de archivos de audio y vídeo, y un texto en una voz natural. Whisper y modelos de voz libres funcionan en tu tarjeta gráfica o en tu procesador: sin nube, sin cuenta, nada sale del equipo.',
      'hero.download': 'Descargar Voicer',
      'hero.features': 'Qué sabe hacer',
      'hero.platforms': 'Libre y de código abierto · Windows · Linux · macOS',
      'features.title': 'Qué hace Voicer',
      'f.stt.title': 'De voz a texto',
      'f.stt.text': 'Suelta un MP3, WAV, MP4, MKV o cualquier otro archivo multimedia y obtén su texto, en unos cien idiomas. Exportación a Markdown.',
      'f.tts.title': 'De texto a voz',
      'f.tts.text': 'Pega un texto, elige una voz o da una muestra de la tuya, y obtén una narración de hasta decenas de minutos.',
      'f.history.title': 'Historial y búsqueda',
      'f.history.text': 'Cada texto y cada narración se guardan con su sonido. Encuentra un registro por palabras o por significado y escúchalo desde ese momento.',
      'f.ask.title': 'Resúmenes y preguntas',
      'f.ask.text': 'Un modelo de lenguaje en tu propio servidor resume una reunión larga y responde preguntas sobre tus registros, con referencias.',
      'f.device.title': 'Tu tarjeta gráfica o tu procesador',
      'f.device.text': 'Vulkan en Windows y Linux, Metal en Apple silicon. ¿Sin tarjeta gráfica? Los modelos ligeros funcionan bien en el procesador.',
      'f.private.title': 'Privado por diseño',
      'f.private.text': 'Las grabaciones nunca salen del equipo. Los modelos se descargan una vez, se comprueban por SHA-256 y se guardan junto al programa.',
      'dl.title': 'Descargar',
      'dl.version': 'versión ',
      'dl.for': 'Descargar para ',
      'dl.button': 'Descargar',
      'dl.windows.text': 'Windows 10 o posterior, x64. El instalador no necesita derechos de administrador.',
      'dl.windows.other': 'o el archivo portátil .zip',
      'dl.linux.text': 'x64. Un paquete .deb para Debian y Ubuntu; FFmpeg de tu gestor de paquetes.',
      'dl.linux.other': 'o el archivo portátil .tar.gz',
      'dl.macos.text': 'Apple silicon e Intel; FFmpeg de Homebrew. La generación de voz necesita Apple silicon.',
      'dl.macos.other': 'o para Mac con procesador Intel',
      'foot.architecture': 'Arquitectura',
      'foot.source': 'Código fuente',
      'foot.releases': 'Todas las versiones'
    },
    fr: {
      'page.title': 'Voicer : de la parole au texte et du texte à la parole sur votre ordinateur',
      'nav.language': 'Langue',
      'nav.features': 'Fonctions',
      'nav.download': 'Télécharger',
      'hero.title': 'De la parole au texte, et retour.<br><em>Sur votre propre ordinateur.</em>',
      'hero.lead': 'Voicer transforme en texte la parole des fichiers audio et vidéo, et un texte en voix naturelle. Whisper et des modèles de voix libres tournent sur votre carte graphique ou votre processeur : pas de cloud, pas de compte, rien ne quitte la machine.',
      'hero.download': 'Télécharger Voicer',
      'hero.features': 'Ce qu\'il sait faire',
      'hero.platforms': 'Libre et open source · Windows · Linux · macOS',
      'features.title': 'Ce que fait Voicer',
      'f.stt.title': 'De la parole au texte',
      'f.stt.text': 'Déposez un MP3, WAV, MP4, MKV ou tout autre fichier multimédia et obtenez son texte, dans une centaine de langues. Export en Markdown.',
      'f.tts.title': 'Du texte à la parole',
      'f.tts.text': 'Collez un texte, choisissez une voix ou donnez un échantillon de la vôtre, et obtenez une narration de plusieurs dizaines de minutes.',
      'f.history.title': 'Historique et recherche',
      'f.history.text': 'Chaque texte et chaque narration sont conservés avec leur son. Trouvez une entrée par les mots ou par le sens et écoutez-la à partir de ce moment.',
      'f.ask.title': 'Résumés et questions',
      'f.ask.text': 'Un modèle de langage sur votre propre serveur résume une longue réunion et répond aux questions sur vos entrées, avec des références.',
      'f.device.title': 'Votre carte graphique ou votre processeur',
      'f.device.text': 'Vulkan sous Windows et Linux, Metal sur Apple silicon. Pas de carte graphique ? Les modèles légers tournent bien sur le processeur.',
      'f.private.title': 'Privé par conception',
      'f.private.text': 'Les enregistrements ne quittent jamais l\'ordinateur. Les modèles sont téléchargés une fois, vérifiés par SHA-256 et conservés à côté du programme.',
      'dl.title': 'Télécharger',
      'dl.version': 'version ',
      'dl.for': 'Télécharger pour ',
      'dl.button': 'Télécharger',
      'dl.windows.text': 'Windows 10 ou plus récent, x64. L\'installateur n\'a pas besoin des droits d\'administrateur.',
      'dl.windows.other': 'ou l\'archive portable .zip',
      'dl.linux.text': 'x64. Un paquet .deb pour Debian et Ubuntu ; FFmpeg depuis votre gestionnaire de paquets.',
      'dl.linux.other': 'ou l\'archive portable .tar.gz',
      'dl.macos.text': 'Apple silicon et Intel ; FFmpeg depuis Homebrew. La synthèse vocale nécessite Apple silicon.',
      'dl.macos.other': 'ou pour les Mac à processeur Intel',
      'foot.architecture': 'Architecture',
      'foot.source': 'Code source',
      'foot.releases': 'Toutes les versions'
    },
    pt: {
      'page.title': 'Voicer: de fala para texto e de texto para fala no seu computador',
      'nav.language': 'Idioma',
      'nav.features': 'Funções',
      'nav.download': 'Transferir',
      'hero.title': 'De fala para texto, e de volta.<br><em>No seu próprio computador.</em>',
      'hero.lead': 'O Voicer transforma em texto a fala de ficheiros de áudio e vídeo, e um texto numa voz natural. O Whisper e modelos de voz livres correm na sua placa gráfica ou no seu processador: sem nuvem, sem conta, nada sai da máquina.',
      'hero.download': 'Transferir o Voicer',
      'hero.features': 'O que sabe fazer',
      'hero.platforms': 'Livre e de código aberto · Windows · Linux · macOS',
      'features.title': 'O que o Voicer faz',
      'f.stt.title': 'De fala para texto',
      'f.stt.text': 'Largue um MP3, WAV, MP4, MKV ou qualquer outro ficheiro multimédia e obtenha o seu texto, em cerca de cem idiomas. Exportação para Markdown.',
      'f.tts.title': 'De texto para fala',
      'f.tts.text': 'Cole um texto, escolha uma voz ou dê uma amostra da sua, e obtenha uma narração de até dezenas de minutos.',
      'f.history.title': 'Histórico e pesquisa',
      'f.history.text': 'Cada texto e cada narração ficam guardados com o seu som. Encontre um registo por palavras ou por significado e ouça-o a partir desse momento.',
      'f.ask.title': 'Resumos e perguntas',
      'f.ask.text': 'Um modelo de linguagem no seu próprio servidor resume uma reunião longa e responde a perguntas sobre os seus registos, com referências.',
      'f.device.title': 'A sua placa gráfica ou o seu processador',
      'f.device.text': 'Vulkan em Windows e Linux, Metal no Apple silicon. Sem placa gráfica? Os modelos leves funcionam bem no processador.',
      'f.private.title': 'Privado por natureza',
      'f.private.text': 'As gravações nunca saem do computador. Os modelos são transferidos uma vez, verificados por SHA-256 e guardados ao lado do programa.',
      'dl.title': 'Transferir',
      'dl.version': 'versão ',
      'dl.for': 'Transferir para ',
      'dl.button': 'Transferir',
      'dl.windows.text': 'Windows 10 ou mais recente, x64. O instalador não precisa de direitos de administrador.',
      'dl.windows.other': 'ou o arquivo portátil .zip',
      'dl.linux.text': 'x64. Um pacote .deb para Debian e Ubuntu; o FFmpeg do seu gestor de pacotes.',
      'dl.linux.other': 'ou o arquivo portátil .tar.gz',
      'dl.macos.text': 'Apple silicon e Intel; o FFmpeg do Homebrew. A geração de voz precisa de Apple silicon.',
      'dl.macos.other': 'ou para Macs com processador Intel',
      'foot.architecture': 'Arquitetura',
      'foot.source': 'Código-fonte',
      'foot.releases': 'Todas as versões'
    },
    ru: {
      'page.title': 'Voicer: речь в текст и текст в речь на вашем компьютере',
      'nav.language': 'Язык',
      'nav.features': 'Возможности',
      'nav.download': 'Скачать',
      'hero.title': 'Речь в текст и обратно.<br><em>На вашем компьютере.</em>',
      'hero.lead': 'Voicer превращает речь из аудио- и видеофайлов в текст, а текст — в живую речь. Whisper и свободные голосовые модели работают на вашей видеокарте или процессоре: без облака, без аккаунта, ничего не покидает компьютер.',
      'hero.download': 'Скачать Voicer',
      'hero.features': 'Что он умеет',
      'hero.platforms': 'Свободный и открытый · Windows · Linux · macOS',
      'features.title': 'Что делает Voicer',
      'f.stt.title': 'Речь в текст',
      'f.stt.text': 'Перетащите MP3, WAV, MP4, MKV или любой другой медиафайл и получите его текст — примерно на сотне языков. Экспорт в Markdown.',
      'f.tts.title': 'Текст в речь',
      'f.tts.text': 'Вставьте текст, выберите голос или дайте образец своего — и получите озвучку длиной до десятков минут.',
      'f.history.title': 'История и поиск',
      'f.history.text': 'Каждый текст и каждая озвучка хранятся вместе со звуком. Найдите запись по словам или по смыслу и слушайте с нужного момента.',
      'f.ask.title': 'Резюме и вопросы',
      'f.ask.text': 'Языковая модель на вашем собственном сервере кратко изложит долгую встречу и ответит на вопросы по записям со ссылками на них.',
      'f.device.title': 'Ваша видеокарта или процессор',
      'f.device.text': 'Vulkan в Windows и Linux, Metal на Apple silicon. Нет видеокарты? Лёгкие модели хорошо работают на процессоре.',
      'f.private.title': 'Приватно по устройству',
      'f.private.text': 'Записи никогда не покидают компьютер. Модели скачиваются один раз, проверяются по SHA-256 и лежат рядом с программой.',
      'dl.title': 'Скачать',
      'dl.version': 'версия ',
      'dl.for': 'Скачать для ',
      'dl.button': 'Скачать',
      'dl.windows.text': 'Windows 10 или новее, x64. Установщику не нужны права администратора.',
      'dl.windows.other': 'или переносной архив .zip',
      'dl.linux.text': 'x64. Пакет .deb для Debian и Ubuntu; FFmpeg — из менеджера пакетов.',
      'dl.linux.other': 'или переносной архив .tar.gz',
      'dl.macos.text': 'Apple silicon и Intel; FFmpeg — из Homebrew. Генерации голоса нужен Apple silicon.',
      'dl.macos.other': 'или для Mac с процессором Intel',
      'foot.architecture': 'Архитектура',
      'foot.source': 'Исходный код',
      'foot.releases': 'Все выпуски'
    }
  };

  // What the HTML says is the English table.
  var english = { 'page.title': document.title, 'nav.language': 'Language', 'dl.version': 'version ', 'dl.for': 'Download for ' };
  var nodes = document.querySelectorAll('[data-t]');
  nodes.forEach(function (node) { english[node.getAttribute('data-t')] = node.innerHTML; });
  texts.en = english;

  var language = 'en';
  var release = null;
  var names = { windows: 'Windows', linux: 'Linux', macos: 'macOS' };

  function text(key) {
    return (texts[language] && texts[language][key]) || english[key] || '';
  }

  function detectSystem() {
    var agent = navigator.userAgent || '';
    if (/Android|iPhone|iPad|iPod/i.test(agent)) { return null; }
    if (/Windows/i.test(agent)) { return 'windows'; }
    if (/Macintosh|Mac OS X/i.test(agent)) { return 'macos'; }
    if (/Linux|X11/i.test(agent)) { return 'linux'; }
    return null;
  }

  // Only https addresses of a release are put on a button; anything else in the file is ignored.
  function safeUrl(value) {
    return typeof value === 'string' && /^https:\/\/[^\s"'<>]+$/.test(value) ? value : null;
  }

  // The cards lead to the files of the latest release when updates.json names them, otherwise to the releases page;
  // the card of the visitor's system is marked, and the button at the top leads to its package.
  function showDownloads() {
    var mine = detectSystem();
    document.querySelectorAll('.download').forEach(function (card) {
      var system = card.getAttribute('data-os');
      var entry = release && release[system];
      var version = entry && typeof entry.version === 'string' && /^[0-9][0-9A-Za-z.\-]{0,30}$/.test(entry.version) ? entry.version : null;
      card.querySelector('.meta').textContent = version ? text('dl.version') + version : '';
      card.querySelectorAll('a[data-field]').forEach(function (link) {
        var url = entry && safeUrl(entry[link.getAttribute('data-field')]);
        if (url) { link.setAttribute('href', url); }
      });
      card.classList.toggle('mine', system === mine);
    });

    var hero = document.getElementById('hero-download');
    if (mine) {
      var entry = release && release[mine];
      var url = entry && ((mine === 'linux' && safeUrl(entry.deb)) || safeUrl(entry.url));
      hero.querySelector('span').textContent = text('dl.for') + names[mine];
      hero.setAttribute('href', url || '#download');
    }
  }

  var picker = document.querySelector('.lang');
  var showPicker = picker && languages ? languages.mountPicker(picker, switchTo) : function () { };

  function apply(next) {
    language = texts[next] ? next : 'en';
    document.documentElement.lang = language;
    document.title = text('page.title');
    nodes.forEach(function (node) { node.innerHTML = text(node.getAttribute('data-t')); });
    if (picker) { picker.setAttribute('aria-label', text('nav.language')); }
    if (languages) {
      document.getElementById('architecture-link').setAttribute('href', languages.docUrl(language, 'Architecture.md'));
    }
    showPicker(language);
    showDownloads();
  }

  // A chosen language fades the page out and back in, so the switch reads as one change rather than a flicker.
  function switchTo(next) {
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      apply(next);
      return;
    }
    document.body.classList.add('switching');
    window.setTimeout(function () {
      apply(next);
      document.body.classList.remove('switching');
    }, 160);
  }

  apply(languages ? languages.detect() : 'en');

  // The latest release: the download buttons lead straight to its files.
  fetch('updates.json', { cache: 'no-cache' })
    .then(function (response) { return response.ok ? response.json() : null; })
    .then(function (json) { if (json && typeof json === 'object') { release = json; showDownloads(); } })
    .catch(function () { /* the buttons keep leading to the releases page */ });
})();
