using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Sim.Core
{
    // Loads every asset of type T that has an Addressables label, for example "vehicle" or "part".
    // New content packs only need the same label, so no code changes when you add cars.
    public class ContentRegistry<T> where T : ScriptableObject, IContent
    {
        readonly Dictionary<string, T> items = new Dictionary<string, T>();

        public IReadOnlyCollection<T> All => items.Values;
        public event Action Changed;

        public async Task LoadLabelAsync(string label)
        {
            var handle = Addressables.LoadAssetsAsync<T>(label, item => items[item.Id] = item);
            await handle.Task;
            Changed?.Invoke();
        }

        public bool TryGet(string id, out T item) => items.TryGetValue(id, out item);
    }
}
