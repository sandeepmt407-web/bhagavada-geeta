#:package System.Drawing.Common@9.0.0
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

// Candidate backdrops for the app, drawn from scratch rather than rendered in Unity.
//
// Each is designed for text to sit on top of it: detail is kept to the edges and the
// upper third, the middle band stays quiet, and nothing competes with saffron-on-indigo.

string outDir = @"C:\Users\sverm\.claude\geeta\build\backgrounds";
Directory.CreateDirectory(outDir);

const int W = 540, H = 960;
const int W_ = 540, H_ = 960;   // consts, so the static drawing helpers can see them

var themes = new (string name, Action<Graphics, Random> draw)[]
{
    ("1-sri-yantra",   DrawSriYantra),
    ("2-lotus-mandala",DrawLotusMandala),
    ("3-cosmic",       DrawCosmic),
    ("4-palm-leaf",    DrawPalmLeaf),
    ("5-ink-wash",     DrawInkWash),
    ("6-chakra",       DrawChakra),
    ("7-peacock",      DrawPeacock),
    ("8-diya",         DrawDiya),
};

var rendered = new List<Bitmap>();
foreach (var (name, draw) in themes)
{
    var bmp = new Bitmap(W, H, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        draw(g, new Random(20260920));
    }
    bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
    rendered.Add(bmp);
    Console.WriteLine($"{name,-18} done");
}

// ---- contact sheet --------------------------------------------------------
const int cw = 270, ch = 480, cols = 4;
int rows = (rendered.Count + cols - 1) / cols;
using (var sheet = new Bitmap(cw * cols, ch * rows))
using (var g = Graphics.FromImage(sheet))
{
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.Clear(Color.FromArgb(10, 10, 14));
    for (int i = 0; i < rendered.Count; i++)
        g.DrawImage(rendered[i], (i % cols) * cw, (i / cols) * ch, cw, ch);
    sheet.Save(Path.Combine(outDir, "all-backgrounds.png"), ImageFormat.Png);
}
foreach (var b in rendered) b.Dispose();
Console.WriteLine($"\nWROTE {rendered.Count} backgrounds + sheet to {outDir}");

// ===========================================================================
// shared helpers
// ===========================================================================

static Color Hex(string rgb) => ColorTranslator.FromHtml("#" + rgb);

/// <summary>Vertical gradient wash, the base of most of these.</summary>
static void Wash(Graphics g, Color top, Color bottom)
{
    using var brush = new LinearGradientBrush(
        new Rectangle(0, -1, W_, H_ + 2), top, bottom, LinearGradientMode.Vertical);
    g.FillRectangle(brush, 0, 0, W_, H_);
}

/// <summary>A soft radial pool of light centred anywhere on the canvas.</summary>
static void Glow(Graphics g, float cx, float cy, float radius, Color colour, int alpha)
{
    using var path = new GraphicsPath();
    path.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
    using var brush = new PathGradientBrush(path)
    {
        CenterPoint = new PointF(cx, cy),
        CenterColor = Color.FromArgb(alpha, colour),
        SurroundColors = new[] { Color.FromArgb(0, colour) },
    };
    g.FillEllipse(brush, cx - radius, cy - radius, radius * 2, radius * 2);
}

/// <summary>Fine grain, so large flat gradients do not band on a phone screen.</summary>
static void Grain(Graphics g, Random rng, int count, int alpha)
{
    for (int i = 0; i < count; i++)
    {
        int x = rng.Next(W_), y = rng.Next(H_);
        int a = rng.Next(alpha / 3, alpha);
        using var b = new SolidBrush(Color.FromArgb(a, 255, 255, 255));
        g.FillRectangle(b, x, y, 1, 1);
    }
}


// ===========================================================================
// 1. Sri Yantra - interlocking triangles, lotus rings, outer gates
// ===========================================================================
static void DrawSriYantra(Graphics g, Random rng)
{
    Wash(g, Hex("0A0E20"), Hex("161A36"));
    Glow(g, W_ * 0.5f, H_ * 0.42f, 300, Hex("2A3270"), 120);

    float cx = W_ * 0.5f, cy = H_ * 0.42f;
    var gold = Hex("E8A33D");

    // outer square with four gates
    using (var pen = new Pen(Color.FromArgb(70, gold), 1.6f))
    {
        for (int i = 0; i < 3; i++)
        {
            float s = 215 - i * 9;
            g.DrawRectangle(pen, cx - s, cy - s, s * 2, s * 2);
        }
        // gate openings
        using var cut = new SolidBrush(Hex("11152C"));
        float gw = 46;
        g.FillRectangle(cut, cx - gw / 2, cy - 218, gw, 30);
        g.FillRectangle(cut, cx - gw / 2, cy + 188, gw, 30);
        g.FillRectangle(cut, cx - 218, cy - gw / 2, 30, gw);
        g.FillRectangle(cut, cx + 188, cy - gw / 2, 30, gw);
    }

    Petals(g, cx, cy, 178, 26, 16, Color.FromArgb(85, gold));
    Petals(g, cx, cy, 148, 22, 8, Color.FromArgb(100, gold));

    using (var pen = new Pen(Color.FromArgb(95, gold), 1.4f))
        g.DrawEllipse(pen, cx - 130, cy - 130, 260, 260);

    // The nine interlocking triangles, alternating upward and downward.
    float[] scales = { 1.00f, 0.86f, 0.72f, 0.58f, 0.44f };
    for (int i = 0; i < scales.Length; i++)
    {
        float r = 124 * scales[i];
        int alpha = 150 - i * 12;
        Triangle(g, cx, cy, r, true, Color.FromArgb(alpha, gold));
        if (i < 4) Triangle(g, cx, cy - 6, r * 0.94f, false, Color.FromArgb(alpha, gold));
    }

    // bindu
    using (var b = new SolidBrush(Color.FromArgb(220, gold)))
        g.FillEllipse(b, cx - 4.5f, cy - 4.5f, 9, 9);
    Glow(g, cx, cy, 40, gold, 90);

    Grain(g, rng, 9000, 16);
}

static void Triangle(Graphics g, float cx, float cy, float r, bool up, Color colour)
{
    var pts = new PointF[3];
    for (int i = 0; i < 3; i++)
    {
        double a = Math.PI * 2 * i / 3 + (up ? -Math.PI / 2 : Math.PI / 2);
        pts[i] = new PointF(cx + (float)Math.Cos(a) * r, cy + (float)Math.Sin(a) * r);
    }
    using var pen = new Pen(colour, 1.5f);
    g.DrawPolygon(pen, pts);
}

/// <summary>A ring of lotus petals, each an arc pair.</summary>
static void Petals(Graphics g, float cx, float cy, float radius, float size, int count, Color colour)
{
    using var pen = new Pen(colour, 1.4f);
    for (int i = 0; i < count; i++)
    {
        double a = Math.PI * 2 * i / count;
        float px = cx + (float)Math.Cos(a) * radius;
        float py = cy + (float)Math.Sin(a) * radius;

        using var path = new GraphicsPath();
        var state = g.Save();
        g.TranslateTransform(px, py);
        g.RotateTransform((float)(a * 180 / Math.PI) + 90);
        path.AddBezier(0, size, size * 0.72f, size * 0.25f, size * 0.55f, -size * 0.7f, 0, -size);
        path.AddBezier(0, -size, -size * 0.55f, -size * 0.7f, -size * 0.72f, size * 0.25f, 0, size);
        g.DrawPath(pen, path);
        g.Restore(state);
    }
}

// ===========================================================================
// 2. Lotus mandala - concentric petal rings, gold on plum
// ===========================================================================
static void DrawLotusMandala(Graphics g, Random rng)
{
    Wash(g, Hex("1A0E1C"), Hex("2C1424"));
    Glow(g, W_ * 0.5f, H_ * 0.40f, 340, Hex("6B2A46"), 110);

    float cx = W_ * 0.5f, cy = H_ * 0.40f;
    var gold = Hex("F0B65A");

    Petals(g, cx, cy, 208, 34, 24, Color.FromArgb(55, gold));
    Petals(g, cx, cy, 166, 30, 18, Color.FromArgb(75, gold));
    Petals(g, cx, cy, 124, 26, 12, Color.FromArgb(100, gold));
    Petals(g, cx, cy, 84, 22, 8, Color.FromArgb(130, gold));

    using (var pen = new Pen(Color.FromArgb(60, gold), 1.2f))
    {
        g.DrawEllipse(pen, cx - 240, cy - 240, 480, 480);
        g.DrawEllipse(pen, cx - 246, cy - 246, 492, 492);
    }

    using (var b = new SolidBrush(Color.FromArgb(200, gold)))
        g.FillEllipse(b, cx - 15, cy - 15, 30, 30);
    using (var b = new SolidBrush(Hex("2C1424")))
        g.FillEllipse(b, cx - 8, cy - 8, 16, 16);

    Glow(g, cx, cy, 70, gold, 70);
    Grain(g, rng, 9000, 15);
}

// ===========================================================================
// 3. Cosmic - the universal form; deep space, nebula, stars
// ===========================================================================
static void DrawCosmic(Graphics g, Random rng)
{
    Wash(g, Hex("05060F"), Hex("0B0A1A"));

    // nebula: a few overlapping coloured pools
    Glow(g, W_ * 0.30f, H_ * 0.26f, 300, Hex("3B2A7A"), 95);
    Glow(g, W_ * 0.74f, H_ * 0.38f, 260, Hex("7A2A55"), 70);
    Glow(g, W_ * 0.52f, H_ * 0.62f, 340, Hex("18355E"), 80);
    Glow(g, W_ * 0.40f, H_ * 0.45f, 150, Hex("C08A3E"), 45);

    // stars, mostly faint with a scatter of bright ones
    for (int i = 0; i < 1400; i++)
    {
        float x = (float)rng.NextDouble() * W_;
        float y = (float)rng.NextDouble() * H_;
        double roll = rng.NextDouble();
        float size = roll > 0.985 ? 2.6f : roll > 0.93 ? 1.7f : 1.0f;
        int alpha = roll > 0.985 ? 235 : roll > 0.93 ? 165 : rng.Next(40, 120);

        using var b = new SolidBrush(Color.FromArgb(alpha, 255, 252, 244));
        g.FillEllipse(b, x, y, size, size);
        if (size > 2f) Glow(g, x, y, 9, Color.White, 70);
    }

    Grain(g, rng, 6000, 12);
}

// ===========================================================================
// 4. Palm leaf - the manuscript the text actually came down on
// ===========================================================================
static void DrawPalmLeaf(Graphics g, Random rng)
{
    Wash(g, Hex("C9A971"), Hex("A8854F"));

    // lengthwise fibre
    for (int i = 0; i < 260; i++)
    {
        float y = (float)rng.NextDouble() * H_;
        int alpha = rng.Next(8, 26);
        using var pen = new Pen(Color.FromArgb(alpha, 92, 62, 28), (float)(rng.NextDouble() * 1.4 + 0.3));
        g.DrawLine(pen, 0, y, W_, y + (float)(rng.NextDouble() * 6 - 3));
    }

    // age mottling
    for (int i = 0; i < 150; i++)
    {
        float x = (float)rng.NextDouble() * W_;
        float y = (float)rng.NextDouble() * H_;
        float r = (float)rng.NextDouble() * 46 + 8;
        Glow(g, x, y, r, Hex("6B4A1E"), rng.Next(10, 30));
    }

    // the two cord holes of a pothi leaf
    foreach (float hx in new[] { W_ * 0.30f, W_ * 0.70f })
    {
        Glow(g, hx, H_ * 0.5f, 26, Hex("4A3313"), 90);
        using var b = new SolidBrush(Color.FromArgb(150, 58, 40, 16));
        g.FillEllipse(b, hx - 7, H_ * 0.5f - 7, 14, 14);
    }

    // darkened, slightly irregular edges
    using (var path = new GraphicsPath())
    {
        path.AddRectangle(new RectangleF(-140, -140, W_ + 280, H_ + 280));
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(W_ * 0.5f, H_ * 0.5f),
            CenterColor = Color.FromArgb(0, 40, 26, 8),
            SurroundColors = new[] { Color.FromArgb(190, 40, 26, 8) },
        };
        g.FillRectangle(brush, 0, 0, W_, H_);
    }

    Grain(g, rng, 14000, 22);
}

// ===========================================================================
// 5. Ink wash - layered peaks, almost nothing else
// ===========================================================================
static void DrawInkWash(Graphics g, Random rng)
{
    Wash(g, Hex("EDE7DA"), Hex("D8CFBE"));

    // pale sun
    Glow(g, W_ * 0.66f, H_ * 0.22f, 120, Hex("B8A282"), 110);
    using (var b = new SolidBrush(Color.FromArgb(70, 120, 104, 78)))
        g.FillEllipse(b, W_ * 0.66f - 34, H_ * 0.22f - 34, 68, 68);

    // ranges receding into mist: darkest in front
    var baseY = new[] { 0.52f, 0.58f, 0.64f, 0.72f, 0.84f };
    var alpha = new[] { 40, 62, 92, 130, 185 };
    for (int layer = 0; layer < baseY.Length; layer++)
    {
        using var path = new GraphicsPath();
        var pts = new List<PointF> { new(-20, H_ + 20) };

        int peaks = 3 + layer;
        float y0 = H_ * baseY[layer];
        for (int i = 0; i <= peaks * 4; i++)
        {
            float t = i / (float)(peaks * 4);
            float x = -20 + t * (W_ + 40);
            float ridge = (float)(Math.Sin(t * Math.PI * peaks + layer * 1.7) * 0.5 + 0.5);
            float y = y0 - ridge * (70 - layer * 8) - (float)rng.NextDouble() * 6;
            pts.Add(new PointF(x, y));
        }
        pts.Add(new PointF(W_ + 20, H_ + 20));

        path.AddClosedCurve(pts.ToArray(), 0.35f);
        using var brush = new SolidBrush(Color.FromArgb(alpha[layer], 46, 52, 58));
        g.FillPath(brush, path);
    }

    Grain(g, rng, 16000, 26);
}

// ===========================================================================
// 6. Dharma chakra - one large wheel, held well back
// ===========================================================================
static void DrawChakra(Graphics g, Random rng)
{
    Wash(g, Hex("0C1128"), Hex("1A2246"));
    Glow(g, W_ * 0.5f, H_ * 0.34f, 320, Hex("35407F"), 120);

    float cx = W_ * 0.5f, cy = H_ * 0.34f, r = 190;
    var gold = Hex("E8A33D");
    int a = 58;   // held back so text reads cleanly over it

    using (var pen = new Pen(Color.FromArgb(a + 24, gold), 7f))
        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
    using (var pen = new Pen(Color.FromArgb(a, gold), 2.4f))
        g.DrawEllipse(pen, cx - r * 0.82f, cy - r * 0.82f, r * 1.64f, r * 1.64f);

    using (var pen = new Pen(Color.FromArgb(a, gold), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        for (int i = 0; i < 24; i++)
        {
            double ang = Math.PI * 2 * i / 24;
            g.DrawLine(pen,
                cx + (float)Math.Cos(ang) * r * 0.17f, cy + (float)Math.Sin(ang) * r * 0.17f,
                cx + (float)Math.Cos(ang) * r * 0.80f, cy + (float)Math.Sin(ang) * r * 0.80f);
        }

    using (var b = new SolidBrush(Color.FromArgb(a + 40, gold)))
        g.FillEllipse(b, cx - r * 0.17f, cy - r * 0.17f, r * 0.34f, r * 0.34f);
    using (var b = new SolidBrush(Hex("121A3A")))
        g.FillEllipse(b, cx - r * 0.07f, cy - r * 0.07f, r * 0.14f, r * 0.14f);

    Grain(g, rng, 9000, 15);
}

// ===========================================================================
// 7. Peacock - the mor pankh Krishna wears
// ===========================================================================
static void DrawPeacock(Graphics g, Random rng)
{
    Wash(g, Hex("06181E"), Hex("0B2430"));
    Glow(g, W_ * 0.5f, H_ * 0.44f, 330, Hex("0E4A55"), 120);

    float cx = W_ * 0.5f, cy = H_ * 0.44f;

    // barbs radiating out from the eye
    for (int i = 0; i < 260; i++)
    {
        double ang = Math.PI * 2 * i / 260 + rng.NextDouble() * 0.02;
        float inner = 58 + (float)rng.NextDouble() * 10;
        float outer = 190 + (float)rng.NextDouble() * 110;
        int alpha = rng.Next(18, 58);

        using var pen = new Pen(Color.FromArgb(alpha, 34, 150, 150), 1.1f);
        g.DrawLine(pen,
            cx + (float)Math.Cos(ang) * inner, cy + (float)Math.Sin(ang) * inner,
            cx + (float)Math.Cos(ang) * outer, cy + (float)Math.Sin(ang) * outer);
    }

    // the eye: bronze ring, blue heart, dark centre
    Glow(g, cx, cy, 120, Hex("1F7F7A"), 110);
    using (var b = new SolidBrush(Color.FromArgb(205, 176, 122, 44)))
        g.FillEllipse(b, cx - 62, cy - 70, 124, 140);
    using (var b = new SolidBrush(Color.FromArgb(225, 24, 70, 122)))
        g.FillEllipse(b, cx - 44, cy - 50, 88, 100);
    using (var b = new SolidBrush(Color.FromArgb(240, 10, 26, 48)))
        g.FillEllipse(b, cx - 24, cy - 28, 48, 56);
    Glow(g, cx, cy - 8, 34, Hex("3FA9C4"), 120);

    Grain(g, rng, 9000, 14);
}

// ===========================================================================
// 8. Diya - one lamp in the dark
// ===========================================================================
static void DrawDiya(Graphics g, Random rng)
{
    Wash(g, Hex("07060A"), Hex("120C0A"));

    float cx = W_ * 0.5f, cy = H_ * 0.66f;

    Glow(g, cx, cy, 380, Hex("C2701C"), 70);
    Glow(g, cx, cy, 210, Hex("E8992E"), 90);
    Glow(g, cx, cy - 10, 96, Hex("FFC45A"), 130);

    // flame
    using (var path = new GraphicsPath())
    {
        path.AddBezier(cx, cy - 74, cx + 21, cy - 34, cx + 15, cy + 4, cx, cy + 14);
        path.AddBezier(cx, cy + 14, cx - 15, cy + 4, cx - 21, cy - 34, cx, cy - 74);
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(cx, cy - 16),
            CenterColor = Color.FromArgb(255, 255, 240, 196),
            SurroundColors = new[] { Color.FromArgb(120, 226, 128, 24) },
        };
        g.FillPath(brush, path);
    }
    Glow(g, cx, cy - 22, 30, Color.FromArgb(255, 255, 236, 180), 190);

    // rising motes
    for (int i = 0; i < 90; i++)
    {
        float x = cx + (float)(rng.NextDouble() - 0.5) * 210;
        float y = cy - (float)rng.NextDouble() * 430;
        float s = (float)rng.NextDouble() * 2.1f + 0.6f;
        int alpha = (int)(58 * (1 - (cy - y) / 430f)) + 12;
        using var b = new SolidBrush(Color.FromArgb(Math.Clamp(alpha, 0, 255), 255, 196, 120));
        g.FillEllipse(b, x, y, s, s);
    }

    Grain(g, rng, 7000, 12);
}
