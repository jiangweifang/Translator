using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using Translator.Service;

namespace Translator.Controllers
{
    public class TranslationHub :  IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly SynthesizerService _synthesizer;
        private readonly TranslationService _translator;
        private readonly ConcurrentQueue<byte[]> _audioQueue;
        private readonly ILogger<TranslationHub> _logger;

        public TranslationHub(IMemoryCache cache, ILogger<TranslationHub> logger, SynthesizerService synthesizer, TranslationService translation)
        {
            _audioQueue = new ConcurrentQueue<byte[]>();
            _cache = cache;
            _logger = logger;
            _synthesizer = synthesizer;
            _translator = translation;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //_ = Task.Run(() => ProcessQueueAsync(cancellationToken));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Init(string fromLang, string toLang, string voiceName)
        {
            _translator.OnRecognized += (lang, text) =>
            {
                _synthesizer.SendTranslation(text);
            };
            _synthesizer.OnAudioReceived += Synthesizer_OnAudioReceived;
            var transConfig = _translator.Initialize(fromLang, toLang);
            var synthConfig = _synthesizer.Initialize(toLang, voiceName);

            _ = _translator.Start(transConfig);
            _synthesizer.Start(synthConfig);
        }

        private void Synthesizer_OnAudioReceived(byte[] obj)
        {
            _audioQueue.Enqueue(obj);
        }

        public void Dispose()
        {
        }

        private void ProcessQueueAsync(CancellationToken token)
        {
           
        }
    }
}
