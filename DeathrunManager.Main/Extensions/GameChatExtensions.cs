using System.Collections.Generic;
using DeathrunManager.Config;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.Types;

namespace DeathrunManager.Extensions;

public static class GameChatExtensions
{
    #region Chat
    
    public static void SendColoredAllChatMessage(string message = "")
    {
        if (string.IsNullOrEmpty(message) is true)
        {
            message = "Global message placeholder!";
        }
        
        var coloredPrefix = ProcessColorCodes(ManagerConfig.BaseConfig.Prefix);
        var coloredMessage = ProcessColorCodes(message);

        var coloredChatMessage = " " + coloredPrefix + " " + coloredMessage;

        DeathrunManager.Bridge.ModSharp.PrintChannelFilter(HudPrintChannel.Chat, coloredChatMessage, new RecipientFilter()); 
    }
    
    public static string ProcessColorCodes(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        // Quick check if the message even contains color codes
        if (!message.Contains('{'))
            return message;

        var result = message;
        foreach (var kvp in ColorCache)
        {
            // Case-insensitive search and replace
            if (result.Contains(kvp.Key, System.StringComparison.OrdinalIgnoreCase))
            {
                result = result.Replace(kvp.Key, kvp.Value, System.StringComparison.OrdinalIgnoreCase);
            }
        }

        return result;
    }
    
    private static readonly Dictionary<string, string> ColorCache = new(System.StringComparer.OrdinalIgnoreCase) {
        { "{white}", ChatColor.White },
        { "{default}", ChatColor.White },
        { "{darkred}", ChatColor.DarkRed },
        { "{pink}", ChatColor.Pink },
        { "{green}", ChatColor.Green },
        { "{lightgreen}", ChatColor.LightGreen },
        { "{lime}", ChatColor.Lime },
        { "{red}", ChatColor.Red },
        { "{grey}", ChatColor.Grey },
        { "{gray}", ChatColor.Grey },
        { "{yellow}", ChatColor.Yellow },
        { "{gold}", ChatColor.Gold },
        { "{silver}", ChatColor.Silver },
        { "{blue}", ChatColor.Blue },
        { "{lightblue}", ChatColor.Blue },
        { "{darkblue}", ChatColor.DarkBlue },
        { "{purple}", ChatColor.Purple },
        { "{lightred}", ChatColor.LightRed },
        { "{muted}", ChatColor.Muted },
        { "{head}", ChatColor.Head }
    };
    
    #endregion
}