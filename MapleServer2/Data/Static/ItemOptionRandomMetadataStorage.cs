﻿using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using ProtoBuf;

namespace MapleServer2.Data.Static;

public static class ItemOptionRandomMetadataStorage
{
    private static readonly Dictionary<int, ItemOptionRandomMetadata> ItemOptionRandom = new();

    public static void Init()
    {
        using FileStream stream = File.OpenRead($"{Paths.RESOURCES_DIR}/ms2-item-option-random-metadata");
        List<ItemOptionRandomMetadata> items = Serializer.Deserialize<List<ItemOptionRandomMetadata>>(stream);
        foreach (ItemOptionRandomMetadata item in items)
        {
            ItemOptionRandom[item.Id] = item;
        }
    }

    public static bool IsValid(int id)
    {
        return ItemOptionRandom.ContainsKey(id);
    }

    public static ItemOptionRandom GetMetadata(int id, int rarity)
    {
        ItemOptionRandomMetadata metadata = ItemOptionRandom.Values.FirstOrDefault(x => x.Id == id);
        if (metadata == null)
        {
            return null;
        }
        return metadata.ItemOptions.FirstOrDefault(x => x.Rarity == rarity);
    }
}
