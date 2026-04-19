using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace StudioPoseBridge.Game
{
    public sealed class CheckpointStore
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, object>> _data =
            new ConcurrentDictionary<string, Dictionary<string, object>>();

        private static string Key(int characterId, string name)
        {
            return characterId + "\u001f" + name;
        }

        public void Put(int characterId, string name, Dictionary<string, object> snapshot)
        {
            _data[Key(characterId, name)] = snapshot;
        }

        public bool TryGet(int characterId, string name, out Dictionary<string, object> snapshot)
        {
            return _data.TryGetValue(Key(characterId, name), out snapshot);
        }

        public void Remove(int characterId, string name)
        {
            Dictionary<string, object> removed;
            _data.TryRemove(Key(characterId, name), out removed);
        }
    }
}
