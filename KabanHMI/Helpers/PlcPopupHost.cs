using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Shows the PLC's messages (PlcPopups) over the main window: one per station,
    /// opened when the PLC writes a number to its trigger, closed when it writes 0
    /// (or another number - that one is shown instead), answered through the
    /// machine's response device.
    /// </summary>
    public static class PlcPopupHost
    {
        private static Form _owner;
        private static readonly Dictionary<int, PlcPopupForm> _open = new Dictionary<int, PlcPopupForm>();

        /// <summary>Opens the sign-in (the main window's); null: none.</summary>
        public static Action<IWin32Window> SignIn;

        /// <summary>The message on show for a station (tests).</summary>
        public static PlcPopupForm OpenFor(int station)
        {
            PlcPopupForm f;
            return _open.TryGetValue(station, out f) && !f.IsDisposed ? f : null;
        }

        /// <summary>Listens to the PLC's messages for the main window (once).</summary>
        public static void Attach(Form owner)
        {
            if (owner == null) return;
            bool first = _owner == null;
            _owner = owner;
            owner.FormClosed += (s, e) => { if (_owner == owner) { CloseAll(); _owner = null; } };
            if (first) PlcPopups.Requested += OnRequested;
        }

        private static void OnRequested(int station, int number)
        {
            var owner = _owner;
            if (owner == null || owner.IsDisposed) return;
            Action show = () => Show(station, number);
            if (owner.InvokeRequired) { try { owner.BeginInvoke(show); } catch { } }
            else show();
        }

        private static void CloseAll()
        {
            foreach (var f in new List<PlcPopupForm>(_open.Values)) try { if (!f.IsDisposed) f.Close(); } catch { }
            _open.Clear();
        }

        /// <summary>Shows a station's message (0: closes it).</summary>
        public static void Show(int station, int number)
        {
            PlcPopupForm old;
            if (_open.TryGetValue(station, out old))
            {
                _open.Remove(station);
                try { if (!old.IsDisposed) old.Close(); } catch { }
            }
            if (number == 0 || _owner == null || _owner.IsDisposed) return;

            var popup = PlcPopups.Find(station, number) ?? Unknown(station, number);
            var form = new PlcPopupForm(popup, station, number);
            form.Answered += b => Answer(form, b);
            form.SignInAsked += () => { if (SignIn != null) SignIn(form); };
            _open[station] = form;
            form.Show(_owner);
            form.PlaceOver(_owner);
            form.Activate();
        }

        /// <summary>A number the machine's file does not have: said as it is, with OK.</summary>
        private static PlcPopups.Popup Unknown(int station, int number)
        {
            var d = PlcPopups.DevicesOf(station);
            var p = new PlcPopups.Popup
            {
                Code = number, Icon = "Info", Station = station, ModuleId = d != null ? d.ModuleId : "",
                Title = new Dictionary<string, string> { { "EN", LanguageManager.GetString("Pop_Number", "PLC message") + " " + number } },
                Text = new Dictionary<string, string>
                {
                    { "EN", string.Format(LanguageManager.GetString("Pop_Unknown", "The PLC shows message {0}, which this HMI does not know yet (not in the machine's Popups.json)."), number) }
                }
            };
            p.Buttons.Add(new PlcPopups.Button { Kind = "OK" });
            AuditLogger.Log("PLC message " + number + " on station " + station + " is not in the machine's Popups.json.");
            return p;
        }

        private static void Answer(PlcPopupForm form, PlcPopups.Button b)
        {
            if (!form.CanAnswer) { form.FollowSignIn(); return; }
            string error;
            var driver = PlcManager.Instance.GetDriverForWrite(form.Station);
            bool ok;
            using (WriteSource.For("PLC message " + form.Number + ": " + b.Kind))
                ok = PlcPopups.Answer(form.Station, form.Number, b.Answer, driver, UserRights.Actor, out error);
            if (!ok) { form.ShowError(error); return; }
            PlcPopupForm open;
            if (_open.TryGetValue(form.Station, out open) && open == form) _open.Remove(form.Station);
            form.Close();
        }
    }
}
