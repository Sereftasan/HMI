using System;
using System.Drawing;
using Kaban.Core.Enums;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// The symbols a command button shows, and how big to draw them.
    ///
    /// TWO PROBLEMS THIS SOLVES
    /// ------------------------
    /// 1. A direction arrow set in the same 9.5pt as a word is far too small to
    ///    aim at on a panel. A glyph is a PICTURE, not text, and wants roughly
    ///    twice the height - but growing every caption would push real words out
    ///    of their buttons. So the size depends on what the caption IS.
    ///
    /// 2. Momentary, Alternate, SET and RESET behave completely differently and
    ///    all looked identical. An operator pressing what they think is a jog
    ///    button and instead latching a relay is a real hazard, so each action
    ///    gets its own mark.
    ///
    /// NONE OF THESE BELONG IN A TRANSLATION FILE. An arrow is an arrow in every
    /// language; a translation row for it is a row that can only ever be wrong.
    /// </summary>
    public static class ButtonGlyphs
    {
        // ---- direction ----------------------------------------------------
        // Solid triangles, not the outline forms: at a distance an outline reads
        // as a smudge, and these are pressed by someone who is looking at the
        // machine rather than the screen.
        public const string Left  = "\u25C0";   // ◀
        public const string Right = "\u25B6";   // ▶
        public const string Up    = "\u25B2";   // ▲
        public const string Down  = "\u25BC";   // ▼

        // ---- action marks, one per bit action ------------------------------
        // A "seek to end" mark: a triangle driven up against a bar. It reads as
        // "put it there and leave it", which is exactly what a latched action
        // does, and it is nothing like a plain jog arrow - which is the point,
        // because pressing what looks like a jog and instead latching a relay is
        // a real hazard.
        //
        //   SET        FILLED triangle - drives the bit on and leaves it
        //   RESET      HOLLOW triangle - drives it off and leaves it
        //   Alternate  whichever of those describes what the NEXT press will do,
        //              so the button always shows its own effect
        //   Momentary  no mark; the plain direction arrow
        // These are TOKENS, not glyphs to render. The composed characters
        // "\u25B6\u2758" drew a stray line above the triangle - the two code points
        // have different cap heights, and no font choice fixes that reliably.
        // ButtonHMI draws the mark with GDI instead; these strings only identify
        // which mark to draw.
        public const string SetMark   = "\u0001SET";
        public const string ResetMark = "\u0001RST";

        /// <summary>
        /// The mark for an action.
        ///
        /// <paramref name="isOn"/> only matters for Alternate: a toggle shows the
        /// SET mark while the bit is off (press to turn on) and the RESET mark
        /// while it is on. A latch that looks the same in both states tells the
        /// operator nothing about what pressing it will do.
        /// </summary>
        public static string MarkFor(BitActionType action, bool isOn)
        {
            switch (action)
            {
                case BitActionType.SET:       return SetMark;
                case BitActionType.RESET:     return ResetMark;
                case BitActionType.Alternate: return isOn ? ResetMark : SetMark;
                default:                      return "";
            }
        }

        public static string MarkFor(BitActionType action) { return MarkFor(action, false); }

        /// <summary>Which way a momentary command drives its mechanism.</summary>
        public enum Direction { None, Left, Right, Up, Down }

        /// <summary>
        /// The arrow for a direction. Up/Down for a mechanism that RISES rather
        /// than travels: an operator jogging a head downward should not be looking
        /// at a left arrow.
        /// </summary>
        public static string ArrowFor(Direction d)
        {
            switch (d)
            {
                case Direction.Left:  return Left;
                case Direction.Right: return Right;
                case Direction.Up:    return Up;
                case Direction.Down:  return Down;
                default:              return "";
            }
        }

        /// <summary>
        /// True when a caption is PURELY symbols - arrows, marks, punctuation -
        /// with no letters or digits in it. Only these get enlarged.
        /// </summary>
        public static bool IsGlyphOnly(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }

        /// <summary>How much bigger a glyph-only caption is drawn.</summary>
        // 1.25. At 1.9 the arrow touched the button border; at 1.42 it still
        // read as oversized. At 1.9 the arrow filled the button edge to edge and
        // touched the border beneath it; a glyph needs to read as a mark ON a
        // button, not as the button itself.
        public const float GlyphScale = 1.25f;

        /// <summary>
        /// The font a button should use for the caption it currently has.
        /// Returns <paramref name="baseFont"/> unchanged for anything containing
        /// a letter or digit, so words keep fitting their buttons.
        ///
        /// The glyph size is also capped to the control, or a tall arrow in a
        /// short button gets its point clipped - which on a triangle removes
        /// exactly the part that shows the direction.
        /// </summary>
        public static Font FontFor(string text, Font baseFont, int controlHeight)
        {
            if (baseFont == null || !IsGlyphOnly(text)) return baseFont;

            float wanted = baseFont.Size * GlyphScale;
            if (controlHeight > 8)
            {
                float fits = (controlHeight - 8) * 72f / 96f;   // px -> pt, with padding
                if (fits > 6 && wanted > fits) wanted = fits;
            }
            if (Math.Abs(wanted - baseFont.Size) < 0.1f) return baseFont;
            return new Font(baseFont.FontFamily, wanted, baseFont.Style);
        }
    }
}

