/// <summary>
/// Every distinct narrative beat in the game. The StoryManager moves through
/// these in order. TutorialFight is a separate scene with no story tracking —
/// it simply loads into the main house scene when done, where BedRest begins.
/// </summary>
public enum StoryPhase
{
    BedRest,             // Phase 0 — confined to bed, physio minigames
    StandingRecovery,    // Phase 1 — can move around house, contacts old sensei
    DojoRejection,       // Phase 2 — removed from old dojo, training alone
    HouseTraining,       // Phase 3 — solo training days, depression beat
    FriendVisit,         // Phase 4 — friend arrives, tells player about new dojo
    NewDojoJoin,         // Phase 5 — player joins new dojo, meets new sensei
    TournamentPrep,      // Phase 6 — beat partners A and B before deadline
    TournamentDay        // Phase 7 — demo ending
}