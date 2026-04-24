using System;
using Il2CppInterop.Runtime;
using ProjectM;
using ProjectM.Network;
using ProjectM.Scripting;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using PrisonerBlood.Models;

namespace PrisonerBlood;

internal static class Helper
{
    public static NativeArray<Entity> GetEntitiesByComponentTypes<T1, T2>(bool includeAll = false, bool includeDisabled = false, bool includeSpawn = false, bool includePrefab = false, bool includeDestroyed = false)
    {
        EntityQueryOptions options = EntityQueryOptions.Default;
        if (includeAll) options |= EntityQueryOptions.IncludeAll;
        if (includeDisabled) options |= EntityQueryOptions.IncludeDisabled;
        if (includeSpawn) options |= EntityQueryOptions.IncludeSpawnTag;
        if (includePrefab) options |= EntityQueryOptions.IncludePrefab;
        if (includeDestroyed) options |= EntityQueryOptions.IncludeDestroyTag;

        var entityQueryBuilder = new EntityQueryBuilder(Allocator.Temp)
            .AddAll(new(Il2CppType.Of<T1>(), ComponentType.AccessMode.ReadWrite))
            .AddAll(new(Il2CppType.Of<T2>(), ComponentType.AccessMode.ReadWrite))
            .WithOptions(options);

        var query = Core.EntityManager.CreateEntityQuery(ref entityQueryBuilder);
        return query.ToEntityArray(Allocator.Temp);
    }

    public static bool TryGetInventoryEntity(Entity characterEntity, out Entity inventoryEntity)
    {
        return InventoryUtilities.TryGetInventoryEntity(Core.EntityManager, characterEntity, out inventoryEntity);
    }

    public static int GetItemCountInInventory(Entity characterEntity, PrefabGUID itemPrefab)
    {
        var em = Core.EntityManager;
        if (!TryGetInventoryEntity(characterEntity, out var inv))
            return 0;

        if (em.HasComponent<InventoryBuffer>(inv))
        {
            var buffer = em.GetBuffer<InventoryBuffer>(inv);
            int total = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                var slot = buffer[i];
                if (slot.ItemType.GuidHash == itemPrefab.GuidHash)
                    total += slot.Amount;
            }
            return total;
        }

        int sum = 0;
        for (int i = 0; i < 36; i++)
        {
            if (InventoryUtilities.TryGetItemAtSlot(em, characterEntity, i, out InventoryBuffer item))
            {
                if (item.ItemType.GuidHash == itemPrefab.GuidHash)
                    sum += item.Amount;
            }
        }
        return sum;
    }

    public static int GetEmptyInventorySlotsCount(Entity characterEntity)
    {
        var em = Core.EntityManager;
        if (!TryGetInventoryEntity(characterEntity, out var inv))
            return 0;

        if (!em.HasComponent<InventoryBuffer>(inv))
            return 0;

        var buffer = em.GetBuffer<InventoryBuffer>(inv);
        int emptyCount = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i].ItemType.GuidHash == 0)
            {
                emptyCount++;
            }
        }
        return emptyCount;
    }

    public static Entity AddItemToInventory(Entity recipient, PrefabGUID guid, int amount)
    {
        try
        {
            var inventoryResponse = Core.ServerGameManager.TryAddInventoryItem(recipient, guid, amount);
            return inventoryResponse.NewEntity;
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }

        return Entity.Null;
    }

    public static bool TryRemoveItemsFromInventory(Entity characterEntity, PrefabGUID itemPrefab, int amount)
    {
        var em = Core.EntityManager;
        if (!TryGetInventoryEntity(characterEntity, out var inv))
            return false;

        if (!em.HasComponent<InventoryBuffer>(inv))
            return false;

        var buffer = em.GetBuffer<InventoryBuffer>(inv);
        int toRemove = amount;

        for (int i = buffer.Length - 1; i >= 0 && toRemove > 0; i--)
        {
            var slot = buffer[i];
            if (slot.ItemType.GuidHash != itemPrefab.GuidHash)
                continue;
            if (slot.Amount <= 0)
                continue;

            int take = math.min(slot.Amount, toRemove);
            slot.Amount -= take;
            toRemove -= take;

            if (slot.Amount <= 0)
            {
                slot.ItemType = new PrefabGUID(0);
                slot.Amount = 0;
            }

            buffer[i] = slot;
        }

        return toRemove == 0;
    }

    public static void BroadcastSystemMessage(string message)
    {
        try
        {
            var fixedMessage = new FixedString512Bytes(message ?? string.Empty);
            ServerChatUtils.SendSystemMessageToAllClients(Core.EntityManager, ref fixedMessage);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static void NotifyUser(Entity userEntity, string message)
    {
        try
        {
            if (userEntity == Entity.Null || !Core.EntityManager.Exists(userEntity))
                return;

            var user = Core.EntityManager.GetComponentData<User>(userEntity);
            var fixedMessage = new FixedString512Bytes(message ?? string.Empty);
            ServerChatUtils.SendSystemMessageToClient(Core.EntityManager, user, ref fixedMessage);
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static readonly HashSet<BloodType> AllowedBloodTypes = new()
    {
        BloodType.Worker,
        BloodType.Warrior,
        BloodType.Rogue,
        BloodType.Brute,
        BloodType.Scholar,
        BloodType.Draculin,
        BloodType.Creature,
        BloodType.Mutant,
        BloodType.Corrupted
    };

    private static readonly HashSet<int> CombatBuffs = new()
    {
        581443919,  // Buff_InCombat
        697095869,  // Buff_InCombat_PvPVampire
        698151145   // Buff_InCombat_Contest
    };
    
    public static bool IsInCombat(Entity characterEntity)
    {
        var em = Core.EntityManager;
        foreach (var buffHash in CombatBuffs)
        {
            if (BuffUtility.HasBuff(em, characterEntity, new PrefabGUID(buffHash)))
            {
                return true;
            }
        }
        return false;
    }
}
