﻿using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using ProtoBuf;

namespace MapleServer2.Data.Static;

public static class ItemDropMetadataStorage
{
    private static readonly Dictionary<int, ItemDropMetadata> Drops = new();

    public static void Init()
    {
        using FileStream stream = File.OpenRead($"{Paths.RESOURCES_DIR}/ms2-item-drop-metadata");
        List<ItemDropMetadata> items = Serializer.Deserialize<List<ItemDropMetadata>>(stream);
        foreach (ItemDropMetadata item in items)
        {
            Drops[item.Id] = item;
        }
    }

    public static ItemDropMetadata GetItemDropMetadata(int boxId)
    {
        return Drops.GetValueOrDefault(boxId);
    }
}
