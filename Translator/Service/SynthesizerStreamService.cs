using Microsoft.CognitiveServices.Speech;
using System.Collections.Concurrent;
using Translator.Models.Configs;

namespace Translator.Service
{
    /// <summary>
    /// 这是一个持续的识别服务
    /// </summary>
    public class SynthesizerStreamService
    {
        private readonly AiSpeechConfig _config;
        private readonly ILogger<SynthesizerStreamService> _logger;
        private readonly ConcurrentQueue<string> _textQueue;
        private SpeechSynthesizer? _synthesizer;
        private CancellationTokenSource? _cts;
        private static object consoleLock = new();

        public SynthesizerStreamService(AiSpeechConfig config, ILogger<SynthesizerStreamService> logger)
        {
            _config = config;
            _logger = logger;
            _textQueue = new();
        }

        public SpeechConfig Initialize(string toLang, string voiceName = "")
        {
            var ttsEndpoint = $"wss://{_config.Region}.tts.speech.microsoft.com/cognitiveservices/websocket/v2";
            SpeechConfig speechConfig = SpeechConfig.FromEndpoint(
                new Uri(ttsEndpoint),
                _config.SubscriptionKey);
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);
            // set a voice name
            speechConfig.SetProperty(PropertyId.SpeechServiceConnection_SynthVoice, voiceName);

            // set timeout value to bigger ones to avoid sdk cancel the request when GPT latency too high
            speechConfig.SetProperty(PropertyId.SpeechSynthesis_FrameTimeoutInterval, "10000");
            speechConfig.SetProperty(PropertyId.SpeechSynthesis_RtfTimeoutThreshold, "10");
            return speechConfig;
        }

        public async Task Start(SpeechConfig speechConfig)
        {
            _cts = new CancellationTokenSource();
            _synthesizer = new SpeechSynthesizer(speechConfig);
            using var request = new SpeechSynthesisRequest(SpeechSynthesisRequestInputType.TextStream);
            var audioData = new MemoryStream();
            var ttsTask = await _synthesizer.StartSpeakingAsync(request);
            var audioTask = Task.Run(() => ReadAudioStream(ttsTask, audioData, _cts.Token));
            var transTask = TransResponse(request, _cts.Token);

            await transTask;
            await audioTask;
        }

        private Task TransResponse(SpeechSynthesisRequest request, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_textQueue.TryDequeue(out string? text))
                {
                    request.InputStream.Write(text);
                }
            }
            request.InputStream.Close();
            return Task.CompletedTask;
        }


        private static void ReadAudioStream(SpeechSynthesisResult ttsTask, MemoryStream audioData, CancellationToken token)
        {
            using var audioDataStream = AudioDataStream.FromResult(ttsTask);
            byte[] buffer = new byte[32000];
            uint totalSize = 0;
            uint totalRead = 0;
            while (!token.IsCancellationRequested)
            {
                uint readSize = audioDataStream.ReadData(buffer);
                if (readSize == 0)
                {
                    continue;
                }

                totalRead += readSize;
                totalSize += readSize;
                audioData.Write(buffer, 0, (int)readSize);
            }
        }
    }
}
