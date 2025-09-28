using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Collections.Concurrent;
using System.Data.Common;
using Translator.Models.Configs;

namespace Translator.Service
{
    /// <summary>
    /// 这是一个持续的识别服务
    /// </summary>
    public class SynthesizerStreamService: IDisposable
    {
        private readonly AiSpeechConfig _config;
        private readonly ILogger<SynthesizerStreamService> _logger;
        private readonly ConcurrentQueue<string> _textQueue;
        private SpeechSynthesizer? _synthesizer;
        private CancellationTokenSource? _cts;

        public event Action<MemoryStream>? OnAudioReceived;
        public SynthesizerStreamService(AiSpeechConfig config, ILogger<SynthesizerStreamService> logger)
        {
            _config = config;
            _logger = logger;
            _textQueue = new();
        }

        public SpeechConfig Initialize(string voiceName = "zh-CN-XiaoxiaoMultilingualNeural")
        {
            var ttsEndpoint = $"wss://{_config.Region}.tts.speech.microsoft.com/cognitiveservices/websocket/v2";
            SpeechConfig speechConfig = SpeechConfig.FromEndpoint(
                new Uri(ttsEndpoint),
                _config.SubscriptionKey);
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw16Khz16BitMonoPcm);
            // set a voice name
            speechConfig.SetProperty(PropertyId.SpeechServiceConnection_SynthVoice, voiceName);

            // set timeout value to bigger ones to avoid sdk cancel the request when GPT latency too high
            speechConfig.SetProperty(PropertyId.SpeechSynthesis_FrameTimeoutInterval, "10000");
            speechConfig.SetProperty(PropertyId.SpeechSynthesis_RtfTimeoutThreshold, "10");
            return speechConfig;
        }

        public Task Start(SpeechConfig speechConfig)
        {
            _cts = new CancellationTokenSource();
            _synthesizer = new SpeechSynthesizer(speechConfig, AudioConfig.FromDefaultSpeakerOutput());
            var request = new SpeechSynthesisRequest(SpeechSynthesisRequestInputType.TextStream);
            _synthesizer.StartSpeakingAsync(request);
            _ = Task.Run(() => TransResponse(request, _cts.Token));
            return Task.CompletedTask;
        }

        private async Task TransResponse(SpeechSynthesisRequest request, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_textQueue.TryDequeue(out string? text))
                {
                    if (string.IsNullOrEmpty(text))
                    {
                        await Task.Delay(20, token).ConfigureAwait(false);
                        continue;
                    };
                    request.InputStream.Write(text);
                    request.InputStream.Write("\n");
                }
                else
                {
                    await Task.Delay(20, token).ConfigureAwait(false); ;
                }
            }
            request.InputStream.Close();
            request.Dispose();
        }

        public void Dispose()
        {
            _textQueue.Clear();
            _cts?.Cancel();
            _synthesizer?.StopSpeakingAsync();
            _synthesizer?.Dispose();
            GC.SuppressFinalize(this);
        }

        public void SendTranslation(string text)
        {
            if (_synthesizer is null)
            {
                throw new InvalidOperationException("SynthesizerService 没有启动.");
            }
            _textQueue.Enqueue(text);
        }
    }
}
