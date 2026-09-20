#:property JsonSerializerIsReflectionEnabled=true
using System.Text.Json;
var d = @"C:\Users\sverm\.claude\geeta\_data";
using var vs = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(d,"verse.json")));
Console.WriteLine("=== Sanskrit corpus, chapter 13 ===");
foreach (var v in vs.RootElement.EnumerateArray())
{
    if (v.GetProperty("chapter_number").GetInt32() != 13) continue;
    int n = v.GetProperty("verse_number").GetInt32();
    if (n > 3) continue;
    var tr = v.GetProperty("transliteration").GetString() ?? "";
    Console.WriteLine($"  {n}: {tr.Replace("\n"," ")[..Math.Min(95, tr.Replace("\n"," ").Length)]}");
}
using var fr = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(d,"fr-senart.json")));
Console.WriteLine("\n=== Senart French, chapter 13 ===");
var c13 = fr.RootElement.GetProperty("13");
foreach (var p in c13.EnumerateObject())
{
    if (int.Parse(p.Name) > 3) continue;
    var s = p.Value.GetString() ?? "";
    Console.WriteLine($"  {p.Name}: {s[..Math.Min(115, s.Length)]}");
}
