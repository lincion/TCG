using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.UI;

namespace TcgEngine.Client
{
    /// <summary>
    /// Represents the visual extra deck (Talent cards) on the board.
    /// Click to show extra deck cards, usable cards highlighted.
    /// </summary>

    public class BoardExtraDeck : MonoBehaviour
    {
        public bool opponent;
        public SpriteRenderer deck_render;
        public Text deck_value;

        private static BoardExtraDeck instance_self;

        void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!GameClient.Get().IsReady())
                return;

            Player player = opponent ? GameClient.Get().GetOpponentPlayer() : GameClient.Get().GetPlayer();
            if (player == null)
                return;

            CardbackData cb = CardbackData.Get(player.cardback);
            if (deck_render != null && cb != null)
                deck_render.sprite = cb.deck;

            if (deck_value != null)
                deck_value.text = player.cards_extra.Count.ToString();
        }

        public void ShowExtraDeckCards()
        {
            if (opponent)
                return; //Cannot see opponent's extra deck

            Player player = GameClient.Get().GetPlayer();
            CardSelector.Get().Show(player.cards_extra, "EXTRA DECK");
        }

        private void OnMouseOver()
        {
            if (!opponent && Input.GetMouseButtonDown(0))
                ShowExtraDeckCards();
        }

        public static BoardExtraDeck Get()
        {
            return instance_self;
        }
    }
}
