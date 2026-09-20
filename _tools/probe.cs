#:package System.Text.Json@9.0.0
using System.Text.Json;

var dir = @"C:\Users\sverm\.claude\geeta\_data";
using var tr = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir, "translation.json")));
var counts = new Dictionary<string,int>();
var langs = new Dictionary<string,int>();
foreach (var e in tr.RootElement.EnumerateArray())
{
    var lang = e.GetProperty("lang").GetString() ?? "?";
    langs[lang] = langs.GetValueOrDefault(lang) + 1;
    if (lang != "english") continue;
    var a = e.GetProperty("authorName").GetString() ?? "?";
    counts[a] = counts.GetValueOrDefault(a) + 1;
}
Console.WriteLine("LANGS:");
foreach (var kv in langs.OrderByDescending(x=>x.Value)) Console.WriteLine($"  {kv.Key,-12} {kv.Value}");
Console.WriteLine("\nENGLISH AUTHORS (verse coverage / 700):");
foreach (var kv in counts.OrderByDescending(x=>x.Value)) Console.WriteLine($"  {kv.Value,4}  {kv.Key}");

using var vs = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir,"verse.json")));
Console.WriteLine($"\nVERSES TOTAL: {vs.RootElement.GetArrayLength()}");
using var ch = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir,"chapters.json")));
Console.WriteLine($"CHAPTERS TOTAL: {ch.RootElement.GetArrayLength()}");
Console.WriteLine("CHAPTER KEYS: " + string.Join(", ", ch.RootElement[0].EnumerateObject().Select(p=>p.Name)));
