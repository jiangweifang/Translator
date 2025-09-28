using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Translator.Service;

namespace Translator.Controllers
{
    /// <summary>
    /// 用流传输的方式进行翻译，这个会突然断掉最后的音频，不知道怎么解决
    /// </summary>
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
                Clients.Caller.SendAsync("Recognized", text);
            };

            var synthConfig = _synthesizer.Initialize(voiceName);
            _ = _translator.Start(["ja-JP"]);
            _ = _synthesizer.Start(synthConfig);

            return Task.CompletedTask;
        }

        public Task Stop()
        {
            Dispose(false);
            return Task.CompletedTask;
        }


        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
            Dispose(false);
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
