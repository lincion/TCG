using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;
using TcgEngine.Client;

namespace TcgEngine.UI
{
    /// <summary>
    /// Dialog shown when clicking a card on the board, offering Attack/Move/ActivateEffect/Cancel choices.
    /// </summary>

    public class CardActionDialog : UIPanel
    {
        public Button attack_btn;
        public Button move_btn;
        public Button cancel_btn;
        public Transform effect_list;
        public GameObject effect_btn_prefab;

        public Text attack_label;
        public Text move_label;
        public Text cancel_label;

        private Card card;
        private BoardCard board_card;
        private List<AbilityData> activatable_abilities = new List<AbilityData>();
        private List<Button> dynamic_buttons = new List<Button>();

        private static CardActionDialog instance;

        protected override void Awake()
        {
            base.Awake();
            instance = this;

            if (attack_btn != null)
                attack_btn.onClick.AddListener(OnClickAttack);
            if (move_btn != null)
                move_btn.onClick.AddListener(OnClickMove);
            if (cancel_btn != null)
                cancel_btn.onClick.AddListener(OnClickCancel);
        }

        protected override void Update()
        {
            base.Update();

            if (visible && card != null)
            {
                // Close if card no longer exists or not our turn
                Game gdata = GameClient.Get().GetGameData();
                Card current = gdata.GetCard(card.uid);
                if (current == null || !gdata.IsPlayerActionTurn(gdata.GetPlayer(card.player_id)))
                {
                    Hide();
                }
            }

            // Close on right-click
            if (visible && Input.GetMouseButtonDown(1))
            {
                Hide();
            }
        }

        public void ShowForCard(BoardCard bcard)
        {
            Game gdata = GameClient.Get().GetGameData();
            card = bcard.GetCard();
            board_card = bcard;

            if (card == null) return;

            // Set the card as selected for highlight logic
            PlayerControls.Get().SetSelectedDirect(bcard);

            activatable_abilities.Clear();
            List<AbilityData> allAbilities = card.GetAbilities();
            foreach (AbilityData ability in allAbilities)
            {
                if (ability != null && ability.trigger == AbilityTrigger.Activate
                    && gdata.CanCastAbility(card, ability))
                {
                    activatable_abilities.Add(ability);
                }
            }

            bool canAttack = gdata.CanAttackTarget(card, gdata.GetOpponentPlayer(card.player_id))
                          || HasAttackableTarget(card);
            bool canMove = card.CanMove() && HasMovableSlot(card);

            // Configure buttons
            if (attack_btn != null)
                attack_btn.interactable = !card.exhausted && canAttack;
            if (move_btn != null)
                move_btn.interactable = canMove;

            if (attack_label != null)
                attack_label.text = "攻击";
            if (move_label != null)
                move_label.text = "移动";
            if (cancel_label != null)
                cancel_label.text = "取消";

            // Populate dynamic effect buttons
            ClearDynamicButtons();
            for (int i = 0; i < activatable_abilities.Count; i++)
            {
                if (effect_btn_prefab != null && effect_list != null)
                {
                    GameObject obj = Instantiate(effect_btn_prefab, effect_list);
                    Button btn = obj.GetComponent<Button>();
                    Text btnText = obj.GetComponentInChildren<Text>();
                    int idx = i;
                    btn.onClick.AddListener(() => OnClickEffect(idx));

                    AbilityData ability = activatable_abilities[i];
                    string label = "效果" + (i + 1);
                    if (ability != null && !string.IsNullOrEmpty(ability.title))
                        label = ability.GetTitle();
                    if (btnText != null) btnText.text = label;

                    dynamic_buttons.Add(btn);
                }
            }

            // Position near the card
            PositionNearCard(bcard);

            Show();
        }

        private void PositionNearCard(BoardCard bcard)
        {
            if (bcard == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = bcard.transform.position + new Vector3(0f, 1.5f, 0f);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    // Clamp to screen bounds
                    float halfW = rect.sizeDelta.x * 0.5f;
                    float halfH = rect.sizeDelta.y * 0.5f;
                    screenPos.x = Mathf.Clamp(screenPos.x, halfW, Screen.width - halfW);
                    screenPos.y = Mathf.Clamp(screenPos.y, halfH, Screen.height - halfH);
                }
                rect.position = screenPos;
            }
        }

        private void ClearDynamicButtons()
        {
            foreach (Button btn in dynamic_buttons)
            {
                if (btn != null) Destroy(btn.gameObject);
            }
            dynamic_buttons.Clear();
        }

        private void OnClickAttack()
        {
            if (card == null) return;

            if (card.exhausted)
            {
                WarningText.ShowExhausted();
                Hide();
                return;
            }

            PlayerControls.Get().SetActionMode(ActionMode.SelectingAttackTarget, card, null);
            Hide();
        }

        private void OnClickMove()
        {
            if (card == null) return;

            if (card.has_moved)
            {
                WarningText.ShowText("已移动过");
                Hide();
                return;
            }

            PlayerControls.Get().SetActionMode(ActionMode.SelectingMoveSlot, card, null);
            Hide();
        }

        private void OnClickEffect(int index)
        {
            if (card == null || index < 0 || index >= activatable_abilities.Count) return;

            AbilityData ability = activatable_abilities[index];
            if (ability == null) return;

            Game gdata = GameClient.Get().GetGameData();

            if (ability.target == AbilityTarget.SelectTarget
                || ability.target == AbilityTarget.PlayTarget
                || ability.target == AbilityTarget.ChoiceSelector
                || ability.target == AbilityTarget.CardSelector)
            {
                // Enter target selection mode, ability will be resolved by the selector
                PlayerControls.Get().SetActionMode(ActionMode.SelectingEffectTarget, card, ability);
            }
            else
            {
                GameClient.Get().CastAbility(card, ability);
            }

            Hide();
        }

        private void OnClickCancel()
        {
            PlayerControls.Get().ClearActionMode();
            PlayerControls.Get().UnselectAll();
            Hide();
        }

        private bool HasAttackableTarget(Card attacker)
        {
            Game gdata = GameClient.Get().GetGameData();
            foreach (Player p in gdata.players)
            {
                if (p.player_id != attacker.player_id)
                {
                    foreach (Card c in p.cards_board)
                    {
                        if (c != null && gdata.CanAttackTarget(attacker, c))
                            return true;
                    }
                    if (gdata.CanAttackTarget(attacker, p))
                        return true;
                }
            }
            return false;
        }

        private bool HasMovableSlot(Card card)
        {
            Game gdata = GameClient.Get().GetGameData();
            foreach (Slot slot in Slot.GetAll(card.player_id))
            {
                if (gdata.CanMoveCard(card, slot))
                    return true;
            }
            return false;
        }

        public override void Hide(bool instant = false)
        {
            ClearDynamicButtons();
            card = null;
            board_card = null;
            activatable_abilities.Clear();
            base.Hide(instant);
        }

        public static CardActionDialog Get()
        {
            return instance;
        }
    }
}
