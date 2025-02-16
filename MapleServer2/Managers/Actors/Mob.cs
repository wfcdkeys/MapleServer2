﻿using Maple2Storage.Enums;
using Maple2Storage.Tools;
using Maple2Storage.Types;
using Maple2Storage.Types.Metadata;
using MapleServer2.Data.Static;
using MapleServer2.Enums;
using MapleServer2.PacketHandlers.Game.Helpers;
using MapleServer2.Packets;
using MapleServer2.Servers.Game;
using MapleServer2.Types;

namespace MapleServer2.Managers;

public partial class FieldManager
{
    private static readonly Random Rand = RandomProvider.Get();

    private partial class Mob : FieldActor<NpcMetadata>, INpc
    {
        private readonly MobAI AI;
        public IFieldObject<MobSpawn> OriginSpawn;

        private CoordF SpawnDistance;

        public NpcState State { get; set; }
        public NpcAction Action { get; set; }
        public MobMovement Movement { get; set; }
        public IFieldActor<Player> Target;

        public Mob(int objectId, int mobId) : this(objectId, NpcMetadataStorage.GetNpcMetadata(mobId)) { }

        public Mob(int objectId, NpcMetadata metadata) : base(objectId, metadata)
        {
            Animation = AnimationStorage.GetSequenceIdBySequenceName(metadata.Model, "Idle_A");
            AI = MobAIManager.GetAI(metadata.AiInfo);
            Stats = new(metadata);
            State = NpcState.Normal;
        }

        public void Attack()
        {
            int roll = RandomProvider.Get().Next(100);
            for (int i = 0; i < Value.NpcMetadataSkill.SkillIds.Length; i++)
            {
                if (roll < Value.NpcMetadataSkill.SkillProbs[i])
                {
                    // Rolled this skill.
                    Cast(new(Value.NpcMetadataSkill.SkillIds[i], Value.NpcMetadataSkill.SkillLevels[i]));
                    StartSkillTimer((Value.NpcMetadataSkill.SkillCooldown > 0) ? Value.NpcMetadataSkill.SkillCooldown : 1000);
                }

                roll -= Value.NpcMetadataSkill.SkillProbs[i];
            }
        }

        private Task StartSkillTimer(int cooldownMilliseconds)
        {
            return Task.Run(async () =>
            {
                await Task.Delay(cooldownMilliseconds);

                OnCooldown = false;
            });
        }

        public void Act()
        {
            if (AI == null)
            {
                return;
            }

            (string actionName, NpcAction actionType) = AI.GetAction(this);

            if (actionName != null)
            {
                Animation = AnimationStorage.GetSequenceIdBySequenceName(Value.Model, actionName);
            }

            Action = actionType;
            Movement = AI.GetMovementAction(this);

            switch (Action)
            {
                case NpcAction.Idle:
                case NpcAction.Bore:
                    Move(MobMovement.Hold); // temp, maybe remove the option to specify movement in AI
                    break;
                case NpcAction.Walk:
                case NpcAction.Run:
                    Move(Movement);
                    break;
                case NpcAction.Skill:
                    // Cast skill
                    if (!OnCooldown)
                    {
                        Attack();
                        Move(MobMovement.Hold);
                        break;
                    }

                    Move(Movement);
                    break;
                case NpcAction.Jump:
                default:
                    break;
            }
        }

        public void Move(MobMovement moveType)
        {
            Random rand = RandomProvider.Get();

            switch (moveType)
            {
                case MobMovement.Patrol:
                    // Fallback Dummy Movement
                    int moveDistance = rand.Next(0, Value.MoveRange);
                    short moveDir = (short) rand.Next(-1800, 1800);

                    Velocity = CoordF.From(moveDistance, moveDir);
                    // Keep near spawn
                    if ((SpawnDistance - Velocity).Length() >= Block.BLOCK_SIZE * 2)
                    {
                        moveDir = (short) SpawnDistance.XYAngle();
                        Velocity = CoordF.From(Block.BLOCK_SIZE, moveDir);
                    }

                    LookDirection = moveDir; // looking direction of the monster
                    break;
                case MobMovement.Follow: // move towards target
                    Velocity = CoordF.From(0, 0, 0);
                    break;
                case MobMovement.Strafe: // move around target
                case MobMovement.Run: // move away from target
                case MobMovement.LookAt:
                case MobMovement.Hold:
                default:
                    Velocity = CoordF.From(0, 0, 0);
                    break;
            }

            SpawnDistance -= Velocity;
        }

        public override void Damage(DamageHandler damage, GameSession session)
        {
            base.Damage(damage, session);

            session.FieldManager.BroadcastPacket(StatPacket.UpdateMobStats(this));
            if (IsDead)
            {
                HandleMobKill(session, this);
            }
        }

        public override void Perish()
        {
            IsDead = true;
            State = NpcState.Dead;
            int randAnim = RandomProvider.Get().Next(Value.StateActions[NpcState.Dead].Length);
            Animation = AnimationStorage.GetSequenceIdBySequenceName(Value.Model, Value.StateActions[NpcState.Dead][randAnim].Item1);
        }

        private static void HandleMobKill(GameSession session, IFieldObject<NpcMetadata> mob)
        {
            // TODO: Add trophy + item drops
            // Drop Money
            bool dropMeso = Rand.Next(2) == 0;
            if (dropMeso)
            {
                // TODO: Calculate meso drop rate
                Item meso = new(90000001, Rand.Next(2, 800));
                session.FieldManager.AddResource(meso, mob, session.Player.FieldPlayer);
            }

            // Drop Meret
            bool dropMeret = Rand.Next(40) == 0;
            if (dropMeret)
            {
                Item meret = new(90000004, 20);
                session.FieldManager.AddResource(meret, mob, session.Player.FieldPlayer);
            }

            // Drop SP
            bool dropSP = Rand.Next(6) == 0;
            if (dropSP)
            {
                Item spBall = new(90000009, 20);
                session.FieldManager.AddResource(spBall, mob, session.Player.FieldPlayer);
            }

            // Drop EP
            bool dropEP = Rand.Next(10) == 0;
            if (dropEP)
            {
                Item epBall = new(90000010, 20);
                session.FieldManager.AddResource(epBall, mob, session.Player.FieldPlayer);
            }

            // Drop Items
            // Send achieves (?)
            // Gain Mob EXP
            session.Player.Levels.GainExp(mob.Value.Experience);
            // Send achieves (2)

            string mapId = session.Player.MapId.ToString();
            // Prepend zero if map id is equal to 7 digits
            if (mapId.Length == 7)
            {
                mapId = $"0{mapId}";
            }

            // Quest Check
            QuestHelper.UpdateQuest(session, mob.Value.Id.ToString(), "npc", mapId);
        }
    }
}
