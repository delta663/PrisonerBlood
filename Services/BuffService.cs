using ProjectM;
using ProjectM.Network;
using ProjectM.Shared;
using Stunlock.Core;
using Unity.Entities;

namespace PrisonerBlood.Services;

internal static class BuffService
{
    public static bool AddBuff(Entity userEntity, Entity targetEntity, PrefabGUID buffPrefab, int duration = -1, bool immortal = true)
    {
        var debugEventsSystem = Core.DebugEventsSystem;
        var buffEvent = new ApplyBuffDebugEvent
        {
            BuffPrefabGUID = buffPrefab
        };

        var fromCharacter = new FromCharacter
        {
            User = userEntity,
            Character = targetEntity
        };

        if (BuffUtility.TryGetBuff(Core.EntityManager, targetEntity, buffPrefab, out _))
            return false;

        debugEventsSystem.ApplyBuff(fromCharacter, buffEvent);

        if (!BuffUtility.TryGetBuff(Core.EntityManager, targetEntity, buffPrefab, out var buffEntity))
            return false;

        if (buffEntity.Has<CreateGameplayEventsOnSpawn>())
            buffEntity.Remove<CreateGameplayEventsOnSpawn>();

        if (buffEntity.Has<GameplayEventListeners>())
            buffEntity.Remove<GameplayEventListeners>();

        if (immortal)
        {
            if (!buffEntity.Has<Buff_Persists_Through_Death>())
                buffEntity.Add<Buff_Persists_Through_Death>();

            if (buffEntity.Has<RemoveBuffOnGameplayEvent>())
                buffEntity.Remove<RemoveBuffOnGameplayEvent>();

            if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>())
                buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
        }

        if (duration > -1 && duration != 0)
        {
            if (!buffEntity.Has<LifeTime>())
            {
                buffEntity.Add<LifeTime>();
                buffEntity.Write(new LifeTime { EndAction = LifeTimeEndAction.Destroy });
            }

            var lifeTime = buffEntity.Read<LifeTime>();
            lifeTime.Duration = duration;
            buffEntity.Write(lifeTime);
        }
        else if (duration == -1)
        {
            if (buffEntity.Has<LifeTime>())
            {
                var lifeTime = buffEntity.Read<LifeTime>();
                lifeTime.EndAction = LifeTimeEndAction.None;
                buffEntity.Write(lifeTime);
            }

            if (buffEntity.Has<RemoveBuffOnGameplayEvent>())
                buffEntity.Remove<RemoveBuffOnGameplayEvent>();

            if (buffEntity.Has<RemoveBuffOnGameplayEventEntry>())
                buffEntity.Remove<RemoveBuffOnGameplayEventEntry>();
        }

        return true;
    }
}
