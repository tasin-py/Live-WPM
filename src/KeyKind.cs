namespace LiveWpm
{
    // The only thing a keyboard reader ever reports: the kind of key, never the key itself.
    // LongPress = a character key was held down for a long time (typical in games, rare in typing).
    // Shared by the Windows app, the Linux app and the Linux keyboard service.
    enum KeyKind { Char, WordBreak, Backspace, LongPress }
}
