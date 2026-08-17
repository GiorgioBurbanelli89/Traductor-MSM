# Traductor MSM · MSM Translator

Traductor de escritorio (WPF, .NET 8) con **interfaz bilingüe Español / English**, traducción
en vivo y **pronunciación con voz neural** en 15 idiomas.

A desktop translator (WPF, .NET 8) with a **bilingual Spanish / English UI**, live translation
and **neural-voice pronunciation** in 15 languages.

---

## Español

- **Selecciona texto en cualquier app** (LinkedIn, Telegram, navegador) con el **Monitor** encendido:
  el texto cae en la caja, se traduce al idioma que elijas y se pronuncia.
- **15 idiomas**: español, inglés, alemán, ruso, portugués, francés, italiano, chino, japonés,
  coreano, árabe, holandés, polaco, turco e hindi.
- **Voz neural** hombre/mujer por idioma (Microsoft Edge / edge-tts), con respaldo Google TTS.
- **Textos largos**: la traducción y la voz procesan el texto completo.
- Atajo global **Ctrl+Shift+T** para traducir el texto seleccionado.
- Cambia el idioma de la interfaz con el selector **🌐 ES / EN** (abajo a la derecha).

## English

- **Select text in any app** (LinkedIn, Telegram, browser) with the **Monitor** on: the text is
  captured, translated to your chosen language and spoken aloud.
- **15 languages**: Spanish, English, German, Russian, Portuguese, French, Italian, Chinese,
  Japanese, Korean, Arabic, Dutch, Polish, Turkish and Hindi.
- **Neural voice** (male/female) per language (Microsoft Edge / edge-tts), with a Google TTS fallback.
- **Long texts**: both translation and voice handle the whole text.
- Global shortcut **Ctrl+Shift+T** to translate the selected text.
- Switch the interface language with the **🌐 ES / EN** selector (bottom-right).

---

## Build

```bash
dotnet build -c Release
```

Requiere **.NET 8 Runtime**. Las voces neurales usan `edge-tts` (Python); si no está, cae a Google TTS.
Requires the **.NET 8 Runtime**. Neural voices use `edge-tts` (Python); if missing, it falls back to Google TTS.
