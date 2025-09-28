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
        public SpeechTranslationConfig Initialize(string fromLang, string toLang)
        {
            _stopTranslation = new();
            SpeechTranslationConfig speechConfig = SpeechTranslationConfig.FromSubscription(_config.SubscriptionKey, _config.Region);
            // Set the source language
            speechConfig.SpeechRecognitionLanguage = fromLang;
            // Add the target languages you want to translate to
            speechConfig.AddTargetLanguage(toLang);
            speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "200");
            return speechConfig;
        }

        public async Task Start(SpeechTranslationConfig speechConfig)
        {
            using var audioInput = AudioConfig.FromDefaultMicrophoneInput();
            using var translationRecognizer = new TranslationRecognizer(speechConfig, audioInput);
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
