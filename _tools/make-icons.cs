#:package System.Drawing.Common@9.0.0
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Generates the launcher art: a dharmachakra in saffron on a deep indigo field.
// Three files, because Android wants a legacy square icon plus separate adaptive
// foreground and background layers.

string outDir = @"C:\Users\sverm\.claude\geeta\Gita3D\Assets\Art\Icons";
Directory.CreateDirectory(outDir);

const int S = 1024;

Color night   = ColorTranslate("0B1026");
Color twilight= ColorTranslate("1B2547");
Color saffron = ColorTranslate("E8A33D");
Color saffronLit = ColorTranslate("F5C563");

static Color ColorTranslate(string hex) => ColorTranslator.FromHtml("#" + hex);

// ---------------------------------------------------------------- background
Bitmap MakeBackground()
{
    var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;

    // Radial falloff: lighter at the centre so the wheel reads against it.
    using var path = new GraphicsPath();
    path.AddEllipse(-S * 0.25f, -S * 0.35f, S * 1.5f, S * 1.5f);
    using var brush = new PathGradientBrush(path)
    {
        CenterPoint = new PointF(S * 0.5f, S * 0.42f),
        CenterColor = twilight,
        SurroundColors = new[] { night }
    };
    g.FillRectangle(brush, 0, 0, S, S);
    return bmp;
}

// ---------------------------------------------------------------- foreground
// scale: the chakra radius as a fraction of the canvas. Adaptive icons get
// cropped to a circle, so the foreground layer is drawn smaller to stay inside
// the 66% safe zone.
Bitmap MakeWheel(float scale)
{
    var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;

    float cx = S * 0.5f, cy = S * 0.5f;
    float r = S * scale;

    // soft halo
    using (var halo = new GraphicsPath())
    {
        halo.AddEllipse(cx - r * 1.55f, cy - r * 1.55f, r * 3.1f, r * 3.1f);
        using var haloBrush = new PathGradientBrush(halo)
        {
            CenterPoint = new PointF(cx, cy),
            CenterColor = Color.FromArgb(70, saffron),
            SurroundColors = new[] { Color.FromArgb(0, saffron) }
        };
        g.FillEllipse(haloBrush, cx - r * 1.55f, cy - r * 1.55f, r * 3.1f, r * 3.1f);
    }

    float rimWidth = r * 0.10f;
    using var rimPen = new Pen(saffron, rimWidth) { Alignment = PenAlignment.Center };
    using var thinPen = new Pen(saffronLit, r * 0.045f);
    using var hubBrush = new SolidBrush(saffron);

    // outer rim
    g.DrawEllipse(rimPen, cx - r, cy - r, r * 2f, r * 2f);
    // inner rim, the felly
    g.DrawEllipse(thinPen, cx - r * 0.80f, cy - r * 0.80f, r * 1.60f, r * 1.60f);

    // twenty-four spokes, as on the dharmachakra
    const int spokes = 24;
    using var spokePen = new Pen(saffron, r * 0.035f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
    for (int i = 0; i < spokes; i++)
    {
        double a = i * Math.PI * 2.0 / spokes;
        float x0 = cx + (float)Math.Cos(a) * r * 0.17f;
        float y0 = cy + (float)Math.Sin(a) * r * 0.17f;
        float x1 = cx + (float)Math.Cos(a) * r * 0.78f;
        float y1 = cy + (float)Math.Sin(a) * r * 0.78f;
        g.DrawLine(spokePen, x0, y0, x1, y1);
    }

    // hub
    g.FillEllipse(hubBrush, cx - r * 0.165f, cy - r * 0.165f, r * 0.33f, r * 0.33f);
    using var hubHole = new SolidBrush(Color.FromArgb(255, 11, 16, 38));
    g.FillEllipse(hubHole, cx - r * 0.070f, cy - r * 0.070f, r * 0.14f, r * 0.14f);

    return bmp;
}

// legacy: background and wheel composited
using (var bg = MakeBackground())
using (var wheel = MakeWheel(0.34f))
{
    using var g = Graphics.FromImage(bg);
    g.DrawImage(wheel, 0, 0);
    bg.Save(Path.Combine(outDir, "icon_legacy.png"), ImageFormat.Png);
}

// adaptive layers
using (var bg = MakeBackground())
    bg.Save(Path.Combine(outDir, "icon_adaptive_back.png"), ImageFormat.Png);

using (var fg = MakeWheel(0.26f)) // smaller: adaptive foregrounds get cropped
    fg.Save(Path.Combine(outDir, "icon_adaptive_fore.png"), ImageFormat.Png);

foreach (var f in Directory.GetFiles(outDir, "*.png"))
    Console.WriteLine($"{Path.GetFileName(f),-28} {new FileInfo(f).Length / 1024} KB");
