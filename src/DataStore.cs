using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LiveWpm
{
    // One calendar day of typing. Only numbers are kept - never any text.
    sealed class DayStat
    {
        public DateTime Date;
        public double PeakWpm;
        public long Words;
        public long Chars;        // characters of "counted" sessions, for the average
        public double Seconds;    // duration of "counted" sessions, for the average
        public long Backspaces;
        public readonly List<SpeedPoint> Points = new List<SpeedPoint>(); // one per run, for the day's graph

        public double AvgWpm
        {
            get { return Seconds > 0 ? Chars / 5.0 / (Seconds / 60.0) : 0; }
        }
    }

    // One typing run on the day's graph: when it started and how fast it was. Numbers only.
    sealed class SpeedPoint
    {
        public int Second;      // seconds after midnight when the run started
        public float Wpm, Peak;
        public int Words;
    }

    // What one finished run added to the stats, and what the records were before it,
    // so "Remove last session" can take it back out exactly. Numbers only.
    sealed class SessionUndo
    {
        public DateTime Started;
        public long Words, Chars, Backspaces;
        public double Seconds;
        public bool Counted, PointAdded;
        public double PrevPeakWpm, PrevPeakAcc, PrevDayPeak;
        public long PrevPeakWords;
        public DateTime PrevPeakTime;
    }

    // Everything the app remembers, stored as a tiny key=value file in %APPDATA%\Live WPM.
    sealed class DataStore
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public string TyperName = "";

        public bool Tracking = true;
        public bool Floating = true;
        public bool OnlyWhenTyping = true;
        public bool Pinned = true;
        public bool StartWithWindows = true;
        public string LastUpdateCheck = "";  // yyyy-MM-dd of the last successful update check
        public int FloatX = int.MinValue, FloatY = int.MinValue;
        public string ThemeName = LiveWpm.Theme.DefaultTheme;
        public int FloatBgOpacity = 100;     // floating window background, 0-100 %; text and dial stay solid

        public double PeakWpm, PeakAcc;
        public long PeakWords;
        public DateTime PeakTime;


        public long TotalWords;
        public long AvgChars, AvgBackspaces;
        public double AvgSeconds;

        public readonly SortedDictionary<DateTime, DayStat> Days = new SortedDictionary<DateTime, DayStat>();

        const int MaxPoints = 600; // runs kept per day for the graph (keeps the file small)
        const int MaxUndo = 50;    // how many recent runs can be removed one by one

        public readonly List<SessionUndo> Undo = new List<SessionUndo>(); // newest last

        // Set by test and preview tools so their sample data can never overwrite the Typer's real stats.
        public static bool NoSave;

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Theme.AppName); }
        }

        static string FilePath
        {
            get { return Path.Combine(Folder, "stats.dat"); }
        }

        public double AvgWpm
        {
            get { return AvgSeconds > 0 ? AvgChars / 5.0 / (AvgSeconds / 60.0) : 0; }
        }

        public double AvgAccuracy
        {
            get { return AvgChars > 0 ? Math.Max(0, (AvgChars - AvgBackspaces) * 100.0 / AvgChars) : 0; }
        }

        public DayStat Day(DateTime date)
        {
            DayStat d;
            if (!Days.TryGetValue(date.Date, out d))
            {
                d = new DayStat();
                d.Date = date.Date;
                Days[d.Date] = d;
            }
            return d;
        }

        // Folds a finished session into the totals. Returns true when it set a new peak.
        public bool AddSession(TypingSession s)
        {
            DayStat day = Day(s.Started);
            SessionUndo u = new SessionUndo {
                Started = s.Started, Words = s.Words, Counted = s.Counts,
                PrevPeakWpm = PeakWpm, PrevPeakAcc = PeakAcc, PrevPeakWords = PeakWords, PrevPeakTime = PeakTime,
                PrevDayPeak = day.PeakWpm };
            Undo.Add(u);
            if (Undo.Count > MaxUndo) Undo.RemoveAt(0);
            TotalWords += s.Words;
            day.Words += s.Words;
            bool newPeak = false;

            if (s.Counts)
            {
                double wpm = s.PeakWpm; // the top live speed - the same "PEAK" the speedometer shows
                AvgChars += s.Chars;
                AvgBackspaces += s.Backspaces;
                AvgSeconds += s.Seconds;
                day.Chars += s.Chars;
                day.Backspaces += s.Backspaces;
                day.Seconds += s.Seconds;
                u.Chars = s.Chars; u.Backspaces = s.Backspaces; u.Seconds = s.Seconds;
                if (wpm > day.PeakWpm) day.PeakWpm = wpm;
                u.PointAdded = day.Points.Count < MaxPoints;
                if (u.PointAdded)
                    day.Points.Add(new SpeedPoint {
                        Second = (int)s.Started.TimeOfDay.TotalSeconds,
                        Wpm = (float)s.AvgWpm, Peak = (float)wpm, Words = s.Words });

                if (wpm > PeakWpm)
                {
                    newPeak = PeakWpm > 0;
                    PeakWpm = wpm; PeakAcc = s.Accuracy; PeakWords = s.Words; PeakTime = s.Ended;
                }
            }
            return newPeak;
        }

        public SessionUndo LastSession { get { return Undo.Count > 0 ? Undo[Undo.Count - 1] : null; } }

        // Takes the most recent run back out of every total. If it set a record, the record
        // it beat comes back - for the day and for all time.
        public bool RemoveLastSession()
        {
            SessionUndo u = LastSession;
            if (u == null) return false;
            Undo.RemoveAt(Undo.Count - 1);
            DayStat day;
            if (Days.TryGetValue(u.Started.Date, out day))
            {
                day.Words = Math.Max(0, day.Words - u.Words);
                if (u.Counted)
                {
                    day.Chars = Math.Max(0, day.Chars - u.Chars);
                    day.Backspaces = Math.Max(0, day.Backspaces - u.Backspaces);
                    day.Seconds = Math.Max(0, day.Seconds - u.Seconds);
                    day.PeakWpm = u.PrevDayPeak;
                    if (u.PointAdded && day.Points.Count > 0) day.Points.RemoveAt(day.Points.Count - 1);
                }
                if (day.Words == 0 && day.Chars == 0 && day.Points.Count == 0) Days.Remove(day.Date);
            }
            TotalWords = Math.Max(0, TotalWords - u.Words);
            if (u.Counted)
            {
                AvgChars = Math.Max(0, AvgChars - u.Chars);
                AvgBackspaces = Math.Max(0, AvgBackspaces - u.Backspaces);
                AvgSeconds = Math.Max(0, AvgSeconds - u.Seconds);
            }
            PeakWpm = u.PrevPeakWpm; PeakAcc = u.PrevPeakAcc; PeakWords = u.PrevPeakWords; PeakTime = u.PrevPeakTime;
            return true;
        }

        // After a day is deleted its runs can no longer be taken out one by one.
        public void ForgetDay(DateTime date)
        {
            Undo.RemoveAll(delegate(SessionUndo u) { return u.Started.Date == date.Date; });
        }

        public void ResetPeak()
        {
            Undo.Clear(); // older runs would otherwise bring back records from before the reset
            PeakWpm = 0; PeakAcc = 0; PeakWords = 0; PeakTime = DateTime.MinValue;
        }

        public void ResetWords()
        {
            Undo.Clear();
            TotalWords = 0;
        }

        public void ResetAll()
        {
            ResetPeak();
            TotalWords = 0; AvgChars = 0; AvgBackspaces = 0; AvgSeconds = 0;
            Days.Clear();
            Undo.Clear();
        }

        // ---- persistence ----------------------------------------------------

        public static DataStore Load()
        {
            DataStore d = new DataStore();
            try
            {
                if (!File.Exists(FilePath)) return d;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    d.Apply(line.Substring(0, eq), line.Substring(eq + 1));
                }
            }
            catch { }
            return d;
        }

        void Apply(string k, string v)
        {
            switch (k)
            {
                case "name": TyperName = v; break;
                case "tracking": Tracking = v == "1"; break;
                case "floating": Floating = v == "1"; break;
                case "onlyTyping": OnlyWhenTyping = v == "1"; break;
                case "pinned": Pinned = v == "1"; break;
                case "startup": StartWithWindows = v == "1"; break;
                case "updateCheck": LastUpdateCheck = v; break;
                case "floatX": FloatX = (int)L(v); break;
                case "floatY": FloatY = (int)L(v); break;
                case "theme": ThemeName = LiveWpm.Theme.Find(v) != null ? LiveWpm.Theme.Find(v).Id : LiveWpm.Theme.DefaultTheme; break;
                case "floatBg": FloatBgOpacity = (int)Math.Max(0, Math.Min(100, L(v))); break;
                case "peakWpm": PeakWpm = D(v); break;
                case "peakAcc": PeakAcc = D(v); break;
                case "peakWords": PeakWords = L(v); break;
                case "peakTime": PeakTime = T(v); break;
                case "totalWords": TotalWords = L(v); break;
                case "avgChars": AvgChars = L(v); break;
                case "avgBackspaces": AvgBackspaces = L(v); break;
                case "avgSeconds": AvgSeconds = D(v); break;
                case "day":
                    string[] p = v.Split('|');
                    if (p.Length < 6) break;
                    DayStat day = new DayStat();
                    DateTime date;
                    if (!DateTime.TryParseExact(p[0], "yyyy-MM-dd", Inv, DateTimeStyles.None, out date)) break;
                    day.Date = date;
                    day.PeakWpm = D(p[1]); day.Words = L(p[2]); day.Chars = L(p[3]);
                    day.Seconds = D(p[4]); day.Backspaces = L(p[5]);
                    day.Points.AddRange(Day(date).Points); // keep points if their line came first
                    Days[date] = day;
                    break;
                case "undo":
                    // undo=startedTicks|words|chars|backspaces|seconds|counted|point|prevPeak|prevAcc|prevWords|prevTimeTicks|prevDayPeak
                    string[] q = v.Split('|');
                    if (q.Length < 12) break;
                    Undo.Add(new SessionUndo {
                        Started = T(q[0]), Words = L(q[1]), Chars = L(q[2]), Backspaces = L(q[3]), Seconds = D(q[4]),
                        Counted = q[5] == "1", PointAdded = q[6] == "1", PrevPeakWpm = D(q[7]), PrevPeakAcc = D(q[8]),
                        PrevPeakWords = L(q[9]), PrevPeakTime = T(q[10]), PrevDayPeak = D(q[11]) });
                    break;
                case "pts":
                    // pts=yyyy-MM-dd|second,wpm,peak,words;second,wpm,peak,words;...
                    int bar = v.IndexOf('|');
                    DateTime pd;
                    if (bar < 0 || !DateTime.TryParseExact(v.Substring(0, bar), "yyyy-MM-dd", Inv, DateTimeStyles.None, out pd)) break;
                    DayStat target = Day(pd);
                    foreach (string item in v.Substring(bar + 1).Split(';'))
                    {
                        string[] f = item.Split(',');
                        if (f.Length < 4) continue;
                        target.Points.Add(new SpeedPoint { Second = (int)L(f[0]), Wpm = (float)D(f[1]), Peak = (float)D(f[2]), Words = (int)L(f[3]) });
                    }
                    break;
            }
        }

        public void Save()
        {
            if (NoSave) return;
            try
            {
                Directory.CreateDirectory(Folder);
                StringBuilder sb = new StringBuilder();
                W(sb, "name", TyperName.Replace("\r", " ").Replace("\n", " "));
                W(sb, "tracking", Tracking ? "1" : "0");
                W(sb, "floating", Floating ? "1" : "0");
                W(sb, "onlyTyping", OnlyWhenTyping ? "1" : "0");
                W(sb, "pinned", Pinned ? "1" : "0");
                W(sb, "startup", StartWithWindows ? "1" : "0");
                W(sb, "updateCheck", LastUpdateCheck);
                W(sb, "floatX", FloatX.ToString(Inv));
                W(sb, "floatY", FloatY.ToString(Inv));
                W(sb, "theme", ThemeName);
                W(sb, "floatBg", FloatBgOpacity.ToString(Inv));
                W(sb, "peakWpm", PeakWpm.ToString("R", Inv));
                W(sb, "peakAcc", PeakAcc.ToString("R", Inv));
                W(sb, "peakWords", PeakWords.ToString(Inv));
                W(sb, "peakTime", PeakTime.Ticks.ToString(Inv));
                W(sb, "totalWords", TotalWords.ToString(Inv));
                W(sb, "avgChars", AvgChars.ToString(Inv));
                W(sb, "avgBackspaces", AvgBackspaces.ToString(Inv));
                W(sb, "avgSeconds", AvgSeconds.ToString("R", Inv));
                foreach (DayStat d in Days.Values)
                {
                    W(sb, "day", string.Join("|", new string[] {
                        d.Date.ToString("yyyy-MM-dd", Inv), d.PeakWpm.ToString("R", Inv), d.Words.ToString(Inv),
                        d.Chars.ToString(Inv), d.Seconds.ToString("R", Inv), d.Backspaces.ToString(Inv) }));
                    if (d.Points.Count == 0) continue;
                    StringBuilder pts = new StringBuilder(d.Date.ToString("yyyy-MM-dd", Inv)).Append('|');
                    for (int i = 0; i < d.Points.Count; i++)
                    {
                        SpeedPoint p = d.Points[i];
                        if (i > 0) pts.Append(';');
                        pts.Append(p.Second.ToString(Inv)).Append(',').Append(p.Wpm.ToString("0.#", Inv)).Append(',')
                           .Append(p.Peak.ToString("0.#", Inv)).Append(',').Append(p.Words.ToString(Inv));
                    }
                    W(sb, "pts", pts.ToString());
                }
                foreach (SessionUndo u in Undo)
                    W(sb, "undo", string.Join("|", new string[] {
                        u.Started.Ticks.ToString(Inv), u.Words.ToString(Inv), u.Chars.ToString(Inv), u.Backspaces.ToString(Inv),
                        u.Seconds.ToString("R", Inv), u.Counted ? "1" : "0", u.PointAdded ? "1" : "0",
                        u.PrevPeakWpm.ToString("R", Inv), u.PrevPeakAcc.ToString("R", Inv), u.PrevPeakWords.ToString(Inv),
                        u.PrevPeakTime.Ticks.ToString(Inv), u.PrevDayPeak.ToString("R", Inv) }));
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch { }
        }

        static void W(StringBuilder sb, string k, string v) { sb.Append(k).Append('=').Append(v).Append('\n'); }
        static double D(string v) { double r; return double.TryParse(v, NumberStyles.Float, Inv, out r) ? r : 0; }
        static long L(string v) { long r; return long.TryParse(v, NumberStyles.Integer, Inv, out r) ? r : 0; }
        static DateTime T(string v) { long t = L(v); return t > 0 && t < DateTime.MaxValue.Ticks ? new DateTime(t) : DateTime.MinValue; }
    }
}
