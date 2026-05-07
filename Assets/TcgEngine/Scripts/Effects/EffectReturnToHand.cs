using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Effect that returns a card from the discard pile to the hand
    /// </summary>

    [CreateAssetMenu(fileName = "effect", menuName = "TcgEngine/Effect/ReturnToHand", order = 10)]
    public class EffectReturnToHand : EffectData
    {
        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Card target)
        {
            Player player = logic.GameData.GetPlayer(target.player_id);
            if (player != null && player.cards_discard.Contains(target))
            {
                player.cards_discard.Remove(target);
                player.cards_hand.Add(target);
            }
        }

        public override void DoEffect(GameLogic logic, AbilityData ability, Card caster, Player target)
        {
            // Discard a random card from hand (used by the spell card)
            if (target.cards_hand.Count > 0)
            {
                Card card = target.cards_hand[0];
                logic.DiscardCard(card);
            }
        }
    }
}
