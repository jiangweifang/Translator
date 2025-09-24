using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Translator.Models.Configs;

namespace Translator.Service
{
    public class SynthesizerService : IDisposable
    {
        private readonly AiSpeechConfig _config;
        private readonly ILogger<SynthesizerService> _logger;
        private readonly ConcurrentQueue<string> _textQueue;
        private SpeechConfig? _speechConfig;
        private CancellationTokenSource? _cts;
        private SpeechSynthesizer? _synthesizer;
        private Connection? _connection;

        public SynthesizerService(AiSpeechConfig config, ILogger<SynthesizerService> logger)
        {
            _config = config;
            _logger = logger;
            _textQueue = new();
        }

        public void Initialize(string toLang, string voiceName = "")
        {
            _speechConfig = SpeechConfig.FromSubscription(_config.SubscriptionKey, _config.Region);
            _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);
            _speechConfig.SpeechSynthesisLanguage = toLang;
            _speechConfig.SpeechSynthesisVoiceName = voiceName;
        }

        public void Start()
        {
            if (_speechConfig is null)
            {
                throw new InvalidOperationException("SynthesizerService 没有初始化.");
            }

            // 初始化 SDK synthesizer（不指定输出，让我们获取 AudioData）
            _synthesizer = new SpeechSynthesizer(_speechConfig, null);
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
                    if (text == null) continue;

                    try
                    {
                        var result = await _synthesizer!.SpeakTextAsync(text);
                        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                        {
                            using var audioDataStream = AudioDataStream.FromResult(result);
                            byte[] buffer = new byte[16000];
                            uint filledSize = 0;
                            while ((filledSize = audioDataStream.ReadData(buffer)) > 0)
                            {
                                Console.WriteLine($"{filledSize} bytes received.");
                            }
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
