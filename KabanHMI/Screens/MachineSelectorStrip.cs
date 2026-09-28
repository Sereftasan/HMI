using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// The SECOND navigation level: which MACHINE the current page is showing.
    ///
    /// It sits in the left bar under the page buttons, separated by a gap, so it
    /// reads as a second menu rather than more of the first. That placement is the
    /// point of it: machine selection used to be a tab strip at the top of the
    /// page, which put two unrelated kinds of navigation - "which page" on the
    /// left, "which machine" across the top - in two different places and shapes.
    ///
    /// IT DISAPPEARS when the current page has nothing to choose between. HMI
    /// Settings is one page for the whole program, and a machine with no
    /// sub-machines has one Home; showing a one-entry machine menu there would be
    /// asking a question with a single answer.
    ///
    /// Every machine carries an ICON as well as a name, so the strip still means
    /// something once the bar is collapsed to icons - the same rule the page
    /// buttons already follow.
    /// </summary>
    public class MachineSelectorStrip : Panel
    {
        private readonly List<ButtonHMI> _buttons = new List<ButtonHMI>();
        private readonly Panel _gap = new Panel();

        private const int ButtonHeight = 38;    // shorter than a page button: a sub-level
        private const int ButtonMarginY = 2;
        // The clear space above the list is reserved by MainForm's spacer, which
        // also absorbs the buttons a lower-level user cannot see - so the list stays
        // at a fixed height on the bar. The strip itself is only its buttons.
        private const int SeparatorHeight = 0;

        private MachineRegistry.RoleGroup _group;
        private int _selectedIndex;
        private bool _collapsed;

        /// <summary>Raised when the operator picks a different machine.</summary>
        public event EventHandler<int> MachineSelected;

        public MachineSelectorStrip()
        {
            // NOT AutoSize, and NOT docked: this lives in the navigation column's
            // FlowLayoutPanel, which lays children out in add order and leaves them
            // the size they declare. An AutoSize panel whose children are all
            // Dock.Top reports a preferred height of zero, which is how the strip
            // managed to be present and invisible at the same time.
            Dock = DockStyle.Top;
            BackColor = Color.Transparent;
            Visible = false;

            // The separating gap IS the thing that makes this read as a second menu
            // rather than four more page buttons. Docked so it survives re-layout.
            // The separating gap IS what makes this read as a second menu rather
            // than four more page buttons - it is the only separator now that the
            // caption is gone (a caption vanishes in collapsed mode anyway, so it
            // could never be what carried the distinction).
            _gap.Dock = DockStyle.Top;
            _gap.Height = SeparatorHeight;
            _gap.BackColor = Color.Transparent;
            _gap.Visible = SeparatorHeight > 0;

        }

        /// <summary>
        /// Rebuilds the strip for the page just opened. Passing a group with one (or
        /// no) machine HIDES it - see the class note.
        /// </summary>
        public void ShowFor(MachineRegistry.RoleGroup group)
        {
            ShowFor(group, 0);
        }

        /// <summary>
        /// Same, with the machine the page is ACTUALLY showing marked.
        ///
        /// The mark was always put on the first (main) machine, because the page
        /// was assumed to open on it - but a machine page remembers its last
        /// machine, so Back/Forward, or returning to Home, showed a sub-machine's
        /// page with the MAIN machine highlighted.
        /// </summary>
        public void ShowFor(MachineRegistry.RoleGroup group, int selectedIndex)
        {
            if (selectedIndex < 0) selectedIndex = 0;
            // SAME MACHINES AS LAST TIME -> leave the strip completely alone.
            //
            // It used to be torn down and rebuilt on every navigation click, so the
            // machine list blinked out and back each time the operator opened a
            // page - even when the very same machines were on offer. The strip
            // should only change when the ANSWER changes.
            if (SameMachines(group, _group))
            {
                if (group != null && group.IsTabbed) Select(selectedIndex, notify: false);
                return;
            }

            _group = group;

            Controls.Clear();
            _buttons.Clear();

            if (group == null || !group.IsTabbed)
            {
                Visible = false;
                Height = 0;
                return;
            }

            // Docked children stack in REVERSE add order, so add bottom-up: the last
            // machine first, then the caption and the gap end up on top.
            for (int i = group.Pages.Count - 1; i >= 0; i--)
            {
                var mounted = group.Pages[i].Key;
                int index = i;

                var btn = new ButtonHMI
                {
                    Dock = DockStyle.Top,
                    Height = ButtonHeight,   // shorter than a page button: a sub-level
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ImageAlign = ContentAlignment.MiddleLeft,
                    TextImageRelation = TextImageRelation.ImageBeforeText,
                    CornerRadius = 4,
                    RoundLeftOnly = false,
                    RoundRightOnly = true,
                    ActiveBackColor = Color.FromArgb(209, 214, 221),
                    RestingBorderColor = Color.FromArgb(150, 158, 170),
                    Margin = new Padding(6, ButtonMarginY, 0, ButtonMarginY),
                    Cursor = Cursors.Hand,
                    FallbackText = MachineTabScreen.DisplayNameOf(mounted),
                    TranslationKey = mounted.Module.DisplayNameKey ?? "",
                    Image = MachineIcon.For(mounted)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) => Select(index, notify: true);

                _buttons.Insert(0, btn);
                Controls.Add(btn);
                btn.ApplyTranslation();
            }

            Controls.Add(_gap);

            // Sized on screen: through the magnifier once the strip is scaled (the buttons are scaled as they are added).
            Height = Kaban.UI.Controls.UiScale.For(this, SeparatorHeight + group.Pages.Count * (ButtonHeight + ButtonMarginY * 2));
            Visible = true;
            // Children are Dock.Top INSIDE this panel; the panel itself is sized here.
            PerformLayout();
            Select(selectedIndex, notify: false);
            ApplyCollapsed(_collapsed);
        }

        /// <summary>
        /// True when two groups offer the same machines in the same order - the test
        /// for "nothing the operator can see has changed", which is what decides
        /// whether the strip may be left untouched.
        /// </summary>
        private static bool SameMachines(MachineRegistry.RoleGroup a, MachineRegistry.RoleGroup b)
        {
            if (a == null || b == null) return a == b;
            if (!a.IsTabbed || !b.IsTabbed) return a.IsTabbed == b.IsTabbed;
            if (a.Pages.Count != b.Pages.Count) return false;
            for (int i = 0; i < a.Pages.Count; i++)
                if (!ReferenceEquals(a.Pages[i].Key, b.Pages[i].Key)) return false;
            return true;
        }

        /// <summary>Highlights a machine, and optionally tells the shell to switch to it.</summary>
        public void Select(int index, bool notify)
        {
            if (index < 0 || index >= _buttons.Count) return;
            _selectedIndex = index;

            for (int i = 0; i < _buttons.Count; i++)
                _buttons[i].IsActivePage = (i == index);

            if (notify && MachineSelected != null) MachineSelected(this, index);
        }

        public int SelectedIndex { get { return _selectedIndex; } }

        /// <summary>Icon-only when the left bar is collapsed, matching the page buttons.</summary>
        public void ApplyCollapsed(bool collapsed)
        {
            _collapsed = collapsed;

            foreach (var b in _buttons)
            {
                // Same rule as the page buttons: HIDE the caption, never blank it.
                // Assigning "" now sets the caption to the key's translation, so
                // the label came back - and on the machine strip it came back
                // clipped, because the button is icon-width when collapsed.
                b.ShowCaption = !collapsed;
                b.Padding = collapsed ? new Padding(12, 0, 0, 0)
                                      : new Padding(12, 0, 4, 0);
            }
        }

        /// <summary>Re-applies captions after a language change.</summary>
        public void RefreshTranslations()
        {
            foreach (var b in _buttons) b.ApplyTranslation();
            ApplyCollapsed(_collapsed);
        }
    }

    /// <summary>
    /// Picks the glyph for a machine in the selector strip.
    ///
    /// A machine may supply its own via IMachineIcon - the machines here draw what
    /// they do, in plain ink (NavGlyphs). Otherwise it gets a drawn
    /// INITIAL - the first letter of its display name in a rounded tile - rather
    /// than a shared generic icon: in collapsed mode a generic icon would make
    /// every machine look identical, which is exactly the state the strip exists
    /// to resolve.
    /// </summary>
    public static class MachineIcon
    {
        private static readonly Dictionary<string, Image> _cache =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private static readonly Color[] Palette =
        {
            Color.FromArgb(52, 106, 168),    // main machine tends to come first
            Color.FromArgb(38, 132, 106),
            Color.FromArgb(160, 96, 40),
            Color.FromArgb(120, 72, 150),
            Color.FromArgb(150, 60, 78)
        };

        /// <summary>
        /// The badge for a module id.
        ///
        /// The alarm menu identifies machines by badge rather than name, and it
        /// only knows the module id - so it needs the same picture the left bar
        /// draws, not a second one that could drift from it.
        /// </summary>
        public static Image ForModule(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId)) return null;
            foreach (var m in MachineRegistry.Modules)
                if (string.Equals(m.Context.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
                    return For(m);
            return null;
        }

        public static Image For(MachineRegistry.MountedModule mounted)
        {
            if (mounted == null) return null;

            var own = mounted.Module as IMachineIcon;
            if (own != null && own.Icon != null) return own.Icon;

            string name = MachineTabScreen.DisplayNameOf(mounted);
            string key = mounted.Context.ModuleId + "|" + name;

            Image cached;
            if (_cache.TryGetValue(key, out cached)) return cached;

            var img = DrawInitial(name, mounted.Context.ModuleId);
            _cache[key] = img;
            return img;
        }

        /// <summary>
        /// The machine's tile colour. Stable per module: the same machine keeps the
        /// same tile between runs, so the operator can learn it as a shape rather
        /// than re-reading it. (A machine that draws its own picture - IMachineIcon -
        /// has no tile: its shape tells it apart.)
        /// </summary>
        public static Color ColourFor(string moduleId)
        {
            int hash = 0;
            foreach (char c in moduleId ?? "") hash = (hash * 31 + c) & 0x7FFFFFFF;
            return Palette[hash % Palette.Length];
        }

        private static Image DrawInitial(string name, string moduleId)
        {
            string letter = "?";
            string source = !string.IsNullOrWhiteSpace(name) ? name : moduleId;
            if (!string.IsNullOrWhiteSpace(source)) letter = source.Substring(0, 1).ToUpperInvariant();

            var colour = ColourFor(moduleId);

            var bmp = new Bitmap(24, 24);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var brush = new SolidBrush(colour))
                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    var r = new Rectangle(1, 1, 22, 22);
                    int d = 8;
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    g.FillPath(brush, path);
                }
                using (var font = new Font("Segoe UI", 10F, FontStyle.Bold))
                    TextRenderer.DrawText(g, letter, font, new Rectangle(0, 0, 24, 24),
                        Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPadding);
            }
            return bmp;
        }
    }

    /// <summary>
    /// Implemented by a module that ships its own navigation icon. Optional - a
    /// machine without one gets a coloured initial, which is enough to tell two
    /// machines apart in collapsed mode.
    /// </summary>
    public interface IMachineIcon
    {
        Image Icon { get; }
    }
}

