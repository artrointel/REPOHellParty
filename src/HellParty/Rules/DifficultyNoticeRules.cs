namespace HellParty.Rules;

internal static class DifficultyNoticeRules
{
    internal static string SpokenMessage(int completed) => completed == 0
        ? "HellParty is now active."
        : "The monsters have grown stronger.";

    internal static bool ShouldAnnounce(int completed, int difficulty) =>
        completed >= 0 && difficulty == DifficultyRules.FromCompletedStages(completed) &&
        (completed == 0 || difficulty > DifficultyRules.FromCompletedStages(completed - 1));

    // Monsters gaining an ability or upgrade at this tier; no skill details.
    internal static string Highlights(int difficulty) => difficulty switch
    {
        2 => "Reaper, Peeper, Animal, Birthday Boy,\nBowtie, Cleanup Crew and Bella just got more dangerous.",
        3 => "Clown, Stalking Doll, Apex Predator, Animal,\nBirthday Boy, Bella and Mentalist just got more dangerous.",
        4 => "Headman, Loom, Huntsman and Bowtie\njust got more dangerous.",
        5 => "Clown, Cleanup Crew, Apex Predator and Elsa\njust got more dangerous.",
        _ => ""
    };
}
