using System.Text.Json.Serialization.Metadata;

namespace PoproshaykaBot.Core.Settings.Stores;

internal static class JsonStoreNullDefaults
{
    public static void Apply(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        foreach (var property in typeInfo.Properties)
        {
            var setter = property.Set;

            if (setter == null || property.IsSetNullable || property.PropertyType.IsValueType)
            {
                continue;
            }

            property.Set = (target, value) =>
            {
                if (value != null)
                {
                    setter(target, value);
                }
            };
        }
    }
}
