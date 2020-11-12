using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SongRequestManagerV2.Extentions
{
    public static class BlockingCollectionExtention
    {
        public static void Clear<T>(this BlockingCollection<T> collection)
        {
            while (collection.Any()) {
                collection.TryTake(out _);
            }
        }

        public static void AddRange<T>(this BlockingCollection<T> collection, IEnumerable<T> items)
        {
            foreach (var item in items) {
                collection.Add(item);
            }
        }
    }
}
