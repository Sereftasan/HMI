using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

namespace Kaban.UI.Settings
{
    // ======================================================================
    // UNDO AND REDO ON THE TRANSLATION PAGE (22 October: "Is it possible to
    // discard changes after the AI translation file import with the Ctrl+Z
    // button on top of the screen - the AI results might be too bad to get on
    // with").
    //
    // A step is either TYPED (a cell typed, a language copied into the empty
    // cells: kept until Save) or WRITTEN (an import - the AI's answer, files,
    // an older installation - or a Save: the texts in force and in their
    // files). Undo puts the last step's cells back as they were: a typed one
    // at once, a written one - asked first - written back into the files, the
    // history saying so. Redo takes an undo back. The program's steps, kept
    // while it runs (the page is built again in another language).
    // "Into the project's files" is no step: it writes the project's copies,
    // which Visual Studio's history keeps.
    // ======================================================================
    public partial class TranslationsControl
    {
        internal sealed class UndoCell
        {
            public string Owner = "", Key = "", Lang = "";
            /// <summary>A typed step: the text typed before (null: none); a written one: the text in force before.</summary>
            public string Before;
            public string After;
        }

        internal sealed class UndoStep
        {
            public string What = "";
            public bool Written;
            public readonly List<UndoCell> Cells = new List<UndoCell>();
        }

        private static readonly List<UndoStep> _undoSteps = new List<UndoStep>();
        private static readonly List<UndoStep> _redoSteps = new List<UndoStep>();
        private const int UndoDepth = 30;

        /// <summary>The steps (the tests read them).</summary>
        internal static IList<UndoStep> UndoSteps { get { return _undoSteps; } }
        internal static IList<UndoStep> RedoSteps { get { return _redoSteps; } }

        private void PushStep(UndoStep step)
        {
            if (step == null || step.Cells.Count == 0) return;
            _undoSteps.Add(step);
            if (_undoSteps.Count > UndoDepth) _undoSteps.RemoveAt(0);
            _redoSteps.Clear();
            UpdateUndoButtons();
        }

        /// <summary>A cell typed (or copied into): its typed text before and after - nothing when the same.</summary>
        private static UndoCell TypedCell(string id, string before, string after)
        {
            if (before == after) return null;
            var p = id.Split('\u0001');
            return new UndoCell { Owner = p[0], Key = p.Length > 1 ? p[1] : "", Lang = p.Length > 2 ? p[2] : "", Before = before, After = after };
        }

        /// <summary>Texts written: each cell's text before and after (the history's changes), one step.</summary>
        private void PushWritten(string what, IEnumerable<TranslationEdits.Change> changes)
        {
            var step = new UndoStep { What = what, Written = true };
            foreach (var c in changes ?? Enumerable.Empty<TranslationEdits.Change>())
                step.Cells.Add(new UndoCell { Owner = c.Owner, Key = c.Key, Lang = c.Lang, Before = c.Old ?? "", After = c.New ?? "" });
            PushStep(step);
        }

        /// <summary>The undo and redo buttons: on while there is a step, their tips saying which.</summary>
        private void UpdateUndoButtons()
        {
            if (_btnUndo == null || _btnRedo == null) return;
            _btnUndo.Enabled = _undoSteps.Count > 0;
            _btnRedo.Enabled = _redoSteps.Count > 0;
            Kaban.UI.Helpers.Shortcuts.Set(_btnUndo, System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Z, _tips, _undoSteps.Count > 0
                ? string.Format(T("Tip_TrnUndoWhat", "Undo: {0}"), Describe(_undoSteps[_undoSteps.Count - 1]))
                : T("Tip_TrnUndo", "Undo the last change: a text typed, an import, a Save"));
            Kaban.UI.Helpers.Shortcuts.Set(_btnRedo, System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Y, _tips, _redoSteps.Count > 0
                ? string.Format(T("Tip_TrnRedoWhat", "Redo: {0}"), Describe(_redoSteps[_redoSteps.Count - 1]))
                : T("Tip_TrnRedo", "Redo the change undone last"));
        }

        private static string Describe(UndoStep s)
        {
            return s.What + " (" + string.Format(T("Lbl_TrnUndoTexts", "{0} text(s)"), s.Cells.Count) + ")";
        }

        /// <summary>The last step undone (a written one asked first). False: none, or not confirmed.</summary>
        internal bool Undo()
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            if (_undoSteps.Count == 0) return false;
            var step = _undoSteps[_undoSteps.Count - 1];
            if (step.Written && !Ask.YesNo(this, T("Tab_Translations", "Translation"),
                    string.Format(T("Msg_TrnUndoWritten", "Undo {0}?\r\n\r\nThe {1} text(s) it wrote are put back as they were before - in force and in their translation files, the history saying so."),
                                  step.What, step.Cells.Count)))
                return false;
            _undoSteps.RemoveAt(_undoSteps.Count - 1);
            int n = Put(step, before: true);
            _redoSteps.Add(step);
            UpdateUndoButtons();
            SetStatus(string.Format(T("Msg_TrnUndone", "Undone: {0} - {1} text(s) back as they were."), step.What, n), false);
            return true;
        }

        /// <summary>The step undone last done again (a written one asked first).</summary>
        internal bool Redo()
        {
            if (_grid.IsCurrentCellInEditMode) _grid.EndEdit();
            if (_redoSteps.Count == 0) return false;
            var step = _redoSteps[_redoSteps.Count - 1];
            if (step.Written && !Ask.YesNo(this, T("Tab_Translations", "Translation"),
                    string.Format(T("Msg_TrnRedoWritten", "Redo {0}?\r\n\r\nIts {1} text(s) are written again - in force and in their translation files."),
                                  step.What, step.Cells.Count)))
                return false;
            _redoSteps.RemoveAt(_redoSteps.Count - 1);
            int n = Put(step, before: false);
            _undoSteps.Add(step);
            UpdateUndoButtons();
            SetStatus(string.Format(T("Msg_TrnRedone", "Done again: {0} - {1} text(s)."), step.What, n), false);
            return true;
        }

        /// <summary>A step's cells put as they were before it (or after): the typed ones typed, the written ones written.</summary>
        private int Put(UndoStep step, bool before)
        {
            int n = 0;
            if (!step.Written)
            {
                foreach (var c in step.Cells)
                {
                    string id = Id(c.Owner, c.Key, c.Lang);
                    string want = before ? c.Before : c.After;
                    if (want == null) _pending.Remove(id); else _pending[id] = want;
                    n++;
                }
                if (_pending.Count > 0) _dirty.MarkDirty(); else _dirty.Reset();
                _grid.Invalidate();
                UpdateStatus();
                return n;
            }
            var rows = _rows.ToDictionary(r => r.Owner + "\u0001" + r.Key, r => r, StringComparer.OrdinalIgnoreCase);
            var wanted = new List<TranslationEdits.Wanted>();
            foreach (var c in step.Cells)
            {
                TranslationEdits.Row row;
                if (!rows.TryGetValue(c.Owner + "\u0001" + c.Key, out row)) continue;
                wanted.Add(new TranslationEdits.Wanted { Row = row, Lang = c.Lang, Text = before ? c.Before : c.After });
                // a text typed in that cell since gives way to the one put back
                _pending.Remove(Id(c.Owner, c.Key, c.Lang));
            }
            int notWritten;
            n = TranslationEdits.Save(wanted, out notWritten, CurrentUser() + (before ? " (undo)" : " (redo)"));
            if (_pending.Count == 0) _dirty.Reset();
            _edits = TranslationEdits.Edits.ToDictionary(e => Id(e.Owner, e.Key, e.Lang), e => e, StringComparer.OrdinalIgnoreCase);
            LoadRows();
            ApplyFilter();
            return n;
        }
    }
}
