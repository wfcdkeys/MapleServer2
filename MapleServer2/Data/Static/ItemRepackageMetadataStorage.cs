﻿using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using ProtoBuf;

namespace MapleServer2.Data.Static;

public static class ItemRepackageMetadataStorage
{
    private static readonly Dictionary<int, ItemRepackageMetadata> ItemsRepackageMetadatas = new();

    public static void Init()
    {
        using FileStream stream = File.OpenRead($"{Paths.RESOURCES_DIR}/ms2-item-repackage-metadata");
        List<ItemRepackageMetadata> items = Serializer.Deserialize<List<ItemRepackageMetadata>>(stream);
        foreach (ItemRepackageMetadata item in items)
        {
            ItemsRepackageMetadatas[item.Id] = item;
        }
    }

    public static bool ItemCanRepackage(int functionId, int itemLevel, int rarity)
    {
        ItemRepackageMetadata metadata = ItemsRepackageMetadatas.GetValueOrDefault(functionId);
        if (itemLevel < metadata.MinLevel || itemLevel > metadata.MaxLevel)
        {
            return false;
        }

        if (!metadata.Rarities.Contains(rarity))
        {
            return false;
        }

        // TODO: Check if slot is valid. Unsure where slot values are assigned in each item
        return true;
    }
}
