﻿using Maple2Storage.Types.Metadata;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Data.Static;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.PacketHandlers.Game;

public class DungeonHandler : GamePacketHandler
{
    public override RecvOp OpCode => RecvOp.ROOM_DUNGEON;

    private enum DungeonMode : byte
    {
        ResetDungeon = 0x01,
        CreateDungeon = 0x02,
        EnterDungeonButton = 0x03,
        EnterDungeonPortal = 0x0A,
        AddRewards = 0x8,
        GetHelp = 0x10,
        Veteran = 0x11,
        Favorite = 0x19
    }

    public override void Handle(GameSession session, PacketReader packet)
    {
        DungeonMode mode = (DungeonMode) packet.ReadByte();

        switch (mode)
        {
            case DungeonMode.EnterDungeonPortal:
                HandleEnterDungeonPortal(session);
                break;
            case DungeonMode.CreateDungeon:
                HandleCreateDungeon(session, packet);
                break;
            case DungeonMode.EnterDungeonButton:
                HandleEnterDungeonButton(session);
                break;
            case DungeonMode.AddRewards:
                HandleAddRewards(session, packet);
                break;
            case DungeonMode.GetHelp:
                HandleGetHelp(session, packet);
                break;
            case DungeonMode.Veteran:
                HandleVeteran(session, packet);
                break;
            case DungeonMode.Favorite:
                HandleFavorite(session, packet);
                break;
            default:
                IPacketHandler<GameSession>.LogUnknownMode(mode);
                break;
        }
    }

    public static void HandleEnterDungeonPortal(GameSession session)
    {
        long instanceId = session.Player.InstanceId;
        DungeonSession dungeonSession = GameServer.DungeonManager.GetDungeonSessionByInstanceId(instanceId);
        if (dungeonSession == null)
        {
            return;
        }
        session.Player.Warp(dungeonSession.DungeonMapIds.First(), instanceId: dungeonSession.DungeonInstanceId);

    }

    public static void HandleCreateDungeon(GameSession session, PacketReader packet)
    {
        int dungeonId = packet.ReadInt();
        bool groupEnter = packet.ReadBool();
        Player player = session.Player;

        if (player.DungeonSessionId != -1)
        {
            session.SendNotice("Leave your current dungeon before opening another.");
            return;
        }

        int dungeonLobbyId = DungeonStorage.GetDungeonByDungeonId(dungeonId).LobbyFieldId;
        MapPlayerSpawn spawn = MapEntityMetadataStorage.GetRandomPlayerSpawn(dungeonLobbyId);

        DungeonSession dungeonSession = GameServer.DungeonManager.CreateDungeonSession(dungeonId, groupEnter ? DungeonType.Group : DungeonType.Solo);

        //TODO: Send packet that greys out enter alone / enter as party when already in a dungeon session (sendRoomDungeon packet/s).
        //the session belongs to the party leader
        if (groupEnter)
        {
            Party party = player.Party;
            if (party.DungeonSessionId != -1)
            {
                session.SendNotice("Need to reset dungeon before entering another instance");
                return;
            }
            foreach (Player member in party.Members)
            {
                if (member.DungeonSessionId != -1)
                {
                    session.SendNotice($"{member.Name} is still in a Dungeon Instance.");
                    return;
                }
            }
            party.DungeonSessionId = dungeonSession.SessionId;
            party.BroadcastPacketParty(PartyPacket.PartyHelp(dungeonId));
            //TODO: Update Party with dungeon Info via party packets (0d,0e and others are involved).
        }
        else // solo join dungeon
        {
            player.DungeonSessionId = dungeonSession.SessionId;
        }
        session.Player.Warp(dungeonLobbyId, instanceId: dungeonSession.DungeonInstanceId);
        //TODO: things after map is created here: spawn doctor npc.
        //This packet sets the banner in the dungeon that displays the dungeonname and the playersize it was created for.
        //party.BroadcastPacketParty(DungeonWaitPacket.Show(dungeonId, DungeonStorage.GetDungeonByDungeonId(dungeonId).MaxUserCount)); 
    }

    public static void HandleEnterDungeonButton(GameSession session)
    {
        Party party = session.Player.Party;
        DungeonSession dungeonSession = GameServer.DungeonManager.GetDungeonSessionBySessionId(party.DungeonSessionId);
        if (dungeonSession == null) //Can be removed when enter dungeon button is removed on dungeonsession deletion.
        {
            return;
        }
        if (dungeonSession.IsDungeonSessionMap(session.Player.MapId))
        {
            session.SendNotice("You are already in a dungeon");
            return;
        }
        session.Player.Warp(dungeonSession.DungeonLobbyId, instanceId: dungeonSession.DungeonInstanceId);
    }

    private static void HandleAddRewards(GameSession session, PacketReader packet)
    {
        int dungeonId = packet.ReadInt();

        session.Send(DungeonPacket.UpdateDungeonInfo(3, dungeonId));
        // session.Send(DungeonPacket.UpdateDungeon(dungeonId, toggle));
    }

    private static void HandleGetHelp(GameSession session, PacketReader packet)
    {
        int dungeonId = packet.ReadInt();

        if (session.Player.Party == null)
        {
            Party newParty = new(session.Player);
            GameServer.PartyManager.AddParty(newParty);

            session.Send(PartyPacket.Create(newParty, false));
            session.Send(PartyPacket.PartyHelp(dungeonId));
            MapleServer.BroadcastPacketAll(DungeonHelperPacket.BroadcastAssist(newParty, dungeonId));

            return;
        }

        Party party = session.Player.Party;

        party.BroadcastPacketParty(PartyPacket.PartyHelp(dungeonId));
        MapleServer.BroadcastPacketAll(DungeonHelperPacket.BroadcastAssist(party, dungeonId));
    }

    private static void HandleVeteran(GameSession session, PacketReader packet)
    {
        int dungeonId = packet.ReadInt();

        session.Send(DungeonPacket.UpdateDungeonInfo(4, dungeonId));
        // session.Send(DungeonPacket.UpdateDungeon(dungeonId, toggle));
    }

    private static void HandleFavorite(GameSession session, PacketReader packet)
    {
        int dungeonId = packet.ReadInt();
        byte toggle = packet.ReadByte();

        session.Send(DungeonPacket.UpdateDungeonInfo(5, dungeonId));
        // session.Send(DungeonPacket.UpdateDungeon(dungeonId, toggle));
    }
}
