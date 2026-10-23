using System;

namespace HellParty.Rules;

// Big entries in the requested Valuables wiki, excluding enemy cores,
// Surplus, Telescope and Unicorn Horn. Use an allowlist so unrelated/new
// prefab names cannot accidentally enter the pool.
internal static class UpscreamDisguiseCatalog
{
    internal static readonly string[] Paths =
    {
        "Valuables/04 Big/Valuable Manor Vase Big",
        "Valuables/04 Big/Valuable Manor Vase Chunky",
        "Valuables/04 Big/Valuable Manor Diamond Display",
        "Valuables/04 Big/Valuable Manor Scream Doll",
        "Valuables/04 Big/Valuable Manor Television",
        "Valuables/04 Big/Valuable Arctic Barrel",
        "Valuables/04 Big/Valuable Arctic Big Sample",
        "Valuables/04 Big/Valuable Arctic Creature Leg",
        "Valuables/04 Big/Valuable Arctic Flamethrower",
        "Valuables/04 Big/Valuable Arctic Guitar",
        // Wiki lists Icepick as Big; its installed native resource is Medium.
        "Valuables/03 Medium/Valuable Arctic Icepick",
        "Valuables/04 Big/Valuable Arctic Ice Saw",
        "Valuables/04 Big/Valuable Arctic Sample Cooler",
        "Valuables/04 Big/Valuable Museum Baby Head",
        "Valuables/04 Big/Valuable Museum Egg",
        "Valuables/04 Big/Valuable Museum Gem Burger",
        "Valuables/04 Big/Valuable Museum Golden Swirl",
        "Valuables/04 Big/Valuable Museum Boombox",
        "Valuables/04 Big/Valuable Museum Uranium Mug Deluxe",
        "Valuables/04 Big/Valuable Wizard Cauldron Box",
        "Valuables/04 Big/Valuable Wizard Cube of Knowledge",
        "Valuables/04 Big/Valuable Wizard Forever Candle",
        "Valuables/04 Big/Valuable Wizard Master Potion",
        "Valuables/04 Big/Valuable Wizard Spider Potion"
    };

    internal static bool Eligible(string path, bool core, bool surplus) => !core && !surplus &&
        Array.Exists(Paths, candidate => string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase));
}
