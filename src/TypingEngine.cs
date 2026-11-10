using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LiveWpm
{
    // Counters for one burst of typing. Holds numbers only.
    sealed class TypingSession
    {
        public DateTime Started, Ended;
        public long FirstMs, LastMs;
        public int Chars, Backspaces, Words;
        public int Breaks;       // spaces / enters / tabs
        public int LongHolds;    // character keys held down game-style
        public bool InWord;
        public bool Confirmed;   // has looked like real typing at least once
        public bool Rejected;    // turned out to be gaming / key spamming: never shown or saved
        public double PeakWpm;

        // Short bursts (a quick "ok") would give silly speeds, so they don't count toward records.
        public const int MinChars = 25;
        public const int MinMs = 3000;

        public double Seconds { get { return Math.Max(2.0, (LastMs - FirstMs) / 1000.0); } }
        public double AvgWpm { get { return Chars / 5.0 / (Seconds / 60.0); } }
        public double Accuracy { get { return Chars > 0 ? Math.Max(0, (Chars - Backspaces) * 100.0 / Chars) : 100; } }
        public int LiveWords { get { return Words + (InWord ? 1 : 0); } }
        public bool Counts { get { return Chars >= MinChars && LastMs - FirstMs >= MinMs; } }

        // Real text has spaces every few letters and keys are only tapped. Games and other
        // key presses (WASD, hotkeys, holding keys) don't look like that.
        public bool LooksLikeGaming
        {
            get
            {
                if (Chars >= 8 && LongHolds * 5 > Chars) return true;      // >20% of keys held down
                if (Chars >= 30 && Chars > (Breaks + 1) * 16) return true; // no spaces for ages
                return false;
            }
        }
    }

    enum Phase { Idle, Typing, Holding }

    // Turns key "kinds" into sessions: a session starts on the first key, ends after
    // 3 s without typing, and its result is held briefly before going back to idle.
    // A session only "wakes" the speedometer once it looks like real typing (two words
    // with spaces); bursts that never do, or that look like gaming, are thrown away.
    sealed class TypingEngine
    {
        // Stop typing -> 3 s later the session ends, 0.2 s later the dial drops and pops out (~4 s in all).
        public const int EndAfterMs = 3000;
        public const int HoldMs = 200;
        public const int WindowMs = 3000;   // live speed looks at the last 3 s of keystrokes
        const int PeakSpanMs = 2000;        // a live reading only counts as a peak once it covers 2 s of typing
        const int ConfirmWords = 2;

        readonly DataStore store;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Queue<long> recent = new Queue<long>(); // timestamps of recent keystrokes, nothing else
        long holdUntil, lastCharMs;

        public Phase Phase = Phase.Idle;
        public TypingSession Session;
        public double StartPeak;   // the Typer's record when this session began
        public bool NewPeak;       // set when the finished session beat the record

        public event Action Started, Ended, HoldDone;

        public TypingEngine(DataStore store) { this.store = store; }

        public long Now { get { return clock.ElapsedMilliseconds; } }

        public long IdleMs { get { return Session == null ? long.MaxValue : Now - Session.LastMs; } }

        // True while the speedometer should show this session.
        public bool Shown
        {
            get
            {
                if (Phase == Phase.Holding) return true;
                return Phase == Phase.Typing && Session != null && Session.Confirmed && !Session.Rejected;
            }
        }

        public void OnKey(KeyKind kind)
        {
            long now = Now;
            if (kind == KeyKind.LongPress)
            {
                if (Phase == Phase.Typing) { Session.LongHolds++; Check(); }
                return;
            }
            if (Phase != Phase.Typing)
            {
                Session = new TypingSession();
                Session.Started = DateTime.Now;
                Session.FirstMs = now;
                StartPeak = store.PeakWpm;
                NewPeak = false;
                recent.Clear();
                Phase = Phase.Typing;
            }
            TypingSession s = Session;
            s.LastMs = now;
            if (kind == KeyKind.Backspace) { s.Backspaces++; return; }

            s.Chars++;
            recent.Enqueue(now);
            lastCharMs = now;
            if (kind == KeyKind.Char) s.InWord = true;
            else
            {
                s.Breaks++;
                if (s.InWord) { s.Words++; s.InWord = false; }
            }
            Check();
        }

        void Check()
        {
            TypingSession s = Session;
            if (s.Rejected) return;
            if (s.LooksLikeGaming) { s.Rejected = true; return; }
            if (!s.Confirmed && s.Words >= ConfirmWords)
            {
                s.Confirmed = true;
                if (Started != null) Started();
            }
        }

        // Average gap between the recent keystrokes, in ms (0 when there are too few).
        double Gap(out long span)
        {
            long now = Now;
            while (recent.Count > 0 && recent.Peek() < now - WindowMs) recent.Dequeue();
            span = recent.Count > 0 ? lastCharMs - recent.Peek() : 0;
            return recent.Count >= 3 && span >= 300 ? span / (double)(recent.Count - 1) : 0;
        }

        // True as soon as the Typer has clearly stopped: no key for 3x their usual gap (at least 0.8 s).
        public bool Paused
        {
            get
            {
                if (Phase != Phase.Typing || Session == null) return true;
                long span;
                double gap = Gap(out span);
                return Now - lastCharMs > Math.Max(800, gap * 3);
            }
        }

        // Live speed: the pace between the keystrokes of the last 3 s. Being measured
        // key-to-key, it doesn't sag between keys or lag behind, and it is 0 the moment you stop.
        public double CurrentWpm
        {
            get
            {
                long span;
                return Paused ? 0 : LiveWpm(out span);
            }
        }

        double LiveWpm(out long span)
        {
            double gap = Gap(out span);
            return gap > 0 ? 60000.0 / gap / 5.0 : 0;
        }

        // The tray app calls this ~10x a second, only while a session is alive.
        public void Tick()
        {
            if (Phase == Phase.Typing)
            {
                TypingSession s = Session;
                long span;
                double live = LiveWpm(out span);
                if (live > 0 && !Paused && span >= PeakSpanMs && Now - s.FirstMs >= TypingSession.MinMs)
                    s.PeakWpm = Math.Max(s.PeakWpm, live);
                if (IdleMs >= EndAfterMs) Finish();
            }
            else if (Phase == Phase.Holding && Now >= holdUntil)
            {
                Phase = Phase.Idle;
                Session = null;
                if (HoldDone != null) HoldDone();
            }
        }

        // Ends the current session right away (also used when tracking is switched off).
        public void Finish()
        {
            if (Phase != Phase.Typing) return;
            TypingSession s = Session;
            recent.Clear();
            if (s.InWord) { s.Words++; s.InWord = false; }

            if (!s.Confirmed || s.Rejected || s.LooksLikeGaming)
            {
                // Not typing (a game, shortcuts, a stray key): forget it completely.
                bool wasShown = s.Confirmed && !s.Rejected;
                Phase = Phase.Idle;
                Session = null;
                if (wasShown && HoldDone != null) HoldDone();
                return;
            }

            s.Ended = DateTime.Now;
            if (s.Counts) s.PeakWpm = Math.Max(s.PeakWpm, s.AvgWpm);
            NewPeak = store.AddSession(s);
            store.Save();
            Phase = Phase.Holding;
            holdUntil = Now + HoldMs;
            if (Ended != null) Ended();
        }

        public void Stop()
        {
            Finish();
            Phase = Phase.Idle;
            Session = null;
        }

        // True once this session's top speed has passed the Typer's record.
        public bool RecordPace
        {
            get
            {
                if (!Shown || StartPeak <= 0) return false;
                if (Phase == Phase.Holding) return NewPeak;
                return Session.Chars >= TypingSession.MinChars && Session.PeakWpm > StartPeak;
            }
        }
    }
}
