using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Plc;

namespace Kaban.UI.Forms
{
    public partial class MainForm : Form
    {
        private UserControl _currentScreen;

        /// <summary>The page on display (the PLC Monitor lists the devices it uses).</summary>
        public Control CurrentPage { get { return _currentScreen; } }
        private readonly System.Collections.Generic.Dictionary<Controls.ButtonHMI, UserControl> _menuButtons
            = new System.Collections.Generic.Dictionary<Controls.ButtonHMI, UserControl>();
        // The role group behind each nav page, so opening a page can populate the
        // second-level MACHINE strip from it (see _machineStrip).
        private readonly System.Collections.Generic.Dictionary<UserControl, Kaban.Core.Machines.MachineRegistry.RoleGroup> _screenGroups
            = new System.Collections.Generic.Dictionary<UserControl, Kaban.Core.Machines.MachineRegistry.RoleGroup>();
        private Screens.MachineSelectorStrip _machineStrip;
        private Panel _navSpacer;
        // Minimum security level required to open each page (SecurityLevel.None = anyone).
        //
        // PER PAGE INSTANCE, not per Type. Every multi-machine page is a
        // SettingsScreen, so keyed by Type they all shared ONE entry - whichever
        // group registered last - and Home could demand Machine Parameters'
        // Maintenance level (the click answered with a permission box and the page
        // "did not load"), or a restricted page could take an open page's level.
        private readonly System.Collections.Generic.Dictionary<UserControl, Kaban.Core.Enums.SecurityLevel> _screenLevels
            = new System.Collections.Generic.Dictionary<UserControl, Kaban.Core.Enums.SecurityLevel>();
        private PlcScanner _scanner;

        // --- Login UI (added programmatically so the .Designer file stays untouched) ---
        private Button _btnLogin;
        private Label _lblUser;

        public MainForm()
        {
            InitializeComponent();
            if (this.DesignMode) return;
            _scanner = new PlcScanner(pnlContent);

            // The hand over everything clickable, in every window - the caption
            // bar, its dropdown menus and the dialogs included (see UiStyle).
            Kaban.UI.Controls.UiStyle.EnableHandCursorsEverywhere();
        }

        public void LoadScreen(UserControl newScreen)
        {
            // Match by INSTANCE, not Type: two nav pages may share a class (e.g. a
            // module yielding two parameterised screens). Type comparison made the
            // second page's button a dead click - it early-returned as "already shown".
            if (_currentScreen != null && ReferenceEquals(_currentScreen, newScreen))
            {
                // REMOVED newScreen.Dispose() here to prevent crash on re-clicking menu buttons
                SyncMachineStrip();
                return;
            }

            // Enforce the page's required security level. A page marked (e.g.)
            // Maintenance must not open for an Operator - previously RequiredLevel
            // was carried on the screen but never checked, which is a false sense
            // of protection.
            Kaban.Core.Enums.SecurityLevel needed;
            if (_screenLevels.TryGetValue(newScreen, out needed)
                && !Kaban.Core.Helpers.SecurityManager.HasAccess(needed))
            {
                string msg = LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:") + " " + needed;
                MessageBox.Show(msg,
                    LanguageManager.GetString("Sys_Disabled", "Action Disabled."),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_currentScreen != null)
            {
                pnlContent.Controls.Remove(_currentScreen);
            }

            _currentScreen = newScreen;
            _currentScreen.Dock = DockStyle.Fill;
            pnlContent.Controls.Add(_currentScreen);

            // The two shared interaction cues - blue border under the pointer, hand
            // that disappears when a control is disabled - are applied HERE, once
            // per page, rather than by each control. The old rule was really
            // "whichever controls remembered to implement it", which is why some
            // turned blue and some did not.
            Kaban.UI.Controls.UiStyle.ApplyInteractionCues(_currentScreen);

            // Highlight the active page's menu button. Match by INSTANCE: two nav
            // entries may host screens of the same Type (two mounted sub-machines),
            // so matching by Type used to mark the wrong button as active.
            foreach (var kv in _menuButtons)
                kv.Key.IsActivePage = ReferenceEquals(kv.Value, newScreen);

            // SECOND-LEVEL MENU: the machines this page can show. It hides itself
            // for a page with only one (HMI Settings, or any page on a machine with
            // no sub-machines), so it never asks a question with a single answer.
            SyncMachineStrip();

            // history: page changes are recorded like browser visits - the page
            // AND the machine/tab it is showing (see RecordLocation).
            RecordLocation();

            // Re-layout every function unit on the freshly shown screen. Re-adding a
            // screen fires no resize/parent event on its children, so without this
            // they keep the bounds they had while detached.
            RelayoutFunctionUnits(_currentScreen);

            // TRIGGER THE SCANNER: Only scan the screen we just loaded!
            _scanner.RefreshTags(true);
            UpdateAlarmPulse();       // opening the alarm page stops the pulse
        }

        /// <summary>
        /// Shows the machine strip for the current page with the machine that page
        /// is ACTUALLY showing marked - not always the first one.
        /// </summary>
        private void SyncMachineStrip()
        {
            if (_machineStrip == null || _currentScreen == null) return;
            Kaban.Core.Machines.MachineRegistry.RoleGroup grp;
            _screenGroups.TryGetValue(_currentScreen, out grp);
            var tabbed = _currentScreen as Kaban.UI.Settings.SettingsScreen;
            int index = tabbed != null ? Math.Max(0, tabbed.ActiveIndex) : 0;
            _machineStrip.ShowFor(grp, index);
            _machineStrip.ApplyCollapsed(_navCollapsed);
        }

        // ==================================================================
        // BACK / FORWARD
        // ------------------------------------------------------------------
        // A history step is a LOCATION: the page in the content area plus the tab
        // chosen on every tab strip inside it (the machine on a multi-machine
        // page, the sub-page on HMI Settings). What went wrong before:
        //  * a step held only the page, and showing the page already on screen
        //    returns at once - so Back from "Home / H500" to "Home / FA2010" did
        //    nothing;
        //  * a tab step re-found its window through FindForm(), which is null once
        //    that page has been navigated away from - Back re-activated a tab
        //    nobody could see;
        //  * returning to a page left the machine strip marking the main machine
        //    whatever machine the page was showing.
        // ==================================================================

        /// <summary>Records where the operator is now as one Back/Forward step.</summary>
        public void RecordLocation()
        {
            var screen = _currentScreen;
            if (screen == null || Helpers.NavigationHistory.IsReplaying) return;

            var chain = Kaban.UI.Settings.SettingsScreen.CaptureChain(screen);
            var key = new System.Collections.Generic.List<object> { screen };
            foreach (var step in chain) { key.Add(step.Key); key.Add(step.Value); }

            Helpers.NavigationHistory.Record(() => RestoreLocation(screen, chain), key.ToArray());
        }

        private void RestoreLocation(UserControl screen,
            System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Kaban.UI.Settings.SettingsScreen, Kaban.UI.Settings.ISettingsPage>> chain)
        {
            if (screen == null || screen.IsDisposed) return;
            LoadScreen(screen);
            if (!ReferenceEquals(_currentScreen, screen)) return;     // refused: permission
            foreach (var step in chain)
                if (step.Key != null && !step.Key.IsDisposed) step.Key.ShowPage(step.Value);
            SyncMachineStrip();
        }

        /// <summary>
        /// Opens the page a device is used on - the machine, the tab on every tab
        /// strip on the way down - and returns the control that uses it, on show,
        /// for the PLC Monitor's Find to point at. Null, with the reason, when the
        /// page cannot be opened (the level of the user) or no longer has it.
        /// </summary>
        public Control ShowDeviceUse(Helpers.DeviceCollector.Use use, out string why)
        {
            why = "";
            if (use == null || use.Screen == null)
            {
                why = LanguageManager.GetString("Msg_FindNoPage", "It is not on a page (an alarm definition).");
                return null;
            }

            // A machine's SIDE PAGE opens in the side panel, beside the page on display.
            if (string.Equals(use.Screen.Role ?? "", Kaban.Core.Machines.ScreenRoles.SidePage, StringComparison.OrdinalIgnoreCase))
            {
                var side = System.Linq.Enumerable.FirstOrDefault(Helpers.SidePages.All(), p => ReferenceEquals(p.Screen, use.Screen));
                if (side == null) { why = LanguageManager.GetString("Msg_FindNoScreen", "That page is not in the menu."); return null; }
                if (!SecurityManager.HasAccess(side.RequiredLevel)) { why = LanguageManager.GetString("Msg_FindNoAccess", "Your level may not open that page."); return null; }
                var sideView = ShowSidePage(side.Key);
                var found = sideView == null ? null : Helpers.DeviceCollector.Locate(sideView, use.ControlPath);
                if (found == null) why = LanguageManager.GetString("Msg_FindGone", "The page has changed - find again.");
                return found;
            }

            UserControl nav = null;
            Kaban.Core.Machines.MachineRegistry.RoleGroup group = null;
            int machine = 0;
            foreach (var kv in _screenGroups)
            {
                int i = kv.Value.Pages.FindIndex(p => ReferenceEquals(p.Value, use.Screen));
                if (i >= 0) { nav = kv.Key; group = kv.Value; machine = i; break; }
            }
            if (nav == null) { why = LanguageManager.GetString("Msg_FindNoScreen", "That page is not in the menu."); return null; }

            LoadScreen(nav);
            if (!ReferenceEquals(_currentScreen, nav)) { why = LanguageManager.GetString("Msg_FindNoAccess", "Your level may not open that page."); return null; }

            Control view = nav;
            var steps = new System.Collections.Generic.List<int>();
            if (group.IsTabbed) steps.Add(machine);          // the machine's tab of a shared page
            steps.AddRange(use.PagePath);
            foreach (int i in steps)
            {
                var tabs = view as Kaban.UI.Settings.SettingsScreen;
                if (tabs == null || i < 0 || i >= tabs.AllPages.Count) { why = LanguageManager.GetString("Msg_FindGone", "The page has changed - find again."); return null; }
                var page = tabs.AllPages[i];
                if (!SecurityManager.HasAccess(page.RequiredLevel)) { why = LanguageManager.GetString("Msg_FindNoAccess", "Your level may not open that page."); return null; }
                tabs.ShowPage(page);
                view = tabs.ViewOf(page);
            }
            SyncMachineStrip();
            RecordLocation();

            var target = Helpers.DeviceCollector.Locate(view, use.ControlPath);
            if (target == null) { why = LanguageManager.GetString("Msg_FindGone", "The page has changed - find again."); return null; }

            // Scrolled into view when the page scrolls.
            for (Control p = target.Parent; p != null && p != pnlContent; p = p.Parent)
            {
                var sc = p as ScrollableControl;
                if (sc != null && sc.AutoScroll) { sc.ScrollControlIntoView(target); break; }
            }
            RelayoutFunctionUnits(_currentScreen);
            Kaban.UI.Plc.PlcScanner.NotifyContentChanged();
            return target;
        }

        private static void RelayoutFunctionUnits(Control root)
        {
            if (root == null) return;
            foreach (Control c in root.Controls)
            {
                if (c is Controls.FunctionUnitHMI unit) unit.Relayout();
                if (c.HasChildren) RelayoutFunctionUnits(c);
            }
        }

        /// <summary>
        /// Builds the whole menu from the mounted machine modules: the MAIN
        /// machine's pages first, then each mounted SUB machine's pages. This is
        /// what makes a sub-machine (a screwdriving unit, a connection robot, ...) appear in
        /// the host machine's navigation and behave exactly like its own pages.
        /// Screens are created lazily via the module's factory.
        /// </summary>
        public void BuildNavigation()
        {
            UserControl first = null;

            // Screens are grouped by ROLE: every machine's "Home" becomes tabs of a
            // single Home button, and so on. That is what makes the left bar look
            // the same on every machine - Home / Manual Movement / Alarms /
            // Settings - with sub-machines appearing as TABS rather than extra
            // buttons. A page with no role (FA2010's Profiles) keeps its own button.
            foreach (var group in Kaban.Core.Machines.MachineRegistry.NavigationGroups())
            {
                var view = Screens.MachineTabScreen.Build(group);
                if (view == null) continue;

                // Every nav row gets an icon: pages that declare none fall back to a
                // generic one so the menu keeps a single "icon + label" format.
                var navIcon = group.Icon?.Invoke() as Image ?? SafeIcon("chevron_right_24");
                AddMenuButton(group.TitleKey, group.TitleFallback, navIcon, view);
                _screenLevels[view] = group.RequiredLevel;
                _screenGroups[view] = group;

                // A multi-machine page tells the shell whenever its machine changes
                // (strip click, Back/Forward, login rebuild), so the strip's mark
                // follows the machine actually shown.
                var tabbed = view as Kaban.UI.Settings.SettingsScreen;
                if (tabbed != null)
                    tabbed.ActivePageChanged += (s2, e2) =>
                    {
                        if (ReferenceEquals(s2, _currentScreen)) SyncMachineStrip();
                    };

                // Auto-select the first page the current user may actually open, so
                // startup doesn't pop a permission warning for a restricted page.
                if (first == null && Kaban.Core.Helpers.SecurityManager.HasAccess(group.RequiredLevel))
                    first = view;
            }

            // A nav button whose group has no page THIS user may open is hidden, not
            // shown-and-refused: Machine Parameters is Maintenance-only, so an
            // operator was offered a button that could only ever answer with a
            // permission warning. Re-evaluated on every login/logout.
            SecurityManager.UserChanged -= OnUserChangedRefreshNav;
            SecurityManager.UserChanged += OnUserChangedRefreshNav;
            RefreshNavVisibility();

            // The machine strip flows AFTER every page button, so it always lands
            // below them - add order is the whole guarantee here.
            // DOCK ORDER. WinForms docks the HIGHEST child index first, so the
            // highest index ends up closest to the top edge. Page buttons were added
            // above; giving the spacer and then the strip the LOWEST indices puts
            // them below every one of those buttons, without touching a single
            // button's own position, spacing or order.
            EnsureMachineStrip();
            if (!pnlMenu.Controls.Contains(_machineStrip)) pnlMenu.Controls.Add(_machineStrip);
            if (!pnlMenu.Controls.Contains(_navSpacer)) pnlMenu.Controls.Add(_navSpacer);

            pnlMenu.Controls.SetChildIndex(_machineStrip, 0);   // docks last -> lowest
            pnlMenu.Controls.SetChildIndex(_navSpacer, 1);      // the gap, just above it

            if (first != null) LoadScreen(first);
        }

        /// <summary>
        /// Keeps the machine list at a FIXED height on the bar by padding out the
        /// space the hidden page buttons would have filled.
        ///
        /// Without this the list slid up and down as buttons appeared and
        /// disappeared with the logged-in user, which is exactly the "it moves with
        /// user entries" complaint - the position of a menu should not be a function
        /// of who is signed in.
        /// </summary>
        /// <summary>
        /// Creates the machine strip and the spacer that reserves its place, if they
        /// do not exist yet.
        ///
        /// Called by BOTH SetupNavToggle and BuildNavigation because neither may
        /// assume the other ran first: MainForm's constructor does its setup inside a
        /// try/catch, so an unrelated startup failure (a missing PlcLabels.csv, a PLC
        /// that will not open) could leave these null while BuildNavigation - called
        /// afterwards from Program.cs - carried on and threw.
        /// </summary>
        private void EnsureMachineStrip()
        {
            if (_navSpacer == null)
            {
                // Reserves the gap under the page buttons, and absorbs the height of
                // any button hidden for the current user, so the machine list sits at
                // the SAME y whoever is logged in.
                _navSpacer = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = MachineStripGap,
                    BackColor = Color.Transparent
                };
            }

            if (_machineStrip == null)
            {
                _machineStrip = new Screens.MachineSelectorStrip();
                _machineStrip.MachineSelected += (s, index) =>
                {
                    // The page for a multi-machine role IS a SettingsScreen holding
                    // one tab per machine; the strip just drives it from outside.
                    var screen = _currentScreen as Kaban.UI.Settings.SettingsScreen;
                    if (screen != null) screen.ActivateByIndex(index);
                };
            }
        }

        /// <summary>Clear space between the page buttons and the machine list.</summary>
        private const int MachineStripGap = 3 * 45;   // about three button heights

        /// <summary>Height of one nav button, for the spacer's reservation maths.</summary>
        private const int NavButtonHeight = 45;

        private void OnUserChangedRefreshNav(object sender, EventArgs e)
        {
            RefreshNavVisibility();

            // The open page may have just become forbidden, or the machine strip may
            // now have more (or fewer) machines this user can see.
            if (_currentScreen != null) SyncMachineStrip();
            SizeNavSpacer();
        }

        /// <summary>
        /// Shows only the nav buttons that lead somewhere for the CURRENT user. A
        /// group whose every page is above the user's level has nothing to display,
        /// so its button is hidden rather than left to refuse the click.
        /// </summary>
        private void RefreshNavVisibility()
        {
            foreach (var kv in _menuButtons)
            {
                Kaban.Core.Machines.MachineRegistry.RoleGroup grp;
                if (!_screenGroups.TryGetValue(kv.Value, out grp) || grp == null) continue;

                bool anyOpenable = false;
                foreach (var page in grp.Pages)
                    if (SecurityManager.HasAccess(page.Value.RequiredLevel)) { anyOpenable = true; break; }

                kv.Key.Visible = anyOpenable && SecurityManager.HasAccess(grp.RequiredLevel);
            }
            SizeNavSpacer();
        }

        private void SizeNavSpacer()
        {
            if (_navSpacer == null) return;
            int hidden = 0;
            foreach (var kv in _menuButtons)
                if (!kv.Key.Visible) hidden++;
            _navSpacer.Height = Kaban.UI.Controls.UiScale.For(_navSpacer, MachineStripGap + hidden * NavButtonHeight);
        }

        public void AddMenuButton(string translationKey, string defaultText, Image icon, UserControl screenToLoad)
        {
            var btn = new Controls.ButtonHMI();
            btn.Text = defaultText;
            btn.FallbackText = defaultText;      // survives collapse + language switches
            btn.TranslationKey = translationKey;

            // ONE format for every nav button. Buttons without an icon used the
            // default centred text while icon buttons were left-aligned, which made
            // the menu look like two different designs. Now text always starts at
            // the same x; a missing icon simply leaves the icon slot empty.
            btn.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            btn.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            if (icon != null)
            {
                btn.Image = icon;
                btn.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
                btn.Padding = new System.Windows.Forms.Padding(12, 0, 4, 0);
            }
            else
            {
                // 12 (pad) + 24 (icon) + image/text gap -> text lines up with icon rows
                btn.Padding = new System.Windows.Forms.Padding(42, 0, 4, 0);
            }

            btn.Dock = System.Windows.Forms.DockStyle.Top;
            btn.Height = 45;
            btn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            // Explorer-style rows (per feedback: the white "merged" look did not
            // work): square at the panel edge, SMALL rounding toward the content,
            // and the ACTIVE page is a DARKER row instead of white.
            btn.CornerRadius = 4;
            btn.RoundLeftOnly = false;
            btn.RoundRightOnly = true;
            btn.ActiveBackColor = System.Drawing.Color.FromArgb(209, 214, 221);
            // outlined when active, so the row reads as a button rather than a
            // block of colour - matching the shaded tab levels
            btn.RestingBorderColor = System.Drawing.Color.FromArgb(150, 158, 170);
            btn.Margin = new System.Windows.Forms.Padding(6, 4, 0, 4); // no right margin -> reaches form edge
            btn.Cursor = System.Windows.Forms.Cursors.Hand;

            btn.Click += (s, e) => LoadScreen(screenToLoad);

            _menuButtons[btn] = screenToLoad;
            pnlMenu.Controls.Add(btn);
            btn.ApplyTranslation();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            // HELP LINKS, discovered once the window has painted: every machine page
            // is built off-screen, its units' error bits collected, then disposed.
            // After Shown rather than here so the operator sees the HMI at once;
            // an alarm raised in that moment simply gains its unit's help when the
            // discovery finishes. The time taken is written to the audit log.
            this.Shown += (s2, e2) => BeginInvoke((Action)(() =>
            {
                try { Kaban.UI.Helpers.AlarmHelpLinks.StartDiscovery(); }   // idle-time steps: the login is ready at once
                catch (Exception ex) { AuditLogger.Log("help discovery failed: " + ex.Message); }
            }));
            Kaban.UI.Settings.LogSettingsStore.Apply();   // persisted log options
            Kaban.Core.Helpers.HmiLayout.Load();
            Kaban.Core.Helpers.HmiLayout.DetectFrom(Screen.FromControl(this).WorkingArea.Height);
            FormClosing += MainForm_FormClosing;

            // Offer the login straight away - most shifts start by signing in. It is
            // a normal dialog, so an operator who just wants to watch the machine can
            // close it and carry on unauthenticated.
            BeginInvoke(new Action(() =>
            {
                if (SecurityManager.CurrentLevel != SecurityLevel.None) return;
                // Restarted for a user's magnifying factor: that user, signed in again.
                if (SignInFromHandoff()) return;
                using (var login = new LoginForm()) login.ShowDialog(this);
                OfferUserMagnifier();
            }));

            try
            {
                if (!PlcManager.Instance.Connect(1))
                {
                    // No popup: a PLC that is not up yet is a NORMAL launch state.
                    // The comm-lost banner shows and the scanner retries every ~2s.
                    AuditLogger.Log("PLC station 1 not connected at startup; scanner will retry.");
                }

                LoadUnit("Unit_Length", UnitType.Length);
                LoadUnit("Unit_Weight", UnitType.Weight);
                LoadUnit("Unit_Temperature", UnitType.Temperature);

                // Starts LOGGED OUT (SecurityLevel.None); the operator signs in via the
                // Login button. Never auto-login: every audit-log entry and every
                // permission check would otherwise name a user who was not there.
                SecurityManager.Logout();
                SetupLoginUi();
                SetupSimMonitorButton();
                SetupNavToggle();
                SetupSidePanel();

                // Color consistency: make the left menu panel the SAME color as the
                // top panel, so the white content area is the only distinct surface.
                // Lighter than the old 243: the bar sat close enough to the white
                // page that the two greys read as a smudge rather than two surfaces,
                // and an INACTIVE nav row (ColorPassive, 235) barely showed against
                // it at all. 249 keeps the bar distinct from white while letting the
                // rows read as objects on it.
                pnlMenu.BackColor = Color.FromArgb(249, 249, 250);

                // The panels are DOCKED (Top / Left / Fill), so the menu's right edge
                // already meets the content area exactly - no manual gap-closing is
                // needed, and the layout now follows the window when it is resized
                // or maximised. Only the menu's fixed width is set here.
                pnlMenu.Width = MenuWidthFull;

                // Keep the login / language / unit-settings block pinned to the
                // right edge as the window grows.
                pnlTop.Resize += (s2, e2) => LayoutTopBar();
                LayoutTopBar();

                // TWO-LAYER translations: a plant-wide COMMON file plus a
                // MACHINE-specific file (checked first). Put CommonTranslations.csv
                // next to the exe and Translations.csv in the machine folder; keys
                // are resolved machine-first, then common, then shown literally.
                string commonCsv = Path.Combine(Application.StartupPath, "CommonTranslations.csv");
                string machineCsv = Path.Combine(Application.StartupPath, "Translations.csv");
                // COMMON layer: prefer the file on disk (editable in the field),
                // otherwise fall back to the copy embedded in Kaban.UI. Because the
                // shared file lives in the UI library, every machine gets the same
                // common keys without repeating them in its own Translations.csv.
                LanguageManager.LoadCommonTranslations(commonCsv);
                if (!LanguageManager.HasCommonTranslations())
                    LoadEmbeddedCommonTranslations();

                // ADD, never LOAD (clear-then-load), here: MountMachines() already
                // ran, and for a compound machine it has ALREADY merged the main
                // machine's own file with every mounted sub-machine's via this same
                // ADD call (see FA2010Module.RegisterData / H500Module.RegisterData).
                // A clearing reload here would wipe that merge back down to just
                // this one file - which is exactly what "Screwing_Holing" turning
                // into the raw key "Machine_H500" traced back to: it survived until
                // whatever first exercised this path (e.g. building a tab strip for
                // the first time on login) forced a fresh lookup against the
                // now-single-file dictionary.
                LanguageManager.AddMachineTranslations(machineCsv);

                // Belt and braces: re-apply alarm texts even though nothing above
                // clears them anymore, in case a future change reintroduces a
                // clearing reload somewhere in this chain.
                AlarmDefinitionLoader.ReapplyTranslations();

                // Visible PLC communication status. Without it a comm failure renders as
                // a screen full of zeros with nothing to say anything is wrong.
                _scanner.CommunicationStateChanged += (s2, e2) => UpdateCommIndicator();
                SetupCommIndicator();

                cmbLanguage.Items.Clear();
                foreach (var lang in LanguageManager.AvailableLanguages)
                {
                    cmbLanguage.Items.Add(lang);
                }

                string savedLang = "";
                try { savedLang = (string)Properties.Settings.Default["AppLanguage"]; } catch { }

                if (!string.IsNullOrEmpty(savedLang) && cmbLanguage.Items.Contains(savedLang))
                {
                    cmbLanguage.SelectedItem = savedLang;
                }
                else if (cmbLanguage.Items.Count > 0)
                {
                    cmbLanguage.SelectedItem = LanguageManager.CurrentLanguage;
                }

                cmbLanguage.SelectedIndexChanged += CmbLanguage_SelectedIndexChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup Error: " + ex.Message);
            }

            // THE MAGNIFIER: the whole window - caption, menu, side bar, the page on
            // display - at the factor this run started with, once everything above
            // is built; pages opened later are scaled as they are added (UiScale).
            try { Kaban.UI.Controls.UiScale.Apply(this); }
            catch (Exception ex) { AuditLogger.Log("magnifier: " + ex.Message); }
        }

        // ==================================================================
        // PLC COMMUNICATION INDICATOR
        // ==================================================================
        private Label _lblComm;

        private void SetupCommIndicator()
        {
            _lblComm = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 28,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Visible = false,
                BackColor = Color.Firebrick,
                ForeColor = Color.White,
                Text = "PLC COMMUNICATION LOST - displayed values are the last known good data"
            };
            this.Controls.Add(_lblComm);
            _lblComm.BringToFront();
        }

        private void UpdateCommIndicator()
        {
            if (_lblComm == null) return;
            if (_lblComm.InvokeRequired)
            {
                _lblComm.BeginInvoke((Action)UpdateCommIndicator);
                return;
            }
            _lblComm.Visible = !_scanner.CommunicationHealthy;
        }

        // ==================================================================
        // LOGIN / LOGOUT (replaces the old hardcoded "Dev" auto-login)
        // ==================================================================

        /// <summary>
        /// Right-aligns the top-bar block (user label, Login, language combo and
        /// Unit Settings) against the CURRENT panel width. Anchors alone were not
        /// enough here: the controls were originally positioned at absolute x=1130
        /// and the panel never resized, so on a wider or maximised window they were
        /// stranded in the middle. This recomputes their positions on every resize.
        /// </summary>
        private void LayoutTopBar()
        {
            if (pnlTop == null || pnlTop.Width <= 0) return;

            if (btnUnitSettings != null) btnUnitSettings.Visible = false;   // lives in Settings
            if (_lblUser != null) _lblUser.Visible = false;                 // user shown in the TITLE now

            const int gap = 18;      // touch screens need room between these buttons
            int midY = pnlTop.Height / 2;
            int margin = UseCustomCaption ? 0 : 12;
            int right = pnlTop.Width - margin;

            // window buttons occupy the far right of the caption
            foreach (var wb in new[] { _btnClose, _btnMax, _btnMin })
            {
                if (wb == null) continue;
                wb.Top = midY - wb.Height / 2;
                wb.Left = right - wb.Width;
                right = wb.Left;
            }
            if (_btnClose != null) right -= gap;          // small break before the app controls

            int titleLeft = 10;
            if (_picLogo != null)
            {
                _picLogo.Height = Math.Max(18, pnlTop.Height - 8);
                _picLogo.Width = _picLogo.Height * 4;
                _picLogo.Top = midY - _picLogo.Height / 2;
                _picLogo.Left = 8;
                titleLeft = _picLogo.Right + 8;
            }
            if (_lblTitle != null)
            {
                _lblTitle.Text = this.Text;
                _lblTitle.Top = midY - _lblTitle.Height / 2;
                _lblTitle.Left = titleLeft;
            }

            // right-most: language selector
            if (_btnLang != null)
            {
                _btnLang.Top = midY - _btnLang.Height / 2;
                _btnLang.Left = right - _btnLang.Width;
                right = _btnLang.Left - gap;
            }
            // login / logout
            if (_btnLogin != null)
            {
                _btnLogin.Top = midY - _btnLogin.Height / 2;
                _btnLogin.Left = right - _btnLogin.Width;
                right = _btnLogin.Left - gap;
            }
            // alarm RESET - appears only while the machine-error bit is set
            if (_btnAlarmReset != null && _machineError)
            {
                _btnAlarmReset.Top = midY - _btnAlarmReset.Height / 2;
                _btnAlarmReset.Left = right - _btnAlarmReset.Width;
                right = _btnAlarmReset.Left - gap;
            }

            // connection warning - sits beside the alarm bell, and only while a
            // controller is actually down
            if (_btnConnection != null && _btnConnection.Visible)
            {
                _btnConnection.Top = midY - _btnConnection.Height / 2;
                _btnConnection.Left = right - _btnConnection.Width;
                right = _btnConnection.Left - gap;
            }

            // (the simulation monitor now lives in the user menu, not on the caption)
            if (_btnSimMonitor != null) _btnSimMonitor.Visible = false;
        }

        private Button _btnSimMonitor;
        private Button _btnLang;
        private Timer _loginBlink;
        private Image _icoLogin, _icoLogout;
        private Color _btnLoginTint = Color.White;
        private Label _lblTitle;
        private PictureBox _picLogo;
        private Image _logo;

        /// <summary>Loads an image embedded in this assembly (null when absent).</summary>
        private static Image LoadEmbeddedImage(string name)
        {
            try
            {
                using (var st = typeof(MainForm).Assembly.GetManifestResourceStream(name))
                {
                    if (st == null) return null;
                    // A COPY. GDI+ keeps reading the stream an Image was loaded
                    // from for as long as the Image lives; this returned an Image
                    // whose stream had already been disposed, which works until the
                    // day it throws "A generic error occurred in GDI+" mid-paint.
                    using (var loaded = Image.FromStream(st))
                        return new Bitmap(loaded);
                }
            }
            catch { return null; }
        }

        private static Icon LoadEmbeddedIcon(string name)
        {
            try
            {
                using (var st = typeof(MainForm).Assembly.GetManifestResourceStream(name))
                    return st == null ? null : new Icon(st);
            }
            catch { return null; }
        }
        private Button _btnMin, _btnMax, _btnClose;

        /// <summary>
        /// Draw our own window caption so the login/language controls can sit next
        /// to the minimise/maximise/close buttons and the separate top strip
        /// disappears - which gives the machine screens the full window height.
        /// Set to false to fall back to the standard Windows frame.
        /// </summary>
        private const bool UseCustomCaption = true;
        private bool _loginBlinkOn;
        // The main window's OWN tooltip - one per window, the normal WinForms
        // pattern - so nothing that happens to pages can reach the login bar's tips.
        private readonly ToolTip _topTips = Kaban.UI.Controls.UiStyle.CreateTips();

        private void SetupLoginUi()
        {
            _lblUser = new Label { AutoSize = true, Visible = false };   // retired: user is in the title

            _btnLogin = new Button
            {
                Text = "",                          // icon is DRAWN below (emoji looked poor)
                Width = 44,
                Height = 26,
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F),
                ForeColor = Color.White,
                BackColor = Color.Black          // the caption is black: no white plate
            };
            _btnLogin.FlatAppearance.BorderSize = 0;
            _btnLogin.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
            _btnLogin.FlatAppearance.MouseDownBackColor = Color.FromArgb(64, 64, 64);
            // The supplied art is a BLACK glyph on transparency, which disappears on
            // the black caption bar. Recolour to white (keeping the anti-aliased
            // alpha) and pre-scale once at 2x the drawn size, so the caption icon is
            // as crisp as the painted language flag.
            using (var art = LoadEmbeddedImage("Kaban.UI.IconLogin.png")) _icoLogin = Whiten(art, 56);
            using (var art = LoadEmbeddedImage("Kaban.UI.IconLogout.png")) _icoLogout = Whiten(art, 56);
            _btnLogin.Paint += (s, e) =>
            {
                bool signedIn = SecurityManager.CurrentLevel != SecurityLevel.None;
                var art = signedIn ? _icoLogout : _icoLogin;
                if (art != null)
                {
                    // supplied artwork, centred; falls back to the drawn glyph if the
                    // resource is ever missing
                    int side = Math.Min(_btnLogin.Height - 4, 28);
                    var box = new Rectangle((_btnLogin.Width - side) / 2, (_btnLogin.Height - side) / 2, side, side);
                    e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    if (_btnLoginTint == Color.White) e.Graphics.DrawImage(art, box);
                    else e.Graphics.DrawImage(TintedCached(art, _btnLoginTint), box);
                }
                else DrawLoginIcon(e.Graphics, _btnLogin.ClientRectangle, Color.White, signedIn);
            };
            _btnLogin.Click += (s, e) => ToggleLogin();

            // A COMBOBOX, not a toggle: the HMI will ship with many languages, and
            // cycling through them one click at a time does not scale.
            // A BUTTON that opens a language menu: a DropDownList always paints the
            // system arrow, and the flag + code already say what it does.
            if (cmbLanguage != null) cmbLanguage.Visible = false;      // wiring kept, chrome hidden
            _btnLang = new Button
            {
                Width = 92,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Black,
                TextAlign = ContentAlignment.MiddleRight,
                Text = "",
                Cursor = Cursors.Hand
            };
            _btnLang.FlatAppearance.BorderSize = 0;
            _btnLang.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
            _btnLang.Paint += (s, e) =>
            {
                // Sizes at factor 1, drawn at the magnifying factor (UiScale.Px).
                Func<int, int> px = Kaban.UI.Controls.UiScale.Px;
                var flag = new Rectangle(px(6), (_btnLang.Height - px(21)) / 2, px(30), px(21));   // 50% larger
                DrawFlag(e.Graphics, flag, LanguageManager.CurrentLanguage);
                TextRenderer.DrawText(e.Graphics, LanguageManager.CurrentLanguage.ToUpperInvariant(), _btnLang.Font,
                    new Rectangle(flag.Right + px(5), 0, _btnLang.Width - flag.Right - px(20), _btnLang.Height),
                    Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
                // caret so it still reads as a chooser without the system combo arrow
                using (var caret = new SolidBrush(Color.White))
                    e.Graphics.FillPolygon(caret, new[]
                    {
                        new PointF(_btnLang.Width - px(14), _btnLang.Height / 2f - px(2)),
                        new PointF(_btnLang.Width - px(6),  _btnLang.Height / 2f - px(2)),
                        new PointF(_btnLang.Width - px(10), _btnLang.Height / 2f + px(3))
                    });
            };
            _btnLang.Click += (s, e) => ShowLanguageMenu();
            _topTips.SetToolTip(_btnLang, LanguageManager.GetString("Ttl_Language", "Language / Dil"));
            pnlTop.Controls.Add(_btnLang);

            if (UseCustomCaption) SetupCaptionBar();
            SetupAlarmIndicator();
            SetupConnectionIndicator();

            // THE CONNECTION POLL, on the UI thread. A Forms timer rather than the
            // thread-pool timer it replaces: the pool timer called MX Component's
            // COM object from a thread that does not own it. The Mitsubishi driver
            // now carries each call to its own thread and waits a bounded time, so
            // polling from here cannot freeze the screen either.
            // Simulated stations move when their buttons are pressed, so pages can
            // be tried without a PLC (see SimulationMotion).
            Kaban.UI.Helpers.SimulationMotion.Start();

            // A FIELD, so closing can stop it: the beat RECONNECTS any station it
            // finds closed, and a tick after DisconnectAll() re-opened a station on
            // the way out - which the restarting instance then could not open.
            _connPoll = new Timer { Interval = ConnectionMonitor.BeatMs };
            _connPoll.Tick += (s, e) =>
            {
                try { ConnectionMonitor.Beat(); }
                catch (Exception ex) { AuditLogger.Log("connection poll: " + ex.Message); }
            };
            _connPoll.Start();

            pnlTop.Controls.Add(_lblUser);
            pnlTop.Controls.Add(_btnLogin);

            // Gentle attention pulse while nobody is signed in; it stops the moment
            // a user logs in so it never nags during production.
            _loginBlink = new Timer { Interval = 300 };   // brisk, hard to miss
            _loginBlink.Tick += (s, e) =>
            {
                if (SecurityManager.CurrentLevel != SecurityLevel.None) { _loginBlink.Stop(); return; }
                _loginBlinkOn = !_loginBlinkOn;
                // pulse the ICON, not the background - a coloured plate on the black
                // caption looked like a stray white button
                _btnLoginTint = _loginBlinkOn ? Color.FromArgb(120, 190, 255) : Color.White;
                _btnLogin.Invalidate();
            };
            _loginBlink.Start();

            SecurityManager.UserChanged += (s, e) => UpdateLoginUi();
            UpdateLoginUi();
        }

        private void ToggleLogin()
        {
            if (SecurityManager.CurrentLevel == SecurityLevel.None)
            {
                using (var login = new LoginForm())
                {
                    login.ShowDialog(this);
                }
                OfferUserMagnifier();
            }
            else
            {
                // A stray tap must not end a shift, so offer the two real choices in
                // a dropdown; clicking elsewhere simply dismisses it.
                var menu = BuildLogoutMenu();
                Kaban.UI.Controls.UiStyle.ShowPopup(menu, _btnLogin, new Point(0, _btnLogin.Height));
            }
        }

        /// <summary>
        /// Turns the top strip INTO the window caption: title on the left, then the
        /// app controls and finally the standard window buttons on the right.
        /// </summary>
        private void SetupCaptionBar()
        {
            FormBorderStyle = FormBorderStyle.None;
            FitMaximizedBounds();                                     // respect the taskbar
            WindowState = FormWindowState.Maximized;                  // HMIs run full screen
            // Moved to another monitor while restored: the next maximise - the
            // button, a double-click on the caption, Win+Up - fills THAT monitor.
            Move += (s, e) => { if (WindowState == FormWindowState.Normal) FitMaximizedBounds(); };
            // 31, not 36. The bar carried more clear space above and below its
            // controls than it needed; on a panel that space is screen the machine
            // pages could be using. The controls below are sized from this, so they
            // shrink with it rather than being clipped by it.
            pnlTop.Height = 31;
            pnlTop.BackColor = Color.Black;                           // company caption bar

            // company mark, embedded so it travels with the assembly
            _logo = LoadEmbeddedImage("Kaban.UI.KabanLogo.png");
            var ico = LoadEmbeddedIcon("Kaban.UI.KabanLogo.ico");
            if (ico != null) this.Icon = ico;                          // window + taskbar
            if (_logo != null)
            {
                _picLogo = new PictureBox
                {
                    Image = _logo,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    // Sized to the caption bar rather than a 24px icon - the mark
                    // is a wide wordmark, so at icon size it was unreadable. Zoom
                    // keeps the aspect, so only the height needs stating.
                    Height = pnlTop.Height - 8,
                    Width = (pnlTop.Height - 8) * 4,
                    BackColor = Color.Transparent
                };
                pnlTop.Controls.Add(_picLogo);
            }

            _lblTitle = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Text = this.Text
            };
            pnlTop.Controls.Add(_lblTitle);

            Func<string, string, EventHandler, Button> win = (glyph, tip, click) =>
            {
                var b = new Button
                {
                    Text = glyph,
                    Width = 44,
                    Height = 26,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9F),
                    TabStop = false,
                    Cursor = Cursors.Hand,      // like every other caption button
                    BackColor = Color.Black,
                    ForeColor = Color.White
                };
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
                b.Click += click;
                _topTips.SetToolTip(b, tip);
                pnlTop.Controls.Add(b);
                return b;
            };
            _btnMin = win("\u2500", "Minimize", (s, e) => WindowState = FormWindowState.Minimized);
            _btnMax = win("\u2610", "Maximize / Restore", (s, e) => ToggleMaximize());
            // Tooltip repeats the menu heading, as on the other caption buttons.
            _btnClose = win("\u2715", LanguageManager.GetString("Ttl_ProgramExit", "Program Exit"),
                            (s, e) => ShowCloseMenu());
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);

            // drag the caption to move the window; double-click to maximise
            MouseEventHandler drag = (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;
                // Detect the double-click OURSELVES: handing the mouse to Windows for
                // the drag (WM_NCLBUTTONDOWN) consumes the second click, so the
                // DoubleClick event never arrived and the caption could not maximise.
                if (e.Clicks >= 2) { ToggleMaximize(); return; }
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            };
            pnlTop.MouseDown += drag;
            _lblTitle.MouseDown += drag;
            pnlTop.DoubleClick += (s, e) => ToggleMaximize();
            _lblTitle.DoubleClick += (s, e) => ToggleMaximize();
        }

        private bool _closeConfirmed;

        /// <summary>
        /// Close confirmation as a DROPDOWN from the close button, in the same spirit
        /// as the logout menu: one entry to confirm, and clicking anywhere else simply
        /// cancels - so no cancel entry is needed. Its whole purpose is to make an
        /// accidental tap harmless by requiring a second, deliberate click.
        /// </summary>
        internal ContextMenuStrip BuildCloseMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = Kaban.UI.Controls.UiStyle.MenuFont };
            menu.Renderer = new Kaban.UI.Controls.HmiMenuRenderer();
            menu.ImageScalingSize = new Size(22, 22);

            // A heading, like the other three. This menu had none, so it was the
            // one that looked different when you moved along the caption bar.
            menu.Items.Add(new ToolStripLabel(LanguageManager.GetString("Ttl_ProgramExit", "Program Exit"))
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            });
            menu.Items.Add(new ToolStripSeparator());

            // "Close" and "Restart" - the heading already says this is about
            // leaving the program, so repeating it in every option is words the
            // operator reads past.
            var item = new ToolStripMenuItem(LanguageManager.GetString("Btn_Close", "Close"))
            {
                // BLACK TEXT, red icon. Red lettering made Close look like a
                // warning among three black entries, when it is simply the option
                // most people came for. The icon carries the caution.
                Image = PowerIcon(22, Color.FromArgb(198, 40, 40))
            };
            item.Click += (s, e) =>
            {
                _closeConfirmed = true;
                Close();
            };
            menu.Items.Add(item);

            // RESTART sits beside Close because most reasons to shut an HMI down are
            // really "make a setting take effect" - PLC connections, language,
            // units. Without it the operator closes the program and then has to find
            // the shortcut again on a panel with no desktop.
            var restart = new ToolStripMenuItem(LanguageManager.GetString("Btn_Restart", "Restart"))
            {
                Image = RestartIcon(22, Color.FromArgb(40, 90, 170))
            };
            restart.Click += (s, e) => RestartApplication();
            menu.Items.Add(restart);
            return menu;
        }

        /// <summary>
        /// Relaunches the HMI and closes this instance.
        ///
        /// The new process is told THIS process's id and waits for it to exit before
        /// it opens anything (see AppBootstrap.AwaitPreviousInstance). Two instances
        /// must never hold the same PLC station at once: MX Component refuses the
        /// second opener rather than sharing the channel, which is why a restart used
        /// to come back with the connection dead - making a just-changed station
        /// setting look like it had not applied at all, while closing and starting
        /// again by hand worked.
        /// </summary>
        public void RestartApplication() { RestartApplication(null); }

        /// <param name="extraArguments">More for the new instance's command line (the sign-in hand-over).</param>
        public void RestartApplication(string extraArguments)
        {
            try
            {
                AuditLogger.Log("restart requested from the HMI.");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Application.ExecutablePath,
                    Arguments = Kaban.UI.AppBootstrap.AwaitExitSwitch + " " +
                                System.Diagnostics.Process.GetCurrentProcess().Id +
                                (string.IsNullOrEmpty(extraArguments) ? "" : " " + extraArguments),
                    // Started from the exe's own folder: the machine's
                    // Translations.csv, PlcLabels.csv and Alarms.json are all resolved
                    // relative to it.
                    WorkingDirectory = System.IO.Path.GetDirectoryName(Application.ExecutablePath),
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);

                _closeConfirmed = true;
                Close();
            }
            catch (Exception ex)
            {
                AuditLogger.Log("restart failed: " + ex.Message);
                MessageBox.Show(this,
                    LanguageManager.GetString("Msg_RestartFailed",
                        "Could not restart automatically. Please close and start the HMI again."),
                    LanguageManager.GetString("Btn_Restart", "Restart the program"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Tells the operator that a setting they just changed needs a restart, and
        /// offers to do it - from ANY page, without that page needing to know about
        /// the shell.
        ///
        /// Never restarts on its own: a setting is often changed in the middle of
        /// other work, and pulling the machine's HMI out from under someone because
        /// they saved a checkbox is not a trade the program gets to make. The
        /// operator decides when.
        /// </summary>
        public static void NotifyRestartRequired(IWin32Window owner, string what)
        {
            string title = LanguageManager.GetString("Ttl_RestartNeeded", "Restart needed");
            string body = string.IsNullOrWhiteSpace(what)
                ? LanguageManager.GetString("Msg_RestartNeeded",
                      "This change takes effect when the HMI restarts.")
                : what + "\r\n\r\n" + LanguageManager.GetString("Msg_RestartNeeded",
                      "This change takes effect when the HMI restarts.");

            var answer = MessageBox.Show(owner,
                body + "\r\n\r\n" + LanguageManager.GetString("Msg_RestartNow", "Restart now?"),
                title, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (answer != DialogResult.Yes) return;

            var shell = (owner as Control) != null ? (owner as Control).FindForm() as MainForm : null;
            if (shell == null) shell = Application.OpenForms.Count > 0
                                       ? Application.OpenForms[0] as MainForm : null;

            if (shell != null) shell.RestartApplication();
            else
                MessageBox.Show(owner,
                    LanguageManager.GetString("Msg_RestartManually",
                        "Please close and start the HMI again for the change to take effect."),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Circular-arrow restart glyph, drawn so it needs no asset.</summary>
        private static Image RestartIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float pad = size * 0.18f;
                var rect = new RectangleF(pad, pad, size - pad * 2, size - pad * 2);
                using (var pen = new Pen(color, Math.Max(2f, size / 11f)))
                {
                    g.DrawArc(pen, rect, 40, 280);          // open circle
                    // arrow head on the open end
                    float cx = rect.X + rect.Width / 2f, cy = rect.Y + rect.Height / 2f;
                    double a = 40 * Math.PI / 180.0;
                    float hx = cx + (rect.Width / 2f) * (float)Math.Cos(a);
                    float hy = cy + (rect.Height / 2f) * (float)Math.Sin(a);
                    float h = size * 0.22f;
                    using (var brush = new SolidBrush(color))
                        g.FillPolygon(brush, new[]
                        {
                            new PointF(hx + h * 0.1f, hy - h * 0.9f),
                            new PointF(hx + h * 0.95f, hy + h * 0.25f),
                            new PointF(hx - h * 0.35f, hy + h * 0.35f)
                        });
                }
            }
            return bmp;
        }

        private void ShowCloseMenu()
        {
            var menu = BuildCloseMenu();
            // drop it from the close button, in the corner of the screen
            Kaban.UI.Controls.UiStyle.ShowPopup(menu, _btnClose, new Point(_btnClose.Width - menu.PreferredSize.Width, _btnClose.Height));
        }

        /// <summary>Standard power symbol, drawn so it needs no font or asset.</summary>
        private static Image PowerIcon(int size, Color colour)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(colour, 2f))
                {
                    float m = size * 0.18f;
                    g.DrawArc(pen, m, m, size - 2 * m, size - 2 * m, -60, 300);
                    g.DrawLine(pen, size / 2f, size * 0.12f, size / 2f, size * 0.46f);
                }
            }
            return bmp;
        }

        private void ToggleMaximize()
        {
            FitMaximizedBounds();
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal : FormWindowState.Maximized;
            if (_btnMax != null) _btnMax.Text = WindowState == FormWindowState.Maximized ? "\u2750" : "\u2610";
        }

        // ==================================================================
        // MAXIMISED ON THE MONITOR THE WINDOW IS ON
        // ------------------------------------------------------------------
        // On an extended (second) screen the full-screen button threw the
        // window off the edge of the desktop. MaximizedBounds was set to the
        // monitor's working area in DESKTOP coordinates - say x = 1920 for a
        // screen to the right of the main one. Windows takes the maximised
        // position (WM_GETMINMAXINFO) relative to the monitor the window
        // maximises on and adds that monitor's origin itself, so the window
        // went to x = 3840: off the desktop. On the main screen, whose origin
        // is 0,0, the two readings agree - which is why it worked there.
        //
        // Now the position is given relative to the monitor, and as a safety
        // net the maximised window is placed on its monitor's working area
        // whatever Windows proposes (WM_WINDOWPOSCHANGING): Windows also
        // "adjusts" the size for a monitor larger than the main one, and the
        // taskbar may sit on either screen.
        // ==================================================================

        /// <summary>The working area of the window's monitor, relative to that monitor.</summary>
        private void FitMaximizedBounds()
        {
            var scr = Screen.FromControl(this);
            var wa = scr.WorkingArea;
            MaximizedBounds = new Rectangle(wa.X - scr.Bounds.X, wa.Y - scr.Bounds.Y, wa.Width, wa.Height);
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd, hwndInsertAfter;
            public int x, y, cx, cy;
            public uint flags;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);

        private const int WM_WINDOWPOSCHANGING = 0x46;
        private static bool _noUser32;
        private const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2;

        /// <summary>A maximised window goes exactly onto its monitor's working area.</summary>
        private void KeepMaximizedOnItsMonitor(ref Message m)
        {
            if (m.LParam == IntPtr.Zero || !IsZoomed(Handle)) return;
            var wp = (WINDOWPOS)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
            if ((wp.flags & SWP_NOMOVE) != 0 && (wp.flags & SWP_NOSIZE) != 0) return;   // z-order only

            // The monitor the window is being placed on: where the proposed
            // rectangle is (a move), or where the window is (a size).
            var target = (wp.flags & SWP_NOMOVE) == 0
                ? Screen.FromRectangle(new Rectangle(wp.x, wp.y, Math.Max(1, wp.cx), Math.Max(1, wp.cy)))
                : Screen.FromHandle(Handle);
            var wa = target.WorkingArea;
            if (wp.x == wa.X && wp.y == wa.Y && wp.cx == wa.Width && wp.cy == wa.Height) return;
            wp.x = wa.X; wp.y = wa.Y; wp.cx = wa.Width; wp.cy = wa.Height;
            wp.flags &= ~(SWP_NOMOVE | SWP_NOSIZE);
            System.Runtime.InteropServices.Marshal.StructureToPtr(wp, m.LParam, false);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1, HTCAPTION = 0x2, WM_NCHITTEST = 0x84;

        /// <summary>Keeps edge RESIZING working on a borderless window.</summary>
        protected override void WndProc(ref Message m)
        {
            if (UseCustomCaption && m.Msg == WM_WINDOWPOSCHANGING && !_noUser32)
            {
                try { KeepMaximizedOnItsMonitor(ref m); }
                catch (DllNotFoundException) { _noUser32 = true; }      // not on Windows (the test bench)
                catch (EntryPointNotFoundException) { _noUser32 = true; }
                catch { }
            }
            base.WndProc(ref m);
            if (!UseCustomCaption || m.Msg != WM_NCHITTEST || WindowState == FormWindowState.Maximized) return;
            const int grip = 6;
            var p = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
            bool left = p.X <= grip, right = p.X >= ClientSize.Width - grip;
            bool top = p.Y <= grip, bottom = p.Y >= ClientSize.Height - grip;
            int hit = 0;
            if (left && top) hit = 13; else if (right && top) hit = 14;
            else if (left && bottom) hit = 16; else if (right && bottom) hit = 17;
            else if (left) hit = 10; else if (right) hit = 11;
            else if (top) hit = 12; else if (bottom) hit = 15;
            if (hit != 0) m.Result = (IntPtr)hit;
        }

        /// <summary>
        /// Logout chooser built exactly like the language selector: a menu that drops
        /// down from the button with two entries and closes by itself when anything
        /// else is clicked - so no cancel button is needed.
        /// Kept separate from the Show call so it can be tested.
        /// </summary>
        internal ContextMenuStrip BuildLogoutMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = Kaban.UI.Controls.UiStyle.MenuFont };
            menu.Renderer = new Kaban.UI.Controls.HmiMenuRenderer();
            menu.ImageScalingSize = new Size(22, 22);

            // Header: who is signed in, with the same person icon as the caption -
            // the bare two-entry list read as an unlabelled menu.
            string who = SecurityManager.CurrentLevel == SecurityLevel.None
                ? LanguageManager.GetString("Sys_NotLoggedIn", "No user logged")
                : SecurityManager.CurrentUser + "  (" + SecurityManager.CurrentLevel + ")";
            // A LABEL, not a disabled option. With an icon in the image margin it
            // sat in the same column as the real entries and read as one of them
            // that happened to be greyed out. A ToolStripLabel with no image starts
            // at the left edge, which is what a heading looks like.
            // Pulled LEFT over the image margin so the heading spans the whole
            // menu. Left in the text column it started where the entries' captions
            // start, which made it look like another entry rather than a title.
            var header = new ToolStripLabel(who)
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            };
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());

            var logout = new ToolStripMenuItem(LanguageManager.GetString("Btn_Logout", "Logout"),
                                               MenuIcon("Kaban.UI.IconMenuLogout.png"));
            logout.Click += (s, e) => SecurityManager.Logout();

            var change = new ToolStripMenuItem(LanguageManager.GetString("Btn_ChangeUser", "Change user"),
                                               MenuIcon("Kaban.UI.IconMenuChangeUser.png"));
            change.Click += (s, e) =>
            {
                SecurityManager.Logout();
                // The list opens straight away: picking another name is the point.
                using (var login = new LoginForm(chooseUser: true)) login.ShowDialog(this);
                OfferUserMagnifier();
            };

            menu.Items.Add(logout);
            menu.Items.Add(change);

            // The simulation monitor is an ADMIN tool, so it belongs with the user
            // menu rather than as a lone button on the caption - where it looked
            // unrelated to anything, especially once the alarm reset appears.
            if (SecurityManager.HasAccess(SecurityLevel.Admin) &&
                SimulationMonitorForm.HasMonitorableStations())
            {
                menu.Items.Add(new ToolStripSeparator());
                var sim = new ToolStripMenuItem(
                    LanguageManager.GetString("Btn_SimMonitor", "Simulation Monitor (F12)"),
                    MonitorIcon(22));
                sim.Click += (s, e) => ShowSimulationMonitor();
                menu.Items.Add(sim);
            }
            return menu;
        }

        /// <summary>
        /// Recolours a glyph to WHITE, preserving its alpha, and renders it at the
        /// given size with high-quality filtering.
        /// </summary>
        private static Image Whiten(Image src, int size)
        {
            if (src == null) return null;
            using (var big = new Bitmap(src, new Size(size, size)))
            {
                var outp = new Bitmap(size, size);
                for (int x = 0; x < size; x++)
                    for (int y = 0; y < size; y++)
                    {
                        var px = big.GetPixel(x, y);
                        outp.SetPixel(x, y, Color.FromArgb(px.A, 255, 255, 255));
                    }
                return outp;
            }
        }

        /// <summary>Little screen with a waveform - the simulation monitor.</summary>
        private static void DrawMonitorGlyph(Graphics g, Rectangle area, Color colour)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = area.X + area.Width / 2f, cy = area.Y + area.Height / 2f;
            var screen = new RectangleF(cx - 10, cy - 8, 20, 14);
            using (var pen = new Pen(colour, 1.8f))
            {
                g.DrawRectangle(pen, screen.X, screen.Y, screen.Width, screen.Height);
                g.DrawLine(pen, cx - 4, cy + 8, cx + 4, cy + 8);          // stand
                g.DrawLine(pen, cx, screen.Bottom, cx, cy + 8);
                // a small live trace inside the screen
                g.DrawLines(pen, new[]
                {
                    new PointF(screen.X + 3, cy + 1), new PointF(screen.X + 6, cy - 3),
                    new PointF(screen.X + 9, cy + 2), new PointF(screen.X + 12, cy - 4),
                    new PointF(screen.Right - 3, cy - 1)
                });
            }
        }

        // The blink colour of each login glyph, made once. Tint() walks every
        // pixel with GetPixel/SetPixel, and it ran on every paint of the blinking
        // login button - several times a second for as long as nobody logged in.
        private readonly System.Collections.Generic.Dictionary<Image, Image> _tinted =
            new System.Collections.Generic.Dictionary<Image, Image>();
        private Color _tintedColour;

        private Image TintedCached(Image art, Color colour)
        {
            if (colour != _tintedColour)
            {
                foreach (var old in _tinted.Values) old.Dispose();
                _tinted.Clear();
                _tintedColour = colour;
            }
            Image img;
            if (!_tinted.TryGetValue(art, out img)) _tinted[art] = img = Tint(art, colour);
            return img;
        }

        /// <summary>Recolours an already-white glyph to another colour (keeps alpha).</summary>
        private static Image Tint(Image src, Color colour)
        {
            var bmp = new Bitmap(src);
            for (int x = 0; x < bmp.Width; x++)
                for (int y = 0; y < bmp.Height; y++)
                {
                    var px = bmp.GetPixel(x, y);
                    if (px.A > 0) bmp.SetPixel(x, y, Color.FromArgb(px.A, colour.R, colour.G, colour.B));
                }
            return bmp;
        }

        /// <summary>
        /// The connection warning, beside the alarm bell.
        ///
        /// Only visible while something is actually down - a light that is always
        /// there is a light nobody looks at.
        /// </summary>
        private void SetupConnectionIndicator()
        {
            _btnConnection = new Button
            {
                Text = "",
                Width = 44,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = pnlTop.BackColor,
                Cursor = Cursors.Hand,
                Visible = false,
                TabStop = false
            };
            _btnConnection.FlatAppearance.BorderSize = 0;
            _btnConnection.FlatAppearance.MouseOverBackColor = pnlTop.BackColor;
            _btnConnection.FlatAppearance.MouseDownBackColor = pnlTop.BackColor;

            _btnConnection.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var r = _btnConnection.ClientRectangle;
                using (var back = new SolidBrush(pnlTop.BackColor)) g.FillRectangle(back, r);

                // A BROKEN-LINK mark, not a second bell: the operator must be able
                // to tell at a glance whether the machine is complaining or has
                // gone quiet.
                // RED. A lost controller is not a caution - the screen is showing
                // values that are no longer true, and amber reads as "look at this
                // when you get a chance".
                DrawLinkGlyph(g, r, _connPhase ? Color.FromArgb(232, 60, 60) : Color.White);
            };

            _btnConnection.Click += (s, e) =>
            {
                var menu = BuildConnectionMenu();
                Kaban.UI.Controls.UiStyle.ShowPopup(menu, _btnConnection, new Point(0, _btnConnection.Height));
            };

            _topTips.SetToolTip(_btnConnection,
                LanguageManager.GetString("Ttl_ConnFailure", "PLC Connection Failure"));
            pnlTop.Controls.Add(_btnConnection);

            var blink = new Timer { Interval = 500 };
            blink.Tick += (s, e) =>
            {
                bool any = ConnectionMonitor.AnyDown;
                if (_btnConnection.Visible != any)
                {
                    _btnConnection.Visible = any;
                    LayoutTopBar();
                }
                if (!any) return;
                _connPhase = !_connPhase;
                _btnConnection.Invalidate();
            };
            blink.Start();
        }

        /// <summary>A broken link: two chain segments with a gap between them.</summary>
        private static void DrawLinkGlyph(Graphics g, Rectangle area, Color colour)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = area.Left + area.Width / 2f;
            float cy = area.Top + area.Height / 2f;
            float w = Math.Min(area.Width, area.Height) * 0.30f;
            float h = w * 0.72f;

            using (var pen = new Pen(colour, Math.Max(1.8f, w * 0.26f)))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawArc(pen, cx - w * 1.15f, cy - h, w * 1.3f, h * 2, 40, 280);
                g.DrawArc(pen, cx - w * 0.15f, cy - h, w * 1.3f, h * 2, 220, 280);
                // the break
                g.DrawLine(pen, cx - w * 0.30f, cy - h * 0.95f, cx + w * 0.30f, cy - h * 1.35f);
            }
        }

        private void SetupAlarmIndicator()
        {
            // A FILLED, LABELLED button - the thin outlined glyph was easy to miss
            // on a machine that has just faulted. It pulses between two reds and is
            // wide enough to hit without looking.
            _btnAlarmReset = new Button
            {
                Text = "",
                Width = 58,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(200, 30, 30),
                Cursor = Cursors.Hand,
                Visible = false
            };
            _btnAlarmReset.FlatAppearance.BorderSize = 0;
            _btnAlarmReset.FlatAppearance.MouseOverBackColor = Color.FromArgb(230, 60, 60);
            _btnAlarmReset.FlatAppearance.MouseDownBackColor = Color.FromArgb(160, 20, 20);
            Kaban.UI.Controls.UiStyle.Round(_btnAlarmReset);
            _btnAlarmReset.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var r = _btnAlarmReset.ClientRectangle;

                // THE GLYPH BLINKS, NOT THE BUTTON. The caption bar keeps its own
                // colour, exactly as the login button does while no user is signed
                // in - a whole red rectangle pulsing next to two flat icons read as
                // a different species of control rather than an urgent one.
                using (var back = new SolidBrush(pnlTop.BackColor))
                    g.FillRectangle(back, r);

                DrawWarningGlyph(g, r, _alarmPhase ? Color.FromArgb(232, 60, 60) : Color.White);
            };
            // A MENU, like the user and language buttons beside it.
            //
            // One machine can have several sub-machines in alarm at once, and a
            // single Reset gave no way to see which - or to clear one without
            // clearing the rest. The menu names each machine, says how many alarms
            // it has, and resets that machine alone.
            _btnAlarmReset.Click += (s, e) =>
            {
                var menu = BuildAlarmMenu();
                Kaban.UI.Controls.UiStyle.ShowPopup(menu, _btnAlarmReset, new Point(0, _btnAlarmReset.Height));
            };
            _topTips.SetToolTip(_btnAlarmReset, LanguageManager.GetString("Ttl_ActiveAlarms", "Active Alarms"));
            pnlTop.Controls.Add(_btnAlarmReset);

            _alarmTimer = new Timer { Interval = 220 };   // twice the old rate - it read as sluggish
            _alarmTimer.Tick += (s, e) =>
            {
                _alarmPhase = !_alarmPhase;
                if (_btnAlarmReset.Visible) _btnAlarmReset.Invalidate();
            };
            _alarmTimer.Start();
        }

        /// <summary>
        /// Active alarms per machine, each with its own Reset.
        ///
        /// Built fresh on every open: it is a live picture of the machine, and a
        /// cached one would show alarms that had already cleared.
        /// </summary>
        /// <summary>
        /// The font every dropdown heading uses.
        ///
        /// UNDERLINED rather than bold. A heading only has to be distinguishable
        /// from the entries under it; bold at the same size as the options competes
        /// with them for attention, and these menus are read a dozen times a shift.
        /// </summary>
        private static Font MenuHeaderFont()
        {
            // PLAIN. It was bold, then underlined - but a separator is drawn
            // directly beneath every heading already, so the rule was a second line
            // saying the same thing. Position and the separator are enough.
            // Shared: a new Font per menu opened was never disposed.
            return Kaban.UI.Controls.UiStyle.MenuFont;
        }

        private ContextMenuStrip BuildAlarmMenu()
        {
            // BUILT LIKE THE LOGOUT MENU, deliberately.
            //
            // Same ShowImageMargin, same icon size, same header treatment - so the
            // three caption-bar menus feel like one family. A menu that renders its
            // own way looks like a different program when you move along the row,
            // and the operator has to re-learn where to aim.
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = Kaban.UI.Controls.UiStyle.MenuFont };
            menu.Renderer = new Kaban.UI.Controls.HmiMenuRenderer();
            menu.ImageScalingSize = new Size(22, 22);

            var header = new ToolStripLabel(
                LanguageManager.GetString("Ttl_ActiveAlarms", "Active Alarms"))
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)   // spans the image column, as in the user menu
            };
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());

            var byMachine = new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in AlarmManager.ActiveAlarms)
            {
                string code = a.AlarmCode ?? "";
                int dot = code.IndexOf('.');
                string machine = dot > 0 ? code.Substring(0, dot) : "";
                if (machine.Length == 0) continue;
                int had; byMachine.TryGetValue(machine, out had);
                byMachine[machine] = had + 1;
            }

            if (byMachine.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem(
                    LanguageManager.GetString("Sys_NoActiveAlarms", "No active alarms")) { Enabled = false });
                return menu;
            }

            foreach (var kv in System.Linq.Enumerable.OrderBy(byMachine, k => k.Key))
            {
                var moduleId = kv.Key;

                // THE WHOLE ROW IS THE BUTTON, so the caption has to say what
                // pressing it does - "Reset (4) Active Alarms", not a count with a
                // separate control beside it.
                var caption = string.Format(
                    LanguageManager.GetString("Btn_ResetNActive", "Reset ({0}) Active Alarms"),
                    kv.Value);

                var item = new ToolStripMenuItem(caption, MachineBadge(moduleId));
                item.ToolTipText = LanguageManager.GetString("Machine_" + moduleId, moduleId);
                item.Click += (s, e) => ResetAlarmsFor(moduleId);
                menu.Items.Add(item);
            }

            return menu;
        }

        /// <summary>The same badge the left bar draws for this machine.</summary>
        private Image MachineBadge(string moduleId)
        {
            return Screens.MachineIcon.ForModule(moduleId);
        }

        /// <summary>
        /// Resets one machine's alarms, leaving the others standing: pulses the
        /// machine's alarm reset bit on ITS OWN controller (station 3 for an H500
        /// mounted under FA2010 - see AlarmReset), then clears its list. Clearing
        /// only the list let an alarm whose cause was latched come straight back.
        ///
        /// When the reset cannot be sent - not signed in, controller not
        /// answering, no reset bit on that machine - the list is still cleared,
        /// as before, and a message says the machine itself was NOT reset: an
        /// operator who believes it was would wait for a machine that is still
        /// holding its alarm.
        /// </summary>
        private void ResetAlarmsFor(string moduleId)
        {
            var mounted = System.Linq.Enumerable.FirstOrDefault(Kaban.Core.Machines.MachineRegistry.Modules,
                m => string.Equals(m.Module.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));
            int station = mounted != null ? mounted.Context.Station(1) : 1;
            string name = mounted != null ? Screens.MachineTabScreen.DisplayNameOf(mounted) : moduleId;

            string why;
            bool sent = Kaban.UI.Helpers.AlarmReset.Pulse(station, name, out why);
            AlarmManager.AcknowledgeMachine(moduleId);

            if (!sent)
                MessageBox.Show(this,
                    string.Format(LanguageManager.GetString("Msg_ResetNotSent",
                        "The alarms were cleared from the list, but no reset was sent to {0}: {1}."), name, why),
                    LanguageManager.GetString("Ttl_AlarmReset", "Alarm reset"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Controllers that are not answering, with the code for each.
        ///
        /// A SEPARATE indicator from the alarm bell on purpose. An alarm means the
        /// machine told us something is wrong; a lost connection means the machine
        /// is telling us nothing at all, and the values on screen are old. Those
        /// call for different people and different actions, so they do not share a
        /// light.
        /// </summary>
        private ContextMenuStrip BuildConnectionMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = Kaban.UI.Controls.UiStyle.MenuFont };
            menu.Renderer = new Kaban.UI.Controls.HmiMenuRenderer();
            menu.ImageScalingSize = new Size(22, 22);

            menu.Items.Add(new ToolStripLabel(
                LanguageManager.GetString("Ttl_ConnFailure", "PLC Connection Failure"))
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            });
            menu.Items.Add(new ToolStripSeparator());

            var down = System.Linq.Enumerable.ToList(ConnectionMonitor.Down);
            if (down.Count == 0)
            {
                menu.Items.Add(new ToolStripMenuItem(
                    LanguageManager.GetString("Conn_AllOk", "All controllers connected")) { Enabled = false });
                AddStationConnectionItem(menu);
                return menu;
            }

            foreach (var s in down)
            {
                // The code, not the driver's wording: it is stable across driver
                // versions, short enough to read down a phone, and it is what the
                // help document is keyed on.
                // THE BADGE ALREADY NAMES THE MACHINE, so the row carries the code
                // instead - which is the part the operator has to quote and the part
                // the help document is keyed on.
                // No label when there is no code to put after it.
                var suffix = PlcManager.StationSuffix(s.ModuleId, s.LocalNumber);
                var caption = string.IsNullOrEmpty(s.ErrorCode)
                    ? LanguageManager.GetString("Sta_Failed", "Connection failed") + suffix
                    : LanguageManager.GetString("Conn_ErrorCode", "Error Code") +
                      ": " + s.ErrorCode + suffix;

                var item = new ToolStripMenuItem(caption, MachineBadge(s.ModuleId));
                item.ToolTipText = ConnectionMonitor.DescribeCode(s.ErrorCode) +
                                   (s.EverConnected
                                       ? "\r\n" + string.Format(
                                           LanguageManager.GetString("Conn_StaleFor", "No reply for {0} s"),
                                           (int)s.StaleFor.TotalSeconds)
                                       : "");
                var code = s.ErrorCode;
                item.Click += (s2, e2) => OpenConnectionHelp(code);
                menu.Items.Add(item);
            }
            AddStationConnectionItem(menu);
            return menu;
        }

        /// <summary>
        /// For an admin: a way from the warning straight to the page that fixes it,
        /// like the Simulation Monitor entry in the user menu.
        /// </summary>
        private void AddStationConnectionItem(ContextMenuStrip menu)
        {
            if (!SecurityManager.HasAccess(SecurityLevel.Admin)) return;
            menu.Items.Add(new ToolStripSeparator());
            var item = new ToolStripMenuItem(
                LanguageManager.GetString("Set_StationConn", "PLC Connection"), LinkIcon(22));
            item.Click += (s, e) => ShowStationConnection();
            menu.Items.Add(item);
        }

        private static Image LinkIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
                DrawLinkGlyph(g, new Rectangle(0, 0, size, size), Color.FromArgb(40, 110, 190));
            return bmp;
        }

        /// <summary>Opens HMI Settings on its PLC Connection tab.</summary>
        private void ShowStationConnection()
        {
            foreach (var kv in _menuButtons)
            {
                var settings = FindSettingsWith(kv.Value, "Set_StationConn");
                if (settings == null) continue;
                LoadScreen(kv.Value);
                settings.ActivateByKey("Set_StationConn");
                return;
            }
        }

        private static Kaban.UI.Settings.SettingsScreen FindSettingsWith(Control root, string key)
        {
            var s = root as Kaban.UI.Settings.SettingsScreen;
            if (s != null && s.HasPage(key)) return s;
            if (root == null) return null;
            foreach (Control c in root.Controls)
            {
                var found = FindSettingsWith(c, key);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Opens the document for a connection error code.</summary>
        private void OpenConnectionHelp(string code)
        {
            var doc = TroubleshootingCatalog.ResolveConnection(code);
            if (doc == null || !doc.Exists)
            {
                MessageBox.Show(this, ConnectionMonitor.DescribeCode(code),
                    LanguageManager.GetString("Ttl_ConnFailure", "PLC Connection Failure"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                AuditLogger.Log("opened connection help for " + code + ".");
                // ONLY THAT CODE'S SECTION, as its own page. The previous version
                // appended "#C02" to the FILE PATH, which Windows took as part of
                // the file name - nothing opened, and the error was swallowed.
                var target = TroubleshootingCatalog.ExtractSection(doc);

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = target, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                // SAID, not just logged. A click that silently does nothing is
                // exactly how this bug hid for two rounds.
                AuditLogger.Log("connection help failed: " + ex.Message);
                MessageBox.Show(this,
                    ConnectionMonitor.DescribeCode(code) + "\r\n\r\n" + ex.Message,
                    LanguageManager.GetString("Ttl_ConnFailure", "PLC Connection Failure"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private Button _btnConnection;
        private bool _connPhase;

        private Button _btnAlarmReset;
        private Timer _alarmTimer;
        private bool _alarmPhase;
        private bool _machineError;

        /// <summary>Address of the machine-error summary bit (same on all machines).</summary>
        public const string MachineErrorAddress = "M800";

        /// <summary>
        /// FA2010 PLC1's alarm reset. Kept for code that names it; the reset of
        /// any machine is AlarmReset.AddressFor(station) - M102 is FA2010's only.
        /// </summary>
        public const string AlarmResetAddress = "M102";

        /// <summary>
        /// Called by the scanner with PLC 1's view of M800. Drives the pulsing menu
        /// entry and the visibility of the reset button.
        /// </summary>
        public void SetMachineError(bool active)
        {
            if (_machineError == active) return;
            _machineError = active;

            UpdateAlarmPulse();

            if (_btnAlarmReset != null)
            {
                _btnAlarmReset.Visible = active;   // shown only while the machine is in error
                LayoutTopBar();
            }
            AuditLogger.Log(active ? "machine error (M800) ACTIVE" : "machine error (M800) cleared");
        }

        /// <summary>
        /// The Alarms entry pulses only while there IS an error AND the operator is
        /// somewhere else - once the alarm page is open the pulse has done its job
        /// and would just be noise.
        /// </summary>
        private void UpdateAlarmPulse()
        {
            foreach (var kv in _menuButtons)
            {
                if (!string.Equals(kv.Key.TranslationKey, "Btn_Alarms", StringComparison.OrdinalIgnoreCase))
                    continue;
                bool onAlarmPage = _currentScreen != null && kv.Value == _currentScreen;
                kv.Key.AlertBlink = _machineError && !onAlarmPage;
            }
        }

        /// <summary>
        /// Reset mark: a near-closed circular arrow, the standard "reset" symbol
        /// (as in the flaticon reference). Drawn with GDI+ rather than shipping
        /// licensed artwork, and it stays crisp at any DPI.
        /// </summary>
        /// <summary>
        /// A warning triangle, OUTLINE ONLY.
        ///
        /// Nothing is filled, so what alternates is the stroke itself against the
        /// caption bar showing through. A filled shape swapping colour reads as a
        /// panel changing state; a drawn line changing colour reads as a warning,
        /// which is what this is.
        ///
        /// Rounded joins because a triangle's points go ragged at small sizes, and
        /// the top corner of this one sits at the very top of a 34px button.
        /// </summary>
        private static void DrawWarningGlyph(Graphics g, Rectangle area, Color colour)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float side = Math.Min(area.Width, area.Height) * 0.78f;
            float cx = area.Left + area.Width / 2f;
            float cy = area.Top + area.Height / 2f;
            float h = side * 0.88f;

            var top = new PointF(cx, cy - h / 2f);
            var left = new PointF(cx - side / 2f, cy + h / 2f);
            var right = new PointF(cx + side / 2f, cy + h / 2f);

            float stroke = Math.Max(1.8f, side * 0.10f);
            using (var pen = new Pen(colour, stroke))
            {
                pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawPolygon(pen, new[] { top, right, left });
            }

            // The exclamation: a tapered bar and a dot, both drawn in the same
            // stroke colour so the whole mark blinks as one.
            float barTop = cy - h * 0.20f;
            float barBottom = cy + h * 0.04f;
            using (var pen = new Pen(colour, Math.Max(1.8f, side * 0.11f)))
            {
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                g.DrawLine(pen, cx, barTop, cx, barBottom);
            }

            float dot = Math.Max(1.6f, side * 0.10f);
            using (var pen = new Pen(colour, dot))
                // Clear of the bar: at 34px a dot placed any closer merged with
                // it and the mark read as a solid stroke rather than an exclamation.
                g.DrawEllipse(pen, cx - dot / 2f, cy + h * 0.20f, dot, dot);
        }

        /// <summary>Small screen-with-trace icon for the user menu.</summary>
        private static Image MonitorIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
                // Blue, matching the Restart icon: both are "go and do something
                // elsewhere" actions, where Logout and Change user are about this
                // session. Colour grouping the entries is quicker to read than
                // three greys that all look the same.
                DrawMonitorGlyph(g, new Rectangle(0, 0, size, size), Color.FromArgb(40, 110, 190));
            return bmp;
        }

        private static Image MenuIcon(string resource)
        {
            using (var img = LoadEmbeddedImage(resource))     // the full-size art is only a source
            {
                if (img == null) return null;
                var scaled = new Bitmap(22, 22);
                using (var g = Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, new Rectangle(0, 0, 22, 22));
                }
                return scaled;
            }
        }

        /// <summary>Language chooser: one entry per available language, with its flag.</summary>
        private void ShowLanguageMenu()
        {
            // The SAME construction as the user and alarm menus. This one was built
            // bare - no header, default icon size, default margins - so moving along
            // the caption bar produced three menus that looked like three programs.
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = Kaban.UI.Controls.UiStyle.MenuFont };
            menu.Renderer = new Kaban.UI.Controls.HmiMenuRenderer();
            menu.ImageScalingSize = new Size(22, 22);

            menu.Items.Add(new ToolStripLabel(LanguageManager.GetString("Ttl_Language", "Language"))
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            });
            menu.Items.Add(new ToolStripSeparator());

            foreach (var lang in LanguageManager.AvailableLanguages)
            {
                var code = lang;
                var item = new ToolStripMenuItem(code.ToUpperInvariant())
                {
                    Checked = string.Equals(code, LanguageManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase),
                    Image = FlagBitmap(code)
                };
                item.Click += (s2, e2) =>
                {
                    LanguageManager.ChangeLanguage(code);
                    _btnLang.Invalidate();
                };
                menu.Items.Add(item);
            }
            Kaban.UI.Controls.UiStyle.ShowPopup(menu, _btnLang, new Point(0, _btnLang.Height));
        }

        private static Bitmap FlagBitmap(string code)
        {
            var bmp = new Bitmap(30, 21);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                DrawFlag(g, new Rectangle(0, 0, 30, 21), code);
            }
            return bmp;
        }

        /// <summary>
        /// Paints a small flag. Windows does not render country-flag EMOJI (the
        /// regional-indicator pair falls back to plain letters), so drawing is the
        /// only way to actually show one.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, Image> _flagCache =
            new System.Collections.Generic.Dictionary<string, Image>();

        /// <summary>The supplied flag artwork for a language, or null when unknown.</summary>
        private static Image FlagArt(string code)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length == 0) return null;
            Image cached;
            if (_flagCache.TryGetValue(code, out cached)) return cached;
            var img = LoadEmbeddedImage("Kaban.UI.Flag." + code + ".png");
            _flagCache[code] = img;
            return img;
        }

        private static void DrawFlag(Graphics g, Rectangle flag, string code)
        {
            // Real artwork when we have it (the flags shipped with the alarm pack,
            // plus the two exact tricolours); the painted fallback only runs for a
            // language with no image yet.
            var art = FlagArt(code);
            if (art != null)
            {
                var old = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(art, flag);
                g.InterpolationMode = old;
                using (var border = new Pen(Color.FromArgb(90, 96, 106))) g.DrawRectangle(border, flag);
                return;
            }

            // Rendered at 4x then scaled down with bicubic filtering: drawing the
            // crescent and star directly at 30x21 left them coarse and lumpy.
            const int SS = 4;
            using (var bmp = new Bitmap(Math.Max(1, flag.Width * SS), Math.Max(1, flag.Height * SS)))
            {
                using (var bg = Graphics.FromImage(bmp))
                {
                    bg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    DrawFlagCore(bg, new Rectangle(0, 0, bmp.Width, bmp.Height), code);
                }
                var old = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(bmp, flag);
                g.InterpolationMode = old;
            }
            using (var border = new Pen(Color.FromArgb(90, 96, 106))) g.DrawRectangle(border, flag);
        }

        /// <summary>Flag geometry, drawn at whatever resolution it is given.</summary>
        private static void DrawFlagCore(Graphics g, Rectangle f, string code)
        {
            switch ((code ?? "").Trim().ToUpperInvariant())
            {
                case "TR":
                    using (var red = new SolidBrush(Color.FromArgb(227, 10, 23))) g.FillRectangle(red, f);
                    // crescent: a white disc with a red disc cut out of it
                    float outer = f.Height * 0.46f;
                    float cxc = f.Left + f.Width * 0.36f, cyc = f.Top + f.Height / 2f;
                    using (var white = new SolidBrush(Color.White))
                        g.FillEllipse(white, cxc - outer, cyc - outer, outer * 2, outer * 2);
                    float inner = outer * 0.80f;
                    float cxi = cxc + outer * 0.28f;
                    using (var red2 = new SolidBrush(Color.FromArgb(227, 10, 23)))
                        g.FillEllipse(red2, cxi - inner, cyc - inner, inner * 2, inner * 2);
                    // five-pointed star
                    float sr = f.Height * 0.20f;
                    float sx = f.Left + f.Width * 0.585f, sy = cyc;
                    var star = new PointF[10];
                    for (int i = 0; i < 10; i++)
                    {
                        double ang = -Math.PI / 2 + i * Math.PI / 5;
                        double rad = (i % 2 == 0) ? sr : sr * 0.42;
                        star[i] = new PointF((float)(sx + Math.Cos(ang) * rad), (float)(sy + Math.Sin(ang) * rad));
                    }
                    using (var white2 = new SolidBrush(Color.White)) g.FillPolygon(white2, star);
                    break;

                case "EN":
                case "GB":
                    using (var blue = new SolidBrush(Color.FromArgb(1, 33, 105))) g.FillRectangle(blue, f);
                    float tw = f.Height * 0.30f, tr = f.Height * 0.16f;
                    using (var w = new Pen(Color.White, tw))
                    {
                        g.DrawLine(w, f.Left, f.Top + f.Height / 2f, f.Right, f.Top + f.Height / 2f);
                        g.DrawLine(w, f.Left + f.Width / 2f, f.Top, f.Left + f.Width / 2f, f.Bottom);
                    }
                    using (var r = new Pen(Color.FromArgb(200, 16, 46), tr))
                    {
                        g.DrawLine(r, f.Left, f.Top + f.Height / 2f, f.Right, f.Top + f.Height / 2f);
                        g.DrawLine(r, f.Left + f.Width / 2f, f.Top, f.Left + f.Width / 2f, f.Bottom);
                    }
                    break;

                default:
                    using (var grey = new SolidBrush(Color.FromArgb(120, 130, 145))) g.FillRectangle(grey, f);
                    break;
            }
        }

        /// <summary>
        /// Person-and-arrow mark, drawn so it stays crisp at any DPI.
        /// signedIn = false -> the button LOGS IN : arrow points INTO the figure.
        /// signedIn = true  -> the button LOGS OUT: arrow points AWAY from it.
        /// (The two were the wrong way round before.)
        /// </summary>
        private static void DrawLoginIcon(Graphics g, Rectangle area, Color colour, bool signedIn)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = area.Left + area.Width / 2f - 4, cy = area.Top + area.Height / 2f;
            using (var pen = new Pen(colour, 1.8f))
            {
                g.DrawEllipse(pen, cx - 5, cy - 9, 10, 10);                       // head
                g.DrawArc(pen, cx - 8, cy + 1, 16, 14, 200, 140);                 // shoulders

                float tipIn = area.Right - 5, tailIn = area.Right - 14;            // shaft span
                if (!signedIn)
                {
                    // LOG IN: arrow travels towards the person (leftwards, head at left)
                    g.DrawLine(pen, tipIn, cy, tailIn, cy);
                    g.DrawLine(pen, tailIn, cy, tailIn + 4, cy - 4);
                    g.DrawLine(pen, tailIn, cy, tailIn + 4, cy + 4);
                }
                else
                {
                    // LOG OUT: arrow travels away from the person (rightwards)
                    g.DrawLine(pen, tailIn, cy, tipIn, cy);
                    g.DrawLine(pen, tipIn, cy, tipIn - 4, cy - 4);
                    g.DrawLine(pen, tipIn, cy, tipIn - 4, cy + 4);
                }
            }
        }

        private void UpdateLoginUi()
        {
            if (_btnLogin == null) return;

            string userPart;
            if (SecurityManager.CurrentLevel == SecurityLevel.None)
            {
                userPart = LanguageManager.GetString("Sys_NotLoggedIn", "No user logged");
                _btnLogin.BackColor = Color.Black;
                if (_loginBlink != null && !_loginBlink.Enabled) _loginBlink.Start();
                _topTips.SetToolTip(_btnLogin, LanguageManager.GetString("Btn_Login", "Login"));
            }
            else
            {
                userPart = SecurityManager.CurrentUser + " (" + SecurityManager.CurrentLevel + ")";
                if (_loginBlink != null) _loginBlink.Stop();                  // logged in: stop pulsing
                _btnLoginTint = Color.White;
                _btnLogin.BackColor = Color.Black;                            // signed in: quiet
                _topTips.SetToolTip(_btnLogin, LanguageManager.GetString("Btn_Logout", "Logout"));
            }

            // TITLE: "FA2010 - V1.0.0.0 - admin (Admin)"
            var asm = System.Reflection.Assembly.GetEntryAssembly();
            string machine = asm != null ? asm.GetName().Name : "";
            int dot = machine.LastIndexOf('.');
            if (dot >= 0) machine = machine.Substring(dot + 1);
            string version = asm != null ? "V" + asm.GetName().Version : "";
            this.Text = machine + " - " + version + " - " + userPart;

            _btnLogin.Invalidate();                 // icon reflects signed-in state
            LayoutTopBar();   // sim-monitor visibility follows the login level
        }

        private void LoadUnit(string key, UnitType type)
        {
            try
            {
                string saved = (string)Properties.Settings.Default[key];
                if (!string.IsNullOrEmpty(saved))
                {
                    UnitManager.SetActive(type, saved);
                }
            }
            catch { }
        }

        /// <summary>
        /// Tears the PLC connection down as the HMI closes.
        ///
        /// THE CLOSE MENU IS THE X BUTTON'S, AND ONLY ITS. The X on the caption
        /// bar sits where a stray tap on a touch panel lands, so it offers Close
        /// and Restart instead of closing outright. Every other way of closing -
        /// the taskbar's "Close window", Alt+F4, the system menu, Windows shutting
        /// down - is a deliberate act done somewhere else, and closes at once. It
        /// used to be cancelled and turned into the same dropdown, so closing from
        /// the taskbar meant going back to the program to click Close again.
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            AuditLogger.Log(_closeConfirmed ? "HMI closed by the operator."
                                            : "HMI closed (" + e.CloseReason + ").");
            try { SaveAllSideViews(); } catch { }

            // Stop scanning FIRST, then close every station - not just station 1.
            // A scan in flight would otherwise re-open a station we just closed, and
            // stations 2..n were previously left to process teardown, which happens
            // on its own schedule. Both matter for restart: the incoming instance
            // cannot open a station the outgoing one still holds.
            try { if (_scanner != null) _scanner.Stop(); } catch { }
            try { if (_connPoll != null) _connPoll.Stop(); } catch { }
            PlcManager.Instance.DisconnectAll();
        }

        private Timer _connPoll;

        private void btnUnitSettings_Click(object sender, EventArgs e)
        {
            using (var form = new Forms.UnitSettingsForm())
            {
                form.ShowDialog();
            }
        }

        private void CmbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbLanguage.SelectedItem != null)
            {
                string selectedLang = cmbLanguage.SelectedItem.ToString();
                LanguageManager.ChangeLanguage(selectedLang);

                try
                {
                    Properties.Settings.Default["AppLanguage"] = selectedLang;
                    Properties.Settings.Default.Save();
                }
                catch { }
            }
        }

        private void pnlMenu_Paint(object sender, PaintEventArgs e)
        {

        }

        /// <summary>
        /// Reads CommonTranslations.csv embedded in Kaban.UI. This is the safety net
        /// that makes the shared keys available to every machine even if the .csv was
        /// not copied next to the .exe.
        /// </summary>
        private static void LoadEmbeddedCommonTranslations()
        {
            try
            {
                var asm = typeof(MainForm).Assembly;
                string name = null;
                foreach (var n in asm.GetManifestResourceNames())
                {
                    if (n.EndsWith("CommonTranslations.csv", StringComparison.OrdinalIgnoreCase))
                    {
                        name = n;
                        break;
                    }
                }
                if (name == null) return;

                using (var stream = asm.GetManifestResourceStream(name))
                    LanguageManager.LoadCommonTranslationsFromStream(stream);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Embedded common translations not loaded: " + ex.Message);
            }
        }
        // ==================================================================
        // PLC MONITOR (diagnostics)
        // ------------------------------------------------------------------
        // A Mitsubishi station can be watched in GX Works; nothing else could be,
        // so there was no way to confirm a control was really reaching station 2.
        // F12 or the admin user menu opens a live view with one tab per
        // CONTROLLER, editable in both directions.
        //
        // The caption-bar button stays tied to a SIMULATED station on purpose: it
        // is a bench tool, and a shortcut to it on a production machine's title
        // bar would be noise. The menu entry and F12 work everywhere.
        // ==================================================================
        /// <summary>
        /// Opens the live PLC memory monitor - a SIDE PAGE: beside the page on
        /// display, which narrows to make room (or in its own window, if it was
        /// taken out into one). Returns it.
        /// </summary>
        public SimulationMonitorForm ShowSimulationMonitor()
        {
            return ShowSidePage(PlcMonitorKey) as SimulationMonitorForm;
        }

        /// <summary>F12: the monitor opens, or closes when it is open in the panel.</summary>
        private void ToggleSimulationMonitor()
        {
            ToggleSidePage(PlcMonitorKey);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F12)
            {
                // ADMIN ONLY, like the menu entry. The monitor writes any device on
                // any station, bypassing every control's own security level and
                // interlock conditions; F12 opened it for whoever stood at the panel.
                // (The caption-bar button stays: it only appears on bench builds
                // with simulated stations.)
                if (SecurityManager.HasAccess(SecurityLevel.Admin)) ToggleSimulationMonitor();
                else AuditLogger.Log("F12 (PLC monitor) refused: admin level required.");
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>Adds a "Sim Monitor" button when simulated stations are present.</summary>

        // ==================================================================
        // COLLAPSIBLE NAVIGATION + HISTORY
        // ------------------------------------------------------------------
        // Top of the menu: [toggle] then [back | forward]. The toggle switches
        // between "icon + label" and icon-only; Back/Forward walk the page AND
        // settings-tab history like a browser.
        //
        // DOCK ORDER: pnlMenu children are all Dock.Top and WinForms docks the
        // LAST-added control FIRST, so SendToBack() (= end of the collection)
        // is what places a control at the TOP. BringToFront put the toggle at
        // the BOTTOM - that was the earlier bug.
        // ==================================================================
        private bool _navCollapsed;
        private Button _btnNavToggle;
        private Button _btnHistBack, _btnHistFwd;
        private int MenuWidthFull = 204;   // user-draggable; persisted
        private const int MenuWidthCompact = 56;

        /// <summary>Resource icon, or null if unavailable (falls back gracefully).</summary>
        private static Image SafeIcon(string name)
        {
            try { return Properties.Resources.ResourceManager.GetObject(name) as Image; }
            catch { return null; }
        }

        private Panel _toggleStrip;
        private Splitter _navSplitter;

        private static string NavWidthPath
        {
            get
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KabanHMI", "NavWidth.cfg");
            }
        }

        private void SaveNavWidth()
        {
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(NavWidthPath));
                System.IO.File.WriteAllText(NavWidthPath, MenuWidthFull.ToString());
            }
            catch { }
        }

        private void LoadNavWidth()
        {
            try
            {
                if (!System.IO.File.Exists(NavWidthPath)) return;
                int w;
                if (int.TryParse(System.IO.File.ReadAllText(NavWidthPath).Trim(), out w) &&
                    w >= MenuWidthCompact + 40 && w <= 600)
                {
                    // Kept at factor 1 (see UiScale); the menu is scaled with the window.
                    MenuWidthFull = w;
                    if (!_navCollapsed) pnlMenu.Width = Kaban.UI.Controls.UiScale.For(pnlMenu, w);
                }
            }
            catch { }
        }

        private void SetupNavToggle()
        {
            // Toggle lives in its own strip so it can sit at the RIGHT end while
            // the labels are shown ("collapse me" affordance) and fill the compact
            // strip when collapsed.
            _toggleStrip = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = pnlMenu.BackColor };
            _btnNavToggle = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F),
                Cursor = Cursors.Hand,
                BackColor = pnlMenu.BackColor
            };
            _btnNavToggle.FlatAppearance.BorderSize = 0;
            // Same blue-on-hover rule as every other control (see UiStyle.HoverBorderColor).
            var toggleHover = Kaban.UI.Controls.UiStyle.HoverBorder(_btnNavToggle);
            _btnNavToggle.Paint += (s, e) =>
            {
                if (!toggleHover()) return;
                using (var pen = new Pen(Kaban.UI.Controls.UiStyle.HoverBorderColor, 1.6f))
                    e.Graphics.DrawRectangle(pen, 1, 1, _btnNavToggle.Width - 3, _btnNavToggle.Height - 3);
            };
            _btnNavToggle.Click += (s, e) => SetNavCollapsed(!_navCollapsed);
            _toggleStrip.Controls.Add(_btnNavToggle);
            _toggleStrip.Resize += (s, e) => LayoutNavToggle();

            // History: BOTTOM of the nav, TEXT arrows so the disabled state greys
            // out automatically (images do not grey on Enabled = false).
            var hist = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = pnlMenu.BackColor };
            _btnHistBack = MakeHistButton("\u25C0", () => Helpers.NavigationHistory.Back());
            _btnHistFwd = MakeHistButton("\u25B6", () => Helpers.NavigationHistory.Forward());
            hist.Controls.Add(_btnHistBack);
            hist.Controls.Add(_btnHistFwd);
            hist.Resize += (s, e) =>
            {
                Func<int, int> px = v => Kaban.UI.Controls.UiScale.For(hist, v);
                _btnHistBack.SetBounds(px(4), px(2), hist.Width / 2 - px(6), hist.Height - px(4));
                _btnHistFwd.SetBounds(hist.Width / 2 + px(2), px(2), hist.Width / 2 - px(6), hist.Height - px(4));
            };

            // A splitter on the menu's right edge: the operator sets how wide the
            // navigation is and it STAYS there (expanded, captions intact) until
            // dragged again - it is not the collapse toggle.
            _navSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = Color.FromArgb(214, 218, 224),
                MinExtra = 200,
                MinSize = MenuWidthCompact
            };
            _navSplitter.SplitterMoved += (s, e) =>
            {
                if (_navCollapsed) return;
                MenuWidthFull = Math.Max(MenuWidthCompact + 40, Kaban.UI.Controls.UiScale.Logical(pnlMenu.Width));
                SaveNavWidth();
            };

            // THE NAVIGATION COLUMN IS A FLOW, NOT A DOCK STACK.
            //
            // Dock.Top ordering is decided by child index in a direction that reads
            // backwards (the HIGHEST index docks closest to the edge), and every
            // attempt to place the machine strip under the page buttons by juggling
            // that order put it somewhere else instead. A TopDown FlowLayoutPanel
            // lays children out in ADD ORDER, top to bottom, which is exactly the
            // guarantee this column needs: buttons first, then the gap, then the
            // machine strip - and no amount of later re-adding can reshuffle it.
            EnsureMachineStrip();
            pnlMenu.Controls.Add(_toggleStrip);
            pnlMenu.Controls.Add(hist);
            _toggleStrip.SendToBack();   // dock-first = TOP (WinForms docks last-added first)

            // ButtonHMI re-applies its own translation when the language changes,
            // which used to re-fill the captions of a COLLAPSED menu (clipped text
            // over the icons). Re-assert the collapsed state afterwards; BeginInvoke
            // makes sure this runs after every control's own handler.
            LanguageManager.LanguageChanged += (s, e) =>
            {
                if (!IsHandleCreated) return;
                BeginInvoke(new Action(() => { if (_navCollapsed) SetNavCollapsed(true); }));
            };

            LoadNavWidth();                       // last width the operator dragged to
            Helpers.NavigationHistory.Changed += (s, e) => UpdateHistButtons();
            UpdateHistButtons();
            LayoutNavToggle();
        }

        private void LayoutNavToggle()
        {
            if (_btnNavToggle == null || _toggleStrip == null) return;
            Func<int, int> px = v => Kaban.UI.Controls.UiScale.For(_toggleStrip, v);
            if (_navCollapsed)
            {
                _btnNavToggle.Text = "\u2630";                        // hamburger: "open me"
                _btnNavToggle.SetBounds(px(4), px(2), _toggleStrip.Width - px(8), _toggleStrip.Height - px(3));
            }
            else
            {
                _btnNavToggle.Text = "\u00AB";                        // <<  : "collapse me"
                _btnNavToggle.SetBounds(_toggleStrip.Width - px(44), px(2), px(40), _toggleStrip.Height - px(3));   // right end
            }
        }

        private Button MakeHistButton(string glyph, Action onClick)
        {
            var b = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Text = "",                       // glyph is PAINTED - see below
                Cursor = Cursors.Hand,
                BackColor = pnlMenu.BackColor
            };
            bool pointsLeft = glyph == "\u25C0";
            b.Paint += (s, e) =>
            {
                // WinForms paints DISABLED button text with the system grey-out, which
                // still looked almost black here. Drawing the triangle ourselves gives
                // full control: dark when usable, clearly pale when not.
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var c = b.Enabled ? Color.FromArgb(24, 28, 34) : Color.FromArgb(196, 201, 208);
                float cx = b.Width / 2f, cy = b.Height / 2f;
                var pts = pointsLeft
                    ? new[] { new PointF(cx + 5, cy - 7), new PointF(cx + 5, cy + 7), new PointF(cx - 6, cy) }
                    : new[] { new PointF(cx - 5, cy - 7), new PointF(cx - 5, cy + 7), new PointF(cx + 6, cy) };
                using (var br = new SolidBrush(c)) e.Graphics.FillPolygon(br, pts);
            };
            b.EnabledChanged += (s, e) => b.Invalidate();
            b.FlatAppearance.BorderSize = 0;
            b.Click += (s, e) => onClick();
            return b;
        }

        private void UpdateHistButtons()
        {
            if (_btnHistBack != null) _btnHistBack.Enabled = Helpers.NavigationHistory.CanBack;
            if (_btnHistFwd != null) _btnHistFwd.Enabled = Helpers.NavigationHistory.CanForward;
        }

        /// <summary>Switches the left navigation between full and icon-only width.</summary>
        public void SetNavCollapsed(bool collapsed)
        {
            _navCollapsed = collapsed;
            pnlMenu.Width = Kaban.UI.Controls.UiScale.For(pnlMenu, collapsed ? MenuWidthCompact : MenuWidthFull);
            LayoutNavToggle();

            foreach (var kv in _menuButtons)
            {
                var b = kv.Key;
                // The caption is HIDDEN, not erased. Blanking Text used to be the
                // way to do this; it now assigns the caption, so a blank asked for
                // the key's translation and the label reappeared.
                b.ShowCaption = !collapsed;
                var pad = collapsed
                    ? new Padding(12, 0, 0, 0)
                    : (b.Image != null ? new Padding(12, 0, 4, 0)
                                       : new Padding(42, 0, 4, 0));
                b.Padding = Kaban.UI.Controls.UiScale.IsScaled(b) ? Kaban.UI.Controls.UiScale.Px(pad) : pad;
            }

            if (_machineStrip != null) _machineStrip.ApplyCollapsed(collapsed);
        }

        private void SetupSimMonitorButton()
        {
            if (!SimulationMonitorForm.HasSimulatedStations()) return;

            _btnSimMonitor = new Button
            {
                Text = "",                         // glyph is DRAWN, to match the caption
                Width = 44,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Black,
                Cursor = Cursors.Hand
            };
            _btnSimMonitor.FlatAppearance.BorderSize = 0;
            _btnSimMonitor.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
            _btnSimMonitor.Paint += (s, e) => DrawMonitorGlyph(e.Graphics, _btnSimMonitor.ClientRectangle, Color.White);
            _topTips.SetToolTip(_btnSimMonitor, "Simulation Monitor (F12)");
            _btnSimMonitor.Click += (s, e) => ToggleSimulationMonitor();
            pnlTop.Controls.Add(_btnSimMonitor);
            LayoutTopBar();
        }

    }
}
