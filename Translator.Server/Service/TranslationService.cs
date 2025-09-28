using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using Translator.Models.Configs;

namespace Translator.Service
{
    public class TranslationService : IDisposable
    {
        private readonly AiSpeechConfig _config;
        private readonly ILogger<TranslationService> _logger;

        private TaskCompletionSource<int>? _stopTranslation;

        public event Action<string, string>? OnRecognizing;
        public event Action<string, string>? OnRecognized;

        public TranslationService(AiSpeechConfig config, ILogger<TranslationService> logger)
        {
            _config = config;
            _logger = logger;
        }
        /// <summary>
        /// 指定目标语言开始翻译
        /// </summary>
        /// <param name="toLang">多个目标语言的原因是在初始化之后可以选择返回的语言类型</param>
        /// <returns></returns>
        public async Task Start(string[] toLang)
        {
            _stopTranslation = new();
            //使用无源语言候选项的多语言语音翻译 [https://learn.microsoft.com/zh-cn/azure/ai-services/speech-service/how-to-translate-speech?tabs=terminal&pivots=programming-language-csharp#multi-lingual-speech-translation-without-source-language-candidates]
            var v2EndpointInString = string.Format("wss://{0}.stt.speech.microsoft.com/speech/universal/v2", _config.Region);
            var v2EndpointUrl = new Uri(v2EndpointInString);
            var speechConfig = SpeechTranslationConfig.FromEndpoint(v2EndpointUrl, _config.SubscriptionKey);

            // Add the target languages you want to translate to
            foreach(var to in toLang)
            {
                speechConfig.AddTargetLanguage(to);
            }
            speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "200");
            //自动识别源语言
            var autoDetectSourceLanguageConfig = AutoDetectSourceLanguageConfig.FromOpenRange();
            //使用服务器的默认麦克风作为音频输入
            using var audioInput = AudioConfig.FromDefaultMicrophoneInput();
            using var translationRecognizer = new TranslationRecognizer(speechConfig, autoDetectSourceLanguageConfig, audioInput);
            // Subscribes to events.
            translationRecognizer.Recognizing += (s, e) =>
            {
                _logger.LogInformation($"RECOGNIZING in 'zh-CN': Text={e.Result.Text}");
                foreach (var element in e.Result.Translations)
                {
                    _logger.LogInformation($"TRANSLATING into '{element.Key}': {element.Value}");
                    OnRecognizing?.Invoke(element.Key, element.Value);
                }
            };

            translationRecognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.TranslatedSpeech)
                {
                    _logger.LogInformation($"RECOGNIZED in 'zh-CN': Text={e.Result.Text}");
                    foreach (var element in e.Result.Translations)
                    {
                        // 此处会收到多种语言的翻译结果，可以根据需要进行处理
                        _logger.LogInformation($"TRANSLATED into '{element.Key}': {element.Value}");
                        OnRecognized?.Invoke(element.Key, element.Value);
                    }
                }
                else if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    _logger.LogInformation($"RECOGNIZED: Text={e.Result.Text}");
                    _logger.LogInformation($"Speech not translated.");
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    _logger.LogInformation($"NOMATCH: Speech could not be recognized.");
                }
            };

            translationRecognizer.Canceled += (s, e) =>
            {
                _logger.LogInformation($"CANCELED: Reason={e.Reason}");
                if (e.Reason == CancellationReason.Error)
                {
                    _logger.LogInformation($"CANCELED: ErrorCode={e.ErrorCode}");
                    _logger.LogInformation($"CANCELED: ErrorDetails={e.ErrorDetails}");
                    _logger.LogInformation($"CANCELED: Did you update the subscription info?");
                }
                _stopTranslation!.TrySetResult(0);
            };
            translationRecognizer.SessionStopped += (s, e) =>
            {
                _logger.LogInformation("\n Session stopped event.");
                _stopTranslation!.TrySetResult(0);
            };

            // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
            _logger.LogInformation("Start translation...");
            await translationRecognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

            // Waits for completion.
            // Use Task.WaitAny to keep the task rooted.
            Task.WaitAny([_stopTranslation!.Task]);

            // Stops translation.
            await translationRecognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        }

        public void Dispose()
        {
            _stopTranslation?.TrySetResult(0);
            GC.SuppressFinalize(this);
        }
    }
}
