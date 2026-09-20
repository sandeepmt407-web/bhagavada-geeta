#:property JsonSerializerIsReflectionEnabled=true
using System.Text.Json;
var dir = @"C:\Users\sverm\.claude\geeta\_data";
using var tr = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "translation.json")));
var byLang = new Dictionary<string, Dictionary<string,int>>();
foreach (var e in tr.RootElement.EnumerateArray())
{
    var lang = e.GetProperty("lang").GetString() ?? "?";
    var a = e.GetProperty("authorName").GetString() ?? "?";
    var d = e.TryGetProperty("description", out var dv) ? dv.GetString() : null;
    if (string.IsNullOrWhiteSpace(d)) continue;
    if (!byLang.TryGetValue(lang, out var m)) byLang[lang] = m = new();
    m[a] = m.GetValueOrDefault(a) + 1;
}
foreach (var (lang, m) in byLang.OrderBy(x => x.Key))
{
    Console.WriteLine($"\n{lang.ToUpper()}:");
    foreach (var kv in m.OrderByDescending(x => x.Value))
        Console.WriteLine($"   {kv.Value,4} verses  {kv.Key}");
}
