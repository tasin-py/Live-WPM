using System;
using System.Collections.Generic;
using System.Drawing;

namespace LiveWpm
{
    // The theme list and the colour maths shared by the Windows app and the Linux edition.
    // Kept free of any drawing code so both can compile it unchanged (C# 5).
    static partial class Theme
    {
        public const string DefaultTheme = "tasin", GlassTheme = "glass";
        public const string AppName = "Live WPM";

        // In the order the theme picker shows them.
        public static readonly Palette[] Palettes = {
            // The original look: flat 2D on #323437, no gradients, glows or shadows.
            new Palette {
                Id = "tasin", Name = "Average Tasin",
                Bg = C(50, 52, 55), Card = C(61, 64, 68), CardHover = C(72, 75, 80), Border = C(78, 81, 87), Track = C(78, 81, 87),
                Text = C(240, 241, 243), Muted = C(165, 169, 177), Accent = C(56, 214, 255), OnAccent = C(8, 18, 26) },

            // Soft blush, white cards, deep plum text, extra-round corners and little hearts. Kept minimal.
            new Palette {
                Id = "tashhid", Name = "Beverage Tashhid", Light = true, Cute = true, Radius = 1.4f,
                Bg = C(255, 243, 247), Card = C(255, 255, 255), CardHover = C(255, 230, 240), Border = C(246, 205, 222), Track = C(250, 216, 230),
                Text = C(58, 18, 40), Muted = C(122, 66, 96), Accent = C(232, 82, 146), OnAccent = C(255, 255, 255),
                Hot = C(200, 30, 110), Orange = C(240, 110, 70), DangerText = C(196, 30, 84), Outline = A(90, 240, 150, 188) },

            // Premium black and gold: near-black surfaces, a metallic #D5A149 accent and a gold-lit edge on every card.
            // The speedometer keeps its own sky-blue dial.
            new Palette {
                Id = "ahnaf", Name = "Ahnaf Dark",
                Bg = C(11, 11, 12), Card = C(21, 21, 23), CardHover = C(31, 30, 32), Border = C(40, 38, 40), Track = C(44, 42, 44),
                Text = C(240, 238, 235), Muted = C(150, 145, 138), Accent = C(213, 161, 73), OnAccent = C(26, 17, 4),
                Hot = C(240, 203, 128), Orange = C(240, 203, 128), Dial = C(77, 196, 255), Shiny = true },

            // Midnight blue with a soft periwinkle accent and warm gold for records.
            new Palette {
                Id = "makam", Name = "Makam Blue",
                Bg = C(12, 19, 38), Card = C(20, 31, 58), CardHover = C(28, 42, 75), Border = C(37, 52, 88), Track = C(38, 54, 92),
                Text = C(232, 238, 252), Muted = C(139, 156, 196), Accent = C(116, 166, 255), OnAccent = C(8, 14, 32),
                Orange = C(255, 196, 87), Outline = A(18, 150, 180, 255) },

            // Minimal and dark: black surfaces, and everything on them white or a grey between. No other colour.
            new Palette {
                Id = "ziraf", Name = "Ziraf Minimal",
                Bg = C(8, 8, 8), Card = C(20, 20, 20), CardHover = C(36, 36, 36), Border = C(52, 52, 52), Track = C(58, 58, 58),
                Text = C(255, 255, 255), Muted = C(176, 176, 176), Accent = C(255, 255, 255), OnAccent = C(0, 0, 0),
                Hot = C(255, 255, 255), Orange = C(255, 255, 255), Danger = C(92, 92, 92), DangerText = C(255, 255, 255),
                Outline = A(26, 255, 255, 255), LogoAccent = C(255, 255, 255) },

            // Deep wine-black with a crimson accent; near the top of the dial the arc turns gold.
            new Palette {
                Id = "adib", Name = "Adib Red",
                Bg = C(23, 15, 17), Card = C(36, 23, 27), CardHover = C(48, 31, 36), Border = C(62, 40, 46), Track = C(64, 42, 48),
                Text = C(248, 238, 239), Muted = C(181, 151, 157), Accent = C(255, 76, 92), OnAccent = C(255, 255, 255),
                Hot = C(255, 196, 77), Orange = C(255, 160, 60), DangerText = C(255, 140, 150), Outline = A(16, 255, 120, 130) },

            // Deep sky-blue glass: raised see-through panes with a lit edge and a soft shadow,
            // no colour washes. The floating window is mostly the blurred desktop with a light sky tint.
            new Palette {
                Id = "glass", Name = "Safir Glass", Glass = true,
                Bg = C(0, 72, 112), Card = A(8, 255, 255, 255), CardHover = A(24, 255, 255, 255), Border = A(60, 255, 255, 255), Track = A(64, 255, 255, 255),
                Text = C(255, 255, 255), Muted = C(226, 241, 250), Accent = C(170, 230, 255), OnAccent = C(0, 50, 80),
                Dial = C(255, 255, 255), FloatTint = A(110, 0, 92, 146) },
        };

        // The Typer's own themes, loaded from files by ThemeFiles. The picker shows them after the built-in ones.
        public static readonly List<Palette> Custom = new List<Palette>();
        public static int ThemeCount { get { return Palettes.Length + Custom.Count; } }
        public static Palette ThemeAt(int i) { return i < Palettes.Length ? Palettes[i] : Custom[i - Palettes.Length]; }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        static Color A(int a, int r, int g, int b) { return Color.FromArgb(a, r, g, b); }

        public static Palette Find(string id)
        {
            if (id == "flat") id = DefaultTheme; // name used by 1.1.0
            foreach (Palette p in Palettes) if (p.Id == id) return p;
            foreach (Palette p in Custom) if (p.Id == id) return p;
            return null;
        }

        public static Color Lerp(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    // One colour theme. Empty optional colours fall back to the defaults in Theme.Apply.
    sealed class Palette
    {
        public string Id, Name;
        public string SourceFile;  // where one of the Typer's own themes is saved; null for a built-in theme
        public Color Bg, Card, CardHover, Border, Track, Text, Muted, Accent, OnAccent;
        public Color Dial, Hot, Orange, Danger, DangerText, Outline, FloatTint, LogoAccent;

        public Color DialColor { get { return Dial.IsEmpty ? Accent : Dial; } }   // the speedometer's arc
        public Color[] Blobs;      // soft colour behind see-through surfaces (glass)
        public bool Light, Glass, Cute, Shiny;
        public float Radius = 1;   // corner roundness
    }
}
