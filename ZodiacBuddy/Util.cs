using System;
using System.Collections.Generic;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace ZodiacBuddy;

/// <summary>
/// Utility methods.
/// </summary>
internal static class Util {
    /// <summary>
    /// Return the item equipped on the slot id.
    /// </summary>
    /// <param name="index">Slot index of the desired item.</param>
    /// <returns>Equipped item on the slot or the default item 0.</returns>
    public static unsafe InventoryItem GetEquippedItem(int index) {
        var im = InventoryManager.Instance();
        if (im == null)
            throw new Exception("InventoryManager was null");

        var equipped = im->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null)
            throw new Exception("EquippedItems was null");

        var slot = equipped->GetInventorySlot(index);
        if (slot == null)
            throw new Exception($"InventorySlot{index} was null");

        return *slot;
    }

    private static readonly List<(TimeOnly, int, TimeOnly)> BonusLightWindows = [
        (new TimeOnly(0, 0), 0, new TimeOnly(2, 0)),
        (new TimeOnly(2, 0), 0, new TimeOnly(4, 0)),
        (new TimeOnly(4, 0), 0, new TimeOnly(6, 0)),
        (new TimeOnly(6, 0), 0, new TimeOnly(8, 0)),
        (new TimeOnly(8, 0), 0, new TimeOnly(10, 0)),
        (new TimeOnly(10, 0), 0, new TimeOnly(12, 0)),
        (new TimeOnly(12, 0), 0, new TimeOnly(14, 0)),
        (new TimeOnly(14, 0), 0, new TimeOnly(16, 0)),
        (new TimeOnly(16, 0), 0, new TimeOnly(18, 0)),
        (new TimeOnly(18, 0), 0, new TimeOnly(20, 0)),
        (new TimeOnly(20, 0), 0, new TimeOnly(22, 0)),
        (new TimeOnly(22, 0), 1, new TimeOnly(0, 0)),
    ];
    
    public static (DateTime, DateTime)? CurrentBonusLightWindow() {
        var now = DateTime.UtcNow;
        
        foreach (var (start, addDays, end) in BonusLightWindows) {
            var startWindow = new DateTime(DateOnly.FromDateTime(now), start, DateTimeKind.Utc);
            var endWindow = new DateTime(DateOnly.FromDateTime(now).AddDays(addDays), end, DateTimeKind.Utc);

            if (now >= startWindow && now < endWindow) {
                return (startWindow, endWindow);
            }
        }

        Service.PluginLog.Error($"No bonus light window found for UTC time: {now:O}");
        return null;
    }
    
    /// <summary>
    /// Get the item name from the item ID.
    /// </summary>
    /// <param name="itemId">Item ID to get the name from.</param>
    /// <returns>Name of the item.</returns>
    public static string GetItemName(uint itemId) {
            (var baseId, ItemKind itemKind) = ItemUtil.GetBaseId(itemId);
            return itemKind switch
            {
                // Item from quest or leve quests
                ItemKind.EventItem =>
                    Service.DataManager.GetExcelSheet<EventItem>().GetRow(baseId).Name.ExtractText(),
                // Normal, hq and collectible are in Item sheet
                _ => Service.DataManager.GetExcelSheet<Item>().GetRow(baseId).Name.ExtractText(),
            };
    }
}
