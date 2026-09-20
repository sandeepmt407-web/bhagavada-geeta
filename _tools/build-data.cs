#:property JsonSerializerIsReflectionEnabled=true
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Consolidates the public-domain Gita corpus into one file the app loads at boot.
//
// The output carries every complete translation in the source, not just one per
// language, so the reader can offer a choice of translator as well as of language.

string dir = @"C:\Users\sverm\.claude\geeta\_data";
string outPath = @"C:\Users\sverm\.claude\geeta\Gita3D\Assets\Resources\gita.json";
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

// ---- the editions we ship, in display order ------------------------------
// id          : stable key, also what gets written into PlayerPrefs
// lang        : BCP-47 language subtag
// langLabel   : shown in the language picker, in that language
// translator  : shown under the verse
// tts         : locale handed to Android TextToSpeech
// poetic      : the literary rendering for that language, offered as its own tab in
//               the reader. Purohit's 1935 English is a genuinely poetic translation;
//               for Hindi no edition is poetic as such, so the second one is offered
//               as an alternate reading and the tab is labelled accordingly.
// source      : "" reads from translation.json by author name; otherwise a file in
//               _data holding {chapter: {verse: text}}, already aligned to our numbering
var editions = new (string id, string lang, string langLabel, string translator,
                    string author, string tts, bool poetic, string source)[]
{
    ("en-sivananda",      "en", "English",  "Swami Sivananda",       "Swami Sivananda",       "en-IN", false, ""),
    ("en-purohit",        "en", "English",  "Shri Purohit Swami",    "Shri Purohit Swami",    "en-IN", true,  ""),
    ("en-gambirananda",   "en", "English",  "Swami Gambirananda",    "Swami Gambirananda",    "en-IN", false, ""),
    ("en-adidevananda",   "en", "English",  "Swami Adidevananda",    "Swami Adidevananda",    "en-IN", false, ""),
    ("en-sankaranarayan", "en", "English",  "Dr. S. Sankaranarayan", "Dr. S. Sankaranarayan", "en-IN", false, ""),
    ("hi-tejomayananda",  "hi", "हिन्दी",   "स्वामी तेजोमयानन्द",       "Swami Tejomayananda",   "hi-IN", false, ""),
    ("hi-ramsukhdas",     "hi", "हिन्दी",   "स्वामी रामसुखदास",         "Swami Ramsukhdas",      "hi-IN", false, ""),
    ("fr-senart",         "fr", "Français", "Émile Senart",          "Émile Senart",          "fr-FR", false, "fr-senart.json"),
};

// ---- external editions ----------------------------------------------------
// chapter -> verse -> text, per source file.
var external = new Dictionary<string, Dictionary<int, Dictionary<int, string>>>();
foreach (var e in editions)
{
    if (e.source.Length == 0 || external.ContainsKey(e.source)) continue;
    string path = Path.Combine(dir, e.source);
    if (!File.Exists(path))
    {
        Console.WriteLine($"!! missing source for {e.id}: {path}");
        external[e.source] = new();
        continue;
    }
    var map = new Dictionary<int, Dictionary<int, string>>();
    using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
    foreach (var chProp in doc.RootElement.EnumerateObject())
    {
        var perVerse = new Dictionary<int, string>();
        foreach (var vProp in chProp.Value.EnumerateObject())
            perVerse[int.Parse(vProp.Name)] = vProp.Value.GetString() ?? "";
        map[int.Parse(chProp.Name)] = perVerse;
    }
    external[e.source] = map;
}

// ---- text hygiene ---------------------------------------------------------
var blankRuns = new Regex(@"\n{3,}");
var spaceRuns = new Regex(@"[ \t]{2,}");
var leadMarker = new Regex(@"^\s*(?:।।\s*)?\d+\.\d+\s*(?:।।|\.)?\s*");

string Clean(string? s, bool keepLines)
{
    if (string.IsNullOrWhiteSpace(s)) return "";
    s = s.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\u00a0', ' ');
    s = leadMarker.Replace(s, "");
    s = string.Join("\n", s.Split('\n').Select(l => spaceRuns.Replace(l.Trim(), " ")));
    s = blankRuns.Replace(s, "\n\n");
    if (!keepLines) s = s.Replace("\n", " ");
    return spaceRuns.Replace(s, " ").Trim();
}

// ---- load -----------------------------------------------------------------
using var chDoc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "chapters.json")));
using var vsDoc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "verse.json")));
using var trDoc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "translation.json")));

string? Str(JsonElement e, string p) =>
    e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
int Int(JsonElement e, string p) =>
    e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

// verse_id -> author -> text
var byVerse = new Dictionary<int, Dictionary<string, string>>();
foreach (var t in trDoc.RootElement.EnumerateArray())
{
    int vid = Int(t, "verse_id");
    string author = Str(t, "authorName") ?? "";
    string text = Str(t, "description") ?? "";
    if (vid == 0 || author.Length == 0 || string.IsNullOrWhiteSpace(text)) continue;
    if (!byVerse.TryGetValue(vid, out var m)) byVerse[vid] = m = new();
    m[author] = text;
}

// ---- chapters -------------------------------------------------------------
var chapters = new List<Chapter>();
foreach (var c in chDoc.RootElement.EnumerateArray())
{
    chapters.Add(new Chapter
    {
        number = Int(c, "chapter_number"),
        name = Clean(Str(c, "name"), false),
        translit = Clean(Str(c, "name_transliterated"), false),
        nameEn = Clean(Str(c, "name_translation"), false),
        meaning = Clean(Str(c, "name_meaning"), false),
        summary = Clean(Str(c, "chapter_summary"), true),
        summaryHi = Clean(Str(c, "chapter_summary_hindi"), true),
        verseCount = Int(c, "verses_count"),
    });
}
chapters.Sort((a, b) => a.number.CompareTo(b.number));

// ---- verses ---------------------------------------------------------------
var verses = new List<Verse>();
foreach (var v in vsDoc.RootElement.EnumerateArray())
{
    int id = Int(v, "id");
    byVerse.TryGetValue(id, out var m);

    int chapterNumber = Int(v, "chapter_number");
    int verseNumber = Int(v, "verse_number");

    var texts = new string[editions.Length];
    for (int i = 0; i < editions.Length; i++)
    {
        string raw;
        if (editions[i].source.Length > 0)
        {
            raw = external[editions[i].source].TryGetValue(chapterNumber, out var chMap) &&
                  chMap.TryGetValue(verseNumber, out var s) ? s : "";
        }
        else
        {
            raw = m != null && m.TryGetValue(editions[i].author, out var s) ? s : "";
        }
        texts[i] = Clean(raw, false);
    }

    verses.Add(new Verse
    {
        c = chapterNumber,
        v = verseNumber,
        sa = Clean(Str(v, "text"), true),
        tr = Clean(Str(v, "transliteration"), true),
        wm = Clean(Str(v, "word_meanings"), false),
        t = texts,
    });
}
verses.Sort((a, b) => a.c != b.c ? a.c.CompareTo(b.c) : a.v.CompareTo(b.v));

// ---- validate -------------------------------------------------------------
Console.WriteLine($"chapters={chapters.Count} verses={verses.Count} editions={editions.Length}");
Console.WriteLine($"missing sanskrit={verses.Count(x => x.sa.Length == 0)} " +
                  $"translit={verses.Count(x => x.tr.Length == 0)} " +
                  $"wordmeanings={verses.Count(x => x.wm.Length == 0)}");
for (int i = 0; i < editions.Length; i++)
{
    int missing = verses.Count(x => x.t[i].Length == 0);
    string flag = missing == 0 ? "complete" : $"MISSING {missing}";
    Console.WriteLine($"  {editions[i].id,-22} {editions[i].lang}  {flag}");
}
foreach (var ch in chapters)
{
    int actual = verses.Count(x => x.c == ch.number);
    if (actual != ch.verseCount)
        Console.WriteLine($"  !! ch{ch.number} has {actual}, declared {ch.verseCount}");
}

// ---- write ----------------------------------------------------------------
using var fs = File.Create(outPath);
var w = new Utf8JsonWriter(fs, new JsonWriterOptions
{
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Indented = false
});
w.WriteStartObject();

w.WriteStartArray("editions");
foreach (var e in editions)
{
    w.WriteStartObject();
    w.WriteString("id", e.id);
    w.WriteString("lang", e.lang);
    w.WriteString("langLabel", e.langLabel);
    w.WriteString("translator", e.translator);
    w.WriteString("tts", e.tts);
    w.WriteBoolean("poetic", e.poetic);
    w.WriteEndObject();
}
w.WriteEndArray();

w.WriteStartArray("chapters");
foreach (var c in chapters)
{
    w.WriteStartObject();
    w.WriteNumber("number", c.number);
    w.WriteString("name", c.name);
    w.WriteString("translit", c.translit);
    w.WriteString("nameEn", c.nameEn);
    w.WriteString("meaning", c.meaning);
    w.WriteString("summary", c.summary);
    w.WriteString("summaryHi", c.summaryHi);
    w.WriteNumber("verseCount", c.verseCount);
    w.WriteEndObject();
}
w.WriteEndArray();

w.WriteStartArray("verses");
foreach (var v in verses)
{
    w.WriteStartObject();
    w.WriteNumber("c", v.c);
    w.WriteNumber("v", v.v);
    w.WriteString("sa", v.sa);
    w.WriteString("tr", v.tr);
    w.WriteString("wm", v.wm);
    w.WriteStartArray("t");
    foreach (var s in v.t) w.WriteStringValue(s);
    w.WriteEndArray();
    w.WriteEndObject();
}
w.WriteEndArray();

w.WriteEndObject();
w.Flush();
Console.WriteLine($"\nWROTE {outPath}  ({fs.Length / 1024} KB)");

class Chapter
{
    public int number; public string name = ""; public string translit = "";
    public string nameEn = ""; public string meaning = ""; public string summary = "";
    public string summaryHi = ""; public int verseCount;
}
class Verse
{
    public int c; public int v; public string sa = "";
    public string tr = ""; public string wm = ""; public string[] t = [];
}
