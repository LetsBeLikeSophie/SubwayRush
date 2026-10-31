using System;
using System.Collections.Generic;
using UnityEngine;

// 그림 파일 없이 코드로 그리는 일러스트. 부드러운 도형을 겹쳐 128px/유닛으로 굽는다.
public static class Art
{
    public const int PPU = 128;
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Color Hex(int rgb, float a = 1) { return new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a); }
    public static readonly Color Line = Hex(0x23222E);

    public class Cv
    {
        public int w, h; public Color[] p;
        public Cv(int w, int h) { this.w = w; this.h = h; p = new Color[w * h]; }

        void Blend(int i, Color c, float a)
        {
            if (a <= 0) return;
            a *= c.a;
            var d = p[i];
            float oa = a + d.a * (1 - a);
            if (oa <= 0) return;
            float t = a / oa;
            d.r += (c.r - d.r) * t; d.g += (c.g - d.g) * t; d.b += (c.b - d.b) * t; d.a = oa;
            p[i] = d;
        }

        delegate float Sdf(float x, float y);
        void Shape(float x0, float y0, float x1, float y1, Sdf f, Func<float, float, Color> col)
        {
            int ax = Mathf.Max(0, (int)x0 - 2), ay = Mathf.Max(0, (int)y0 - 2), bx = Mathf.Min(w - 1, (int)x1 + 2), by = Mathf.Min(h - 1, (int)y1 + 2);
            for (int j = ay; j <= by; j++)
                for (int i = ax; i <= bx; i++)
                {
                    float a = Mathf.Clamp01(.5f - f(i + .5f, j + .5f));
                    if (a > 0) Blend(j * w + i, col(i, j), a);
                }
        }

        static float RectD(float px, float py, float x, float y, float rw, float rh, float r)
        {
            float qx = Mathf.Abs(px - x - rw / 2) - rw / 2 + r, qy = Mathf.Abs(py - y - rh / 2) - rh / 2 + r;
            float ox = Mathf.Max(qx, 0), oy = Mathf.Max(qy, 0);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
        }

        public void R(float x, float y, float rw, float rh, float r, Color c) { Shape(x, y, x + rw, y + rh, (a, b) => RectD(a, b, x, y, rw, rh, r), (a, b) => c); }
        public void RV(float x, float y, float rw, float rh, float r, Color top, Color bot)
        {
            Shape(x, y, x + rw, y + rh, (a, b) => RectD(a, b, x, y, rw, rh, r), (a, b) => Color.Lerp(bot, top, Mathf.Clamp01((b - y) / rh)));
        }
        public void RO(float x, float y, float rw, float rh, float r, Color c, float t = 3) { R(x - t, y - t, rw + 2 * t, rh + 2 * t, r + t, Line); R(x, y, rw, rh, r, c); }
        public void E(float cx, float cy, float rx, float ry, Color c)
        {
            float m = Mathf.Min(rx, ry);
            Shape(cx - rx, cy - ry, cx + rx, cy + ry, (a, b) => { float u = (a - cx) / rx, v = (b - cy) / ry; return (Mathf.Sqrt(u * u + v * v) - 1) * m; }, (a, b) => c);
        }
        public void EO(float cx, float cy, float rx, float ry, Color c, float t = 3) { E(cx, cy, rx + t, ry + t, Line); E(cx, cy, rx, ry, c); }
        public void Cap(float x1, float y1, float x2, float y2, float r, Color c)
        {
            float dx = x2 - x1, dy = y2 - y1, l = dx * dx + dy * dy;
            Shape(Mathf.Min(x1, x2) - r, Mathf.Min(y1, y2) - r, Mathf.Max(x1, x2) + r, Mathf.Max(y1, y2) + r, (a, b) =>
            {
                float t = l > 0 ? Mathf.Clamp01(((a - x1) * dx + (b - y1) * dy) / l) : 0;
                float qx = a - x1 - dx * t, qy = b - y1 - dy * t;
                return Mathf.Sqrt(qx * qx + qy * qy) - r;
            }, (a, b) => c);
        }
        public void CapO(float x1, float y1, float x2, float y2, float r, Color c, float t = 3) { Cap(x1, y1, x2, y2, r + t, Line); Cap(x1, y1, x2, y2, r, c); }
        public void Poly(Color c, params float[] xy)
        {
            int n = xy.Length / 2; float x0 = 1e9f, y0 = 1e9f, x1 = -1e9f, y1 = -1e9f;
            for (int i = 0; i < n; i++) { x0 = Mathf.Min(x0, xy[i * 2]); x1 = Mathf.Max(x1, xy[i * 2]); y0 = Mathf.Min(y0, xy[i * 2 + 1]); y1 = Mathf.Max(y1, xy[i * 2 + 1]); }
            Shape(x0, y0, x1, y1, (a, b) =>
            {
                float d = 1e9f; bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i, i++)
                {
                    float ax = xy[i * 2], ay = xy[i * 2 + 1], bx = xy[j * 2], by = xy[j * 2 + 1];
                    float ex = bx - ax, ey = by - ay, wx = a - ax, wy = b - ay;
                    float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                    float qx = wx - ex * t, qy = wy - ey * t;
                    d = Mathf.Min(d, qx * qx + qy * qy);
                    if ((ay > b) != (by > b) && a < ex * (b - ay) / ey + ax) inside = !inside;
                }
                d = Mathf.Sqrt(d);
                return inside ? -d : d;
            }, (a, b) => c);
        }
        // 구멍 뚫기: 창문·문 자리
        public void Cut(float x, float y, float rw, float rh, float r)
        {
            int ax = Mathf.Max(0, (int)x - 2), ay = Mathf.Max(0, (int)y - 2), bx = Mathf.Min(w - 1, (int)(x + rw) + 2), by = Mathf.Min(h - 1, (int)(y + rh) + 2);
            for (int j = ay; j <= by; j++)
                for (int i = ax; i <= bx; i++)
                {
                    float a = Mathf.Clamp01(.5f - RectD(i + .5f, j + .5f, x, y, rw, rh, r));
                    p[j * w + i].a *= 1 - a;
                }
        }
        public Texture2D Tex()
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            // 투명한 칸의 색을 가까운 색으로 채워 가장자리 검은 테를 막는다
            for (int k = 0; k < p.Length; k++)
                if (p[k].a <= 0)
                {
                    Color n = default; bool found = false;
                    if (k % w > 0 && p[k - 1].a > 0) { n = p[k - 1]; found = true; }
                    else if (k % w < w - 1 && p[k + 1].a > 0) { n = p[k + 1]; found = true; }
                    else if (k >= w && p[k - w].a > 0) { n = p[k - w]; found = true; }
                    else if (k < p.Length - w && p[k + w].a > 0) { n = p[k + w]; found = true; }
                    if (found) { n.a = 0; p[k] = n; }
                }
            t.SetPixels(p); t.Apply();
            return t;
        }
        public Sprite Spr(float px, float py) { return Sprite.Create(Tex(), new Rect(0, 0, w, h), new Vector2(px, py), PPU, 0, SpriteMeshType.FullRect); }
    }

    static Sprite Get(string key, Func<Sprite> make)
    {
        if (!cache.TryGetValue(key, out var s)) { s = make(); cache[key] = s; }
        return s;
    }

    public static Sprite Pixel() { return Get("pixel", () => { var c = new Cv(4, 4); c.R(-2, -2, 8, 8, 0, Color.white); return Sprite.Create(c.Tex(), new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4); }); }

    // ---------- 사람 ----------

    public class Look { public Color top, pants, shoe, skin = Hex(0xF6CFB0), hair = Hex(0x2B2530); public float width = 1; }

    static Look LookOf(string id)
    {
        var l = new Look { top = Hex(0x7E8496), pants = Hex(0x3C3F4D), shoe = Hex(0x26252E) };
        switch (id)
        {
            case "player": l.top = Hex(0x2F3C66); l.pants = Hex(0x28325A); break;
            case "phone": l.top = Hex(0x8A909E); l.pants = Hex(0x444754); l.hair = Hex(0x3B2F2C); break;
            case "school": l.top = Hex(0x2C4C94); l.pants = Hex(0x565B6E); l.shoe = Hex(0xEDEDED); break;
            case "hike": l.top = Hex(0xE8622C); l.pants = Hex(0x4F5A45); l.shoe = Hex(0x6B4A30); l.skin = Hex(0xE5B795); l.hair = Hex(0xB9B9BE); break;
            case "bundle": l.top = Hex(0xB65C9C); l.pants = Hex(0x7B4A8C); l.skin = Hex(0xE5B795); l.hair = Hex(0x9A5B3A); l.width = 1.08f; break;
            case "spread": l.top = Hex(0xB39C72); l.pants = Hex(0x5A5040); l.skin = Hex(0xE5B795); l.width = 1.3f; break;
            case "makeup": l.top = Hex(0xF4BCCB); l.pants = Hex(0xF2EEE6); l.shoe = Hex(0xD8496A); l.hair = Hex(0x74492F); break;
            case "box": l.top = Hex(0x3E7F68); l.pants = Hex(0x484B55); l.skin = Hex(0xE5B795); l.hair = Hex(0xA8A8AE); break;
            case "drunk": l.top = Hex(0x4A4A58); l.pants = Hex(0x33333F); l.skin = Hex(0xF0A58E); break;
            case "survey": l.top = Hex(0xDAD4BC); l.pants = Hex(0x75695A); l.hair = Hex(0x40362F); break;
            case "sleeper": l.top = Hex(0x6E7486); l.pants = Hex(0x3C3F4D); l.hair = Hex(0x33302E); break;
            case "trainer": l.top = Hex(0xE03E4E); l.pants = Hex(0x25252F); l.shoe = Hex(0xEEEEEE); l.skin = Hex(0xD79F78); l.width = 1.3f; break;
            case "kid": l.top = Hex(0xF2C230); l.pants = Hex(0x4A7FC8); l.shoe = Hex(0xE85A5A); break;
            case "ajumma": l.top = Hex(0x8E44AD); l.pants = Hex(0x4C3A70); l.skin = Hex(0xE5B795); l.hair = Hex(0x3A2C22); break;
            case "tourist": l.top = Hex(0x4FB7C5); l.pants = Hex(0xCDB98E); l.shoe = Hex(0xEDEDED); l.skin = Hex(0xF8DCC8); l.hair = Hex(0xD8B25A); break;
            case "dancer": l.top = Hex(0x2BBE8A); l.pants = Hex(0x2B2B38); l.shoe = Hex(0xEDEDED); l.hair = Hex(0x5B3AA8); break;
            case "gimbap": l.top = Hex(0xA9B0BE); l.pants = Hex(0x505666); l.hair = Hex(0x3B2F2C); break;
            case "tteok": l.top = Hex(0xF4EFE0); l.pants = Hex(0x7A6A58); l.skin = Hex(0xE5B795); l.hair = Hex(0x4A3A2C); break;
            case "boss": l.top = Hex(0x25252F); l.pants = Hex(0x1E1E27); l.skin = Hex(0xE5B795); l.hair = Hex(0x9F9FA6); l.width = 1.35f; break;
        }
        return l;
    }

    static Color Dark2(Color c) { return Color.Lerp(c, Color.black, .3f); }

    public enum Face { Normal, Hurt, Dizzy }

    public static Sprite Head(string id, Face f) { return Get("h_" + id + f, () => DrawHead(id, f)); }

    static Sprite DrawHead(string id, Face f)
    {
        var l = LookOf(id); var c = new Cv(136, 136);
        float cx = 68, cy = 60;
        // 뒤쪽 머리
        if (id == "makeup") { c.RO(cx - 42, cy - 42, 84, 96, 36, l.hair); }
        if (id == "bundle") for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; c.EO(cx + Mathf.Cos(a) * 42, cy + 8 + Mathf.Sin(a) * 42, 15, 15, l.hair); }
        // 귀와 얼굴
        c.EO(cx + 41, cy - 2, 8, 10, l.skin, 2.5f); c.EO(cx - 41, cy - 2, 8, 10, l.skin, 2.5f);
        c.EO(cx, cy, 40, 44, l.skin);
        c.E(cx + 22, cy - 14, 9, 6, new Color(.95f, .45f, .4f, id == "drunk" ? .75f : .22f));
        c.E(cx - 22, cy - 14, 9, 6, new Color(.95f, .45f, .4f, id == "drunk" ? .75f : .22f));
        // 눈과 입
        float ex = cx + 15, ex2 = cx - 13, ey = cy + 2;
        if (f == Face.Hurt)
        {
            c.Cap(ex - 8, ey + 6, ex + 7, ey, 2.6f, Line); c.Cap(ex - 8, ey - 6, ex + 7, ey, 2.6f, Line);
            c.Cap(ex2 + 8, ey + 6, ex2 - 7, ey, 2.6f, Line); c.Cap(ex2 + 8, ey - 6, ex2 - 7, ey, 2.6f, Line);
            c.EO(cx + 2, cy - 22, 11, 8, Hex(0xA8353D), 2.5f);
        }
        else if (f == Face.Dizzy)
        {
            c.E(ex, ey, 9, 9, Line); c.E(ex, ey, 6, 6, Color.white); c.E(ex, ey, 3, 3, Line);
            c.E(ex2, ey, 9, 9, Line); c.E(ex2, ey, 6, 6, Color.white); c.E(ex2, ey, 3, 3, Line);
            c.Cap(cx - 6, cy - 22, cx + 10, cy - 20, 2.4f, Line);
        }
        else if (id == "phone" || id == "drunk" || id == "sleeper")
        {
            c.Cap(ex - 7, ey, ex + 7, ey, 2.6f, Line); c.Cap(ex2 - 7, ey, ex2 + 7, ey, 2.6f, Line);
            c.Cap(cx - 3, cy - 22, cx + 9, cy - 22, 2.2f, Line);
        }
        else
        {
            c.E(ex, ey, 5.5f, 7.5f, Line); c.E(ex2, ey, 5.5f, 7.5f, Line); c.E(ex + 1.5f, ey + 3, 2, 2, Color.white); c.E(ex2 + 1.5f, ey + 3, 2, 2, Color.white);
            c.Cap(cx - 5, cy - 22, cx + 9, cy - 21, 2.2f, id == "makeup" ? Hex(0xD22F54) : Line);
        }
        // 눈썹
        if (f == Face.Normal && id != "drunk")
        {
            bool cross = id == "boss" || id == "hike" || id == "bundle";
            c.Cap(ex - 7, ey + (cross ? 15 : 17), ex + 8, ey + (cross ? 11 : 17), 2.4f, Line);
            c.Cap(ex2 - 8, ey + (cross ? 11 : 17), ex2 + 7, ey + (cross ? 15 : 17), 2.4f, Line);
        }
        // 머리 모양
        switch (id)
        {
            case "player":
                c.Poly(l.hair, cx - 42, cy + 8, cx - 38, cy + 40, cx - 10, cy + 54, cx + 28, cy + 50, cx + 42, cy + 22, cx + 30, cy + 24, cx + 8, cy + 30, cx - 28, cy + 26);
                c.E(cx + 15, ey - 11, 8, 2.5f, new Color(.35f, .3f, .5f, .45f)); c.E(ex2, ey - 11, 8, 2.5f, new Color(.35f, .3f, .5f, .45f)); // 다크서클
                break;
            case "phone": c.Poly(l.hair, cx - 42, cy + 2, cx - 34, cy + 40, cx, cy + 50, cx + 34, cy + 40, cx + 42, cy + 8, cx + 22, cy + 30, cx - 26, cy + 30); break;
            case "school": c.Poly(l.hair, cx - 43, cy, cx - 38, cy + 38, cx, cy + 52, cx + 38, cy + 38, cx + 43, cy, cx + 30, cy + 18, cx + 14, cy + 14, cx, cy + 20, cx - 16, cy + 14, cx - 30, cy + 18); break;
            case "hike":
                c.Poly(l.hair, cx - 42, cy + 2, cx - 38, cy + 30, cx + 38, cy + 30, cx + 42, cy + 2, cx + 28, cy + 24, cx - 28, cy + 24);
                c.RO(cx - 40, cy + 26, 80, 14, 7, Hex(0xE6E6E0)); c.RO(cx + 6, cy + 24, 52, 9, 4, Hex(0x2C6FB5), 2.5f);
                break;
            case "bundle": c.E(cx, cy + 38, 38, 15, l.hair); for (int i = -3; i <= 3; i++) c.E(cx + i * 11, cy + 30 + (3 - Mathf.Abs(i)) * 2, 8, 8, l.hair); break;
            case "spread": c.Poly(l.hair, cx - 42, cy, cx - 38, cy + 24, cx - 26, cy + 26, cx - 32, cy + 6); c.Poly(l.hair, cx + 42, cy, cx + 38, cy + 24, cx + 26, cy + 26, cx + 32, cy + 6); c.Cap(cx - 14, cy + 40, cx + 14, cy + 44, 1.6f, l.hair); c.Cap(cx - 2, cy - 15, cx + 14, cy - 15, 3.2f, l.hair); break;
            case "makeup":
                c.Poly(l.hair, cx - 42, cy - 4, cx - 38, cy + 40, cx, cy + 52, cx + 38, cy + 40, cx + 42, cy - 4, cx + 32, cy + 22, cx - 6, cy + 32, cx - 30, cy + 16);
                c.Cap(ex - 7, ey + 8, ex + 8, ey + 9, 1.6f, Line);
                break;
            case "box": c.RO(cx - 42, cy + 24, 84, 26, 13, Hex(0x3E7F68)); c.RO(cx + 12, cy + 22, 48, 9, 4, Hex(0x2C5E4C), 2.5f); break;
            case "drunk":
                c.Poly(l.hair, cx - 42, cy + 4, cx - 36, cy + 42, cx + 36, cy + 42, cx + 42, cy + 4, cx + 22, cy + 28, cx - 20, cy + 26);
                c.R(cx - 44, cy + 24, 88, 9, 3, Hex(0xB7222F)); c.Poly(Hex(0xB7222F), cx - 44, cy + 28, cx - 62, cy + 6, cx - 50, cy + 2, cx - 38, cy + 26);
                break;
            case "survey":
                c.Poly(l.hair, cx - 42, cy + 4, cx - 34, cy + 42, cx + 14, cy + 50, cx + 38, cy + 36, cx + 42, cy + 8, cx + 28, cy + 26, cx - 22, cy + 22);
                c.E(ex, ey, 11, 11, Line); c.E(ex, ey, 8.6f, 8.6f, new Color(1, 1, 1, .35f)); c.E(ex2, ey, 11, 11, Line); c.E(ex2, ey, 8.6f, 8.6f, new Color(1, 1, 1, .35f)); c.E(ex, ey, 4, 5, Line); c.E(ex2, ey, 4, 5, Line);
                break;
            case "sleeper": c.Poly(l.hair, cx - 42, cy + 4, cx - 34, cy + 42, cx + 34, cy + 42, cx + 42, cy + 4, cx + 20, cy + 28, cx - 22, cy + 26); c.Cap(cx + 8, cy - 24, cx + 8, cy - 34, 2.2f, Hex(0x9BD3F2)); break;
            case "trainer": c.Poly(l.hair, cx - 42, cy + 10, cx - 34, cy + 40, cx + 34, cy + 40, cx + 42, cy + 10, cx + 28, cy + 26, cx - 28, cy + 26); c.R(cx - 44, cy + 20, 88, 9, 3, Hex(0xE03E4E)); break;
            case "kid": c.Poly(l.hair, cx - 43, cy, cx - 38, cy + 40, cx, cy + 52, cx + 38, cy + 40, cx + 43, cy, cx + 30, cy + 16, cx, cy + 22, cx - 30, cy + 16); c.RO(cx - 40, cy + 30, 80, 16, 8, Hex(0xF2C230)); break;
            case "ajumma":
                c.Poly(l.hair, cx - 42, cy + 2, cx - 38, cy + 30, cx + 38, cy + 30, cx + 42, cy + 2, cx + 28, cy + 22, cx - 28, cy + 22);
                c.RO(cx - 46, cy + 26, 92, 16, 7, Hex(0xB57EDC)); c.RO(cx - 18, cy + 18, 84, 12, 6, Hex(0x8E44AD), 2.5f);
                break;
            case "tourist":
                c.Poly(l.hair, cx - 42, cy + 2, cx - 36, cy + 40, cx + 36, cy + 40, cx + 42, cy + 2, cx + 26, cy + 24, cx - 26, cy + 24);
                c.R(ex - 11, ey - 6, 22, 13, 4, Line); c.R(ex2 - 11, ey - 6, 22, 13, 4, Line); c.Cap(ex2 + 11, ey, ex - 11, ey, 1.6f, Line);
                break;
            case "dancer":
                c.Poly(l.hair, cx - 42, cy + 2, cx - 34, cy + 44, cx + 34, cy + 44, cx + 42, cy + 2, cx + 26, cy + 26, cx - 24, cy + 24);
                c.Cap(cx - 40, cy + 2, cx - 34, cy + 42, 4, Line); c.Cap(cx + 40, cy + 2, cx + 34, cy + 42, 4, Line); c.Cap(cx - 34, cy + 42, cx + 34, cy + 42, 4, Line);
                c.EO(cx - 42, cy, 10, 13, Hex(0xE03E4E), 2.5f); c.EO(cx + 42, cy, 10, 13, Hex(0xE03E4E), 2.5f);
                break;
            case "gimbap":
                c.Poly(l.hair, cx - 42, cy + 2, cx - 34, cy + 40, cx, cy + 50, cx + 34, cy + 40, cx + 42, cy + 8, cx + 22, cy + 30, cx - 26, cy + 30);
                c.EO(cx + 4, cy - 20, 13, 9, Hex(0x1F2A24), 2); c.E(cx + 4, cy - 20, 8, 5, Color.white); c.E(cx + 4, cy - 20, 3, 2, Hex(0xF2A23A));
                break;
            case "tteok":
                c.E(cx, cy + 38, 38, 15, l.hair); c.R(cx - 44, cy + 24, 88, 10, 4, Hex(0xE85A5A));
                break;
            case "boss":
                c.Poly(l.hair, cx - 42, cy - 4, cx - 40, cy + 22, cx - 26, cy + 24, cx - 32, cy + 2); c.Poly(l.hair, cx + 42, cy - 4, cx + 40, cy + 22, cx + 26, cy + 24, cx + 32, cy + 2);
                c.E(cx - 6, cy + 32, 12, 5, new Color(1, 1, 1, .35f));
                c.R(ex - 12, ey - 7, 24, 15, 3, Line); c.R(ex - 9.5f, ey - 4.5f, 19, 10, 2, new Color(.8f, .9f, 1f, .7f)); c.R(ex2 - 12, ey - 7, 24, 15, 3, Line); c.R(ex2 - 9.5f, ey - 4.5f, 19, 10, 2, new Color(.8f, .9f, 1f, .7f)); c.E(ex, ey, 3.5f, 4, Line); c.E(ex2, ey, 3.5f, 4, Line); c.Cap(ex2 + 12, ey, ex - 12, ey, 1.6f, Line);
                break;
        }
        return c.Spr(.5f, .12f);
    }

    public static Sprite Torso(string id) { return Get("t_" + id, () => DrawTorso(id)); }

    static Sprite DrawTorso(string id)
    {
        var l = LookOf(id); var c = new Cv(170, 120);
        float bw = 74 * l.width, x = 85 - bw / 2, y = 8, bh = 100;
        c.RO(x, y, bw, bh, 22, l.top);
        c.RV(x, y, bw, bh * .45f, 18, new Color(0, 0, 0, 0), new Color(0, 0, 0, .14f));
        float m = 85;
        switch (id)
        {
            case "player":
                c.Poly(Color.white, m - 16, y + bh, m + 16, y + bh, m, y + bh - 42);
                c.Poly(Hex(0xC2303F), m - 6, y + bh - 8, m + 6, y + bh - 8, m + 7, y + 26, m, y + 16, m - 7, y + 26);
                c.Cap(m - 22, y + bh - 2, m + 16, y + 34, 2.4f, Hex(0x2B6CC0)); c.Cap(m + 22, y + bh - 2, m + 16, y + 34, 2.4f, Hex(0x2B6CC0));
                c.RO(m + 5, y + 8, 24, 30, 3, Color.white, 2); c.R(m + 9, y + 22, 9, 11, 1, Hex(0x2B6CC0)); c.R(m + 9, y + 13, 16, 3, 1, Hex(0x888888));
                break;
            case "phone": c.Poly(Color.white, m - 11, y + bh, m + 11, y + bh, m, y + bh - 20); break;
            case "school": c.R(m - 2, y + 4, 4, bh - 6, 2, Hex(0x20386E)); c.Cap(m - 24, y + bh - 6, m - 22, y + 14, 4, Hex(0xC93642)); c.Cap(m + 24, y + bh - 6, m + 22, y + 14, 4, Hex(0xC93642)); break;
            case "hike":
                c.R(m - 2, y + 2, 4, bh - 4, 1, Hex(0x2A2A30)); c.R(x, y + 50, bw, 16, 0, Hex(0x2C6FB5)); c.RO(m + 8, y + 18, 20, 20, 3, Hex(0xC54E20), 2);
                break;
            case "bundle":
                for (int i = 0; i < 14; i++) { float a = i * 2.4f; c.E(m + Mathf.Cos(a) * (10 + i * 2.1f), y + 52 + Mathf.Sin(a) * (8 + i * 2.8f), 4.5f, 4.5f, i % 2 == 0 ? Hex(0xF2D85E) : Hex(0xF3EFEA)); }
                break;
            case "spread": c.E(m, y + 34, bw * .42f, 28, Hex(0xC4AE85)); c.E(m, y + 28, 2.6f, 2.6f, Line); c.R(x, y + 8, bw, 8, 0, Hex(0x3A3026)); break;
            case "makeup": c.E(m, y + bh - 8, 14, 9, Hex(0xFCE4EA)); c.R(m - 2, y + 8, 4, bh - 20, 2, Hex(0xE79BB0)); break;
            case "box": c.R(m - 3, y, 6, bh, 0, Hex(0x2D5E4C)); c.R(x, y + 44, bw, 9, 0, Hex(0xE9C74B)); break;
            case "drunk":
                c.Poly(Color.white, m - 14, y + bh, m + 14, y + bh, m, y + bh - 34);
                c.Cap(m - 4, y + bh - 34, m - 12, y + 62, 2.2f, Hex(0xE0E0E0));
                c.E(m + 14, y + 40, 8, 6, new Color(.9f, .75f, .3f, .55f));
                break;
            case "survey": c.Poly(Hex(0xEDEBDD), m - 14, y + bh, m + 14, y + bh, m, y + bh - 20); c.R(m + 6, y + 44, 20, 2.6f, 1, Hex(0x9E9476)); break;
            case "trainer": c.R(m - 28, y + 58, 56, 4, 2, Dark2(l.top)); c.R(m - 2, y + 18, 4, 40, 2, Dark2(l.top)); c.Cap(m - 36, y + 66, m - 8, y + 66, 6, new Color(1, 1, 1, .15f)); break;
            case "kid": c.R(m - 26, y + 40, 52, 10, 2, Color.white); c.Cap(m - 24, y + bh - 6, m - 22, y + 14, 4, Hex(0x4A7FC8)); c.Cap(m + 24, y + bh - 6, m + 22, y + 14, 4, Hex(0x4A7FC8)); break;
            case "ajumma": c.R(m - 2, y + 4, 4, bh - 6, 2, Hex(0x5E2C78)); c.R(m + 8, y + 40, 18, 16, 3, Hex(0xB57EDC)); break;
            case "tourist": for (int i = 0; i < 6; i++) c.E(m - 24 + i * 10, y + 30 + (i % 2) * 22, 6, 4, Hex(0xF2E26A)); c.RO(m - 10, y + 62, 20, 15, 3, Hex(0x2B2B38), 2); c.E(m, y + 69, 4, 4, Hex(0x7CD0FF)); break;
            case "dancer": c.Poly(Hex(0xF2E26A), m - 14, y + 62, m + 14, y + 62, m, y + 34); break;
            case "gimbap": c.R(m - 16, y + 34, 32, 16, 3, Hex(0xE8E2D2)); c.R(m - 13, y + 38, 26, 8, 2, Hex(0x222A25)); break;
            case "tteok": c.R(m - 24, y + 22, 48, 24, 4, Hex(0xF2B8C8)); c.R(m - 24, y + 62, 48, 8, 2, Hex(0xE85A5A)); break;
            case "boss":
                c.Poly(Color.white, m - 20, y + bh, m + 20, y + bh, m, y + bh - 52);
                c.Poly(Hex(0xA62430), m - 7, y + bh - 8, m + 7, y + bh - 8, m + 9, y + 34, m, y + 22, m - 9, y + 34);
                c.E(m - 28, y + 74, 5, 5, Hex(0xE8C24F)); c.R(x, y + 6, bw, 7, 0, Hex(0x15151C)); c.E(m, y + 8, 5, 5, Hex(0xD4AF37));
                break;
        }
        return c.Spr(.5f, 8f / 120);
    }

    // 팔: 어깨가 위쪽 기준점
    public static Sprite Arm(string id, bool front) { return Get("a_" + id + front, () =>
    {
        var l = LookOf(id); var c = new Cv(84, 110);
        c.CapO(42, 92, 42, 34, 13, l.top); c.CapO(42, 26, 42, 20, 10.5f, l.skin);
        if (front)
            switch (id)
            {
                case "phone": c.RO(49, 8, 16, 30, 3, Hex(0x1F2028), 2); c.R(52, 13, 10, 20, 1, Hex(0x7CD0FF)); break;
                case "makeup": c.RO(50, 8, 8, 26, 2, Hex(0xD92B55), 2); break;
                case "survey": c.RO(50, 2, 30, 38, 3, Hex(0x8F6A42), 2); c.R(54, 6, 22, 28, 1, Color.white); c.R(57, 26, 16, 2, 0, Hex(0x999999)); c.R(57, 20, 16, 2, 0, Hex(0x999999)); break;
                case "hike": c.Cap(50, 40, 50, 0, 2.2f, Hex(0xB9BCC2)); break;
            }
        return c.Spr(.5f, 92f / 110);
    }); }

    public static Sprite Leg(string id) { return Get("l_" + id, () =>
    {
        var l = LookOf(id); var c = new Cv(90, 108);
        c.CapO(38, 92, 38, 22, 15, l.pants);
        c.RO(24, 4, 50, 19, 9, l.shoe); c.R(30, 15, 34, 3, 1.5f, new Color(1, 1, 1, .18f));
        return c.Spr(38f / 90, 92f / 108);
    }); }

    public static Sprite BackPart(string id) { return Get("b_" + id, () =>
    {
        var c = new Cv(130, 130);
        if (id == "school") { c.RO(30, 18, 72, 92, 20, Hex(0xC93642)); c.RO(38, 28, 56, 38, 10, Hex(0xA92A36)); c.R(30, 72, 72, 5, 0, Hex(0xE9C74B)); }
        else if (id == "bundle") { c.EO(60, 56, 50, 46, Hex(0x46925A)); for (int i = 0; i < 10; i++) { float a = i * 2.1f; c.Cap(60 + Mathf.Cos(a) * 26, 56 + Mathf.Sin(a) * 24, 60 + Mathf.Cos(a) * 26 + 10, 56 + Mathf.Sin(a) * 24 + 5, 2.6f, Hex(0xE7EDB0)); } c.Poly(Hex(0x46925A), 88, 96, 118, 118, 100, 88); }
        else return null;
        return c.Spr(.72f, .22f);
    }); }

    public static Sprite FrontPart(string id) { return Get("f_" + id, () =>
    {
        var c = new Cv(150, 110);
        if (id != "box") return null;
        c.RO(16, 10, 118, 88, 4, Hex(0xC69662)); c.R(66, 10, 18, 88, 0, Hex(0xE2CDA0)); c.R(98, 18, 26, 18, 1, Color.white); c.R(101, 28, 20, 2, 0, Line); c.R(101, 22, 14, 2, 0, Line);
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite Shadow() { return Get("shadow", () =>
    {
        var c = new Cv(128, 40);
        for (int j = 0; j < 40; j++) for (int i = 0; i < 128; i++) { float a = (i - 63.5f) / 64f, b = (j - 19.5f) / 20f; c.p[j * 128 + i] = new Color(.03f, .03f, .08f, Mathf.Clamp01(1 - (a * a + b * b)) * .5f); }
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite Slash() { return Get("slash", () =>
    {
        var c = new Cv(256, 256);
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
        {
            float dx = x - 80, dy = y - 128, d = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
            if (Mathf.Abs(ang) > 1.25f) continue;
            float w = 1 - Mathf.Abs(ang) / 1.25f, inner = 120 - 34 * w, a = d > inner && d < 124 ? Mathf.Clamp01((d - inner) / (124 - inner)) * w : 0;
            c.p[y * 256 + x] = new Color(1, 1, 1, Mathf.Clamp01(a * 1.8f));
        }
        return c.Spr(.3f, .5f);
    }); }

    public static Sprite Cloud() { return Get("cloud", () =>
    {
        var c = new Cv(320, 120);
        c.E(90, 50, 62, 30, Color.white); c.E(160, 64, 72, 44, Color.white); c.E(236, 50, 60, 28, Color.white); c.R(50, 22, 230, 28, 14, Color.white);
        return c.Spr(0, .5f);
    }); }

    // ---------- 무기·아이템 아이콘 ----------

    public static Sprite Icon(string id) { return Get("i_" + id, () => DrawIcon(id)); }
    public static Texture2D IconTex(string id) { return Icon(id).texture; }

    static Sprite DrawIcon(string id)
    {
        var c = new Cv(128, 128);
        switch (id)
        {
            case "backpack":
                c.RO(52, 104, 24, 16, 7, Hex(0x1C2C63)); c.RO(24, 16, 80, 94, 26, Hex(0x2D4AA0)); c.RV(24, 16, 80, 50, 22, new Color(0, 0, 0, 0), new Color(0, 0, 0, .2f));
                c.RO(36, 24, 56, 38, 11, Hex(0x233A80), 2.5f); c.R(36, 44, 56, 4, 0, Hex(0xF0C44B)); c.R(24, 70, 80, 6, 0, Hex(0xF0C44B)); c.E(64, 44, 4, 6, Hex(0xE8E8E8));
                break;
            case "tumbler":
                c.RO(38, 12, 52, 80, 9, Hex(0xBCC6CC)); c.R(46, 14, 10, 76, 5, new Color(1, 1, 1, .7f)); c.R(38, 40, 52, 22, 0, Hex(0x1F7A52)); c.E(64, 51, 9, 9, Color.white); c.E(64, 51, 6, 6, Hex(0x1F7A52));
                c.RO(32, 92, 64, 22, 7, Hex(0x1F7A52));
                break;
            case "beadbag":
                c.Cap(44, 76, 44, 110, 4.5f, Hex(0xB46E8E)); c.Cap(84, 76, 84, 110, 4.5f, Hex(0xB46E8E)); c.Cap(44, 110, 84, 110, 4.5f, Hex(0xB46E8E));
                c.RO(18, 12, 92, 68, 14, Hex(0xF39BBF));
                Color[] bc = { Hex(0xFFF27A), Hex(0x7DE3F4), Color.white, Hex(0xC59CFF) };
                for (int j = 0; j < 5; j++) for (int i = 0; i < 7; i++) { var col = bc[(i * 3 + j) % 4]; float bx = 28 + i * 12 + (j % 2) * 5, by = 21 + j * 12; c.E(bx, by, 4.8f, 4.8f, Line); c.E(bx, by, 3.6f, 3.6f, col); }
                break;
            case "keyring":
                c.E(44, 86, 27, 27, Line); c.E(44, 86, 24, 24, Hex(0xC9CDD3)); c.E(44, 86, 15, 15, Line); c.Cut(44 - 12, 86 - 12, 24, 24, 12);
                c.Poly(Line, 60, 70, 74, 80, 118, 8, 112, 6); c.Poly(Hex(0xE8EBEF), 62, 71, 73, 78, 115, 11, 113, 10);
                break;
            case "keybag":
                c.Cap(48, 82, 48, 108, 4.5f, Hex(0xB5728E)); c.Cap(80, 82, 80, 108, 4.5f, Hex(0xB5728E)); c.Cap(48, 108, 80, 108, 4.5f, Hex(0xB5728E));
                c.RO(30, 46, 68, 40, 10, Hex(0xE8A0BC));
                // 주렁주렁 달린 인형들
                Color[] dc = { Hex(0xE8C07A), Hex(0xF4F0E8), Hex(0x9BC8F0), Hex(0xF0A0A0), Hex(0xBDE29A) };
                for (int i = 0; i < 5; i++)
                {
                    float dx = 18 + i * 23, dy = i % 2 == 0 ? 20 : 30;
                    c.Cap(dx, 46, dx, dy + 12, 1.6f, Hex(0x888888));
                    c.EO(dx - 7, dy + 10, 4.5f, 4.5f, dc[i], 2); c.EO(dx + 7, dy + 10, 4.5f, 4.5f, dc[i], 2);
                    c.EO(dx, dy, 10, 10, dc[i], 2.4f); c.E(dx - 3.5f, dy + 1, 1.6f, 1.6f, Line); c.E(dx + 3.5f, dy + 1, 1.6f, 1.6f, Line); c.E(dx, dy - 3, 1.5f, 1.2f, Line);
                }
                break;
            case "umbrella":
                c.Poly(Line, 22, 114, 34, 118, 110, 26, 92, 12); c.Poly(Hex(0x3F4DA6), 26, 112, 34, 115, 106, 26, 92, 16); c.Cap(38, 98, 96, 26, 1.8f, Hex(0x2C3780)); c.Cap(100, 20, 116, 6, 3.2f, Line);
                c.CapO(24, 114, 14, 104, 3.4f, Hex(0xA9743F), 2); c.CapO(14, 104, 18, 94, 3.4f, Hex(0xA9743F), 2);
                break;
            case "selfie":
                c.CapO(14, 16, 74, 76, 3.6f, Hex(0xBFC3CA), 2.5f);
                c.RO(62, 62, 42, 58, 7, Hex(0x1F2028)); c.R(66, 66, 34, 50, 3, Hex(0x79C9F5)); c.E(83, 100, 8, 8, Hex(0xF6CFB0)); c.Cap(78, 96, 88, 96, 1.2f, Line);
                break;
            case "lanyard":
                c.Cap(36, 66, 58, 118, 5, Hex(0xC2303F)); c.Cap(92, 66, 70, 118, 5, Hex(0xC2303F)); c.Cap(58, 118, 70, 118, 5, Hex(0xC2303F));
                c.RO(30, 8, 68, 62, 6, Color.white); c.R(30, 56, 68, 14, 5, Hex(0x2B6CC0)); c.E(50, 34, 11, 13, Hex(0xF6CFB0)); c.R(38, 12, 24, 10, 3, Hex(0x2F3C66)); c.R(68, 34, 22, 4, 1, Hex(0x777777)); c.R(68, 24, 16, 4, 1, Hex(0xAAAAAA));
                break;
            case "card":
                c.RO(10, 36, 108, 62, 4, Color.white); c.R(22, 76, 46, 7, 2, Line); c.R(22, 60, 74, 3.5f, 1, Hex(0x888888)); c.R(22, 50, 58, 3.5f, 1, Hex(0xAAAAAA)); c.E(98, 80, 8, 8, Hex(0x2B6CC0));
                break;
            case "heel":
                c.Poly(Line, 10, 30, 10, 48, 74, 90, 108, 96, 116, 72, 90, 56, 62, 28);
                c.Poly(Hex(0xD3253E), 14, 34, 14, 45, 74, 85, 104, 90, 111, 74, 88, 60, 60, 33);
                c.Poly(Line, 92, 70, 114, 70, 112, 8, 104, 8); c.Poly(Hex(0xA61B30), 96, 70, 111, 70, 109, 11, 106, 11);
                break;
            case "dash":
                c.RO(44, 28, 74, 28, 14, Hex(0x6C4128)); c.RO(44, 28, 44, 58, 14, Hex(0x6C4128)); c.R(40, 20, 80, 8, 3, Line); c.R(60, 56, 24, 3, 1, Color.white); c.R(60, 46, 24, 3, 1, Color.white);
                c.Cap(8, 80, 34, 80, 3.4f, Hex(0xB0B4BC)); c.Cap(14, 60, 36, 60, 3.4f, Hex(0xB0B4BC)); c.Cap(4, 40, 30, 40, 3.4f, Hex(0xB0B4BC));
                break;
            case "hook":
                c.Cap(48, 74, 48, 102, 3.4f, Hex(0x8B6A3C)); c.Cap(80, 74, 80, 102, 3.4f, Hex(0x8B6A3C)); c.Cap(48, 102, 80, 102, 3.4f, Hex(0x8B6A3C));
                c.RO(24, 14, 80, 64, 3, Hex(0xE9D5A8)); c.E(64, 46, 16, 16, Hex(0xC2385A)); c.E(64, 46, 9, 9, Hex(0xE9D5A8));
                break;
            case "flash":
                c.Poly(new Color(1, .96f, .55f, .8f), 54, 62, 122, 118, 122, 8); c.RO(14, 26, 44, 76, 7, Hex(0x1F2028)); c.E(36, 86, 7, 7, Hex(0xFFF27A));
                break;
            case "sanitizer":
                c.RO(36, 12, 56, 76, 9, Hex(0xBFE8F4)); c.R(42, 16, 10, 66, 5, new Color(1, 1, 1, .7f)); c.R(44, 34, 40, 26, 2, Color.white); c.Cap(58, 47, 70, 47, 3, Hex(0x2B6CC0)); c.Cap(64, 41, 64, 53, 3, Hex(0x2B6CC0));
                c.RO(52, 88, 24, 16, 2, Hex(0x2B6CC0)); c.RO(52, 104, 46, 10, 3, Hex(0x2B6CC0));
                break;
            case "doll":
                c.EO(42, 86, 15, 15, Hex(0xE8C07A), 3); c.EO(86, 86, 15, 15, Hex(0xE8C07A), 3);
                c.EO(64, 62, 38, 38, Hex(0xE8C07A), 3.5f); c.E(50, 66, 5, 5, Line); c.E(78, 66, 5, 5, Line); c.E(64, 52, 6, 4, Line); c.EO(64, 16, 22, 16, Hex(0xE8C07A), 3);
                break;
            case "i_power": c.Poly(Line, 64, 124, 80, 84, 122, 80, 90, 54, 102, 12, 64, 36, 26, 12, 38, 54, 6, 80, 48, 84); c.Poly(Hex(0xE8524A), 64, 112, 77, 78, 108, 76, 84, 56, 92, 24, 64, 42, 36, 24, 44, 56, 20, 76, 51, 78); break;
            case "i_guard": c.Poly(Line, 64, 4, 112, 26, 112, 72, 64, 124, 16, 72, 16, 26); c.Poly(Hex(0x4C8ADA), 64, 14, 104, 32, 104, 70, 64, 112, 24, 70, 24, 32); c.Poly(Hex(0x7DB2EE), 64, 14, 64, 112, 24, 70, 24, 32); break;
            case "i_cool": c.EO(64, 64, 50, 50, Color.white); c.Cap(64, 64, 64, 96, 4, Line); c.Cap(64, 64, 86, 54, 4, Line); c.E(64, 64, 6, 6, Hex(0xE8524A)); break;
            case "i_push": c.RO(26, 28, 70, 72, 22, Hex(0xF6CFB0)); c.Cap(40, 96, 40, 112, 8, Line); c.Cap(60, 98, 60, 116, 8, Line); c.Cap(80, 96, 80, 112, 8, Line); c.Cap(96, 62, 116, 62, 7, Hex(0xF2C230)); break;
            case "i_heart": c.E(44, 78, 28, 28, Line); c.E(84, 78, 28, 28, Line); c.Poly(Line, 18, 66, 110, 66, 64, 8); c.E(44, 78, 24, 24, Hex(0xE8524A)); c.E(84, 78, 24, 24, Hex(0xE8524A)); c.Poly(Hex(0xE8524A), 24, 68, 104, 68, 64, 16); break;
            case "laptop":
                c.RO(14, 18, 100, 78, 8, Hex(0x34363F)); c.R(50, 96, 28, 6, 3, Line); c.RO(24, 30, 80, 44, 3, Hex(0xC4C8CE), 2); c.R(30, 36, 68, 32, 1, Hex(0x79C9F5)); c.E(64, 52, 5, 5, Color.white);
                break;
            case "newspaper":
                c.RO(24, 40, 92, 44, 16, Hex(0xE8E4D6)); for (int i = 0; i < 5; i++) c.R(34 + i * 15, 44, 9, 36, 2, Hex(0xB5B1A2)); c.E(26, 62, 12, 21, Hex(0xD0CCBC)); c.E(26, 62, 7, 15, Line);
                break;
            case "baguette":
                c.CapO(18, 24, 110, 104, 16, Hex(0xD99A4E)); for (int i = 0; i < 4; i++) c.Cap(36 + i * 20, 34 + i * 18, 50 + i * 20, 52 + i * 18, 3, Hex(0xF4D9A0));
                break;
            case "folding":
                c.RO(48, 30, 32, 76, 12, Hex(0xC2385A)); c.R(48, 54, 32, 5, 0, Hex(0x8E2240)); c.R(48, 78, 32, 5, 0, Hex(0x8E2240)); c.RO(54, 12, 20, 18, 4, Hex(0x2A2A33)); c.Cap(64, 106, 64, 118, 3, Hex(0x8F9196));
                break;
            case "battery":
                c.RO(26, 16, 76, 96, 12, Hex(0x2B2D36)); c.R(50, 112, 28, 6, 2, Hex(0x8F9196)); for (int i = 0; i < 1; i++) c.R(38, 28, 52, 14, 3, Hex(0xE8524A)); c.R(38, 50, 52, 14, 3, new Color(1, 1, 1, .12f)); c.R(38, 72, 52, 14, 3, new Color(1, 1, 1, .12f)); c.R(38, 94, 52, 10, 3, new Color(1, 1, 1, .12f));
                break;
            case "suitcase":
                c.Cap(50, 96, 50, 120, 3.4f, Hex(0x8F9196)); c.Cap(78, 96, 78, 120, 3.4f, Hex(0x8F9196)); c.Cap(50, 120, 78, 120, 3.4f, Hex(0x8F9196));
                c.RO(28, 20, 72, 78, 10, Hex(0x3B8A8C)); for (int i = 0; i < 3; i++) c.R(40 + i * 18, 26, 6, 66, 3, Hex(0x2C6C6E)); c.E(40, 12, 7, 7, Line); c.E(88, 12, 7, 7, Line);
                break;
            case "lunchbox":
                c.Cap(50, 84, 50, 108, 4, Hex(0xB0472F)); c.Cap(78, 84, 78, 108, 4, Hex(0xB0472F)); c.Cap(50, 108, 78, 108, 4, Hex(0xB0472F));
                c.RO(20, 16, 88, 72, 12, Hex(0xE86A4A)); c.R(20, 48, 88, 6, 0, Hex(0xB0472F)); c.E(38, 72, 5, 5, Color.white); c.E(90, 28, 6, 6, Hex(0xFFD84A));
                break;
            case "garlic":
                c.EO(64, 54, 44, 42, Hex(0xF4F0E4)); c.Cap(64, 90, 64, 114, 7, Line); c.Cap(64, 90, 64, 112, 4.5f, Hex(0xE4DEC8)); c.Cap(46, 18, 40, 76, 2, Hex(0xCFC4DE)); c.Cap(82, 18, 88, 76, 2, Hex(0xCFC4DE)); c.Cap(64, 14, 64, 86, 2, Hex(0xCFC4DE));
                break;
            case "cry":
                c.EO(64, 64, 48, 48, Hex(0xFFD45E)); c.Cap(36, 74, 54, 70, 3, Line); c.Cap(74, 70, 92, 74, 3, Line); c.Cap(50, 40, 64, 48, 3, Line); c.Cap(64, 48, 78, 40, 3, Line); c.R(38, 18, 12, 50, 6, Hex(0x79C9F5)); c.R(78, 18, 12, 50, 6, Hex(0x79C9F5));
                break;
            case "latte":
                c.RO(26, 22, 66, 70, 10, Color.white); c.E(59, 88, 30, 9, Hex(0x8A5A34)); c.E(59, 88, 15, 4, Hex(0xF4E4C8)); c.EO(100, 58, 15, 18, Color.white); c.E(100, 58, 7, 10, new Color(0, 0, 0, 0)); c.Cut(93, 48, 14, 20, 7); c.R(16, 12, 86, 6, 3, Hex(0xD6D2C4));
                break;
            case "seatcall":
                c.RO(30, 48, 68, 54, 12, Hex(0x3C78C8)); c.RO(22, 22, 84, 30, 12, Hex(0x4C8ADA)); c.R(30, 10, 10, 14, 2, Line); c.R(88, 10, 10, 14, 2, Line); c.E(64, 76, 6, 16, Color.white); c.E(64, 54, 5, 5, Color.white);
                break;
            case "perfume":
                c.RO(30, 12, 68, 72, 12, new Color(.98f, .72f, .84f)); c.R(38, 18, 13, 60, 6, new Color(1, 1, 1, .6f)); c.R(52, 30, 26, 22, 2, Color.white);
                c.RO(52, 84, 24, 12, 2, Hex(0xD6B25A)); c.RO(46, 96, 36, 20, 4, Hex(0x2A2A33));
                break;
            case "speaker":
                c.RO(12, 28, 104, 72, 22, Hex(0x25262F)); c.E(42, 64, 20, 20, Hex(0x45D1BC)); c.E(42, 64, 11, 11, Hex(0x16161C)); c.E(86, 64, 20, 20, Hex(0x45D1BC)); c.E(86, 64, 11, 11, Hex(0x16161C)); c.R(50, 92, 28, 3, 1, Hex(0x666666));
                break;
            case "coffee":
                c.Poly(Line, 28, 92, 100, 92, 90, 8, 38, 8); c.Poly(Hex(0xFAF7EE), 32, 89, 96, 89, 87, 11, 41, 11); c.Poly(Hex(0x8A5A34), 34, 66, 94, 66, 90, 32, 38, 32); c.E(64, 49, 9, 9, Hex(0xFAF7EE));
                c.RO(24, 92, 80, 14, 5, Hex(0x3F2A1E)); c.Cap(72, 106, 88, 122, 3, Hex(0x2F8F5B));
                break;
            case "cough":
                c.E(40, 60, 26, 26, Line); c.E(84, 64, 30, 30, Line); c.E(62, 86, 26, 26, Line); c.E(40, 60, 23, 23, Hex(0xD9DEE3)); c.E(84, 64, 27, 27, Hex(0xE9ECEF)); c.E(62, 86, 23, 23, Hex(0xF4F6F7));
                break;
            case "phone":
                c.RO(34, 6, 60, 116, 11, Hex(0x1F2028)); c.R(40, 18, 48, 90, 4, Hex(0x35B26B)); c.E(64, 74, 15, 15, Color.white); c.Poly(Hex(0x35B26B), 56, 72, 62, 66, 72, 76, 68, 82); c.R(58, 10, 12, 3, 1.5f, Hex(0x555555));
                break;
            case "badge":
                c.EO(64, 64, 52, 52, Hex(0xEF7CA6)); c.E(64, 64, 39, 39, Color.white); c.E(64, 84, 10, 10, Hex(0xEF7CA6)); c.Cap(64, 72, 64, 44, 8, Hex(0xEF7CA6)); c.E(70, 56, 13, 13, Hex(0xEF7CA6));
                break;
            case "cup":
                c.Poly(Line, 34, 104, 94, 104, 84, 10, 44, 10); c.Poly(Hex(0xEDE1BB), 38, 100, 90, 100, 81, 13, 47, 13); c.Poly(Hex(0xF6D35A), 40, 72, 88, 72, 83, 16, 45, 16);
                c.RO(30, 102, 68, 8, 4, Color.white); c.Cap(68, 110, 80, 126, 3.2f, Hex(0xD9434F));
                break;
            case "food":
                c.Poly(Line, 64, 116, 8, 18, 120, 18); c.Poly(Color.white, 64, 110, 14, 22, 114, 22); c.Poly(Hex(0x222A25), 34, 22, 94, 22, 84, 46, 44, 46); c.Poly(Hex(0xE8524A), 58, 82, 70, 82, 64, 92);
                break;
            default:
                c.EO(64, 64, 46, 46, Hex(0xF2C230)); c.E(64, 64, 33, 33, Hex(0xFFE27A)); c.Poly(Hex(0xC98C14), 64, 88, 72, 70, 90, 70, 76, 58, 82, 40, 64, 50, 46, 40, 52, 58, 38, 70, 56, 70);
                break;
        }
        return c.Spr(.5f, .5f);
    }

    // ---------- 열차 안 ----------

    // 문 하나 구간(가로 4유닛). 안에서 건너편 벽을 바라본 모습: 위부터 천장·손잡이 봉·광고·창문·좌석
    public const int WallH = 474;
    public static Sprite Wall(Color line) { return Get("w" + line, () =>
    {
        var c = new Cv(512, WallH);
        c.RV(0, 0, 512, 384, 0, Hex(0xE9E6DC), Hex(0xC9C7BD));
        // 천장: 위로 갈수록 환하게, 형광등과 환풍구
        c.RV(0, 384, 512, 90, 0, Hex(0xFBFAF4), Hex(0xCFCEC6));
        c.R(0, 384, 512, 3, 0, Hex(0x9C9B94));
        for (int i = 0; i < 2; i++) { float lx = 40 + i * 256; c.R(lx - 6, 432, 188, 28, 6, new Color(1, .98f, .85f, .35f)); c.R(lx, 438, 176, 16, 4, Hex(0xFFFEF0)); }
        for (int i = 0; i < 5; i++) c.R(214 + i * 18, 402, 12, 20, 2, Hex(0xA9A8A2));
        // 양쪽 창문과 좌석
        foreach (float x0 in new float[] { 12, 346 })
        {
            float ww = 154, wy = 168, wh = 150;
            c.R(x0 - 5, wy - 5, ww + 10, wh + 10, 20, Hex(0x8D8E90)); c.R(x0 - 2, wy - 2, ww + 4, wh + 4, 18, Hex(0x3A3B40));
            c.Cut(x0, wy, ww, wh, 16);
            c.R(x0 + ww / 2 - 3, wy, 6, wh, 0, Hex(0x8D8E90));
            // 광고 자리
            c.R(x0 - 2, 330, ww + 4, 48, 3, Hex(0xB9B8B0));
            // 좌석: 등받이와 방석, 끝자리는 분홍 배려석
            bool leftSide = x0 < 100;
            c.RV(x0 - 6, 66, ww + 12, 88, 12, Hex(0x3C78C8), Hex(0x28579E));
            for (int i = 1; i < 3; i++) c.R(x0 - 6 + i * (ww + 12) / 3 - 1.5f, 68, 3, 84, 1, Hex(0x1F4580));
            float px = leftSide ? x0 - 6 + (ww + 12) * 2 / 3 : x0 - 6;
            c.RV(px, 66, (ww + 12) / 3, 88, 12, Hex(0xF290B4), Hex(0xCE5F8C));
            c.RV(x0 - 10, 40, ww + 20, 32, 12, Hex(0x4C8ADA), Hex(0x2C5EA8));
            c.RV(px - 2, 40, (ww + 12) / 3 + 4, 32, 12, Hex(0xF7A5C4), Hex(0xCE5F8C));
            c.RV(x0 - 10, 0, ww + 20, 40, 2, Hex(0x45454C), Hex(0x303036));
            // 옆 칸막이
            c.R(leftSide ? x0 + ww + 6 : x0 - 16, 0, 10, 158, 5, Hex(0xC6C8CC));
        }
        // 노선색 띠
        c.R(0, 322, 170, 6, 0, line); c.R(342, 322, 170, 6, 0, line);
        c.R(0, 160, 170, 4, 0, new Color(line.r, line.g, line.b, .7f)); c.R(342, 160, 170, 4, 0, new Color(line.r, line.g, line.b, .7f));
        // 문: 금속 틀, 구멍, 위쪽 전광판
        c.R(176, 0, 160, 348, 6, Hex(0x96979B));
        c.Cut(184, 0, 144, 340, 4);
        c.RV(176, 348, 160, 36, 3, Hex(0xB7B7B1), Hex(0xA3A39D));
        c.R(198, 354, 116, 25, 4, Hex(0x141417));
        return c.Spr(.5f, 0);
    }); }

    public static Sprite DoorLeaf() { return Get("door", () =>
    {
        var c = new Cv(72, 340);
        c.RV(0, 0, 72, 340, 0, Hex(0xD6D8DB), Hex(0xB3B6BA));
        c.R(10, 136, 52, 176, 8, Hex(0x45474C));
        c.Cut(13, 139, 46, 170, 6);
        c.R(0, 0, 72, 10, 0, Hex(0x56575B));
        c.E(36, 108, 12, 12, Hex(0xF0C44B)); c.E(36, 108, 8, 8, Color.white);
        c.Cap(30, 106, 42, 110, 2.2f, Hex(0xD2303F));
        return c.Spr(.5f, 0);
    }); }

    public const int FloorH = 294;
    public static Sprite Floor() { return Get("floor", () =>
    {
        var c = new Cv(512, FloorH);
        c.RV(0, 0, 512, FloorH, 0, Hex(0x8B8A85), Hex(0xB9B7AF));
        var rnd = new System.Random(3);
        for (int i = 0; i < 700; i++) { float x = rnd.Next(512), y = rnd.Next(FloorH); c.R(x, y, 2, 2, 1, new Color(0, 0, 0, .05f)); }
        // 천장 형광등이 바닥에 비친 띠
        c.R(0, 136, 512, 52, 0, new Color(1, 1, 1, .13f)); c.R(0, 148, 512, 26, 0, new Color(1, 1, 1, .1f));
        // 벽 아래 걸레받이, 좌석 밑 그림자
        c.R(0, FloorH - 22, 512, 22, 0, new Color(0, 0, 0, .28f)); c.R(0, FloorH - 44, 512, 22, 0, new Color(0, 0, 0, .1f));
        // 문 앞 금속 발판
        c.Poly(Hex(0x77787C), 182, FloorH, 330, FloorH, 346, FloorH - 44, 166, FloorH - 44);
        for (int i = 0; i < 4; i++) c.R(172 + i * 2, FloorH - 10 - i * 10, 168 - i * 4, 2, 0, new Color(0, 0, 0, .3f));
        c.Poly(Hex(0xE9BE3C), 166, FloorH - 44, 346, FloorH - 44, 348, FloorH - 52, 164, FloorH - 52);
        return c.Spr(.5f, 1);
    }); }

    public static Sprite Strap() { return Get("strap", () =>
    {
        var c = new Cv(44, 110);
        c.R(18, 44, 8, 66, 3, Hex(0x5B5B62));
        c.Poly(Line, 22, 52, 2, 6, 42, 6); c.Poly(Hex(0xF4F4F0), 22, 45, 8, 10, 36, 10); c.Poly(new Color(0, 0, 0, 0), 22, 30, 15, 13, 29, 13);
        c.Cut(16, 12, 12, 12, 5);
        return c.Spr(.5f, 1);
    }); }

    public static Sprite Pole() { return Get("pole", () =>
    {
        var c = new Cv(24, 256);
        for (int j = 0; j < 256; j++) for (int i = 4; i < 20; i++)
            { float u = (i - 4) / 15f; c.p[j * 24 + i] = Color.Lerp(Hex(0x8F9196), Color.white, Mathf.Sin(u * Mathf.PI) * .85f - .05f); c.p[j * 24 + i].a = 1; }
        return c.Spr(.5f, 0);
    }); }

    public static Sprite Ad(int seed) { return Get("ad" + seed, () =>
    {
        int[] t = { 0xFFF3B8, 0xC8E6FA, 0xFAD2DA, 0xCFF3CF, 0xE3D4FA };
        var c = new Cv(150, 44);
        c.RV(0, 0, 150, 44, 2, Hex(t[seed % 5]), Color.Lerp(Hex(t[seed % 5]), Color.black, .1f));
        c.R(0, 0, 150, 3, 0, new Color(0, 0, 0, .2f));
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite ConnectDoor(Color line) { return Get("conn" + line, () =>
    {
        var c = new Cv(150, 474);
        c.R(0, 0, 150, 474, 0, Hex(0x5A5B60)); c.RV(10, 0, 130, 400, 5, Hex(0xC2C3C6), Hex(0xA3A4A8));
        c.R(32, 186, 86, 150, 8, Hex(0x34353A));
        c.RV(36, 190, 78, 142, 6, Hex(0xE8E6D8), Hex(0xB8B5A8));
        c.R(36, 300, 78, 8, 0, line); c.R(42, 210, 30, 66, 4, Hex(0x3C3D44)); c.R(80, 210, 26, 66, 4, Hex(0x3C3D44));
        c.Cap(120, 140, 120, 170, 5, Hex(0xE8E8E8));
        return c.Spr(.5f, 0);
    }); }

    public static Sprite Tunnel() { return Get("tunnel", () =>
    {
        var c = new Cv(512, 240);
        c.RV(0, 0, 512, 240, 0, Hex(0x20222B), Hex(0x0B0C10));
        for (int i = 0; i < 4; i++) { c.R(i * 128 + 20, 0, 18, 240, 0, new Color(1, 1, 1, .035f)); c.R(i * 128 + 38, 0, 3, 240, 0, new Color(0, 0, 0, .4f)); }
        foreach (int y in new[] { 150, 164, 176 }) c.R(0, y, 512, 3, 1, Hex(0x34363F));
        c.R(0, 60, 512, 5, 2, Hex(0x2C2E37));
        return c.Spr(0, .5f);
    }); }

    public static Sprite Sky() { return Get("sky", () =>
    {
        var c = new Cv(16, 64);
        c.RV(-2, -2, 20, 68, 0, Hex(0xA5D3F2), Hex(0xFBE5C8));
        return Sprite.Create(c.Tex(), new Rect(0, 0, 16, 64), new Vector2(.5f, .5f), 16);
    }); }

    public static Sprite Skyline(int layer) { return Get("sky" + layer, () =>
    {
        var c = new Cv(1024, 240); var rnd = new System.Random(layer * 31 + 5);
        Color b = layer == 0 ? Hex(0xA9BDCF) : Hex(0x6F8499);
        float x = 0;
        while (x < 1024)
        {
            float w = rnd.Next(40, 110), h = rnd.Next(layer == 0 ? 90 : 50, layer == 0 ? 190 : 140);
            c.R(x, 50, w, h, 2, b);
            if (layer == 1)
                for (float wy = 60; wy < 50 + h - 10; wy += 14) for (float wx = x + 6; wx < x + w - 8; wx += 12)
                    if (rnd.NextDouble() < .55) c.R(wx, wy, 6, 8, 1, new Color(1, .95f, .75f, .55f));
            x += w + rnd.Next(0, 10);
        }
        // 한강
        c.RV(0, 0, 1024, 56, 0, Hex(0x7FB2CF), Hex(0x4C86A8));
        for (int i = 0; i < 40; i++) c.R(rnd.Next(1000), rnd.Next(4, 50), rnd.Next(20, 60), 2, 1, new Color(1, 1, 1, .22f));
        return c.Spr(0, .5f);
    }); }

    public static Sprite Truss() { return Get("truss", () =>
    {
        var c = new Cv(256, 240); Color s = Hex(0x3F4C5A);
        c.R(0, 0, 256, 12, 0, s); c.R(0, 220, 256, 12, 0, s); c.R(0, 0, 12, 232, 0, s);
        c.Cap(6, 6, 250, 226, 4, s); c.Cap(250, 6, 6, 226, 4, s);
        return c.Spr(0, .5f);
    }); }

    public static Sprite TunnelLight() { return Get("tl", () =>
    {
        var c = new Cv(180, 40);
        for (int j = 0; j < 40; j++) for (int i = 0; i < 180; i++) { float a = (i - 89.5f) / 90f, b = (j - 19.5f) / 20f; float d = Mathf.Clamp01(1 - (a * a + b * b)); c.p[j * 180 + i] = new Color(1, .86f, .55f, d * d); }
        c.R(40, 16, 100, 8, 4, new Color(1, .97f, .85f));
        return c.Spr(.5f, .5f);
    }); }

    // 역: 스크린도어와 벽. 열차가 설 때 창밖으로 밀려 들어온다
    public static Sprite Station(Color line) { return Get("st" + line, () =>
    {
        var c = new Cv(768, 480);
        c.RV(0, 0, 768, 480, 0, Hex(0xF2F0E8), Hex(0xCFCCC2));
        for (int x = 0; x < 768; x += 64) c.R(x, 0, 2, 480, 0, new Color(0, 0, 0, .05f));
        for (int y = 0; y < 480; y += 48) c.R(0, y, 768, 2, 0, new Color(0, 0, 0, .05f));
        c.R(0, 360, 768, 16, 0, line); c.R(0, 0, 768, 60, 0, Hex(0x77787C));
        c.R(120, 240, 280, 100, 8, Hex(0x13294F)); c.R(120, 240, 280, 8, 0, line);
        foreach (int x in new[] { 40, 460, 620 }) { c.R(x, 60, 8, 170, 0, Hex(0x55565A)); c.R(x + 60, 60, 8, 170, 0, Hex(0x55565A)); }
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite Vignette() { return Get("vig", () =>
    {
        var c = new Cv(128, 72);
        for (int j = 0; j < 72; j++) for (int i = 0; i < 128; i++)
            { float a = (i - 63.5f) / 64f, b = (j - 35.5f) / 36f; float d = Mathf.Clamp01((Mathf.Sqrt(a * a + b * b * 1.1f) - .72f) * 1.1f); c.p[j * 128 + i] = new Color(.02f, .02f, .07f, d * d * .9f); }
        return Sprite.Create(c.Tex(), new Rect(0, 0, 128, 72), new Vector2(.5f, .5f), 12);
    }); }

    public static Sprite Bead(int i) { return Get("bead" + (i % 4), () =>
    {
        Color[] cs = { Hex(0xFFF27A), Hex(0x7DE3F4), Hex(0xF39BBF), Hex(0xC59CFF) };
        var c = new Cv(24, 24);
        c.E(12, 12, 10, 10, Line); c.E(12, 12, 8, 8, cs[i % 4]); c.E(9, 15, 2.6f, 2.6f, Color.white);
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite Zone() { return Get("zone", () =>
    {
        var c = new Cv(384, 128);
        for (int j = 0; j < 128; j++) for (int i = 0; i < 384; i++)
            { float a = (i - 191.5f) / 192f, b = (j - 63.5f) / 64f, d = a * a + b * b; c.p[j * 384 + i] = new Color(1, 1, 1, d < 1 ? Mathf.Clamp01((1 - d) * 3) * (.45f + d * .55f) : 0); }
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite Ring() { return Get("ring", () =>
    {
        var c = new Cv(256, 256);
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            { float d = Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f)) / 127f; float a = d < 1 ? Mathf.Clamp01((1 - d) * 40) * (Mathf.Pow(d, 6) * .9f + .08f) : 0; c.p[y * 256 + x] = new Color(1, 1, 1, a); }
        return Sprite.Create(c.Tex(), new Rect(0, 0, 256, 256), new Vector2(.5f, .5f), 128);
    }); }

    public static Sprite Spark() { return Get("spark", () =>
    {
        var c = new Cv(48, 48);
        c.Poly(Color.white, 24, 46, 29, 29, 46, 24, 29, 19, 24, 2, 19, 19, 2, 24, 19, 29);
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite Puddle() { return Get("puddle", () =>
    {
        var c = new Cv(420, 110);
        c.E(210, 55, 200, 45, Hex(0x5E3B20, .92f)); c.E(90, 45, 70, 34, Hex(0x5E3B20, .92f)); c.E(320, 66, 80, 30, Hex(0x5E3B20, .92f));
        c.E(180, 66, 100, 14, new Color(1, 1, 1, .16f));
        return c.Spr(.5f, .5f);
    }); }

    public static Sprite LobbyWall() { return Get("lobbyw", () =>
    {
        var c = new Cv(512, WallH);
        c.RV(0, 0, 512, WallH, 0, Hex(0xF3F2EE), Hex(0xCFCDC6));
        c.R(0, 150, 512, 200, 0, Hex(0x83AFC9)); c.RV(0, 150, 512, 200, 0, Hex(0xA9D2E8), Hex(0x6E9BB8));
        for (int x = 0; x < 512; x += 128) c.R(x, 150, 5, 200, 0, Hex(0x5C6A74));
        c.R(0, 146, 512, 5, 0, Hex(0x5C6A74)); c.R(0, 350, 512, 5, 0, Hex(0x5C6A74));
        c.R(40, 432, 180, 16, 6, Hex(0xFFFEF0)); c.R(296, 432, 180, 16, 6, Hex(0xFFFEF0));
        return c.Spr(.5f, 0);
    }); }

    public static Sprite LobbyFloor() { return Get("lobbyf", () =>
    {
        var c = new Cv(512, FloorH);
        c.RV(0, 0, 512, FloorH, 0, Hex(0xA8A49A), Hex(0xD9D5CA));
        for (int x = 0; x < 512; x += 128) c.R(x, 0, 2, FloorH, 0, new Color(0, 0, 0, .1f));
        for (int y = 0; y < FloorH; y += 72) c.R(0, y, 512, 2, 0, new Color(0, 0, 0, .1f));
        c.R(0, 90, 512, 80, 0, new Color(1, 1, 1, .14f)); c.R(0, FloorH - 16, 512, 16, 0, new Color(0, 0, 0, .25f));
        return c.Spr(.5f, 1);
    }); }

    public static Sprite Gate() { return Get("gate", () =>
    {
        var c = new Cv(420, 210);
        foreach (int x in new[] { 8, 170, 332 })
        {
            c.RO(x, 6, 76, 170, 14, Hex(0x565963)); c.R(x + 8, 150, 60, 14, 5, Hex(0x4BE37A)); c.R(x + 18, 110, 40, 26, 4, Hex(0x1C1D24));
        }
        foreach (int x in new[] { 84, 246 }) { c.R(x, 90, 86, 26, 6, new Color(.75f, .9f, 1f, .55f)); c.R(x, 110, 86, 5, 2, Hex(0xDD4A4A)); }
        return c.Spr(.5f, 0);
    }); }
}
