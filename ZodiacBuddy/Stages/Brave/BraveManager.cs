using System;
using System.Linq;

using Dalamud.Game;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ZodiacBuddy.BonusLight;

namespace ZodiacBuddy.Stages.Brave;

/// <summary>
/// Your buddy for the Zodiac Brave stage.
/// </summary>
internal class BraveManager : IDisposable
{
    // Any reason to not multiply all light level * 2 and put the progress on 80 instead of 40 ?
    private static readonly BonusLightLevel[] BonusLightValues =
    {
        #pragma warning disable format,SA1008,SA1025
        new( 4, 4660), // Feeble
        new( 8, 4661), // Faint
        new(16, 4662), // Gentle
        new(24, 4663), // Steady
        new(48, 4664), // Forceful
        new(64, 4665), // Nigh Sings
        new(40, 4666), // Completed
        #pragma warning restore format,SA1008,SA1025
    };

    [Signature("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 F3 0F 10 05 ?? ?? ?? ?? 49 8B D8", DetourName = nameof(AddonRelicMagiciteOnSetupDetour))]
    private readonly Hook<AddonRelicGlassOnSetupDelegate> addonRelicMagiciteOnSetupHook = null!;
    private readonly BraveWindow window;

    /// <summary>
    /// Initializes a new instance of the <see cref="BraveManager"/> class.
    /// </summary>
    public BraveManager()
    {
        this.window = new BraveWindow();

        Service.Framework.Update += this.OnUpdate;
        Service.Toasts.QuestToast += this.OnToast;
        Service.Interface.UiBuilder.Draw += this.window.Draw;

        SignatureHelper.Initialise(this);
        this.addonRelicMagiciteOnSetupHook?.Enable();
    }

    private delegate void AddonRelicGlassOnSetupDelegate(IntPtr addon, uint a2, IntPtr relicInfoPtr);

    private static BraveConfiguration Configuration => Service.Configuration.Brave;

    private static BonusLightConfiguration LightConfiguration => Service.Configuration.BonusLight;

    /// <inheritdoc/>
    public void Dispose()
    {
        Service.Interface.UiBuilder.Draw -= this.window.Draw;
        Service.Toasts.QuestToast -= this.OnToast;

        this.addonRelicMagiciteOnSetupHook?.Disable();
    }

    private static bool CheckMessage(SeString toast, BonusLightLevel lightLevel)
    {
        var toastAsString = toast.ToString();
        return lightLevel.Message.Split("  ")
                   .All(text => toastAsString.Contains(text)) ||
               lightLevel.Message.Split("「」") // Japanese
                   .All(text => toastAsString.Contains(text));
    }

    private void AddonRelicMagiciteOnSetupDetour(IntPtr addonRelicMagicite, uint a2, IntPtr relicInfoPtr)
    {
        this.addonRelicMagiciteOnSetupHook.Original(addonRelicMagicite, a2, relicInfoPtr);

        try
        {
            this.UpdateRelicMagiciteAddon(0);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Unhandled error during {nameof(BraveManager)}.{nameof(this.AddonRelicMagiciteOnSetupDetour)}");
        }
    }

    private unsafe void UpdateRelicMagiciteAddon(int slot)
    {
        var item = Util.GetEquippedItem(slot);
        if (!BraveRelic.Items.ContainsKey(item.ItemID))
            return;

        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("RelicMagicite", 1);
        if (addon == null)
            return;

        var lightText = (AtkTextNode*)addon->UldManager.SearchNodeById(9);
        if (lightText == null)
            return;

        if (Configuration.ShowNumbersInRelicMagicite)
        {
            var value = item.Spiritbond % 500;
            lightText->SetText($"{lightText->NodeText}\n{value / 2}/40");
        }

        if (!Configuration.DontPlayRelicMagiciteAnimation)
            return;

        var analyzeText = (AtkTextNode*)addon->UldManager.SearchNodeById(8);
        if (analyzeText == null)
            return;

        analyzeText->SetText(lightText->NodeText.ToString());
    }

    private void OnUpdate(Framework framework)
    {
        try
        {
            this.OnUpdateInner();
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Unhandled error during {nameof(BraveManager)}.{nameof(this.OnUpdate)}");
        }
    }

    private void OnUpdateInner()
    {
        if (!Configuration.DisplayRelicInfo)
        {
            this.window.ShowWindow = false;
            return;
        }

        // Brave sword and shield are managed together
        // You can't take/validate magicite without both in your inventory
        // and only the sword gain spiritbond with duty
        var mainhand = Util.GetEquippedItem(0);

        var shouldShowWindow = BraveRelic.Items.ContainsKey(mainhand.ItemID);

        this.window.ShowWindow = shouldShowWindow;
        this.window.MainhandItem = mainhand;
    }

    private void OnToast(ref SeString message, ref QuestToastOptions options, ref bool isHandled)
    {
        try
        {
            this.OnToastInner(ref message, ref options, ref isHandled);
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, $"Unhandled error during {nameof(BraveManager)}.{nameof(this.OnToast)}");
        }
    }

    private void OnToastInner(ref SeString message, ref QuestToastOptions options, ref bool isHandled)
    {
        if (isHandled)
            return;

        foreach (var lightLevel in BonusLightValues)
        {
            if (!CheckMessage(message, lightLevel))
                continue;

            if (lightLevel == BonusLightValues[6]) // Ignore completed magicite for now
                return;

            Service.Plugin.PrintMessage($"Light Intensity has increased by {lightLevel.Intensity}.");

            var territoryId = Service.ClientState.TerritoryType;
            if (!BonusLightDuty.TryGetValue(territoryId, out var territoryLight))
                return;

            // Brave light gain look like territoryLight.DefaultLightIntensity /2
            // Still not sure about that, will need more data
            if (lightLevel.Intensity > territoryLight!.DefaultLightIntensity / 2)
            {
                // Service.BonusLightManager.AddLightBonus(territoryId,
                //     $"Light bonus detected on \"{territoryLight.DutyName}\"");
            }
        }
    }
}