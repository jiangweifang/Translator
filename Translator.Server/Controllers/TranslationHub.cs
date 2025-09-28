using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using Translator.Service;

namespace Translator.Controllers
{
    public class TranslationHub : Hub
    {
        private readonly IMemoryCache _cache;
        private readonly SynthesizerService _synthesizer;
        private readonly TranslationService _translator;
        private readonly ConcurrentQueue<byte[]> _audioQueue;
        private readonly ILogger<TranslationHub> _logger;
        private string? _sessionId;

        public TranslationHub(IMemoryCache cache, ILogger<TranslationHub> logger, SynthesizerService synthesizer, TranslationService translation)
        {
            _audioQueue = new ConcurrentQueue<byte[]>();
            _cache = cache;
            _logger = logger;
            _synthesizer = synthesizer;
            _translator = translation;
        }

        public Task Start(string fromLang, string toLang, string voiceName)
        {
            string cacheKey = $"transToLang::{_sessionId}";
            _cache.Set(cacheKey, toLang);
            if (string.IsNullOrEmpty(voiceName))
            {
                voiceName = "zh-CN-XiaoxiaoMultilingualNeural";
            }
            _translator.OnRecognized += (lang, text) =>
            {
                var transLang = _cache.Get<string>(cacheKey);
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
            _ = _translator.Start([fromLang, toLang], [fromLang, toLang]);
            _synthesizer.Start(synthConfig);

            return Task.CompletedTask;
        }

        public Task Reversal(string toLang)
        {
            string cacheKey = $"transToLang::{_sessionId}";
            _cache.Set(cacheKey, toLang);
            return Task.CompletedTask;
        }

        public Task Stop()
        {
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
