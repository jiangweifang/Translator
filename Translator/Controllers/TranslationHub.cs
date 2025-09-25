using Microsoft.Extensions.Caching.Memory;
using SoundFlow.Abstracts.Devices;
using SoundFlow.Backends.MiniAudio;
using SoundFlow.Components;
using SoundFlow.Enums;
using SoundFlow.Providers;
using SoundFlow.Structs;
using System.Collections.Concurrent;
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
        private readonly ConcurrentQueue<byte[]> _audioQueue;
        private readonly ILogger<TranslationHub> _logger;
        private AudioPlaybackDevice? _playbackDevice;

        public TranslationHub(IMemoryCache cache, ILogger<TranslationHub> logger, SynthesizerService synthesizer, TranslationService translation)
        {
            _audioQueue = new ConcurrentQueue<byte[]>();
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
            _playbackDevice = _engine.InitializePlaybackDevice(_device, _audioFormat);
            _playbackDevice.Start();
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

        private void Synthesizer_OnAudioReceived(byte[] obj)
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
                if (_audioQueue.TryDequeue(out byte[]? audio))
                {
                    if (audio == null) continue;
                    using var dataProvider = new RawDataProvider(audio, SampleFormat.S16, 16000, 1);
                    using var soundPlayer = new SoundPlayer(_engine, _audioFormat, dataProvider);
                    _playbackDevice!.MasterMixer.AddComponent(soundPlayer);
                    soundPlayer.Play();
                    while (soundPlayer.State == PlaybackState.Playing)
                    {
                        Thread.Sleep(100);
                    }
                    _playbackDevice!.MasterMixer.RemoveComponent(soundPlayer);

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
