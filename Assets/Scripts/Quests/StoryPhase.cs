/// <summary>
/// Every distinct narrative beat in the game. The StoryManager moves through
/// these in order. TutorialFight is a separate scene with no story tracking -
/// it simply loads into the main house scene when done, where BedRest begins.
///
/// DojoRejection and HouseTraining were removed — that content now plays as
/// a non-interactive comic-panel cutscene during the transition from BedRest
/// to FriendVisit, rather than as separate playable phases, to shorten the
/// gap between fights for the demo build.
/// </summary>
public enum StoryPhase
{
    BedRest,             // Phase 0 - confined to bed, physio minigames
    FriendVisit,         // Phase 1 - friend arrives, invites player out
    NewDojoJoin,         // Phase 2 - player joins new dojo, meets new sensei
    TournamentPrep,      // Phase 3 - beat partners A and B before deadline
    TournamentDay        // Phase 4 - demo ending
}