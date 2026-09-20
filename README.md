# Bhagavad Gita

An Android app presenting the complete Bhagavad Gita — all 18 chapters and 701 verses —
over a real-time 3D scene of Kurukshetra at dawn. Built with Unity 6 (6000.6.0f1) and URP.

Store identity: application ID `com.theops.bhagvadgeeta`, label **Bhagavad Gita**. Both are
set in `Assets/Editor/ProjectConfig.cs` and applied by `ProjectConfig.All`. The ID is written
per-target with `SetApplicationIdentifier(NamedBuildTarget.Android, ...)`, because the plain
`applicationIdentifier` property only touches whichever build target happens to be active.

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
           VerseMood                  ten looks, mapped verse by verse
           MoodDirector               blends the set between them
           Motifs                     the drifting element in each look
           ProcMesh                   ground, ridges, cloth, rings, cones
           CameraDirector             named shots, blended moves
           PostFX                     runtime volume: ACES, bloom, grade, DOF
  UI/      Theme, UIKit               design tokens, runtime uGUI construction
           ScaledText                 reader-adjustable type scale
           SafeAreaFitter             clearance from the notch and the navigation bar
           SystemInsets, SystemBarScrim   the Android side of that
           NativeText, ShapedText     Devanagari shaping (see below)
           HomeScreen                 title, verse of the day, progress, entry points
           ChaptersScreen             the eighteen yogas
           ReaderScreen               one verse at a time; bookmark, share, listen
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
           Ambience                   the bansuri bed, and its ducking
Plugins/Android/com/gita/text/TextRasterizer.java   Devanagari shaping
Plugins/Android/com/gita/ui/Insets.java             real system-bar insets
Plugins/Android/com/gita/audio/Narrator.java        text-to-speech
Plugins/Android/com/gita/share/Sharer.java          image and text sharing
```

Home is always the opening screen. The language picker was briefly put in front of it
on a first run, which meant the app opened on a settings page; it is reached from the
pill at the top of Home instead.



## Reading it

The type scale was raised across the board after the app was read on a phone rather than
looked at in a screenshot. Body text now lands around 16sp on a 1080p handset, which is
what the rest of Android uses for running text; the old scale was elegant and genuinely
hard to read in the hand. On top of that the reader can pick one of four
sizes - the app ships on Large - and the settings screen sets a real line of verse at the
chosen size, because a word like "Large" means nothing until you see it.

Everything is authored at its natural size and multiplied by that setting. `UIKit.Text`
attaches a `ScaledText` marker holding the authored size, so changing the setting re-sizes
every block in the app, including screens that are currently hidden - they are inactive,
not destroyed, and would otherwise come back at the old size.

### Fitting at every size, and growing with it

Two different answers are needed, because two different things are being sized.

**Rows and cards inside a scrolling list grow with the type.** A chapter card, a search
result, a settings row: these can afford to get taller, because the list simply becomes
longer and scrolls further. `Theme.Scaled` carries the reader's setting into their
heights and internal offsets, so the text stays at the size that was asked for instead of
being shrunk back to fit a box measured for somebody else. At the largest setting a
chapter title that fitted on one line wraps onto two and the card grows to hold it.

**Fixed chrome shrinks instead.** A header strip, a tab chip, a footer button: these
cannot grow without eating the space being read in. Those call `UIKit.Fit`, where the
authored size is the ceiling and the text steps down only as far as it must, and only
when it would otherwise not fit.

Shaped Devanagari needs its own version, because it is a bitmap that sizes its own rect
rather than a block TextMeshPro can shrink. `ShapedText.SetMaxHeightRef` bounds it: on
Android it measures through `TextRasterizer.measureHeight` — laying the line out without
rasterising — and steps the size down until it fits; on the desktop fallback it lets
TextMeshPro do the shrinking.

The chapter list is laid out by hand rather than by a layout group, so it rebuilds its
cards when it is shown and the type has changed since last time — every card position
after the first one moves.

Every screen is rendered at the largest setting before shipping, with `-gita-textsize 3`.
That check caught the verse-of-the-day Sanskrit overflowing both ends of its card and
landing on top of the heading: the fallback path was setting `sizeDelta` on a node that
stretches to fill its parent, where that value is an offset from the anchors rather than
a height. Nothing about it was visible at the default size.
### System bars

The app renders edge to edge, and from Android 15 the platform enforces that regardless.
Unity's own `SafeArea` component works from `Screen.safeArea` alone, which reported the
display cutout here but not the navigation bar, so the footer of the verse page sat
underneath the phone's back gesture and the controls were hard to hit.

`Insets.java` asks Android for the real per-edge insets, and `SafeAreaFitter` takes the
**larger** of that and Unity's figure for each edge. Whichever source is right wins, and
because it is a maximum rather than a sum the two can never be counted twice. If the
native call fails the app falls back to `Screen.safeArea` exactly as before. `SystemBarScrim`
then continues the dark ground of the interface into the strips behind the bars, so there
is no hard edge where a panel stops.
## The set, per verse

Every verse gets its own treatment of the Kurukshetra set. `VerseMood` holds ten looks -
one per movement of the argument - and each is a whole visual state rather than a colour
tweak: sun colour and strength, the fill light, haze colour and density, sky tint,
exposure and rotation, the grade, a camera framing laid over whatever shot the screen
asked for, and the element drifting through the frame.

| Look | Where it is used | Drifting element |
|---|---|---|
| Before the light | chapter 1, and 2.1–2.10 | cold dust |
| The work in front of you | chapter 3, and 2.47–2.48 | warm dust |
| What does not die | chapters 2, 4, 5 | rising embers |
| A leaf, a flower, some water | chapters 9, 12, 17, and 18.62–66 | falling petals |
| A lamp in a windless place | chapters 6, 8, 2.54–72, 5.24–29 | still stars |
| A thousand suns | chapter 11, and 11.32–34 | sparks |
| Whenever dharma declines | chapters 7, 10, 4.6–8, 16.1–3 | descending light |
| The field and its knower | chapters 13, 15 | leaves on a wind |
| Three strands | chapters 14, 16 | three-coloured motes |
| Let go, and be free | chapter 18 | descending light |

Chapters map to looks, and then a table of passages corrects the places where a chapter
turns inside itself. The opening despair does not stop at the end of chapter one - it
runs to 2.10, and the answer begins at 2.11. Getting that wrong would have put the
consolation of the second chapter over the grief of the first.

On top of the look, each verse shifts the sky rotation, haze, sun and framing by a small
amount derived from its own number, so no two pages are the same image and a verse is
always the same image when you come back to it. Changes are blended, not cut: about two
seconds across a change of mood, under one within a mood.

Each look also carries how much the reader screen must darken the set behind its text.
That cannot be one number across ten looks - the eleventh chapter is a blazing gold sky
that swallows cream text, and the sixth is close to black, where the same scrim would
leave nothing to see. It was obvious the moment the two were rendered side by side and
not before.

Render the ten looks with `Gita.EditorTools.LookDev.RenderMoods` (needs a graphics
device, so omit `-nographics`); they land in `build/moods/`.

### Inspecting the interface

The desktop preview build takes three arguments, all inert on Android, which is the only
way to look at a screen here without a handset:

```bash
BhagavadGita.exe -screen-width 540 -screen-height 960 -screen-fullscreen 0 \
                 -gita-verse 11.12 -gita-screen Reader -gita-shot C:\out\shot.png
```

`-gita-shot` reads the framebuffer and quits. Capturing the window from outside gets the
wrong pixels on a scaled display, and the ScreenCapture module is deliberately not in
this project.
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


Narration is **one control**, not two. There used to be a speaker in the tab row and a
mute in the header, which is two different answers to the same question. The single
button in the footer of the verse page plays what is on screen and, in the same gesture,
decides whether the next verse speaks itself. It shows what it will do: a triangle and
LISTEN when nothing is being read, two bars and STOP while it reads. Both icons are
drawn, not set as glyphs - whether any given bundled font carries U+25B6 is not something
to leave to chance on the app's main control.

The engine is now chosen rather than accepted. Google's speech engine is used where it is
installed, because its voices are the best generally available on Android, and the best
individual voice for the language is picked out of everything that engine offers instead
of taking the default - which on most devices is the oldest and most synthetic of the
set. Offline voices are preferred over network ones even when the network voice scores
higher: an app for scripture should work on a train. Pitch sits slightly below the engine
default, because Android voices are tuned bright for navigation prompts and that is the
wrong register for this text.

It will still sound like a speech synthesiser, because it is one. The ceiling is the
device's, not the app's, and the settings screen says so plainly and names the voice in
use, so a reader who wants better knows what to install rather than concluding the app
sounds bad.



### Shloka or meaning

Narration reads one of two things, and which one is a standing choice rather than a
per-page decision: whichever way it is set, every verse opens reading that, so nobody
presses play on each page. The switch is the **SHLOKA** button on the verse's own
reference line, next to "2.47".

It is there rather than in settings because it sits on the Sanskrit whose fate it
decides, and because the line it shares was empty space either side of a centred verse
number, so it costs no height. It is not a fifth tab either: the tabs choose which
*translation* is shown, while the Sanskrit is displayed above them either way, and a
"Sanskrit" tab would wrongly imply the others hide it. The footer control keeps its one
job — whether narration happens at all.

Tapping the switch turns narration on if it was stopped and reads the verse again
immediately, so the choice is audible rather than theoretical.

**Android has no Sanskrit voice.** There is no `sa-IN`, so the shloka is read by a Hindi
voice — the only one on the device that can pronounce the script at all. Hindi
pronunciation rules are not Sanskrit's: schwa deletion gives *rām* where Sanskrit
requires *rāma*, and visarga, anusvāra and vowel length all suffer. For a translation
that is fine. For scripture recited aloud it is wrong in a way anyone who knows Sanskrit
hears in the first line.

That is a known interim. The UI, the switch and the sequencing do not change when a real
recitation replaces it — only what plays. And of everything that could be commissioned,
Sanskrit recitation has by far the best reach for the cost: it is recorded once and
serves every reader whatever translation they use, where recording a translation serves
only the people reading that one.

Searched and not found: archive.org carries 294 audio items matching the Gita, and
essentially every Sanskrit recitation among them is CC BY-NC-ND or states no licence at
all. Wikimedia Commons has no Gita recitation; its only Sanskrit chanting is a Guru
Stotram. There is no freely licensed recitation of this text to ship.

The button's label is set in Latin rather than as संस्कृत on purpose. Labels outside the
verse blocks go through TextMeshPro, which cannot form that word's conjunct — it would
render visibly wrong on a handset, which is the one thing this app must not do to
Devanagari.
### Choosing a voice

There is no published list of the voices any given Android phone carries: it depends on
the speech engine, the Android version and which voice data the owner has installed. So
the settings screen asks the device — `Narrator.listVoices` enumerates what the engine
actually has, filtered to India-locale voices, and the reader picks from that. Tapping a
voice selects it *and* speaks a line of the Gita in it, because "Voice 3, high quality"
means nothing until you have heard it.

"Best available" stays at the top and is the default, so nobody has to audition six
voices to start reading. The choice is stored per language: an English voice is no use
to the Hindi tab, and switching language and back should not mean choosing again.

The voices are the engine's own and cost nothing to use. Quality is the device's
ceiling, not the app's.


### The default voice

`AppSettings.DefaultEnglishVoice` ships as `en-in-x-ena-network`, chosen by listening
rather than by score. It is a preference, not a requirement, and it is a *network* voice,
so two fallbacks sit behind it:

- A device that does not carry that voice — a different engine, or the data never
  installed — falls back to the best voice it does have.
- If the engine fails mid-verse because there is no connection, `onError` picks the best
  *installed* voice and says the same line again. The reader hears a pause, not silence.
  Doing it this way means the app does not have to ask for a connectivity permission to
  guess in advance.

A reader who picks their own voice overrides all of it, per language. An unset preference
and a preference set to "best available" are deliberately different states: the first
means nobody has chosen and gets the shipped default, the second means someone chose
automatic and gets it.
### The bansuri bed

A 99-second public-domain bansuri phrase loops under the whole app, streamed rather than
decompressed into memory. It **ducks out of the way whenever a verse is being read** —
narration goes through Android's speech engine on a stream this app has no mixer control
over, so `Ambience` watches what the narrator is doing and pulls its own level down:
fast on the way down, about a quarter of a second, so no word ever competes with a
flute; slow on the way back, near two seconds, so the return is not itself a
distraction. It steps back rather than cutting out, because a bed that disappears draws
more attention than one that recedes.

Music and narration are separate settings. Wanting the bed without a voice, or a voice
without the bed, are both ordinary.

Bansuri: `Bansuri sample E bass` from Wikimedia Commons, public domain.
Sky HDRI: `qwantani_sunrise_puresky` from [Poly Haven](https://polyhaven.com), CC0.
Fonts: Noto Serif/Sans Devanagari, EB Garamond, Inter — all SIL Open Font License.

## Known limitations

- **The navigation-bar fix has not been run on real hardware either.** `Insets.java`
  compiles into `classes.dex` and the fallback path is the old behaviour, so the worst
  case is that nothing improves rather than that anything breaks. It is the first thing
  to check on a device: the Previous / Listen / Next row at the bottom of the verse page
  must sit clear of the back gesture strip.
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
