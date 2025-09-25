using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using System.Collections.Concurrent;
using System.Threading.Channels;
using Translator.Service;

namespace Translator.Controllers
{
    public class TranslationHub : IHostedService, IDisposable
    {
        private readonly IMemoryCache _cache;
        private readonly SynthesizerService _synthesizer;
        private readonly TranslationService _translator;
        private readonly AudioFormat _audioFormat;
        private readonly MiniAudioEngine _engine;
        private DeviceInfo? _device;
        private readonly ConcurrentQueue<MemoryStream> _audioQueue;
        private readonly ILogger<TranslationHub> _logger;

        public TranslationHub(IMemoryCache cache, ILogger<TranslationHub> logger, SynthesizerService synthesizer, TranslationService translation)
        {
            _audioQueue = new ConcurrentQueue<MemoryStream>();
            _cache = cache;
            _logger = logger;
            _synthesizer = synthesizer;
            _translator = translation;
            _engine = new MiniAudioEngine();
            _audioFormat = new AudioFormat
            {
                SampleRate = 16000,
                Channels = 1,
                Format = SampleFormat.S16
            };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _device = _engine.PlaybackDevices.FirstOrDefault(x => x.IsDefault);
            _ = Task.Run(() => ProcessQueueAsync(cancellationToken));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Init(string fromLang, string toLang, string voiceName)
        {
            _translator.OnTranslationReceived += (lang, text) =>
            {
                _synthesizer.SendTranslation(text);
            };
            _synthesizer.OnAudioReceived += Synthesizer_OnAudioReceived;
            var transConfig = _translator.Initialize(fromLang, toLang);
            var synthConfig = _synthesizer.Initialize(toLang, voiceName);

            _ = _translator.Start(transConfig);
            _synthesizer.Start(synthConfig);
        }

        private void Synthesizer_OnAudioReceived(MemoryStream obj)
        {
            _audioQueue.Enqueue(obj);
        }

        public void Dispose()
        {
            _engine.Dispose();
        }

        private void ProcessQueueAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_audioQueue.TryDequeue(out MemoryStream? audio))
                {
                    if (audio == null) continue;
                    using var dataProvider = new StreamDataProvider(_engine, _audioFormat, audio);
                    using var soundPlayer = new SoundPlayer(_engine, _audioFormat, dataProvider);
                    _logger.LogInformation("播放音频（队列消费）");
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
    }
}
