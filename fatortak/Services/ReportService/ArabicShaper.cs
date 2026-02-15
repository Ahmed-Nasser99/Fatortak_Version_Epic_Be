using System;
using System.Collections.Generic;
using System.Text;

namespace fatortak.Services.ReportService
{
    public static class ArabicShaper
    {
        private class ArabicChar
        {
            public char Isolated { get; set; }
            public char Initial { get; set; }
            public char Medial { get; set; }
            public char Final { get; set; }
            public bool ConnectsToPrevious { get; set; } = true;
            public bool ConnectsToNext { get; set; } = true;

            public ArabicChar(char isolated, char initial, char medial, char final, bool connectsToNext = true)
            {
                Isolated = isolated;
                Initial = initial;
                Medial = medial;
                Final = final;
                ConnectsToNext = connectsToNext;
            }
        }

        private static readonly Dictionary<char, ArabicChar> Map = new Dictionary<char, ArabicChar>
        {
            { '\u0621', new ArabicChar('\uFE80', '\uFE80', '\uFE80', '\uFE80', false) }, // Hamza
            { '\u0622', new ArabicChar('\uFE81', '\uFE81', '\uFE82', '\uFE82', false) }, // Alif Madda
            { '\u0623', new ArabicChar('\uFE83', '\uFE83', '\uFE84', '\uFE84', false) }, // Alif Hamza Above
            { '\u0624', new ArabicChar('\uFE85', '\uFE85', '\uFE86', '\uFE86', false) }, // Waw Hamza Above
            { '\u0625', new ArabicChar('\uFE87', '\uFE87', '\uFE88', '\uFE88', false) }, // Alif Hamza Below
            { '\u0626', new ArabicChar('\uFE89', '\uFE8B', '\uFE8C', '\uFE8A') }, // Yeh Hamza Above
            { '\u0627', new ArabicChar('\uFE8D', '\uFE8D', '\uFE8E', '\uFE8E', false) }, // Alif
            { '\u0628', new ArabicChar('\uFE8F', '\uFE91', '\uFE92', '\uFE90') }, // Beh
            { '\u0629', new ArabicChar('\uFE93', '\uFE93', '\uFE94', '\uFE94', false) }, // Teh Marbuta
            { '\u062A', new ArabicChar('\uFE95', '\uFE97', '\uFE98', '\uFE96') }, // Teh
            { '\u062B', new ArabicChar('\uFE99', '\uFE9B', '\uFE9C', '\uFE9A') }, // Theh
            { '\u062C', new ArabicChar('\uFE9D', '\uFE9F', '\uFEA0', '\uFE9E') }, // Jeem
            { '\u062D', new ArabicChar('\uFEA1', '\uFEA3', '\uFEA4', '\uFEA2') }, // Hah
            { '\u062E', new ArabicChar('\uFEA5', '\uFEA7', '\uFEA8', '\uFEA6') }, // Khah
            { '\u062F', new ArabicChar('\uFEA9', '\uFEA9', '\uFEAA', '\uFEAA', false) }, // Dal
            { '\u0630', new ArabicChar('\uFEAB', '\uFEAB', '\uFEAC', '\uFEAC', false) }, // Thal
            { '\u0631', new ArabicChar('\uFEAD', '\uFEAD', '\uFEAE', '\uFEAE', false) }, // Reh
            { '\u0632', new ArabicChar('\uFEAF', '\uFEAF', '\uFEB0', '\uFEB0', false) }, // Zain
            { '\u0633', new ArabicChar('\uFEB1', '\uFEB3', '\uFEB4', '\uFEB2') }, // Seen
            { '\u0634', new ArabicChar('\uFEB5', '\uFEB7', '\uFEB8', '\uFEB6') }, // Sheen
            { '\u0635', new ArabicChar('\uFEB9', '\uFEBB', '\uFEBC', '\uFEBA') }, // Sad
            { '\u0636', new ArabicChar('\uFEBD', '\uFEBF', '\uFEC0', '\uFEBE') }, // Dad
            { '\u0637', new ArabicChar('\uFEC1', '\uFEC3', '\uFEC4', '\uFEC2') }, // Tah
            { '\u0638', new ArabicChar('\uFEC5', '\uFEC7', '\uFEC8', '\uFEC6') }, // Zah
            { '\u0639', new ArabicChar('\uFEC9', '\uFECB', '\uFECC', '\uFECA') }, // Ain
            { '\u063A', new ArabicChar('\uFECD', '\uFECF', '\uFED0', '\uFECE') }, // Ghain
            { '\u0640', new ArabicChar('\u0640', '\u0640', '\u0640', '\u0640') }, // Tatweel
            { '\u0641', new ArabicChar('\uFED1', '\uFED3', '\uFED4', '\uFED2') }, // Feh
            { '\u0642', new ArabicChar('\uFED5', '\uFED7', '\uFED8', '\uFED6') }, // Qaf
            { '\u0643', new ArabicChar('\uFED9', '\uFEDB', '\uFEDC', '\uFEDA') }, // Kaf
            { '\u0644', new ArabicChar('\uFEDD', '\uFEDF', '\uFEE0', '\uFEDE') }, // Lam
            { '\u0645', new ArabicChar('\uFEE1', '\uFEE3', '\uFEE4', '\uFEE2') }, // Meem
            { '\u0646', new ArabicChar('\uFEE5', '\uFEE7', '\uFEE8', '\uFEE6') }, // Noon
            { '\u0647', new ArabicChar('\uFEE9', '\uFEEB', '\uFEEC', '\uFEEA') }, // Heh
            { '\u0648', new ArabicChar('\uFEED', '\uFEED', '\uFEEE', '\uFEEE', false) }, // Waw
            { '\u0649', new ArabicChar('\uFEEF', '\uFEEF', '\uFEF0', '\uFEF0', false) }, // Alef Maksura
            { '\u064A', new ArabicChar('\uFEF1', '\uFEF3', '\uFEF4', '\uFEF2') }, // Yeh
            { '\u067E', new ArabicChar('\uFB56', '\uFB58', '\uFB59', '\uFB57') }, // Peh
            { '\u0686', new ArabicChar('\uFB7A', '\uFB7C', '\uFB7D', '\uFB7B') }, // Tcheh
            { '\u06A9', new ArabicChar('\uFB8E', '\uFB90', '\uFB91', '\uFB8F') }, // Keheh
            { '\u06AF', new ArabicChar('\uFB92', '\uFB94', '\uFB95', '\uFB93') }, // Gaf
            { '\u06CC', new ArabicChar('\uFEEF', '\uFEF3', '\uFEF4', '\uFEF0') }, // Farsi Yeh
        };

        public static string Shape(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var result = new StringBuilder();
            var chars = text.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                char current = chars[i];
                if (!Map.ContainsKey(current))
                {
                    result.Append(current);
                    continue;
                }

                char prev = i > 0 ? chars[i - 1] : '\0';
                char next = i < chars.Length - 1 ? chars[i + 1] : '\0';

                bool connectPrev = Map.ContainsKey(prev) && Map[prev].ConnectsToNext;
                bool connectNext = Map.ContainsKey(next) && Map[next].ConnectsToPrevious; // All defined chars connect to previous except if they are start of word? No, ConnectsToPrevious is implicitly true for all letters in Map, but we need to check if *next* char exists in map.

                // Actually, ConnectsToPrevious is true for all Arabic letters.
                // The condition is: Does the *previous* letter allow connecting to its next?
                
                // Refined Logic:
                // Initial: Prev does NOT connect to me, Next DOES connect to me.
                // Medial: Prev connects to me, Next connects to me.
                // Final: Prev connects to me, Next does NOT connect to me.
                // Isolated: Prev does NOT connect to me, Next does NOT connect to me.

                // Check if previous char connects to us
                bool prevConnects = false;
                if (Map.ContainsKey(prev))
                {
                    prevConnects = Map[prev].ConnectsToNext;
                }

                // Check if next char connects to us (we connect to it, and it is an Arabic char)
                bool nextConnects = false;
                if (Map.ContainsKey(next))
                {
                    // We can connect to next if *we* allow connecting to next AND next is a valid char
                    nextConnects = Map[current].ConnectsToNext;
                }

                var form = Map[current];
                if (!prevConnects && !nextConnects)
                    result.Append(form.Isolated);
                else if (!prevConnects && nextConnects)
                    result.Append(form.Initial);
                else if (prevConnects && nextConnects)
                    result.Append(form.Medial);
                else if (prevConnects && !nextConnects)
                    result.Append(form.Final);
            }

            // Handle Lam-Alef ligatures separately if needed, but basic shaping is often enough.
            // For now, let's stick to basic shaping.
            
            // Reverse the result for RTL rendering if needed
            // Since iText with RTL direction usually expects logical order, we might NOT need to reverse.
            // BUT if the user says it's "reverted" (reversed), maybe they mean it's showing LTR.
            // If we shape it, we get the correct glyphs.
            // If we reverse it, we force the visual order.
            
            // Let's try returning the shaped text first. If it's still wrong, we can reverse it.
            // Actually, usually manual shaping implies manual reversing too because you often turn off the engine's bidi to avoid double processing.
            
            return Reverse(result.ToString());
        }

        private static string Reverse(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            char[] charArray = text.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
    }
}
