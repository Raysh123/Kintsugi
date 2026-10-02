using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Point values per move type, for the point-based scoring system (MatchScore) - a punch
/// might be worth 1 point, a kick worth 2, etc. Reuses PlayerStateMachine.AttackTypes rather
/// than inventing a separate enum, since that's already what distinguishes moves everywhere
/// else in the fight code.
///
/// Assign one shared asset to MatchScore. Since it's a ScriptableObject, you can tweak point
/// values without touching code or even leaving the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "AttackTypes", menuName = "Scriptable Objects/AttackTypes")]
public class AttackTypes : ScriptableObject
{
    [System.Serializable]
    public class PointValue
    {
        public PlayerStateMachine.AttackTypes type;
        public int points = 1;
    }

    [Tooltip("One entry per move type that can score. Anything not listed here (or listed " +
             "as None) falls back to Default Points below.")]
    [SerializeField] private List<PointValue> pointValues = new List<PointValue>();

    [Tooltip("Used if a move type isn't found in Point Values above.")]
    [SerializeField] private int defaultPoints = 1;

    public int GetPoints(PlayerStateMachine.AttackTypes type)
    {
        PointValue match = pointValues.Find(p => p.type == type);
        return match != null ? match.points : defaultPoints;
    }
}