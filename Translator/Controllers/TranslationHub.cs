using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Translator.Service;

namespace Translator.Controllers
{
    public class TranslationHub : Hub
    {
        private readonly IMemoryCache _cache;
        private readonly SynthesizerService _synthesizer;
        private readonly TranslationService _translator;

        public TranslationHub(IMemoryCache cache,SynthesizerService synthesizer,TranslationService translation)
        {
            _cache = cache;
            _synthesizer = synthesizer;
            _translator = translation;
        }

        private static string CacheKey<T>(string connectionId) => $"{nameof(T)}::{connectionId}";

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public Task Init(string fromLang, string toLang, string voiceName)
        {
            var cid = Context.ConnectionId;
            var syntheKey = CacheKey<SynthesizerService>(cid);
            var transKey = CacheKey<TranslationService>(cid);

            _cache.Remove(transKey);
            _cache.Remove(syntheKey);


            return Clients.All.SendAsync("ReceiveMessage");
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
    }
}
