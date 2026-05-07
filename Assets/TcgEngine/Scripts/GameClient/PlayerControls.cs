using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using UnityEngine.Events;
using TcgEngine.UI;

namespace TcgEngine.Client
{
    /// <summary>
    /// Script that contain main controls for clicking on cards, attacking, activating abilities
    /// Holds the currently selected card and will send action to GameClient on click release
    /// </summary>

    public class PlayerControls : MonoBehaviour
    {
        private BoardCard selected_card = null;
        private ActionMode current_action_mode = ActionMode.None;
        private Card action_card = null;
        private AbilityData action_ability = null;

        private static PlayerControls instance;

        void Awake()
        {
            instance = this;
        }

        void Update()
        {
            if (!GameClient.Get().IsReady())
                return;

            Game gdata = GameClient.Get().GetGameData();

            //Right-click during priority/chain phase = pass priority
            if (Input.GetMouseButtonDown(1) && gdata.phase == GamePhase.PriorityWait)
            {
                if (GameClient.Get().GetPlayerID() == gdata.current_priority_player)
                    GameClient.Get().PassPriority();
                return;
            }

            //Right-click cancels action mode or unselects all
            if (Input.GetMouseButtonDown(1))
            {
                ClearActionMode();
                UnselectAll();
                return;
            }

            //Handle action mode target selection
            if (current_action_mode != ActionMode.None && Input.GetMouseButtonDown(0))
            {
                HandleActionModeClick();
                return;
            }

            if (selected_card != null)
            {
                if (Input.GetMouseButtonUp(0))
                {
                    ReleaseClick();
                    UnselectAll();
                }
            }
        }

        private void HandleActionModeClick()
        {
            if (GameUI.IsOverUI())
                return;

            Game gdata = GameClient.Get().GetGameData();
            Vector3 wpos = GameBoard.Get().RaycastMouseBoard();
            BSlot tslot = BSlot.GetNearest(wpos);
            Card target = tslot?.GetSlotCard(wpos);

            switch (current_action_mode)
            {
                case ActionMode.SelectingAttackTarget:
                    if (target != null && target.player_id != action_card.player_id
                        && gdata.CanAttackTarget(action_card, target))
                    {
                        if (action_card.exhausted)
                            WarningText.ShowExhausted();
                        else
                            GameClient.Get().AttackTarget(action_card, target);
                    }
                    else if (tslot is BoardSlotPlayer && tslot.GetPlayer() != null
                        && tslot.GetPlayer().player_id != action_card.player_id
                        && gdata.CanAttackTarget(action_card, tslot.GetPlayer()))
                    {
                        if (action_card.exhausted)
                            WarningText.ShowExhausted();
                        else
                            GameClient.Get().AttackPlayer(action_card, tslot.GetPlayer());
                    }
                    break;

                case ActionMode.SelectingMoveSlot:
                    if (tslot != null && tslot is BoardSlot)
                    {
                        Slot slot = tslot.GetSlot();
                        if (gdata.CanMoveCard(action_card, slot))
                        {
                            GameClient.Get().Move(action_card, slot);
                        }
                    }
                    break;

                case ActionMode.SelectingEffectTarget:
                    if (action_ability != null && action_card != null)
                    {
                        GameClient.Get().CastAbility(action_card, action_ability);
                    }
                    break;
            }

            ClearActionMode();
            UnselectAll();
        }

        public void SelectCard(BoardCard bcard)
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
            Card card = bcard.GetFocusCard();

            if (gdata.IsPlayerSelectorTurn(player) && gdata.selector == SelectorType.SelectTarget)
            {
                if (!Tutorial.Get().CanDo(TutoEndTrigger.SelectTarget, card))
                    return;

                //Target selector, select this card
                GameClient.Get().SelectCard(card);
            }
            else if (gdata.IsPlayerActionTurn(player) && card.player_id == player.player_id)
            {
                // Show action dialog instead of directly starting drag
                ShowActionDialog(bcard);
            }
        }

        public void SelectCardRight(BoardCard card)
        {
            if (!Input.GetMouseButton(0))
            {
                //Nothing on right-click
            }
        }

        private void ShowActionDialog(BoardCard bcard)
        {
            // Always set selected_card for drag-to-move support
            selected_card = bcard;

            CardActionDialog dialog = CardActionDialog.Get();
            if (dialog != null)
            {
                dialog.ShowForCard(bcard);
            }
        }

        private void ReleaseClick()
        {
            bool yourturn = GameClient.Get().IsYourTurn();
            Game gdata = GameClient.Get().GetGameData();

            if (yourturn && selected_card != null)
            {
                Card card = selected_card.GetCard();
                Vector3 wpos = GameBoard.Get().RaycastMouseBoard();
                BSlot tslot = BSlot.GetNearest(wpos);
                Card target = tslot?.GetSlotCard(wpos);
                AbilityButton ability = AbilityButton.GetFocus(wpos, 1f);

                //Priority/chain phase: use abilities or pass
                if (gdata.phase == GamePhase.PriorityWait)
                {
                    if (ability != null && ability.IsInteractable())
                    {
                        if (!Tutorial.Get().CanDo(TutoEndTrigger.CastAbility, card))
                            return;
                        GameClient.Get().ChainAbility(card, ability.GetAbility());
                    }
                    return;
                }

                if (ability != null && ability.IsInteractable())
                {
                    if (!Tutorial.Get().CanDo(TutoEndTrigger.CastAbility, card))
                        return;

                    GameClient.Get().CastAbility(card, ability.GetAbility());
                }
                else if (tslot is BoardSlotPlayer)
                {
                    if (!Tutorial.Get().CanDo(TutoEndTrigger.AttackPlayer, card))
                        return;

                    if (card.exhausted)
                        WarningText.ShowExhausted();
                    else
                        GameClient.Get().AttackPlayer(card, tslot.GetPlayer());
                }
                //Garrison: drag creature onto friendly talent card
                else if (target != null && target.CardData.IsTalent()
                         && target.player_id == card.player_id
                         && card.player_id == GameClient.Get().GetPlayerID()
                         && card.CardData.IsCharacter() && !card.CardData.IsTalentCreature())
                {
                    GameClient.Get().TalentGarrison(target, card);
                }
                else if (target != null && target.uid != card.uid && target.player_id != card.player_id)
                {
                    if (!Tutorial.Get().CanDo(TutoEndTrigger.Attack, card) && !Tutorial.Get().CanDo(TutoEndTrigger.Attack, target))
                        return;

                    if (card.exhausted)
                        WarningText.ShowExhausted();
                    else
                        GameClient.Get().AttackTarget(card, target);
                }
                else if (tslot != null && tslot is BoardSlot)
                {
                    if (!Tutorial.Get().CanDo(TutoEndTrigger.Move, tslot.GetSlot()))
                        return;

                    GameClient.Get().Move(card, tslot.GetSlot());
                }
            }
        }

        public void SetSelectedDirect(BoardCard bcard)
        {
            selected_card = bcard;
        }

        public void SetActionMode(ActionMode mode, Card card, AbilityData ability)
        {
            current_action_mode = mode;
            action_card = card;
            action_ability = ability;
            selected_card = BoardCard.Get(card.uid);
        }

        public void ClearActionMode()
        {
            current_action_mode = ActionMode.None;
            action_card = null;
            action_ability = null;
        }

        public ActionMode GetActionMode()
        {
            return current_action_mode;
        }

        public Card GetActionCard()
        {
            return action_card;
        }

        public void UnselectAll()
        {
            selected_card = null;
        }

        public BoardCard GetSelected()
        {
            return selected_card;
        }

        public static PlayerControls Get()
        {
            return instance;
        }
    }

    public enum ActionMode
    {
        None = 0,
        SelectingAttackTarget = 1,
        SelectingMoveSlot = 2,
        SelectingEffectTarget = 3,
    }
}
