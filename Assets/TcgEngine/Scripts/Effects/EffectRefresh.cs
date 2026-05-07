using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that fully refreshes/resets a card: unexhausts, clears moved flag, removes Tapped status
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/Refresh", order = 10)]
    public class EffectRefresh : EffectData
    {
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            target.exhausted = false;
            target.has_moved = false;
            target.RemoveStatus(StatusType.Tapped);
        }
    }
}
