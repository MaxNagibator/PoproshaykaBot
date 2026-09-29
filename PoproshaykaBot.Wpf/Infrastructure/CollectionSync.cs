using System.Collections.ObjectModel;

namespace PoproshaykaBot.Wpf.Infrastructure;

public static class CollectionSync
{
    public static void SyncTo<T>(this ObservableCollection<T> collection, IReadOnlyList<T> items)
        where T : class
    {
        var common = Math.Min(collection.Count, items.Count);

        for (var index = 0; index < common; index++)
        {
            if (!ReferenceEquals(collection[index], items[index]))
            {
                collection[index] = items[index];
            }
        }

        while (collection.Count > items.Count)
        {
            collection.RemoveAt(collection.Count - 1);
        }

        for (var index = collection.Count; index < items.Count; index++)
        {
            collection.Add(items[index]);
        }
    }
}
