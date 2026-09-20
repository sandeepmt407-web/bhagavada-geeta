#:property JsonSerializerIsReflectionEnabled=true
using System.Text.Json;
var d = @"C:\Users\sverm\.claude\geeta\_data";
using var vs = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(d,"verse.json")));
using var tr = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(d,"translation.json")));
using var fr = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(d,"fr-senart.json")));

// verse_id -> (chapter, verse)
var ids = new Dictionary<int,(int c,int v)>();
foreach (var v in vs.RootElement.EnumerateArray())
    ids[v.GetProperty("id").GetInt32()] = (v.GetProperty("chapter_number").GetInt32(), v.GetProperty("verse_number").GetInt32());

var en = new Dictionary<(int,int),string>();
foreach (var t in tr.RootElement.EnumerateArray())
{
    if (t.GetProperty("authorName").GetString() != "Swami Sivananda") continue;
    if (!ids.TryGetValue(t.GetProperty("verse_id").GetInt32(), out var k)) continue;
    en[k] = (t.GetProperty("description").GetString() ?? "").Replace("\n"," ").Trim();
}

(int,int)[] probes = { (1,1), (2,47), (11,32), (13,2), (13,35), (15,11), (18,78) };
foreach (var (c,v) in probes)
{
    string f = fr.RootElement.TryGetProperty(c.ToString(), out var ch) && ch.TryGetProperty(v.ToString(), out var fv)
        ? fv.GetString() ?? "" : "(none)";
    string e = en.GetValueOrDefault((c,v), "(none)");
    Console.WriteLine($"--- {c}.{v} ---");
    Console.WriteLine($"  EN: {e[..Math.Min(130,e.Length)]}");
    Console.WriteLine($"  FR: {f[..Math.Min(130,f.Length)]}");
}
