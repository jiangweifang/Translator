using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Translator.Service;

namespace Translator.Controllers
{
    public class TranslationStream :Hub
    {
        private readonly IMemoryCache _cache;
        private readonly SynthesizerStreamService _synthesizer;
        private readonly TranslationService _translator;
        private readonly ILogger<TranslationStream> _logger;

        public TranslationStream(IMemoryCache cache, ILogger<TranslationStream> logger, SynthesizerStreamService synthesizer, TranslationService translation)
        {
            _cache = cache;
            _logger = logger;
            _synthesizer = synthesizer;
            _translator = translation;
        }

        public Task Start(string fromLang, string toLang, string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName))
            {
                voiceName = "zh-CN-XiaoxiaoMultilingualNeural";
            }
            _translator.OnRecognized += (lang, text) =>
            {
                Clients.Caller.SendAsync("Recognized", text);
                _synthesizer.SendTranslation(text);
            };
            _translator.OnRecognizing += (lang, text) =>
            {
                Clients.Caller.SendAsync("Recognizing", text);
            };

            var transConfig = _translator.Initialize(fromLang, toLang);
            var synthConfig = _synthesizer.Initialize(voiceName);

            _ = _translator.Start(transConfig);
            _ = _synthesizer.Start(synthConfig);

            return Task.CompletedTask;
        }
       

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            Dispose(true);
        }

        protected override void Dispose(bool disposing)
        {
            _translator.OnRecognized -= (lang, text) => { };
            _translator.OnRecognizing -= (lang, text) => { };
            _translator.Dispose();
            _synthesizer.Dispose();
            _logger.LogWarning("TranslationStream 已释放");
            base.Dispose(disposing);
        }
    }
}
