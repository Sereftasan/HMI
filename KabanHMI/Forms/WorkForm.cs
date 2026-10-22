using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// A WORK THAT TAKES SECONDS, WAITED FOR (22 October): a simple motion module writing its
    /// flash ROM (up to 5 s), a PLC's devices read into a backup or written back from one. The
    /// work runs on a thread of its own - the controller calls go through the drivers, which
    /// keep their own threads - while this small window says what is being done, how far it
    /// has got, and offers Cancel where stopping half way is safe.
    ///
    ///     var result = WorkForm.Run(owner, "Flash ROM", "Writing ...", p => DoIt(p), cancellable: false);
    /// </summary>
    public sealed class WorkForm : Form
    {
        /// <summary>What the work tells the window, from its own thread.</summary>
        public sealed class Progress
        {
            private volatile bool _cancelled;
            internal WorkForm Form;

            /// <summary>Cancel was pressed: the work stops at its next safe point.</summary>
            public bool Cancelled { get { return _cancelled; } }
            internal void Cancel() { _cancelled = true; }

            /// <summary>How far: <paramref name="done"/> of <paramref name="total"/> (0: unknown), and what now.</summary>
            public void Report(long done, long total, string text)
            {
                var f = Form;
                if (f == null || f.IsDisposed) return;
                try { f.BeginInvoke((Action)(() => f.ShowProgress(done, total, text))); } catch { }
            }
        }

        /// <summary>
        /// For the tests: the work runs at once on the calling thread, no window shown
        /// (<see cref="LastText"/> keeps its last report).
        /// </summary>
        public static bool Inline;
        public static string LastText = "";

        private readonly Label _text;
        private readonly ProgressBar _bar;
        private readonly Button _cancel;
        private readonly Progress _progress = new Progress();
        private Exception _error;
        private bool _done;

        private WorkForm(string title, string text, bool cancellable)
        {
            UiScale.ApplyOnLoad(this);
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ControlBox = false;                       // closed by the work, not by the user
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(460, cancellable ? 150 : 110);

            _text = new Label { AutoSize = false, Location = new Point(16, 14), Size = new Size(428, 44), UseMnemonic = false, Text = text ?? "" };
            _bar = new ProgressBar { Location = new Point(16, 64), Size = new Size(428, 20), Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
            Controls.Add(_text);
            Controls.Add(_bar);
            if (cancellable)
            {
                _cancel = new Button { Text = LanguageManager.GetString("Btn_Cancel", "Cancel"), Size = new Size(110, 32), Location = new Point(334, 102) };
                _cancel.Click += (s, e) =>
                {
                    _progress.Cancel();
                    _cancel.Enabled = false;
                    _text.Text = LanguageManager.GetString("Msg_WorkStopping", "Stopping at the next safe point...");
                };
                Controls.Add(_cancel);
            }
            _progress.Form = this;
        }

        private void ShowProgress(long done, long total, string text)
        {
            if (IsDisposed) return;
            if (text != null) _text.Text = text;
            if (total > 0)
            {
                if (_bar.Style != ProgressBarStyle.Continuous) _bar.Style = ProgressBarStyle.Continuous;
                _bar.Maximum = 1000;
                _bar.Value = (int)Math.Max(0, Math.Min(1000, done * 1000 / total));
            }
        }

        /// <summary>
        /// Runs <paramref name="work"/> on a thread of its own while the window is shown (modal to
        /// <paramref name="owner"/>), and returns its result once it has finished. An exception
        /// the work throws is thrown here again, on the caller's thread.
        /// </summary>
        public static T Run<T>(IWin32Window owner, string title, string text, Func<Progress, T> work, bool cancellable = false)
        {
            if (Inline)
            {
                var p = new Progress();
                LastText = text ?? "";
                return work(p);
            }
            T result = default(T);
            using (var f = new WorkForm(title, text, cancellable))
            {
                f.Shown += (s, e) =>
                {
                    var thread = new Thread(() =>
                    {
                        try { result = work(f._progress); }
                        catch (Exception ex) { f._error = ex; }
                        f._done = true;
                        try { f.BeginInvoke((Action)(() => { f.DialogResult = DialogResult.OK; f.Close(); })); } catch { }
                    });
                    thread.IsBackground = true;
                    thread.Name = "Work: " + title;
                    thread.Start();
                };
                f.FormClosing += (s, e) => { if (!f._done) e.Cancel = true; };   // only the work closes it
                f.ShowDialog(owner);
                if (f._error != null)
                    throw new InvalidOperationException(f._error.Message, f._error);
            }
            return result;
        }
    }
}
