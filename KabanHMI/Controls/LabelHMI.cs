using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Controls
{
    public class LabelHMI : Label, ILocalizable
    {
        [Category("  Display")]
        public string TranslationKey { get; set; } = "";

        // Named, and removed in Dispose: a lambda on the static event could never
        // be unsubscribed and kept every label (and its page) alive for good.
        public LabelHMI() { LanguageManager.LanguageChanged += OnLanguageChanged; }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (!IsDisposed) ApplyTranslation();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) LanguageManager.LanguageChanged -= OnLanguageChanged;
            base.Dispose(disposing);
        }

        public void ApplyTranslation() 
        { 
            if (!string.IsNullOrEmpty(TranslationKey)) 
                this.Text = LanguageManager.GetString(TranslationKey, this.Text); 
        }
    }
}
