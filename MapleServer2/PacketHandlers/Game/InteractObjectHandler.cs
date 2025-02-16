﻿using Maple2Storage.Enums;
using Maple2Storage.Tools;
using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Enums;
using MapleServer2.Managers;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

internal class InteractObjectHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.INTERACT_OBJECT;

    private enum InteractObjectMode : byte
    {
        Cast = 0x0B,
        Interact = 0x0C
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        InteractObjectMode mode = (InteractObjectMode) packet.ReadByte();

        switch (mode)
        {
            case InteractObjectMode.Cast:
                HandleCast(session, packet);
                break;
            case InteractObjectMode.Interact:
                HandleInteract(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(mode);
                break;
        }
    }

    private static void HandleCast(GameSession session, PacketReader packet)
    {
        string id = packet.ReadString();
        session.FieldManager.State.InteractObjects.TryGetValue(id, out InteractObject interactObject);
        if (interactObject == null)
        {
            return;
        }

        // TODO: Change state of object only if player succeeds in the cast.
    }

    private static void HandleInteract(GameSession session, PacketReader packet)
    {
        Player player = session.Player;

        string id = packet.ReadString();
        session.FieldManager.State.InteractObjects.TryGetValue(id, out InteractObject interactObject);
        if (interactObject == null)
        {
            return;
        }

        InteractObjectMetadata metadata = InteractObjectMetadataStorage.GetInteractObjectMetadata(interactObject.InteractId);

        switch (interactObject.Type)
        {
            case InteractObjectType.Binoculars:
                session.Send(InteractObjectPacket.Use(interactObject));
                QuestHelper.UpdateExplorationQuest(session, interactObject.InteractId.ToString(), "interact_object_rep");
                break;
            case InteractObjectType.Ui:
                session.Send(InteractObjectPacket.Use(interactObject));
                break;
            case InteractObjectType.RankBoard:
                session.Send(WebOpenPacket.Open(metadata.Web.Url));
                break;
            case InteractObjectType.AdBalloon:
                session.Send(PlayerHostPacket.AdBalloonWindow((AdBalloon) interactObject));
                break;
            case InteractObjectType.Gathering:
                GatheringHelper.HandleGathering(session, metadata.Gathering.RecipeId, out int numDrop);
                session.Send(InteractObjectPacket.Use(interactObject, (short) (numDrop > 0 ? 0 : 1), numDrop));
                break;
            case InteractObjectType.Common:
                // Unsure if all interact objects need to be set as disabled.
                interactObject.State = InteractObjectState.Disable;

                session.Send(InteractObjectPacket.Update(interactObject));
                session.Send(InteractObjectPacket.Interact(interactObject));

                foreach ((int questId, QuestState state) in metadata.Quests)
                {
                    if (!player.QuestData.TryGetValue(questId, out QuestStatus questStatus) || questStatus.State != state)
                    {
                        continue;
                    }

                    QuestHelper.UpdateQuest(session, interactObject.InteractId.ToString(), "interact_object");

                    interactObject.State = InteractObjectState.Activated;
                    session.Send(InteractObjectPacket.Update(interactObject));
                }

                DropItems();

                TrophyManager.OnObjectInteract(player, interactObject.InteractId);

                if (interactObject is MapChest)
                {
                    // Unsure if setting as activated is specific of map chests
                    interactObject.State = InteractObjectState.Activated;

                    // Delayed removal of the chest
                    Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        session.FieldManager.State.RemoveInteractObject(interactObject.Id);

                        session.FieldManager.BroadcastPacket(InteractObjectPacket.Update(interactObject));
                        session.FieldManager.BroadcastPacket(InteractObjectPacket.Remove(interactObject));
                    });
                }

                return;
        }

        session.Send(InteractObjectPacket.Interact(interactObject));

        void DropItems()
        {
            foreach (int boxId in metadata.Drop.IndividualDropBoxId)
            {
                ItemDropMetadata itemDropMetadataStorage = ItemDropMetadataStorage.GetItemDropMetadata(boxId);
                if (itemDropMetadataStorage is null)
                {
                    continue;
                }

                foreach (DropGroup dropGroup in itemDropMetadataStorage.DropGroups)
                {
                    foreach (DropGroupContent dropGroupContent in dropGroup.Contents)
                    {
                        foreach (int itemId in dropGroupContent.ItemIds)
                        {
                            Item item = new(itemId)
                            {
                                Amount = RandomProvider.Get().Next((int) dropGroupContent.MinAmount, (int) dropGroupContent.MaxAmount),
                                Rarity = dropGroupContent.Rarity
                            };

                            session.FieldManager.AddItem(session, item);
                        }
                    }
                }
            }

            foreach (int boxId in metadata.Drop.GlobalDropBoxId)
            {
                ItemDropMetadata itemDropMetadataStorage = ItemDropMetadataStorage.GetItemDropMetadata(boxId);
                if (itemDropMetadataStorage is null)
                {
                    continue;
                }

                foreach (DropGroup dropGroup in itemDropMetadataStorage.DropGroups)
                {
                    foreach (DropGroupContent dropGroupContent in dropGroup.Contents)
                    {
                        foreach (int itemId in dropGroupContent.ItemIds)
                        {
                            Item item = new(itemId)
                            {
                                Amount = RandomProvider.Get().Next((int) dropGroupContent.MinAmount, (int) dropGroupContent.MaxAmount),
                                Rarity = dropGroupContent.Rarity
                            };

                            session.FieldManager.AddItem(session, item);
                        }
                    }
                }
            }
        }
    }
}
