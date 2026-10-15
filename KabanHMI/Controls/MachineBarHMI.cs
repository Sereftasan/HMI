using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A bar of controls that stays in sight at the top of a page: the axis pages'
    /// heading (AxisPageFrame) keeps it in place while the rows scroll, up and down
    /// and sideways.
    /// </summary>
    public interface IPageBar { }

    /// <summary>
    /// A MACHINE'S OWN BAR, designed ONCE and shown wherever it is needed (14 October:
    /// "the servo manual movement speed and movement interval set - I copied it to all
    /// the manual movement pages which have an AxisUnitHMI; we should show it on the top
    /// of Machine Parameters > Axis and on the Axis Monitor too. What is the best way to
    /// show this control set on all these pages without copy / pasting everywhere one by
    /// one? ... I will have to change it at all of these pages"):
    ///
    /// A machine makes one UserControl from this (FA2010: Screens\Shared\AxisJogBar),
    /// designed in Visual Studio like a page. A page shows it by holding it - it is in
    /// the Toolbox once the machine is built, dropped like any control - and the module
    /// declares it (ScreenRoles.AxisBar) so the pages made by the program (Axis Settings,
    /// the Axis Monitor) show it too. Changed in its own designer, it is changed on every
    /// page.
    ///
    /// Where it is narrower than designed (the Axis Monitor beside a page) its parts go
    /// on in rows, in their order from left to right (FitTo); as wide or wider, they are
    /// where the designer put them.
    /// </summary>
    [ToolboxItem(false)]
    public class MachineBarHMI : UserControl, IPageBar
    {
        private Dictionary<Control, Rectangle> _designed;
        private Size _designedSize;

        public MachineBarHMI()
        {
            BackColor = Color.White;
        }

        /// <summary>
        /// Its parts in rows when <paramref name="width"/> is narrower than the designer's
        /// layout, where they were designed otherwise; its height follows. Returns the height.
        /// </summary>
        public int FitTo(int width)
        {
            if (_designed == null)
            {
                _designed = Controls.Cast<Control>().ToDictionary(c => c, c => c.Bounds);
                _designedSize = Size;
            }
            int right = _designed.Count == 0 ? 0 : _designed.Values.Max(r => r.Right);
            SuspendLayout();
            try
            {
                if (width >= right || _designed.Count == 0)
                {
                    foreach (var kv in _designed) kv.Key.Bounds = kv.Value;
                    Size = new Size(Math.Max(width, _designedSize.Width), _designedSize.Height);
                    return Height;
                }
                int left = _designed.Values.Min(r => r.Left), top = _designed.Values.Min(r => r.Top);
                int gapX = Math.Max(4, UiScale.For(this, 6)), gapY = Math.Max(4, UiScale.For(this, 6));
                int x = left, y = top, rowH = 0;
                foreach (var kv in _designed.OrderBy(k => k.Value.Top / Math.Max(1, k.Value.Height)).ThenBy(k => k.Value.Left))
                {
                    var r = kv.Value;
                    if (x > left && x + r.Width > width) { x = left; y += rowH + gapY; rowH = 0; }
                    kv.Key.SetBounds(x, y, r.Width, r.Height);
                    x += r.Width + gapX;
                    rowH = Math.Max(rowH, r.Height);
                }
                Size = new Size(width, y + rowH + Math.Max(0, _designedSize.Height - _designed.Values.Max(r => r.Bottom)));
                return Height;
            }
            finally { ResumeLayout(); }
        }
    }
}
