using UnityEngine;

namespace DunGen
{
    /// <summary>
    /// The 12 cycle types from the Cyclic Dungeon Generation document
    /// </summary>
    public enum CycleType
    {
        /// <summary>
        /// The dungeoneers are presented with a choice between
        /// two alternate long paths to the goal. Each path
        /// embodies a distinct or opposite theme, such as monsters
        /// versus traps, melee versus magic, or corridors versus
        /// caves. Each path is equally dangerous and rewarding.
        /// </summary>
        TwoAlternativePaths = 1,

        /// <summary>
        /// The dungeoneers are presented with two different long
        /// paths through the cycle. Both paths eventually meet at
        /// the goal, which is a chamber that contains a lock (see
        /// page 5). Near the midway point of each long path is a
        /// key; both keys must be used, combined, or manipulated
        /// to open the lock.
        /// </summary>
        TwoKeys = 2,

        /// <summary>
        /// The dungeoneers are presented with a long and
        /// dangerous path to the goal. However, a secret short and
        /// less dangerous path to the goal is hidden in (or near) the
        /// start chamber.
        /// </summary>
        HiddenShortcut = 3,

        /// <summary>
        /// The dungeoneers are presented with a choice between
        /// less dangerous long path to the goal or a much more
        /// dangerous short path. This danger might be a powerful
        /// monster, a lethal gauntlet of traps, a perilous
        /// environmental hazard, or some combination thereof.
        /// </summary>
        /// 
        DangerousRoute = 4,

        /// <summary>
        /// The dungeoneers are teased with a view of the goal from
        /// the start chamber (or a chamber near the start).
        /// However, the dungeoneers are unable to reach the goal
        /// from the start chamber. A long path loops around,
        /// eventually leading the dungeoneers to the goal.
        /// </summary>
        ForeshadowingLoop = 5,

        /// <summary>
        /// The dungeoneers are presented with a short path to the
        /// goal; however, the goal chamber contains a lock. A long
        /// path from the goal chamber leads to its corresponding
        /// key. The chamber containing the key features an exit
        /// back to the starting chamber, such as a one-way portal
        /// or door that only opens from the key’s side. This exit
        /// allows the dungeoneers to loop back quickly to the goal.
        /// </summary>
        LockAndKeyCycle = 6,

        /// <summary>
        /// The dungeoneers are presented with a short path to the
        /// goal. The goal requires the dungeoneers to press farther
        /// into the dungeon, complete an objective, and then
        /// return. However, a barrier (see page 6) just beyond the
        /// goal chamber blocks the dungeoneers from returning
        /// until they follow a long and dangerous route that loops
        /// back to the starting chamber.
        /// Alternatively, the goal chamber contains something
        /// useful to the dungeoneers, such as a safe place to rest
        /// or an exit back to the surface.
        /// </summary>
        BlockedRetreat = 7,

        /// <summary>
        /// A very powerful monster patrols a short circular path
        /// between the start and the goal. Players moving through
        /// the start, goal, or any chambers in between must be
        /// careful to avoid the monster. There is likely something
        /// valuable in a chamber patrolled by the monster that the
        /// dungeoneers must retrieve or manipulate.
        /// </summary>
        MonsterPatrol = 8,

        /// <summary>
        /// The dungeoneers are presented with a short path to the goal. The start chamber contains a monster, trap, or
        /// hazard. When the dungeoneers are forced to backtrack
        /// through the start chamber after reaching the goal, the
        /// nature of that monster, trap, or hazard changes and/or
        /// becomes more perilous.
        /// Alternatively, the start chamber is safe when first
        /// entered, but becomes dangerous upon backtracking.
        /// </summary>
        AlteredReturn = 9,

        /// <summary>
        /// The dungeoneers are presented with a short path to what initially appears to be the goal. 
        /// However, it is revealed that the “goal” is a trap or trick, and that the
        /// true goal lies at the end of a long path that extends from
        /// the false goal chamber (or a chamber nearby).
        /// There is a 1-in-3 chance that this long path to the true
        /// goal is concealed by a secret.
        /// </summary>
        FalseGoal = 10,

        /// <summary>
        /// The dungeoneers are presented with a short path to the goal; 
        /// however, the goal chamber contains a lock. A short
        /// path from the goal chamber leads to the key
        /// </summary>
        SimpleLockAndKey = 11,

        /// <summary>
        /// The dungeoneers are presented with a short path to the goal. 
        /// An optional additional reward is visible from the goal chamber, 
        /// but a dangerous guardian or obstacle blocks the short path toward it.
        /// </summary>
        Gambit = 12
    }
}