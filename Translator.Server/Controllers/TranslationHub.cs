using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Translator.Service;

namespace Translator.Controllers
{
    public class TranslationHub : Hub
    {
        private readonly IMemoryCache _cache;
        private readonly SynthesizerService _synthesizer;
        private readonly TranslationService _translator;
        private readonly ILogger<TranslationHub> _logger;
        private string? _sessionId;

        public TranslationHub(IMemoryCache cache, ILogger<TranslationHub> logger, SynthesizerService synthesizer, TranslationService translation)
        {
            _cache = cache;
            _logger = logger;
            _synthesizer = synthesizer;
            _translator = translation;
        }

        /// <summary>
        /// 开始翻译和语音合成
        /// </summary>
        /// <param name="fromLang">来自哪个语言，用于合成时选择目标的候选项</param>
        /// <param name="toLang">翻译到哪个语言，用于合成时选择目标的默认选项</param>
        /// <param name="voiceName">发音人 默认为zh-CN-XiaoxiaoMultilingualNeural </param>
        /// <returns></returns>
        public Task Start(string fromLang, string toLang, string voiceName)
        {
            // 缓存目标语言
            string cacheKey = $"transToLang::{_sessionId}";
            _cache.Set(cacheKey, toLang);
            if (string.IsNullOrEmpty(voiceName))
            {
                voiceName = "zh-CN-XiaoxiaoMultilingualNeural";
            }
            _translator.OnRecognized += (lang, text) =>
            {
                // 仅当识别语言包含目标语言时，才进行翻译和语音合成
                var transLang = _cache.Get<string>(cacheKey);
                // 这样可以挑选识别结果中指定的目标语言，进行翻译和语音合成
                if (transLang != null && lang.Contains(transLang, StringComparison.OrdinalIgnoreCase))
                {
                    Clients.Caller.SendAsync("Recognized", text);
                    _synthesizer.SendTranslation(text);
                }
            };
            _translator.OnRecognizing += (lang, text) =>
            {
                var transLang = _cache.Get<string>(cacheKey);
                if (transLang != null && lang.Contains(transLang, StringComparison.OrdinalIgnoreCase))
                {
                    Clients.Caller.SendAsync("Recognized", text);
                }
            };

            var synthConfig = _synthesizer.Initialize(voiceName);
            _ = _translator.Start([fromLang, toLang]);
            _synthesizer.Start(synthConfig);

            return Task.CompletedTask;
        }
        /// <summary>
        /// 可以通过这个方法来切换Start方法中传入的目标语言，她必须是包含在Start方法中传入的目标语言列表中的一个
        /// </summary>
        /// <param name="toLang"></param>
        /// <returns></returns>
        public Task Reversal(string toLang)
        {
            _cache.Set($"transToLang::{_sessionId}", toLang);
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            _cache.Remove($"transToLang::{_sessionId}");
            Dispose(false);
            return Task.CompletedTask;
        }

        public override Task OnConnectedAsync()
        {
            _sessionId = Context.ConnectionId;
            return base.OnConnectedAsync();
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
