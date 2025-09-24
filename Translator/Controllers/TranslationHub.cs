using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;

namespace Translator.Controllers
{
    public class TranslationHub : Hub
    {
        private readonly IMemoryCache _cache;
        public TranslationHub(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static string CacheKey(string connectionId) => $"conn::{connectionId}";

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }

        public Task Init(string message)
        {
            var cid = Context.ConnectionId;
            var key = CacheKey(cid);



            return Clients.All.SendAsync("ReceiveMessage", message);
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
    }
}
