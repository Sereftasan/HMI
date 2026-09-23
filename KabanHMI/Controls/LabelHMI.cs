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

        public LabelHMI() { LanguageManager.LanguageChanged += (s, e) => ApplyTranslation(); }

        public void ApplyTranslation() 
        { 
            if (!string.IsNullOrEmpty(TranslationKey)) 
                this.Text = LanguageManager.GetString(TranslationKey, this.Text); 
        }
    }
}
