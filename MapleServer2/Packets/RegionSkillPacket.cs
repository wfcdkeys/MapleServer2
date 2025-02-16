﻿using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Constants;
using MapleServer2.Types;

namespace MapleServer2.Packets;

public static class RegionSkillPacket
{
    private enum RegionSkillMode : byte
    {
        Add = 0x0,
        Remove = 0x1
    }

    public static PacketWriter Send(SkillCast skill)
    {
        PacketWriter pWriter = PacketWriter.Of(SendOp.REGION_SKILL);

        pWriter.Write(RegionSkillMode.Add);
        pWriter.WriteInt(skill.SkillObjectId);
        pWriter.WriteInt(skill.CasterObjectId);
        pWriter.WriteInt(skill.ServerTick);
        pWriter.WriteByte((byte) skill.EffectCoords.Count);
        if (skill.EffectCoords.Count == 0)
        {
            return pWriter;
        }

        foreach (CoordF effectCoord in skill.EffectCoords)
        {
            pWriter.Write(effectCoord);
        }

        pWriter.WriteInt(skill.SkillId);
        pWriter.WriteShort(skill.SkillLevel);
        pWriter.WriteFloat();
        pWriter.WriteFloat();

        return pWriter;
    }

    public static PacketWriter Remove(int sourceObjectId)
    {
        PacketWriter pWriter = PacketWriter.Of(SendOp.REGION_SKILL);
        pWriter.Write(RegionSkillMode.Remove);
        pWriter.WriteInt(sourceObjectId); // Uid regionEffect
        return pWriter;
    }
}
