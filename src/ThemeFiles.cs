using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace LiveWpm
{
    // The Typer's own themes: small text files of colours, kept in the app's data folder.
    // A file can only ever set a name and colours, and nothing in it is run or sent anywhere.
    static class ThemeFiles
    {
        public const string Extension = ".lwtheme";
        public const int MaxThemes = 9;
        const int MaxNameLength = 20, MaxFileBytes = 16 * 1024;
        const double MinTextContrast = 3.0, MinAccentContrast = 1.5;

        // Set by test tools so they can never touch the Typer's real themes.
        public static string FolderOverride;

        public const string Starter =
            "# Live WPM theme. Change the colours, save, then choose Upload theme in the theme picker.\r\n" +
            "# Colours look like #RRGGBB. A colour picker online or in Paint can give you these codes.\r\n" +
            "\r\n" +
            "name = My Theme\r\n" +
            "background = #14202B\r\n" +
            "card = #1E2F3D\r\n" +
            "text = #F2F6F8\r\n" +
            "accent = #4FD1B5\r\n" +
            "\r\n" +
            "# Optional. Delete a line and Live WPM works out a colour that fits.\r\n" +
            "muted = #A3B4BF\r\n" +
            "border = #33495A\r\n" +
            "dial = #4FD1B5\r\n";

        static string Folder
        {
            get { return FolderOverride ?? Path.Combine(DataStore.Folder, "themes"); }
        }

        // Reads every saved theme. A file that no longer passes the checks is skipped.
        public static void LoadAll()
        {
            Theme.Custom.Clear();
            string[] files;
            try
            {
                if (!Directory.Exists(Folder)) return;
                files = Directory.GetFiles(Folder, "*" + Extension);
            }
            catch { return; }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                if (Theme.Custom.Count >= MaxThemes) break;
                try
                {
                    if (new FileInfo(file).Length > MaxFileBytes) continue;
                    string error;
                    Palette p = Parse(File.ReadAllText(file), out error);
                    if (p == null || FindByName(p.Name) != null) continue;
                    p.SourceFile = file;
                    Theme.Custom.Add(p);
                }
                catch { }
            }
        }

        // Checks a chosen file and adds it. Returns the theme, or null with the reason in error.
        // Null with no error means the Typer chose not to replace a theme of the same name.
        public static Palette Import(string path, Func<string, bool> confirmReplace, out string error)
        {
            try
            {
                if (new FileInfo(path).Length > MaxFileBytes) { error = "That file is too big to be a theme."; return null; }
                Palette p = Parse(File.ReadAllText(path), out error);
                if (p == null) return null;

                Palette same = FindByName(p.Name);
                if (same != null && same.SourceFile == null)
                {
                    error = "\"" + same.Name + "\" is a built-in theme. Give your theme a different name.";
                    return null;
                }
                if (same == null && Theme.Custom.Count >= MaxThemes)
                {
                    error = "You can keep up to " + MaxThemes + " of your own themes. Remove one to add another.";
                    return null;
                }
                if (same != null && !confirmReplace(same.Name)) return null;

                Directory.CreateDirectory(Folder);
                p.SourceFile = same != null ? same.SourceFile : Path.Combine(Folder, p.Id + Extension);
                File.WriteAllText(p.SourceFile, Serialize(p), new UTF8Encoding(false));
                if (same != null) Theme.Custom[Theme.Custom.IndexOf(same)] = p;
                else Theme.Custom.Add(p);
                return p;
            }
            catch (Exception ex)
            {
                error = "That file couldn't be added:\n" + ex.Message;
                return null;
            }
        }

        public static void Remove(Palette p)
        {
            File.Delete(p.SourceFile);
            Theme.Custom.Remove(p);
        }

        static Palette FindByName(string name)
        {
            foreach (Palette p in Theme.Palettes) if (SameName(p.Name, name)) return p;
            foreach (Palette p in Theme.Custom) if (SameName(p.Name, name)) return p;
            return null;
        }

        static bool SameName(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        // Text to a theme, or null with a reason the Typer can act on.
        public static Palette Parse(string text, out string error)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (line.Length == 0 || line[0] == '#' || eq < 1) continue;
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
            }

            string name;
            values.TryGetValue("name", out name);
            name = CleanName(name);
            if (name.Length == 0) { error = "This doesn't look like a theme file: it needs a line like\nname = My Theme"; return null; }
            if (name.Length > MaxNameLength) { error = "The theme's name is too long. Use up to " + MaxNameLength + " characters."; return null; }

            Color bg, card, text2, accent, muted, border, dial;
            if (!Required(values, "background", out bg, out error) || !Required(values, "card", out card, out error) ||
                !Required(values, "text", out text2, out error) || !Required(values, "accent", out accent, out error) ||
                !Optional(values, "muted", out muted, out error) || !Optional(values, "border", out border, out error) ||
                !Optional(values, "dial", out dial, out error))
                return null;

            if (Contrast(text2, bg) < MinTextContrast) { error = "The text would be hard to read on the background. Make \"text\" and \"background\" more different."; return null; }
            if (Contrast(text2, card) < MinTextContrast) { error = "The text would be hard to read on the cards. Make \"text\" and \"card\" more different."; return null; }
            if (Contrast(accent, card) < MinAccentContrast) { error = "The accent wouldn't show up on the cards. Make \"accent\" and \"card\" more different."; return null; }

            if (border.IsEmpty) border = Theme.Lerp(card, text2, 0.16);
            if (muted.IsEmpty) muted = Theme.Lerp(text2, card, 0.35);
            error = null;
            bool light = Luminance(bg) > 0.4;
            return new Palette
            {
                Id = "custom-" + StableId(name), Name = name, Light = light,
                Bg = bg, Card = card, CardHover = Theme.Lerp(card, text2, 0.10), Border = border, Track = border,
                Text = text2, Muted = muted, Accent = accent, Dial = dial,
                DangerText = light ? Color.FromArgb(196, 30, 60) : Color.Empty,  // the stock pale red is too faint on a light theme
                OnAccent = Contrast(Color.White, accent) >= Contrast(Color.Black, accent) ? Color.White : Color.Black
            };
        }

        static string Serialize(Palette p)
        {
            StringBuilder sb = new StringBuilder("# Live WPM theme\r\n");
            sb.Append("name = ").Append(p.Name).Append("\r\n");
            Line(sb, "background", p.Bg); Line(sb, "card", p.Card); Line(sb, "text", p.Text); Line(sb, "accent", p.Accent);
            Line(sb, "muted", p.Muted); Line(sb, "border", p.Border);
            if (!p.Dial.IsEmpty) Line(sb, "dial", p.Dial);
            return sb.ToString();
        }

        static void Line(StringBuilder sb, string key, Color c)
        {
            sb.Append(key).Append(" = #").Append(c.R.ToString("X2")).Append(c.G.ToString("X2")).Append(c.B.ToString("X2")).Append("\r\n");
        }

        static string CleanName(string name)
        {
            if (name == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in name) if (!char.IsControl(c)) sb.Append(c);
            return sb.ToString().Trim();
        }

        static bool Required(Dictionary<string, string> values, string key, out Color c, out string error)
        {
            if (!values.ContainsKey(key))
            {
                c = Color.Empty;
                error = "The theme is missing a \"" + key + "\" colour. Add a line like\n" + key + " = #1E2F3D";
                return false;
            }
            return Optional(values, key, out c, out error);
        }

        static bool Optional(Dictionary<string, string> values, string key, out Color c, out string error)
        {
            c = Color.Empty;
            error = null;
            string v;
            if (!values.TryGetValue(key, out v)) return true;
            if (TryColor(v, out c)) return true;
            error = "\"" + v + "\" isn't a colour for \"" + key + "\". Colours look like #1E2F3D.";
            return false;
        }

        static bool TryColor(string v, out Color c)
        {
            c = Color.Empty;
            if (v.StartsWith("#")) v = v.Substring(1);
            int rgb;
            if (v.Length != 6 || !int.TryParse(v, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb)) return false;
            c = Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            return true;
        }

        // The same name gives the same id on every run, so a saved choice keeps pointing at its theme.
        static string StableId(string name)
        {
            uint h = 2166136261;
            foreach (char ch in name.ToLowerInvariant()) { h ^= ch; h *= 16777619; }
            return h.ToString("x8");
        }

        // WCAG contrast: 1 for identical colours, 21 for black on white.
        static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        static double Luminance(Color c)
        {
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        static double Linear(int v)
        {
            double s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
    }
}
