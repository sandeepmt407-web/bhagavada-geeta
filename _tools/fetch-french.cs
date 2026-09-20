#:property JsonSerializerIsReflectionEnabled=true
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Pulls Émile Senart's 1922 French translation of the Gita from French Wikisource.
//
// Chosen because it is public domain (Senart died 1928), carries Wikisource's
// "fully proofread" mark rather than being raw OCR, and - crucially - numbers every
// verse, so it can be aligned to the Sanskrit rather than pasted in as loose prose.

const string Base = "https://fr.wikisource.org/w/api.php";
const string Page = "La Bhagavadgîtâ (trad. Senart)";
string outPath = @"C:\Users\sverm\.claude\geeta\_data\fr-senart.json";

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
http.DefaultRequestHeaders.Add("User-Agent", "GitaApp/1.0 (offline corpus build)");

var tagRun = new Regex(@"<[^>]*>");
var styleRun = new Regex(@"<(style|script)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
// The digits of a verse number are sometimes spaced apart in the transcription,
// e.g. "1 1." for verse 11, so spaces are allowed inside the number.
var verseLine = new Regex(@"^(\d[\d ]{0,3})\.\s*(.+)$");
var speakerLine = new Regex(@"^([\p{Lu}ÂÊÎÔÛÄËÏÖÜÇÑṚḌṢÇ' \u0300-\u036f]{3,40})\s+dit\s*:\s*$");
var footnoteMark = new Regex(@"\[\d+\]");
var spaceRun = new Regex(@"[ \t\u00a0]{2,}");

// Senart follows a recension of chapter 13 that omits Arjuna's opening question -
// the verse our Sanskrit corpus numbers 13.1. His verse n is therefore our n+1.
// Verified by comparing the text: his 13.1, "Le corps, ô fils de Kuntî, est appelé le
// kshetra", is the Sanskrit 13.2, "idaṁ śharīraṁ kaunteya kṣhetram ity abhidhīyate".
// Without this shift every French verse in the chapter would sit against the wrong
// shloka, which the verse counts alone would not have revealed.
var chapterOffset = new Dictionary<int, int> { [13] = 1 };

var chapters = new SortedDictionary<int, SortedDictionary<int, string>>();

// Resume from a previous run: Wikisource rate-limits, and there is no reason to ask
// it again for chapters already in hand.
if (File.Exists(outPath))
{
    using var prev = JsonDocument.Parse(File.ReadAllBytes(outPath));
    foreach (var chProp in prev.RootElement.EnumerateObject())
    {
        var verses = new SortedDictionary<int, string>();
        foreach (var vProp in chProp.Value.EnumerateObject())
            verses[int.Parse(vProp.Name)] = vProp.Value.GetString() ?? "";
        if (verses.Count > 0) chapters[int.Parse(chProp.Name)] = verses;
    }
    Console.WriteLine($"resuming: {chapters.Count} chapters already fetched\n");
}

for (int ch = 1; ch <= 18; ch++)
{
    if (chapters.ContainsKey(ch) && chapters[ch].Count > 0) continue;

    string url = $"{Base}?action=parse&page={Uri.EscapeDataString($"{Page}/{ch}")}" +
                 "&prop=text&format=json&formatversion=2";

    string raw = null;
    for (int attempt = 0; attempt < 5 && raw == null; attempt++)
    {
        if (attempt > 0) await Task.Delay(TimeSpan.FromSeconds(5 * attempt));
        try { raw = await http.GetStringAsync(url); }
        catch (HttpRequestException e) when ((int?)e.StatusCode == 429)
        {
            Console.WriteLine($"ch{ch}: rate limited, backing off");
        }
        catch (Exception e)
        {
            Console.WriteLine($"ch{ch}: fetch failed - {e.Message}");
            break;
        }
    }
    if (raw == null) { Console.WriteLine($"ch{ch}: gave up"); continue; }

    // Be a good citizen; the API asks for modest request rates.
    await Task.Delay(TimeSpan.FromMilliseconds(1200));

    using var doc = JsonDocument.Parse(raw);
    if (!doc.RootElement.TryGetProperty("parse", out var parse) ||
        !parse.TryGetProperty("text", out var textEl))
    {
        Console.WriteLine($"ch{ch}: no text in response");
        continue;
    }

    string html = textEl.GetString() ?? "";
    html = styleRun.Replace(html, " ");
    html = tagRun.Replace(html, "\n");
    html = WebUtility.HtmlDecode(html);

    var verses = new SortedDictionary<int, string>();
    string pendingSpeaker = null;
    int lastNumber = 0;

    foreach (var rawLine in html.Split('\n'))
    {
        string line = rawLine.Replace('\u00a0', ' ').Trim();
        if (line.Length == 0) continue;

        // The ornament closes the chapter; everything after it is editorial footnotes,
        // which contain their own numbers and would otherwise be read as verses.
        if (line.StartsWith("֎") || line.StartsWith("↑")) break;

        var speaker = speakerLine.Match(line);
        if (speaker.Success)
        {
            pendingSpeaker = Capitalise(speaker.Groups[1].Value.Trim());
            continue;
        }

        var m = verseLine.Match(line);
        if (!m.Success) continue;

        // Non-breaking spaces were already normalised to plain ones above.
        if (!int.TryParse(m.Groups[1].Value.Replace(" ", ""), out int number))
            continue;
        // Numbers only ever climb. A gap is tolerated - one unparsed verse should not
        // desynchronise the whole chapter, which is what a strict +1 rule did.
        if (number <= lastNumber || number > 200) continue;

        string body = footnoteMark.Replace(m.Groups[2].Value, "");
        body = spaceRun.Replace(body, " ").Trim();
        if (body.Length == 0) continue;

        if (pendingSpeaker != null)
        {
            body = $"{pendingSpeaker} dit : {body}";
            pendingSpeaker = null;
        }

        verses[number + chapterOffset.GetValueOrDefault(ch)] = body;
        lastNumber = number;
    }

    chapters[ch] = verses;
    Console.WriteLine($"ch{ch,2}: {verses.Count,3} verses");
}

// ---- compare against the Sanskrit corpus ---------------------------------
var declared = new Dictionary<int, int>();
using (var vs = JsonDocument.Parse(File.ReadAllBytes(@"C:\Users\sverm\.claude\geeta\_data\verse.json")))
{
    foreach (var v in vs.RootElement.EnumerateArray())
    {
        int c = v.GetProperty("chapter_number").GetInt32();
        declared[c] = declared.GetValueOrDefault(c) + 1;
    }
}

Console.WriteLine("\nalignment against the Sanskrit:");
int total = 0, aligned = 0;
foreach (var (ch, verses) in chapters)
{
    int want = declared.GetValueOrDefault(ch);
    total += want;
    int have = 0;
    for (int v = 1; v <= want; v++) if (verses.ContainsKey(v)) have++;
    aligned += have;
    string flag = have == want ? "complete" : $"{have}/{want}";
    Console.WriteLine($"  ch{ch,2} {flag}");
}
Console.WriteLine($"\ncovered {aligned} of {total} verses");

// ---- write ----------------------------------------------------------------
using var fs = File.Create(outPath);
var w = new Utf8JsonWriter(fs, new JsonWriterOptions
{
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    Indented = false
});
w.WriteStartObject();
foreach (var (ch, verses) in chapters)
{
    w.WriteStartObject(ch.ToString());
    foreach (var (v, text) in verses) w.WriteString(v.ToString(), text);
    w.WriteEndObject();
}
w.WriteEndObject();
w.Flush();
Console.WriteLine($"\nWROTE {outPath} ({fs.Length / 1024} KB)");

static string Capitalise(string s)
{
    if (string.IsNullOrEmpty(s)) return s;
    var sb = new StringBuilder(s.Length);
    bool start = true;
    foreach (var ch in s.ToLowerInvariant())
    {
        sb.Append(start && char.IsLetter(ch) ? char.ToUpperInvariant(ch) : ch);
        start = !char.IsLetter(ch);
    }
    return sb.ToString();
}
