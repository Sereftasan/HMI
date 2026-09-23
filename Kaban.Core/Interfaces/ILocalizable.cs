namespace Kaban.Core.Interfaces
{
    public interface ILocalizable
    {
        // The dictionary key (e.g. "Btn_Start")
        string TranslationKey { get; set; }

        // A method to force the control to refresh its text
        void ApplyTranslation();
    }
}
