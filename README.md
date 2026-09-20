# Bhagavad Gita 3D

An Android app presenting the complete Bhagavad Gita — all 18 chapters and 701 verses —
over a real-time 3D scene of Kurukshetra at dawn. Built with Unity 6 (6000.6.0f1) and URP.

---

## Layout

```
geeta/
  Gita3D/                 Unity project
  build/                  APK, AAB, and look-development renders
  keystore/gita.keystore  release signing key  ← BACK THIS UP
  _data/                  raw Gita corpus downloaded from gita/gita
  _tools/                 .NET scripts: corpus consolidation, icon generation
  android-sdk/            emulator + x86_64 system image (unused - see Known limitations)
```

## Building

The whole project is configured from code; nothing depends on hand-authored scene state.

```bash
UNITY="/c/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe"

# one-time, or after changing player/render settings
"$UNITY" -quit -batchmode -nographics -projectPath Gita3D -buildTarget Android \
         -executeMethod Gita.EditorTools.ProjectConfig.All -logFile build/config.log

# artifacts
export GITA_KEYSTORE="$PWD/keystore/gita.keystore"
export GITA_KEYSTORE_PASS='...'        # see "Signing" below
export GITA_KEY_ALIAS=gita

"$UNITY" -quit -batchmode -nographics -projectPath Gita3D -buildTarget Android \
         -executeMethod Gita.EditorTools.GitaBuild.BuildBoth -logFile build/final.log
```

`BuildAPK` and `BuildAAB` exist separately. Look-development renders come from
`Gita.EditorTools.LookDev.Render` (needs a graphics device, so omit `-nographics`).

If the font assets or TMP resources are ever missing, re-run
`Gita.EditorTools.GitaSetup.BuildFontAssets`.

## Signing

`keystore/gita.keystore` — PKCS12, alias `gita`, valid 30 years.

**Keep this file and its password.** Google Play ties an app listing to its signing key
permanently; losing the keystore means never being able to update the app under the same
listing. Back it up somewhere outside this folder.

The build reads the password from `GITA_KEYSTORE_PASS`. With no password set it silently
falls back to a debug key, which produces an installable APK but an AAB Play will reject.

## Architecture

The scene file is nearly empty — a single `AppRoot` component. Everything else (world,
camera, lighting, post-processing, the entire interface) is constructed at runtime from
code. That keeps the project diffable and reproducible, with no binary scene state to
merge or lose.

```
Scripts/
  Data/    GitaModels, GitaDatabase   corpus loading and indexing
  World/   CinematicWorld             procedural Kurukshetra set
           ProcMesh                   ground, ridges, cloth, rings, cones
           CameraDirector             named shots, blended moves
           PostFX                     runtime volume: ACES, bloom, grade, DOF
  UI/      Theme, UIKit               design tokens, runtime uGUI construction
           NativeText, ShapedText     Devanagari shaping (see below)
           HomeScreen                 title, verse of the day, progress, entry points
           ChaptersScreen             the eighteen yogas
           ReaderScreen               one verse at a time; bookmark, share, mute
           VerseListScreen            shared list body for the three below
           SearchScreen               live search over every translation
           BookmarksScreen            saved verses
           CollectionsScreen          the eight themed ways in
           LanguageScreen             language, translator, narration, daily verse
  App/     AppRoot                    boot, navigation, device tiering
           AppSettings                PlayerPrefs: edition, audio, reading position
           ReadingLog                 bookmarks, and progress as a packed bitset
           Narration                  bridge to the Android speech engine
           DailyVerse                 the morning notification
           VerseShare                 renders and shares the verse card
           AudioSession               holds the screen awake while narrating
Plugins/Android/com/gita/text/TextRasterizer.java   Devanagari shaping
Plugins/Android/com/gita/audio/Narrator.java        text-to-speech
Plugins/Android/com/gita/share/Sharer.java          image and text sharing
```

Home is always the opening screen. The language picker was briefly put in front of it
on a first run, which meant the app opened on a settings page; it is reached from the
pill at the top of Home instead.

## Devanagari

**TextMeshPro cannot shape Indic scripts.** This was verified by rendering a test sheet
(`build/shaping-test.png`): the i-matra is not reordered to the left of its consonant,
conjuncts like क्ष do not form, and reph does not rise above the following letter.
Sanskrit set in TMP is visibly wrong on every line.

Devanagari is therefore rasterised by Android's own text stack (HarfBuzz + ICU) through
`TextRasterizer.java`, which returns an alpha coverage bitmap that `ShapedText` tints and
displays. If the native path fails for any reason, `NativeText.Available` latches false
and every block falls back to TextMeshPro — degraded typography, never a crash.

The bundled fonts live in `StreamingAssets/Fonts/` so `Typeface.createFromAsset` can reach
them inside the APK.

## Content

Sanskrit, IAST transliteration and word-by-word glosses come from
[gita/gita](https://github.com/gita/gita), released into the public domain (Unlicense).
`_tools/build-data.cs` consolidates everything into `Resources/gita.json` (~1.75 MB).

**Eight translations across three languages:**

| Language | Translations |
|---|---|
| English | Sivananda, Purohit *(poetic)*, Gambirananda, Adidevananda, Sankaranarayan |
| हिन्दी | Tejomayananda, Ramsukhdas |
| Français | Émile Senart, 1922 |

The French comes from [French Wikisource](https://fr.wikisource.org/wiki/La_Bhagavadgîtâ_(trad._Senart)),
fetched by `_tools/fetch-french.cs`. Senart died in 1928, so the text is public domain,
and Wikisource marks it fully proofread rather than raw OCR.

Two things that transcription needed, both of which would have shipped silently:

- Chapter 15 verse 11 is transcribed **"1 1."**, with the digits spaced apart. The
  original parser required each verse number to be exactly the previous plus one, so
  this one miss desynchronised the rest of the chapter and yielded 10 verses of 20.
  Numbers may now contain spaces, and a gap no longer breaks the sequence.
- Senart follows a recension of **chapter 13 that omits Arjuna's opening question**, the
  verse the Sanskrit numbers 13.1. His verse *n* is our *n+1*. The verse counts alone
  would not have shown this — only comparing the text did. `chapterOffset` in the fetch
  tool corrects it, and 13.1 has no French as a result.

Coverage is 700 of 701 verses. Where an edition has a gap, `GitaDatabase.TextOf` falls
back — preferring the same language first — and reports which edition it actually used,
so the credit on screen never names a translator who did not write that line.

### Reader tabs

The verse page builds its tabs from the corpus rather than hard-coding them:

| Tab | Shows |
|---|---|
| Translation | the chosen edition |
| Poetic | the edition flagged `poetic` in the same language, else any second edition, labelled *Alternate* |
| Hindi | the first Hindi edition — always offered, and hidden only when Hindi is already the app language |
| Word by word | the per-word gloss |

The speaker button reads whichever tab is open **in that tab's own language**, so the
Hindi tab narrates with `hi-IN` rather than an English voice mispronouncing Devanagari.

### Adding a language

The reader picks a language, then a translator within it. Both come from the `editions`
array at the top of `gita.json`, so a new language is a data change, not a code change:

1. Add a row to the `editions` table in `_tools/build-data.cs` — id, BCP-47 `lang`,
   the label as written in that language, translator, and the `tts` locale.
2. Make its text reachable under that translator's name in the source data.
3. Re-run `dotnet run build-data.cs`. It prints a completeness check per edition.

Nothing in the UI is hard-coded to English or Hindi; the picker and the narrator both
read the table.

**Only properly licensed translations should be added.** A Telugu dataset was evaluated
and rejected during development because the repository carried no licence at all, which
is not something to ship in a paid-listing app worldwide. Machine translation was also
rejected: it is not an acceptable way to render scripture.

## Audio

Narration uses Android's `TextToSpeech` (`Plugins/Android/com/gita/audio/Narrator.java`),
not shipped recordings. Recording 701 verses per language is a studio project, and the
files would be far larger than the app. TTS runs offline once the voice data is
installed and follows whichever language is selected.

It is on by default and mutable from the header of the verse page. If the device has no
engine, or no voice for the chosen language, the language screen says so plainly and the
app stays silent rather than failing.

Sky HDRI: `qwantani_sunrise_puresky` from [Poly Haven](https://polyhaven.com), CC0.
Fonts: Noto Serif/Sans Devanagari, EB Garamond, Inter — all SIL Open Font License.

## Known limitations

- **The native Devanagari path has not been run on real hardware.** It is verified only
  as far as static checks go: the class compiles into `classes.dex`
  (`Lcom/gita/text/TextRasterizer;` with `rasterize` and `measureHeight`), and the fonts
  ship at `assets/Fonts/`. It could not be executed here, because Unity 6.6 dropped
  Android x86-64 (`AndroidArchitecture.X86_64` is obsolete), so an ARM-only build cannot
  run on a standard x86_64 emulator, and no ARM device was attached.
  **Install the APK on an Android phone and check one verse before releasing.** What to
  look for, using 1.3: the transliteration reads *śhiṣhyeṇa*, so the Devanagari must
  render शिष्येण — if it shows शष्येिण, with the i-hook after the consonant instead of
  before it, the native path did not engage and it has fallen back to TextMeshPro.
- The Windows preview build (`build/windows/`) always shows the TextMeshPro fallback,
  since the shaper is Android-only. Its Sanskrit is mis-shaped by design; use it to
  judge layout, scene and navigation, not typography.
- The 3D set is procedural and deliberately abstract — a backlit silhouette of the halted
  chariot. It is competent, not remarkable. Real modelled assets would raise it
  considerably; this is the weakest part of the app.
- Unity Personal shows the Unity splash screen; it cannot be disabled on this licence.
