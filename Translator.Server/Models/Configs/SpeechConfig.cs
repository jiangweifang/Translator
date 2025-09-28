namespace Translator.Models.Configs
{
    public class AiSpeechConfig
    {
        public string SubscriptionKey { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public string FromLanguage { get; set; } = string.Empty;
        public List<string> ToLanguages { get; set; } = [];
    }
}
