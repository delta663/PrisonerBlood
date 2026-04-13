using System;
using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PrisonerBlood.Services;

internal class UnitSpawnerService
{
    private static readonly Entity EmptyEntity = new();
    internal const int DefaultMinRange = 1;
    internal const int DefaultMaxRange = 1;

    internal Dictionary<long, (float ActualDuration, Action<Entity> Actions)> PostActions { get; } = [];

    public void SpawnWithCallback(Entity user, PrefabGUID unit, float2 position, float duration, Action<Entity> postActions, float yPosition = -1)
    {
        if (yPosition == -1)
        {
            var translation = Core.EntityManager.GetComponentData<Translation>(user);
            yPosition = translation.Value.y;
        }

        var pos = new float3(position.x, yPosition, position.y);
        var unitSpawnerUpdateSystem = Core.Server.GetExistingSystemManaged<UnitSpawnerUpdateSystem>();

        UnitSpawnerReactSystemPatch.Enabled = true;

        var durationKey = NextKey();
        unitSpawnerUpdateSystem.SpawnUnit(EmptyEntity, unit, pos, 1, DefaultMinRange, DefaultMaxRange, durationKey);
        PostActions.Add(durationKey, (duration, postActions));
    }

    private long NextKey()
    {
        var random = new System.Random();
        long key;
        int breaker = 5;

        do
        {
            key = random.NextInt64(10000) * 3;
            breaker--;
            if (breaker < 0)
                throw new Exception("Failed to generate a unique key for UnitSpawnerService");
        }
        while (PostActions.ContainsKey(key));

        return key;
    }

    [HarmonyPatch(typeof(UnitSpawnerReactSystem), nameof(UnitSpawnerReactSystem.OnUpdate))]
    public static class UnitSpawnerReactSystemPatch
    {
        public static bool Enabled { get; set; }

        public static void Prefix(UnitSpawnerReactSystem __instance)
        {
            if (!Enabled)
                return;

            var entities = __instance._Query.ToEntityArray(Unity.Collections.Allocator.Temp);

            foreach (var entity in entities)
            {
                if (!Core.EntityManager.HasComponent<LifeTime>(entity))
                    continue;

                var lifeTime = Core.EntityManager.GetComponentData<LifeTime>(entity);
                var durationKey = (long)Mathf.Round(lifeTime.Duration);
                if (!Core.UnitSpawner.PostActions.TryGetValue(durationKey, out var unitData))
                    continue;

                var (actualDuration, actions) = unitData;
                Core.UnitSpawner.PostActions.Remove(durationKey);

                var endAction = actualDuration < 0 ? LifeTimeEndAction.None : LifeTimeEndAction.Destroy;
                var newLifeTime = new LifeTime
                {
                    Duration = actualDuration,
                    EndAction = endAction
                };

                Core.EntityManager.SetComponentData(entity, newLifeTime);
                actions(entity);
            }

            entities.Dispose();
        }
    }
}
