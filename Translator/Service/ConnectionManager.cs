using System.Collections.Concurrent;

namespace Translator.Service
{
    public class ConnectionContext
    {
        public SynthesizerService? Synthesizer { get; set; }
        public TranslationService? Translation { get; set; }
    }

    public class ConnectionManager
    {
        private readonly ConcurrentDictionary<string, ConnectionContext> _map = new();

        public void Add(string connectionId, ConnectionContext context)
        {
            _map[connectionId] = context;
        }

        public bool TryGet(string connectionId, out ConnectionContext? context)
        {
            return _map.TryGetValue(connectionId, out context);
        }

        public void Remove(string connectionId)
        {
            _map.TryRemove(connectionId, out _);
        }
    }
}