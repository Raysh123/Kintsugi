/// <summary>
/// Tiny static gate tracking "is any dialogue currently on screen, anywhere" - updated
/// automatically by DialogueRunner and ConversationRunner the instant they start/stop
/// running (see IsRunning in each). PlayerMovement checks AnyActive alongside its own
/// movementLocked bool, so movement locks itself for the duration of ANY dialogue with
/// zero extra wiring needed at the call site - including dialogue that fires on its own,
/// like OneTimePhaseDialogueTrigger's automatic phase-entry lines, which previously had
/// no movement lock around them at all.
///
/// Uses a count rather than a bool so two dialogues somehow overlapping (e.g. one starting
/// the same frame another is finishing) can't cause one's End() to prematurely unlock
/// movement while the other is still actually running.
///
/// Existing manual player.movementLocked = true/false calls elsewhere (SenseiInteraction,
/// ParkActivity, TravelMapController, PostFightHandler, DojoArrivalHandler) are still fine
/// to keep - they're redundant now for the dialogue itself, but still needed for the parts
/// of those sequences that aren't dialogue (e.g. the fight selection menu being open).
/// </summary>
public static class ActiveDialogueTracker
{
    private static int activeCount;

    public static bool AnyActive => activeCount > 0;

    public static void Begin() => activeCount++;

    public static void End()
    {
        if (activeCount > 0) activeCount--;
    }
}