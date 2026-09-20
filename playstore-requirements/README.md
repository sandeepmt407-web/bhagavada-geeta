# Play Store requirements

Everything the Play Console asks for, except the two things only you can do: sign in to
Firebase, and take the screenshots on a phone.

**Nothing in here touches the app.** No Firebase SDK was added, no `google-services.json`
was put into the Unity project, and no database was set up — none of that is needed to
host two web pages.

```
playstore-requirements/
  legal/                         the site to deploy
    public/index.html            landing page linking to both
    public/privacy.html          privacy policy
    public/terms.html            terms & conditions
    firebase.json                hosting config
    .firebaserc                  points at project myweb-d8cca
  listing/
    store-listing.md             app name, descriptions, data-safety answers
  graphics/
    feature-graphic-1024x500.png ready to upload
    screenshots/                 DRAFTS ONLY — see below
```

---

## 1. Deploy the two legal pages

I could not do this step. Firebase sign-in is an interactive Google OAuth flow through a
browser, and this session has no way to complete one. Node is not installed on this
machine either, so the standalone Firebase binary is the shortest path — it needs no Node.

Open a terminal and run:

```powershell
# one-off: fetch the standalone CLI (no Node required)
Invoke-WebRequest -Uri "https://firebase.tools/bin/win/instant/latest" -OutFile "$env:USERPROFILE\firebase.exe"

# sign in as sandeepmt407@gmail.com — this opens your browser
& "$env:USERPROFILE\firebase.exe" login

# deploy
cd C:\Users\sverm\.claude\geeta\playstore-requirements\legal
& "$env:USERPROFILE\firebase.exe" deploy --only hosting
```

If `myweb-d8cca` has never had Hosting enabled, the CLI will offer to enable it; say yes.
It has no cost at this size and needs no database.

The deploy prints the live URL. It will be one of:

- `https://myweb-d8cca.web.app/privacy`
- `https://myweb-d8cca.firebaseapp.com/privacy`

Open it before pasting it into the Play Console. Google rejects listings whose privacy
policy URL does not load.

### If you would rather not use Firebase

The three files in `legal/public/` are plain HTML with no dependencies. GitHub Pages,
Netlify, Cloudflare Pages or any static host will serve them unchanged. Play only requires
that the privacy policy is at a public URL that loads.

---

## 2. Screenshots — these must be taken on your phone

**Do not upload the files in `graphics/screenshots/`.** They are drafts, for composition
only.

They come from the desktop preview build, where Devanagari is not shaped correctly —
Android's text engine does the shaping and Windows has no equivalent. In every draft, the
Sanskrit is subtly wrong: conjuncts do not form and the i-matra sits on the wrong side of
its consonant. Anyone who reads Devanagari would see it immediately in a store listing,
and it is the one mistake this app exists to avoid.

I confirmed this twice while preparing these assets: TextMeshPro cannot shape it, and
neither can Windows' own text stack, which is why the feature graphic carries no
Devanagari at all.

Take them on the device instead, with the APK installed:

```powershell
# with the phone connected and USB debugging on
adb exec-out screencap -p > 1-home.png
```

Or just use the phone's own screenshot buttons and copy the files across. Either gives you
the real thing at full resolution.

**Which screens to capture** — the drafts show the composition I would use:

| # | Screen | Why |
|---|---|---|
| 1 | Home, with the verse of the day | Shows the scene and what the app is |
| 2 | Verse 2.47 | The verse everyone knows, in the dawn treatment |
| 3 | Verse 11.12 | The eleventh chapter — completely different sky |
| 4 | Verse 6.19 | Night. Shows the scene really does change |
| 5 | The eighteen chapters | Shows the whole book is there |
| 6 | Collections | Shows the curated ways in |

Play needs at least 2 and allows up to 8, so six is comfortable. Requirements: PNG or JPEG,
16:9 or 9:16, shortest side at least 320px, longest at most 3840px. A straight phone
screenshot satisfies all of that.

---

## 3. Feature graphic

`graphics/feature-graphic-1024x500.png` is ready to upload as it is — exactly 1024×500,
with the title and all text kept well inside the safe area, since Play crops and overlays
the edges.

It uses the app's own rendered scene as its background, so it shows the real thing rather
than stock artwork.

---

## 4. App icon

Already in the app and generated from `ProjectConfig.ApplyIcons`. Play also wants a
**512×512 PNG** uploaded separately for the listing. That is not in this folder yet — say
the word and I will export it from the same source the launcher icon uses, so the two
match exactly.

---

## Still outstanding before you can publish

| Item | Status |
|---|---|
| Privacy policy and terms written | Done — needs deploying (step 1) |
| Store listing copy | Done — `listing/store-listing.md` |
| Feature graphic | Done |
| Screenshots | **Must be retaken on a phone** (step 2) |
| 512×512 listing icon | Not exported yet |
| Signed AAB | **Blocked** — every build so far used the debug key, because `GITA_KEYSTORE_PASS` was never set in the build shell. Play will reject a debug-signed bundle. |
| Signing certificate name | Still reads `CN=Bhagavad Gita 3D` from before the rename. Cosmetic and invisible to users, but it can only be changed by generating a new key — free now, impossible after your first upload. |

---

## A note on the google-services.json you sent

That file is how you would add Firebase *into* the app, which is not needed here — hosting
two web pages requires nothing inside the APK. So it has not been added to the Unity
project, and the app is unchanged.

The API key in it (`AIzaSy...`) is a client key. Those are designed to ship inside apps and
are not secrets in the way a server key is, so pasting it was not a problem. It is still
worth opening the Google Cloud console and restricting that key to the Android app it
belongs to, which stops it being reused elsewhere.
