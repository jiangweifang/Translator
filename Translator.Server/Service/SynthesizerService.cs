using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.IO;
using Translator.Models.Configs;

namespace Translator.Service
{
    public class SynthesizerService : IDisposable
    {
        private readonly AiSpeechConfig _config;
        private readonly ILogger<SynthesizerService> _logger;
        private readonly ConcurrentQueue<string> _textQueue;
        private CancellationTokenSource? _cts;
        private SpeechSynthesizer? _synthesizer;
        private Connection? _connection;

        public event Action<byte[]>? OnAudioReceived;

        public SynthesizerService(AiSpeechConfig config, ILogger<SynthesizerService> logger)
        {
            _config = config;
            _logger = logger;
            _textQueue = new();
        }

        public SpeechConfig Initialize(string toLang, string voiceName = "")
        {
            SpeechConfig speechConfig = SpeechConfig.FromSubscription(_config.SubscriptionKey, _config.Region);
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);
            speechConfig.SpeechSynthesisLanguage = toLang;
            speechConfig.SpeechSynthesisVoiceName = voiceName;
            return speechConfig;
        }

        public void Start(SpeechConfig speechConfig)
        {
            // 初始化 SDK synthesizer（不指定输出，让我们获取 AudioData）
            _synthesizer = new SpeechSynthesizer(speechConfig, AudioConfig.FromDefaultSpeakerOutput());
            _connection = Connection.FromSpeechSynthesizer(_synthesizer);
            _connection.Open(true);

            // 初始化 OpenAL 上下文
            //InitOpenAl();

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => ProcessQueueAsync(_cts.Token));
        }

        private async Task ProcessQueueAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_textQueue.TryDequeue(out string? text))
                {
                    if (string.IsNullOrEmpty(text)) continue;

                    try
                    {
                        var result = await _synthesizer!.SpeakTextAsync(text);
                        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                        {
                            OnAudioReceived?.Invoke(result.AudioData);
                        }
                        else if (result.Reason == ResultReason.Canceled)
                        {
                            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
                            _logger.LogError("Speech synthesis canceled: {0}", cancellation?.ErrorDetails);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "合成或播放过程中发生错误");
                    }
                }
                else
                {
                    Thread.Sleep(20);
                }
            }
        }

        /// <summary>
        /// 这个方法应该是保存到一个队列中
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public void SendTranslation(string text)
        {
            if (_synthesizer is null || _connection is null)
            {
                throw new InvalidOperationException("SynthesizerService 没有启动.");
            }
            _textQueue.Enqueue(text);
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _connection?.Close();
            _connection?.Dispose();
            _synthesizer?.Dispose();
            GC.SuppressFinalize(this);
            //DisposeOpenAl();
        }
    }
}
