#:property JsonSerializerIsReflectionEnabled=true
using System.Text.Json;

var doc = JsonDocument.Parse(File.ReadAllBytes(@"C:\Users\sverm\.claude\geeta\Gita3D\Assets\Resources\gita.json"));
var text = new Dictionary<(int,int), string>();
foreach (var v in doc.RootElement.GetProperty("verses").EnumerateArray())
{
    var t = v.GetProperty("t");
    text[(v.GetProperty("c").GetInt32(), v.GetProperty("v").GetInt32())] =
        t.GetArrayLength() > 0 ? (t[0].GetString() ?? "") : "";
}

var themes = new (string name, (int,int)[] refs)[]
{
    ("Fear and anxiety",      new[]{(2,3),(2,40),(2,56),(4,10),(16,1),(18,66)}),
    ("Duty and action",       new[]{(2,47),(2,48),(3,8),(3,19),(3,35),(18,47)}),
    ("Death and the self",    new[]{(2,12),(2,13),(2,20),(2,22),(2,23),(2,27)}),
    ("Devotion",              new[]{(9,22),(9,26),(9,34),(12,6),(18,65),(18,66)}),
    ("Knowledge",             new[]{(2,29),(4,38),(4,39),(7,19),(13,8),(18,63)}),
    ("Peace of mind",         new[]{(2,66),(2,70),(2,71),(5,29),(6,7),(6,19)}),
    ("Letting go",            new[]{(2,62),(2,63),(2,64),(5,10),(6,35),(12,15)}),
    ("The Supreme",           new[]{(7,7),(9,4),(10,20),(10,41),(11,32),(15,15)}),
};

foreach (var (name, refs) in themes)
{
    Console.WriteLine($"\n### {name}");
    foreach (var r in refs)
    {
        var s = text.GetValueOrDefault(r, "*** MISSING ***");
        Console.WriteLine($"  {r.Item1}.{r.Item2,-3} {s[..Math.Min(112, s.Length)]}");
    }
}
