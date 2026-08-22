using System.Globalization;
using System.Reflection;
using System.Resources;

namespace osu.EzRealmSync.AppModel.Localization
{
    public static class Loc
    {
        private static readonly ResourceManager resources = new ResourceManager("osu.EzRealmSync.AppModel.Localization.Strings", Assembly.GetExecutingAssembly());

        public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.ZhHans;

        public static event Action? LanguageChanged;

        public static void SetLanguage(AppLanguage language)
        {
            CurrentLanguage = language;
            CultureInfo.CurrentUICulture = language switch
            {
                AppLanguage.En => new CultureInfo("en"),
                _ => new CultureInfo("zh-Hans"),
            };
            LanguageChanged?.Invoke();
        }

        public static string Get(string key) => resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
