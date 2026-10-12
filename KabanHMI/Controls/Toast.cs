using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A SHORT NOTE THAT GOES BY ITSELF (9 October: "we could remove this long text
    /// popup window as well"): one or two lines at the bottom of the window, over
    /// the page, for a few seconds - nothing to click away, the focus left where it
    /// was. A tap on it closes it at once. A warning is amber and stays longer.
    /// </summary>
    public sealed class Toast : Form
    {
        private readonly Timer _timer = new Timer();
        private readonly Label _text = new Label();
        private static Toast _current;

        /// <summary>The note shown last (tests), and whether it was a warning.</summary>
        public static string LastText { get; private set; }
        public static bool LastWarning { get; private set; }

        private Toast(string text, bool warning, int ms)
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = warning ? Color.FromArgb(255, 236, 196) : Color.FromArgb(38, 42, 48);
            Padding = new Padding(UiScale.Px(14), UiScale.Px(10), UiScale.Px(14), UiScale.Px(10));
            Name = "toast";
            _text.AutoSize = true;
            _text.MaximumSize = new Size(UiScale.Px(560), 0);
            _text.Font = UiStyle.Body;
            _text.ForeColor = warning ? Color.FromArgb(90, 60, 0) : Color.White;
            _text.BackColor = Color.Transparent;
            _text.UseMnemonic = false;
            _text.Text = text ?? "";
            _text.Location = new Point(Padding.Left, Padding.Top);
            Controls.Add(_text);
            ClientSize = new Size(_text.PreferredSize.Width + Padding.Horizontal, _text.PreferredSize.Height + Padding.Vertical);
            _timer.Interval = Math.Max(1000, ms);
            _timer.Tick += (s, e) => Close();
            Click += (s, e) => Close();
            _text.Click += (s, e) => Close();
            FormClosed += (s, e) => { _timer.Stop(); _timer.Dispose(); if (_current == this) _current = null; };
        }

        /// <summary>Shown without taking the focus from the page.</summary>
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000080;   // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        /// <summary>
        /// Shows <paramref name="text"/> at the bottom centre of <paramref name="owner"/>'s
        /// window for a few seconds (a warning longer). One at a time: a new one replaces it.
        /// </summary>
        public static void Show(IWin32Window owner, string text, bool warning = false)
        {
            LastText = text;
            LastWarning = warning;
            try
            {
                if (_current != null && !_current.IsDisposed) _current.Close();
                var form = OwnerForm(owner);
                var toast = new Toast(text, warning, warning ? 9000 : 5000);
                Rectangle area = form != null && form.Visible ? form.Bounds : Screen.PrimaryScreen.WorkingArea;
                toast.Location = new Point(area.Left + (area.Width - toast.Width) / 2, area.Bottom - toast.Height - UiScale.Px(48));
                _current = toast;
                if (form != null && form.Visible) toast.Show(form); else toast.Show();
                toast._timer.Start();
            }
            catch { /* a note that cannot show must never stop what was done */ }
        }

        private static Form OwnerForm(IWin32Window owner)
        {
            var c = owner as Control;
            if (c == null && owner != null)
                try { c = Control.FromHandle(owner.Handle); } catch { c = null; }
            // the window on the screen (a page's form inside the side panel is not one)
            var form = c != null ? c.TopLevelControl as Form : null;
            return form ?? Form.ActiveForm;
        }
    }
}
