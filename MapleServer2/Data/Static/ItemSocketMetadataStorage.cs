﻿using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using ProtoBuf;

namespace MapleServer2.Data.Static;

public static class ItemSocketMetadataStorage
{
    private static readonly Dictionary<int, ItemSocketMetadata> ItemSocketMetadatas = new();

    public static void Init()
    {
        using FileStream stream = File.OpenRead($"{Paths.RESOURCES_DIR}/ms2-item-socket-metadata");
        List<ItemSocketMetadata> items = Serializer.Deserialize<List<ItemSocketMetadata>>(stream);
        foreach (ItemSocketMetadata item in items)
        {
            ItemSocketMetadatas[item.Id] = item;
        }
    }

    public static ItemSocketMetadata GetMetadata(int socketDataId)
    {
        return ItemSocketMetadatas.GetValueOrDefault(socketDataId);
    }
}
