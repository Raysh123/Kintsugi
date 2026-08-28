/// <summary>
/// Every distinct narrative beat in the game. The StoryManager moves through
/// these in order. TutorialFight is a separate scene with no story tracking -
/// it simply loads into the main house scene when done, where BedRest begins.
/// </summary>
public enum StoryPhase
{
    BedRest,             // Phase 0 - confined to bed, physio minigames
    DojoRejection,       // Phase 1 - removed from old dojo, training alone
    HouseTraining,       // Phase 2 - solo training days, depression beat
    FriendVisit,         // Phase 3 - friend arrives, tells player about new dojo
    NewDojoJoin,         // Phase 4 - player joins new dojo, meets new sensei
    TournamentPrep,      // Phase 5 - beat partners A and B before deadline
    TournamentDay        // Phase 6 - demo ending
}