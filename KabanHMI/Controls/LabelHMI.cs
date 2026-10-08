using System;
using System.ComponentModel;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Controls
{

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
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

        /// <summary>
        /// Translated when it is shown, too: it used to wait for the language to
        /// CHANGE, so a page opened in Turkish showed its headings in English.
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTranslation();
        }

        public void ApplyTranslation() 
        { 
            if (!string.IsNullOrEmpty(TranslationKey)) 
                this.Text = LanguageManager.GetString(TranslationKey, this.Text); 
        }
    }
}
