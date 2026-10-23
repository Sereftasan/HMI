using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Profiles;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// A LANGUAGE'S FLAG (HMI Settings > Translation, the flag button; 23 October: "We need a language emoji
    /// addition tool so we can add flags to the new added languages"): the flag a language is shown with - on the
    /// caption bar, in the language menu, in the preferences - made from a country's flag (its name, its code, or
    /// its flag emoji pasted: 🇵🇱), from stripes or a cross in the colours chosen, or from a picture (a file, or
    /// pasted). Kept as Translations\Flags\&lt;code&gt;.png (LanguageFlags); "The program's own" takes it away.
    /// </summary>
    public sealed class TranslationFlagForm : Form
    {
        private readonly ComboBox _language = new ComboBox { Name = "flagLanguage", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly PictureBox _now = new PictureBox { Name = "flagNow", SizeMode = PictureBoxSizeMode.Zoom };
        private readonly PictureBox _new = new PictureBox { Name = "flagNew", SizeMode = PictureBoxSizeMode.Zoom };
        private readonly PictureBox _nowSmall = new PictureBox(), _newSmall = new PictureBox();
        private readonly TabControl _ways = new TabControl { Name = "flagWays" };
        private readonly TextBox _search = new TextBox { Name = "flagSearch" };
        private readonly ListView _countries = new ListView { Name = "flagCountries", View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false };
        private readonly ImageList _countryFlags = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(24, 16) };
        private readonly ComboBox _pattern = new ComboBox { Name = "flagPattern", DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button[] _colours = new Button[3];
        private readonly Label _status = new Label { Name = "flagStatus" };
        private readonly Button _save, _remove;
        private readonly ToolTip _tips = UiStyle.Tips();

        private Bitmap _chosen;
        private string _chosenWhat = "";

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        /// <summary>The flag chosen, not yet kept (null: none).</summary>
        internal Bitmap Chosen { get { return _chosen; } }

        /// <summary>The language whose flag it is.</summary>
        internal string Language
        {
            get { var o = _language.SelectedItem as Lang; return o != null ? o.Code : ""; }
            set
            {
                for (int i = 0; i < _language.Items.Count; i++)
                    if (string.Equals(((Lang)_language.Items[i]).Code, value, StringComparison.OrdinalIgnoreCase)) { _language.SelectedIndex = i; return; }
            }
        }

        internal string StatusText { get { return _status.Text; } }

        private sealed class Lang
        {
            public string Code, Text;
            public override string ToString() { return Text; }
        }

        public TranslationFlagForm(IEnumerable<string> languages, string language)
        {
            UiScale.ApplyOnLoad(this);
            Text = T("Ttl_FlgTool", "A language's flag");
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            Size = new Size(860, 560);
            MinimumSize = new Size(720, 480);

            // ---- the language, the flags now and new ----
            var left = new Panel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(12, 10, 10, 10) };
            var langLabel = new Label { Text = T("Lbl_FlgLanguage", "Language:"), AutoSize = true, Location = new Point(12, 12) };
            _language.SetBounds(12, 34, 250, 28);
            foreach (var l in languages.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var name = TranslationLanguageForm.NameFor(l);
                _language.Items.Add(new Lang { Code = l.ToUpperInvariant(), Text = l.ToUpperInvariant() + (string.IsNullOrWhiteSpace(name) ? "" : " - " + name) });
            }
            var nowLabel = new Label { Text = T("Lbl_FlgNow", "Its flag now:"), AutoSize = true, Location = new Point(12, 76) };
            _now.SetBounds(12, 98, 180, 120);
            _nowSmall.SetBounds(204, 196, 30, 21);
            var newLabel = new Label { Text = T("Lbl_FlgNew", "The flag chosen:"), AutoSize = true, Location = new Point(12, 236) };
            _new.SetBounds(12, 258, 180, 120);
            _newSmall.SetBounds(204, 356, 30, 21);
            var sizeNote = new Label
            {
                Text = T("Lbl_FlgSmall", "Beside: as small as on the caption bar."), Location = new Point(12, 386), Size = new Size(250, 40),
                ForeColor = Color.FromArgb(110, 116, 124)
            };
            foreach (var p in new[] { _now, _new, _nowSmall, _newSmall }) { p.BorderStyle = BorderStyle.FixedSingle; p.BackColor = Color.FromArgb(245, 246, 248); }
            _nowSmall.SizeMode = _newSmall.SizeMode = PictureBoxSizeMode.Normal;
            left.Controls.AddRange(new Control[] { langLabel, _language, nowLabel, _now, _nowSmall, newLabel, _new, _newSmall, sizeNote });

            // ---- the ways to make one ----
            _ways.Dock = DockStyle.Fill;
            var country = new TabPage(T("Tab_FlgCountry", "A country's flag")) { BackColor = Color.White, Padding = new Padding(8), Name = "flagWayCountry" };
            var searchRow = new Panel { Dock = DockStyle.Top, Height = 36 };
            var searchLabel = new Label { Text = T("Lbl_FlgSearch", "Its name or code - or paste its flag emoji:"), AutoSize = true, Location = new Point(0, 8) };
            searchRow.Controls.Add(searchLabel);
            _search.SetBounds(0, 0, 200, 26);
            _search.Dock = DockStyle.Right;
            _search.TextChanged += (s, e) => FillCountries();
            searchRow.Controls.Add(_search);
            _countries.Dock = DockStyle.Fill;
            _countries.SmallImageList = _countryFlags;
            _countries.Columns.Add(T("Col_FlgCountry", "Country"), 300);
            _countries.Columns.Add(T("Col_FlgCode", "Code"), 70);
            _countries.SelectedIndexChanged += (s, e) =>
            {
                if (_countries.SelectedItems.Count == 0) return;
                ChooseCountry((string)_countries.SelectedItems[0].Tag);
            };
            foreach (var c in LanguageFlags.Countries)
                using (var img = LanguageFlags.Render(c.Draw, 24, 16)) _countryFlags.Images.Add(c.Code, new Bitmap(img));
            country.Controls.Add(_countries);
            country.Controls.Add(searchRow);

            var pattern = new TabPage(T("Tab_FlgPattern", "Stripes or a cross")) { BackColor = Color.White, Padding = new Padding(8), Name = "flagWayPattern" };
            var patternLabel = new Label { Text = T("Lbl_FlgPattern", "The pattern:"), AutoSize = true, Location = new Point(10, 14) };
            _pattern.SetBounds(10, 38, 260, 28);
            foreach (LanguageFlags.Pattern p in Enum.GetValues(typeof(LanguageFlags.Pattern))) _pattern.Items.Add(new PatternItem(p));
            _pattern.SelectedIndex = 2;
            _pattern.SelectedIndexChanged += (s, e) => { ShowColourButtons(); DrawPattern(); };
            var colourLabel = new Label { Text = T("Lbl_FlgColours", "Its colours (from the top, from the left; the field first, then the cross):"), Location = new Point(10, 82), Size = new Size(500, 22) };
            pattern.Controls.AddRange(new Control[] { patternLabel, _pattern, colourLabel });
            var start = new[] { Color.FromArgb(0, 85, 164), Color.White, Color.FromArgb(239, 65, 53) };
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                var b = new Button { Name = "flagColour" + i, FlatStyle = FlatStyle.Flat, BackColor = start[i], Size = new Size(70, 40), Location = new Point(10 + i * 84, 108), Cursor = Cursors.Hand };
                b.FlatAppearance.BorderColor = Color.FromArgb(150, 156, 166);
                b.Click += (s, e) => PickColour(n);
                _tips.SetToolTip(b, T("Tip_FlgColour", "Click: choose this colour"));
                _colours[i] = b;
                pattern.Controls.Add(b);
            }
            var usePattern = UiStyle.TextButton(T("Btn_FlgUsePattern", "Use these"), 0, (s, e) => DrawPattern());
            usePattern.Location = new Point(10, 164);
            pattern.Controls.Add(usePattern);

            var picture = new TabPage(T("Tab_FlgPicture", "A picture")) { BackColor = Color.White, Padding = new Padding(8), Name = "flagWayPicture" };
            var open = UiStyle.TextButton(T("Btn_FlgOpen", "Open a picture file..."), 0, (s, e) => OpenPicture());
            open.Location = new Point(10, 14);
            var paste = UiStyle.TextButton(T("Btn_FlgPaste", "Paste a picture"), 0, (s, e) => PastePicture());
            paste.Location = new Point(10, 14 + open.Height + 10);
            var pictureNote = new Label
            {
                Text = T("Lbl_FlgPictureNote", "A PNG, JPG, BMP or GIF of any size: its middle is taken in the flags' shape (3:2), cut at the sides or the top and bottom. " +
                                               "A flag found on the Internet (Wikipedia has every country's and region's) saved as a picture works."),
                Location = new Point(10, paste.Bottom + 14), Size = new Size(500, 80), ForeColor = Color.FromArgb(90, 96, 106)
            };
            picture.Controls.AddRange(new Control[] { open, paste, pictureNote });

            _ways.TabPages.Add(country);
            _ways.TabPages.Add(pattern);
            _ways.TabPages.Add(picture);

            // ---- keep it ----
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8, 10, 8, 6), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var close = UiStyle.DialogButton(T("Btn_Close", "Close"), DialogResult.Cancel);
            _save = UiStyle.TextButton(T("Btn_FlgSave", "Keep it as its flag"), 0, (s, e) => SaveFlag());
            _save.Name = "flagSave";
            _remove = UiStyle.TextButton(T("Btn_FlgRemove", "The program's own"), 0, (s, e) => RemoveFlag());
            _remove.Name = "flagRemove";
            _tips.SetToolTip(_remove, T("Tip_FlgRemove", "Take the flag made here away: the program's own picture, or the language's country's flag, is shown again"));
            bottom.Controls.Add(close);
            bottom.Controls.Add(_save);
            bottom.Controls.Add(_remove);
            CancelButton = close;
            _status.Dock = DockStyle.Bottom;
            _status.Height = 26;
            _status.Padding = new Padding(12, 4, 8, 0);
            _status.ForeColor = Color.FromArgb(70, 76, 86);

            Controls.Add(_ways);
            Controls.Add(left);
            Controls.Add(_status);
            Controls.Add(bottom);

            _language.SelectedIndexChanged += (s, e) => LanguageShown();
            ShowColourButtons();
            FillCountries();
            if (_language.Items.Count > 0)
            {
                Language = language ?? "";
                if (_language.SelectedIndex < 0) _language.SelectedIndex = 0;
            }
            Disposed += (s, e) => { if (_chosen != null) _chosen.Dispose(); _countryFlags.Dispose(); };
        }

        private sealed class PatternItem
        {
            public readonly LanguageFlags.Pattern Pattern;
            public PatternItem(LanguageFlags.Pattern p) { Pattern = p; }
            public override string ToString() { return LanguageFlags.PatternName(Pattern); }
        }

        /// <summary>The language's flag as it is shown now; its country's offered in the list.</summary>
        private void LanguageShown()
        {
            ShowFlag(_now, _nowSmall, null);
            var c = LanguageFlags.CountryOfLanguage(Language);
            _remove.Enabled = File.Exists(LanguageFlags.FileOf(Language));
            Choose(null, "");
            if (c != null)
            {
                foreach (ListViewItem item in _countries.Items) item.Selected = (string)item.Tag == c.Code;
                var sel = _countries.Items.Cast<ListViewItem>().FirstOrDefault(i => (string)i.Tag == c.Code);
                if (sel != null) sel.EnsureVisible();
            }
        }

        /// <summary>A flag in the two boxes: <paramref name="flag"/>, or (null) the language's as the program shows it.</summary>
        private void ShowFlag(PictureBox big, PictureBox small, Image flag)
        {
            var oldBig = big.Image; var oldSmall = small.Image;
            if (flag == null && big == _new) { big.Image = null; small.Image = null; }
            else
            {
                var b = new Bitmap(180, 120);
                using (var g = Graphics.FromImage(b))
                {
                    if (flag != null) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(flag, new Rectangle(0, 0, 180, 120)); }
                    else Kaban.UI.Forms.MainForm.DrawFlag(g, new Rectangle(0, 0, 179, 119), Language);
                }
                var sm = new Bitmap(30, 21);
                using (var g = Graphics.FromImage(sm))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    if (flag != null) { g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(flag, new Rectangle(0, 0, 30, 21)); }
                    else Kaban.UI.Forms.MainForm.DrawFlag(g, new Rectangle(0, 0, 29, 20), Language);
                }
                big.Image = b; small.Image = sm;
            }
            if (oldBig != null) oldBig.Dispose();
            if (oldSmall != null) oldSmall.Dispose();
        }

        private void Choose(Bitmap flag, string what)
        {
            if (_chosen != null && !ReferenceEquals(_chosen, flag)) _chosen.Dispose();
            _chosen = flag;
            _chosenWhat = what ?? "";
            ShowFlag(_new, _newSmall, flag);
            _save.Enabled = flag != null && Language.Length > 0;
        }

        // ---- a country's ----

        /// <summary>The countries whose name or code has the text searched, or whose flag emoji it is.</summary>
        internal void FillCountries()
        {
            string q = (_search.Text ?? "").Trim();
            string emoji = LanguageFlags.CodeOfEmoji(q);
            _countries.BeginUpdate();
            _countries.Items.Clear();
            foreach (var c in LanguageFlags.Countries)
            {
                bool shown = q.Length == 0 || (emoji != null ? c.Code == emoji
                                                             : c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(c.Code, q, StringComparison.OrdinalIgnoreCase));
                if (!shown) continue;
                _countries.Items.Add(new ListViewItem(new[] { c.Name, c.Code }, c.Code) { Tag = c.Code });
            }
            _countries.EndUpdate();
            if (emoji != null && _countries.Items.Count == 1) _countries.Items[0].Selected = true;
            else if (emoji != null && _countries.Items.Count == 0)
                _status.Text = string.Format(T("Msg_FlgNoCountry", "{0} is not in the list - make it with stripes or a cross, or from a picture."), emoji);
        }

        /// <summary>The countries listed (tests).</summary>
        internal List<string> CountriesListed { get { return _countries.Items.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList(); } }

        internal void Search(string text) { _search.Text = text ?? ""; }

        internal bool ChooseCountry(string code)
        {
            var c = LanguageFlags.CountryOf(code);
            if (c == null) return false;
            Choose(LanguageFlags.Render(c.Draw), string.Format("the flag of {0}", c.Name));
            _status.Text = string.Format(T("Msg_FlgCountryChosen", "{0}'s flag chosen - Keep it as its flag."), c.Name);
            return true;
        }

        // ---- stripes or a cross ----

        private LanguageFlags.Pattern CurrentPattern
        {
            get { var p = _pattern.SelectedItem as PatternItem; return p != null ? p.Pattern : LanguageFlags.Pattern.ThreeBands; }
        }

        private void ShowColourButtons()
        {
            int n = LanguageFlags.ColoursOf(CurrentPattern);
            for (int i = 0; i < _colours.Length; i++) _colours[i].Visible = i < n;
        }

        private void PickColour(int i)
        {
            using (var dlg = new ColorDialog { Color = _colours[i].BackColor, FullOpen = true, AnyColor = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _colours[i].BackColor = dlg.Color;
            }
            DrawPattern();
        }

        /// <summary>The pattern in the colours chosen (tests: set first).</summary>
        internal void ChoosePattern(LanguageFlags.Pattern p, params Color[] colours)
        {
            for (int i = 0; i < _pattern.Items.Count; i++) if (((PatternItem)_pattern.Items[i]).Pattern == p) _pattern.SelectedIndex = i;
            for (int i = 0; colours != null && i < colours.Length && i < _colours.Length; i++) _colours[i].BackColor = colours[i];
            DrawPattern();
        }

        private void DrawPattern()
        {
            var p = CurrentPattern;
            var colours = _colours.Take(LanguageFlags.ColoursOf(p)).Select(b => b.BackColor).ToArray();
            Choose(LanguageFlags.Render((g, f) => LanguageFlags.DrawPattern(g, f, p, colours)),
                   LanguageFlags.PatternName(p) + ": " + string.Join(", ", colours.Select(c => "#" + (c.ToArgb() & 0xFFFFFF).ToString("X6"))));
            _status.Text = T("Msg_FlgPatternChosen", "Click a colour to change it - Keep it as its flag.");
        }

        // ---- a picture ----

        private void OpenPicture()
        {
            using (var dlg = new OpenFileDialog { Filter = T("Flt_Pictures", "Pictures") + " (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                ChoosePicture(dlg.FileName);
            }
        }

        /// <summary>A picture file fitted to the flags' shape; false (said): not a picture.</summary>
        internal bool ChoosePicture(string path)
        {
            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                using (var img = Image.FromStream(ms))
                    Choose(LanguageFlags.Fit(img), "the picture " + Path.GetFileName(path));
                _status.Text = string.Format(T("Msg_FlgPictureChosen", "{0}: its middle taken - Keep it as its flag."), Path.GetFileName(path));
                return true;
            }
            catch (Exception ex)
            {
                _status.Text = string.Format(T("Msg_FlgNoPicture", "{0} could not be read as a picture ({1})."), Path.GetFileName(path), ex.Message);
                return false;
            }
        }

        private void PastePicture()
        {
            Image img = null;
            try { if (Clipboard.ContainsImage()) img = Clipboard.GetImage(); } catch { }
            if (img == null)
            {
                // a flag emoji copied: its country's
                string text = null;
                try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
                var code = LanguageFlags.CodeOfEmoji(text);
                if (code != null && ChooseCountry(code)) return;
                _status.Text = T("Msg_FlgNothingToPaste", "No picture copied (copy one - in a browser: right-click it, Copy image).");
                return;
            }
            using (img) Choose(LanguageFlags.Fit(img), "a pasted picture");
            _status.Text = T("Msg_FlgPasted", "The picture pasted: its middle taken - Keep it as its flag.");
        }

        // ---- kept ----

        /// <summary>The flag chosen kept as the language's (Maintenance and above).</summary>
        internal bool SaveFlag()
        {
            if (_chosen == null || Language.Length == 0) return false;
            if (!SecurityManager.HasAccess(Kaban.Core.Enums.SecurityLevel.Maintenance))
            {
                _status.Text = T("Msg_FlgLevel", "A language's flag is made by Maintenance and above.");
                return false;
            }
            try { LanguageFlags.Save(Language, _chosen, _chosenWhat); }
            catch (Exception ex) { _status.Text = ex.Message; return false; }
            _status.Text = string.Format(T("Msg_FlgSaved", "{0}'s flag kept - shown on the caption bar and in the language lists."), Language);
            ShowFlag(_now, _nowSmall, null);
            _remove.Enabled = true;
            return true;
        }

        /// <summary>The flag made here taken away: the program's own (or the country's) again.</summary>
        internal bool RemoveFlag()
        {
            if (!SecurityManager.HasAccess(Kaban.Core.Enums.SecurityLevel.Maintenance)) { _status.Text = T("Msg_FlgLevel", "A language's flag is made by Maintenance and above."); return false; }
            bool done;
            try { done = LanguageFlags.Remove(Language); }
            catch (Exception ex) { _status.Text = ex.Message; return false; }
            _status.Text = done ? string.Format(T("Msg_FlgRemoved", "{0}: the flag made here taken away - the program's own shown again."), Language)
                                : T("Msg_FlgNoneMade", "No flag was made here for it.");
            ShowFlag(_now, _nowSmall, null);
            _remove.Enabled = false;
            return done;
        }

        /// <summary>Tests: the window given to it instead of shown.</summary>
        public static Action<TranslationFlagForm> Showing;

        /// <summary>The tool for <paramref name="language"/> (or the first).</summary>
        public static void Run(IWin32Window owner, IEnumerable<string> languages, string language)
        {
            using (var f = new TranslationFlagForm(languages, language))
            {
                if (Showing != null) { Showing(f); return; }
                f.ShowDialog(owner);
            }
        }
    }
}
