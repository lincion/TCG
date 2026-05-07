using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    //Contains all gameplay state data that is sync across network

    [System.Serializable]
    public class Game
    {
        public string game_uid;
        public GameSettings settings;

        //Game state
        public int first_player = 0;
        public int current_player = 0;
        public int turn_count = 0;
        public float turn_timer = 0f;

        public GameState state = GameState.Connecting;
        public GamePhase phase = GamePhase.None;

        //Players
        public Player[] players;

        //Selector
        public SelectorType selector = SelectorType.None;
        public int selector_player_id = 0;
        public string selector_ability_id;
        public string selector_caster_uid;

        //Other reference values
        public string last_played;
        public string last_target;
        public string last_destroyed;
        public string last_summoned;
        public string ability_triggerer;
        public int rolled_value;
        public int selected_value;

        //Other reference arrays
        public HashSet<string> ability_played = new HashSet<string>();
        public HashSet<string> cards_attacked = new HashSet<string>();

        //连锁系统字段
        public ChainStack chain_stack = new ChainStack();   //连锁堆叠
        public int current_priority_player = 0;             //当前持有优先权的玩家
        public bool skip_chain = false;                     //跳过连锁（用户设置）
        public bool both_passed = false;                    //双方都放弃优先权

        //市场字段
        public List<Card> cards_market = new List<Card>();  //市场区的卡牌（双方可见）

        public Game() { }
        
        public Game(string uid, int nb_players)
        {
            this.game_uid = uid;
            players = new Player[nb_players];
            for (int i = 0; i < nb_players; i++)
                players[i] = new Player(i);
            settings = GameSettings.Default;
        }

        public virtual bool AreAllPlayersReady()
        {
            int ready = 0;
            foreach (Player player in players)
            {
                if (player.IsReady())
                    ready++;
            }
            return ready >= settings.nb_players;
        }

        public virtual bool AreAllPlayersConnected()
        {
            int ready = 0;
            foreach (Player player in players)
            {
                if (player.IsConnected())
                    ready++;
            }
            return ready >= settings.nb_players;
        }

        //Check if its player's turn
        public virtual bool IsPlayerTurn(Player player)
        {
            return IsPlayerActionTurn(player) || IsPlayerSelectorTurn(player);
        }

        public virtual bool IsPlayerActionTurn(Player player)
        {
            return player != null && current_player == player.player_id 
                && state == GameState.Play && phase == GamePhase.Main && selector == SelectorType.None;
        }

        public virtual bool IsPlayerSelectorTurn(Player player)
        {
            return player != null && selector_player_id == player.player_id 
                && state == GameState.Play && phase == GamePhase.Main && selector != SelectorType.None;
        }

        public virtual bool IsPlayerMulliganTurn(Player player)
        {
            return phase == GamePhase.Mulligan && !player.ready;
        }
        
        //Check if a card is allowed to be played on slot
        public virtual bool CanPlayCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (card == null)
                return false;

            Player player = GetPlayer(card.player_id);
            if (!skip_cost && !player.CanPayMana(card))
                return false; //Cant pay mana
            if (!player.HasCard(player.cards_hand, card))
                return false; // Card not in hand
            if (player.is_ai && card.CardData.IsDynamicManaCost() && player.mana == 0)
                return false; // AI cant play X-cost card at 0 cost

            if (card.CardData.IsBoardCard())
            {
                if (!slot.IsValid() || IsCardOnSlot(slot))
                    return false;   //Slot already occupied
                if (Slot.GetP(card.player_id) != slot.p)
                    return false; //Cant play on opponent side

                //天赋卡特殊入场规则：必须在己方天赋卡/生物卡距离1的位置打出
                if (card.CardData.IsTalent() || card.CardData.IsTalentCreature())
                {
                    if (!IsTalentPlacementValid(card, slot))
                        return false;
                }

                return true;
            }
            if (card.CardData.IsEquipment())
            {
                if (!slot.IsValid())
                    return false;

                Card target = GetSlotCard(slot);
                if (target == null || target.CardData.type != CardType.Character || target.player_id != card.player_id)
                    return false; //Target must be an allied character

                return true;
            }
            if (card.CardData.IsRequireTargetSpell())
            {
                return IsPlayTargetValid(card, slot); //Check play target on slot
            }
            if (card.CardData.type == CardType.Spell)
            {
                return CanAnyPlayAbilityTrigger(card); //Check if spell will have abilities
            }
            return true;
        }

        //Check if a card is allowed to move to slot
        public virtual bool CanMoveCard(Card card, Slot slot, bool skip_cost = false)
        {
            if (card == null || !slot.IsValid())
                return false;

            if (!IsOnBoard(card))
                return false; //Only cards in play can move

            if (!card.CanMove(skip_cost))
                return false; //Card cant move

            if (Slot.GetP(card.player_id) != slot.p)
                return false; //Card played wrong side

            if (card.slot == slot)
                return false; //Cant move to same slot

            Card slot_card = GetSlotCard(slot);
            if (slot_card != null)
                return false; //Already a card there

            //曼哈顿距离移动范围判定：移动距离 <= 行动值
            int move_range = card.action_value;
            int distance = card.slot.ManhattanDistance(slot);
            if (distance > move_range)
                return false; //超出移动距离

            //十字方向移动：x和y只能有一个方向变化（不能走斜角）
            int dx = Mathf.Abs(card.slot.x - slot.x);
            int dy = Mathf.Abs(card.slot.y - slot.y);
            if (dx > 0 && dy > 0)
                return false; //不能斜角移动

            //路径阻挡检查
            if (HasPathBlock(card, slot))
                return false; //路径上有阻挡

            return true;
        }

        /// <summary>检查移动路径上是否有阻挡（不能经过对方卡牌、己方生物）</summary>
        public virtual bool HasPathBlock(Card mover, Slot target)
        {
            Slot from = mover.slot;
            int step_x = (target.x > from.x) ? 1 : (target.x < from.x) ? -1 : 0;
            int step_y = (target.y > from.y) ? 1 : (target.y < from.y) ? -1 : 0;

            int cur_x = from.x + step_x;
            int cur_y = from.y + step_y;

            //遍历路径上的每个格子（不包括起点和终点）
            while (cur_x != target.x || cur_y != target.y)
            {
                Slot check_slot = new Slot(cur_x, cur_y, from.p);
                Card blocking_card = GetSlotCard(check_slot);
                if (blocking_card != null)
                {
                    if (blocking_card.player_id != mover.player_id)
                        return true; //对方卡牌阻挡
                    if (blocking_card.CardData.IsCharacter())
                        return true; //己方生物阻挡
                }
                cur_x += step_x;
                cur_y += step_y;
            }

            return false;
        }

        //Check if a card is allowed to attack a player
        public virtual bool CanAttackTarget(Card attacker, Player target, bool skip_cost = false)
        {
            if(attacker == null || target == null)
                return false;

            if (!attacker.CanAttack(skip_cost))
                return false; //Card cant attack

            if (attacker.player_id == target.player_id)
                return false; //Cant attack same player

            if (!IsOnBoard(attacker) || !attacker.CardData.IsCharacter())
                return false; //Cards not on board

            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; //Protected by taunt

            return true;
        }

        //Check if a card is allowed to attack another one
        public virtual bool CanAttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return false;

            if (!attacker.CanAttack(skip_cost))
                return false; //Card cant attack

            if (attacker.player_id == target.player_id)
                return false; //Cant attack same player

            if (!IsOnBoard(attacker) || !IsOnBoard(target))
                return false; //Cards not on board

            if (!attacker.CardData.IsCharacter() || !target.CardData.IsBoardCard())
                return false; //Only character can attack

            if (target.HasStatus(StatusType.Stealth))
                return false; //Stealth cant be attacked

            if (target.HasStatus(StatusType.CannotBeAttacked))
                return false; //不能被攻击

            if (target.HasStatus(StatusType.Protected) && !attacker.HasStatus(StatusType.Flying))
                return false; //Protected by adjacent card

            //曼哈顿距离攻击范围判定（默认攻击距离为1，action_value表示攻击距离/移动力）
            int attack_range = attacker.action_value;
            int distance = attacker.slot.ManhattanDistance(target.slot);
            if (distance > attack_range)
                return false; //超出攻击距离

            return true;
        }

        public virtual bool CanCastAbility(Card card, AbilityData ability)
        {
            if (ability == null || card == null || !card.CanDoActivatedAbilities())
                return false; //This card cant cast

            if (ability.trigger != AbilityTrigger.Activate)
                return false; //Not an activated ability

            Player player = GetPlayer(card.player_id);
            if (!player.CanPayAbility(card, ability))
                return false; //Cant pay for ability

            if (!ability.AreTriggerConditionsMet(this, card))
                return false; //Conditions not met

            return true;
        }

        //For choice selector
        public virtual bool CanSelectAbility(Card card, AbilityData ability)
        {
            if (ability == null || card == null || !card.CanDoAbilities())
                return false; //This card cant cast

            Player player = GetPlayer(card.player_id);
            if (!player.CanPayAbility(card, ability))
                return false; //Cant pay for ability

            if (!ability.AreTriggerConditionsMet(this, card))
                return false; //Conditions not met

            return true;
        }

        public virtual bool CanAnyPlayAbilityTrigger(Card card)
        {
            if (card == null)
                return false;
            if (card.CardData.IsDynamicManaCost())
                return true; //Cost not decided so condition could be false

            foreach (AbilityData ability in card.GetAbilities())
            {
                if (ability.trigger == AbilityTrigger.OnPlay && ability.AreTriggerConditionsMet(this, card))
                    return true;
            }
            return false;
        }

        //Check if Player play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Player target)
        {
            if (caster == null || target == null)
                return false;

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        //Check if Card play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Card target)
        {
            if (caster == null || target == null)
                return false;

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        //Check if Slot play target is valid, play target is the target when a spell requires to drag directly onto another card
        public virtual bool IsPlayTargetValid(Card caster, Slot target)
        {
            if (caster == null)
                return false;

            if (target.IsPlayerSlot())
                return IsPlayTargetValid(caster, GetPlayer(target.p)); //Slot 0,0, means we are targeting a player

            Card slot_card = GetSlotCard(target);
            if (slot_card != null)
                return IsPlayTargetValid(caster, slot_card); //Slot has card, check play target on that card

            foreach (AbilityData ability in caster.GetAbilities())
            {
                if (ability && ability.trigger == AbilityTrigger.OnPlay && ability.target == AbilityTarget.PlayTarget)
                {
                    if (!ability.CanTarget(this, caster, target))
                        return false;
                }
            }
            return true;
        }

        public Player GetPlayer(int id)
        {
            if (id >= 0 && id < players.Length)
                return players[id];
            return null;
        }

        public Player GetActivePlayer()
        {
            return GetPlayer(current_player);
        }

        public Player GetOpponentPlayer(int id)
        {
            int oid = id == 0 ? 1 : 0;
            return GetPlayer(oid);
        }

        public Card GetCard(string card_uid)
        {
            foreach (Player player in players)
            {
                Card acard = player.GetCard(card_uid);
                if (acard != null)
                    return acard;
            }
            return null;
        }

        public Card GetBoardCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetEquipCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_equip)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetHandCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_hand)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetDeckCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_deck)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetDiscardCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_discard)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetSecretCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_secret)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetTempCard(string card_uid)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_temp)
                {
                    if (card != null && card.uid == card_uid)
                        return card;
                }
            }
            return null;
        }

        public Card GetSlotCard(Slot slot)
        {
            foreach (Player player in players)
            {
                foreach (Card card in player.cards_board)
                {
                    if (card != null && card.slot == slot)
                        return card;
                }
            }
            return null;
        }
        
        public virtual Player GetRandomPlayer(System.Random rand)
        {
            Player player = GetPlayer(rand.NextDouble() < 0.5 ? 1 : 0);
            return player;
        }

        public virtual Card GetRandomBoardCard(System.Random rand)
        {
            Player player = GetRandomPlayer(rand);
            return player.GetRandomCard(player.cards_board, rand);
        }

        public virtual Slot GetRandomSlot(System.Random rand)
        {
            Player player = GetRandomPlayer(rand);
            return player.GetRandomSlot(rand);
        }

        public bool IsInHand(Card card)
        {
            return card != null && GetHandCard(card.uid) != null;
        }

        public bool IsOnBoard(Card card)
        {
            return card != null && GetBoardCard(card.uid) != null;
        }

        public bool IsEquipped(Card card)
        {
            return card != null && GetEquipCard(card.uid) != null;
        }

        public bool IsInDeck(Card card)
        {
            return card != null && GetDeckCard(card.uid) != null;
        }

        public bool IsInDiscard(Card card)
        {
            return card != null && GetDiscardCard(card.uid) != null;
        }

        public bool IsInSecret(Card card)
        {
            return card != null && GetSecretCard(card.uid) != null;
        }

        public bool IsInTemp(Card card)
        {
            return card != null && GetTempCard(card.uid) != null;
        }

        public bool IsCardOnSlot(Slot slot)
        {
            return GetSlotCard(slot) != null;
        }

        public bool HasStarted()
        {
            return state != GameState.Connecting;
        }

        public bool HasEnded()
        {
            return state == GameState.GameEnded;
        }

        /// <summary>检查天赋卡/天赋生物卡的入场位置是否合法：必须在己方天赋卡/生物卡距离1的位置</summary>
        public virtual bool IsTalentPlacementValid(Card card, Slot targetSlot)
        {
            Player player = GetPlayer(card.player_id);

            //检查战场上是否有己方卡牌在目标格距离1范围内
            foreach (Card boardCard in player.cards_board)
            {
                if (boardCard == null) continue;

                //己方天赋卡或生物卡都可以作为"锚点"
                if (boardCard.CardData.IsTalent() || boardCard.CardData.IsCharacter())
                {
                    int dist = targetSlot.ManhattanDistance(boardCard.slot);
                    if (dist == 1)
                        return true; //距离1内有己方卡牌
                }
            }

            return false;
        }

        /// <summary>获取驻留在指定天赋卡上的生物卡</summary>
        public virtual Card GetGarrisonCard(Card talentCard)
        {
            if (talentCard == null || string.IsNullOrEmpty(talentCard.garrison_card_uid))
                return null;
            return GetCard(talentCard.garrison_card_uid);
        }

        /// <summary>获取侵占指定天赋卡的对方生物卡</summary>
        public virtual Card GetOccupyCard(Card talentCard)
        {
            if (talentCard == null || string.IsNullOrEmpty(talentCard.occupy_card_uid))
                return null;
            return GetCard(talentCard.occupy_card_uid);
        }

        //Same as clone, but also instantiates the variable (much slower)
        public static Game CloneNew(Game source)
        {
            Game game = new Game();
            Clone(source, game);
            return game;
        }

        //Clone all variables into another var, used mostly by the AI when building a prediction tree
        public static void Clone(Game source, Game dest)
        {
            dest.game_uid = source.game_uid;
            dest.settings = source.settings;

            dest.first_player = source.first_player;
            dest.current_player = source.current_player;
            dest.turn_count = source.turn_count;
            dest.turn_timer = source.turn_timer;
            dest.state = source.state;
            dest.phase = source.phase;

            if (dest.players == null)
            {
                dest.players = new Player[source.players.Length];
                for(int i=0; i< source.players.Length; i++)
                    dest.players[i] = new Player(i);
            }

            for (int i = 0; i < source.players.Length; i++)
                Player.Clone(source.players[i], dest.players[i]);

            dest.selector = source.selector;
            dest.selector_player_id = source.selector_player_id;
            dest.selector_caster_uid = source.selector_caster_uid;
            dest.selector_ability_id = source.selector_ability_id;

            dest.last_destroyed = source.last_destroyed;
            dest.last_played = source.last_played;
            dest.last_target = source.last_target;
            dest.last_summoned = source.last_summoned;
            dest.ability_triggerer = source.ability_triggerer;
            dest.rolled_value = source.rolled_value;
            dest.selected_value = source.selected_value;

            CloneHash(source.ability_played, dest.ability_played);
            CloneHash(source.cards_attacked, dest.cards_attacked);

            //连锁系统
            ChainStack.Clone(source.chain_stack, dest.chain_stack);
            dest.current_priority_player = source.current_priority_player;
            dest.skip_chain = source.skip_chain;
            dest.both_passed = source.both_passed;

            //市场——通过各玩家的cards_all字典查找已克隆的卡牌引用
            dest.cards_market.Clear();
            foreach (Card scard in source.cards_market)
            {
                Card found = null;
                foreach (Player p in dest.players)
                {
                    if (p.cards_all.TryGetValue(scard.uid, out found))
                        break;
                }
                if (found != null)
                    dest.cards_market.Add(found);
            }
        }

        public static void CloneHash(HashSet<string> source, HashSet<string> dest)
        {
            dest.Clear();
            foreach (string str in source)
                dest.Add(str);
        }
    }

    [System.Serializable]
    public enum GameState
    {
        Connecting = 0, //Players are not connected
        Play = 20,      //Game is being played
        GameEnded = 99,
    }

    [System.Serializable]
    public enum GamePhase
    {
        None = 0,
        Mulligan = 5,
        StartTurn = 10, //Start of turn resolution
        Main = 20,      //Main play phase
        CombatDeclare = 22,   //攻击宣言阶段
        CombatDamage = 24,    //伤害结算阶段
        CombatResolve = 26,   //战斗结算阶段
        PriorityWait = 28,    //优先权等待（连锁响应）
        EndTurn = 30,   //End of turn resolutions
    }

    [System.Serializable]
    public enum SelectorType
    {
        None = 0,
        SelectTarget = 10,
        SelectorCard = 20,
        SelectorChoice = 30,
        SelectorCost = 40,
    }

    /// <summary>
    /// 连锁堆叠（参考游戏王/万智牌堆叠机制）
    /// 后发动的效果先处理（LIFO）
    /// </summary>
    [System.Serializable]
    public class ChainStack
    {
        public List<ChainEntry> entries = new List<ChainEntry>();   //堆叠中的效果

        /// <summary>将效果加入连锁堆叠</summary>
        public void AddEffect(string ability_id, string caster_uid, string triggerer_uid)
        {
            ChainEntry entry = new ChainEntry();
            entry.ability_id = ability_id;
            entry.caster_uid = caster_uid;
            entry.triggerer_uid = triggerer_uid;
            entry.chain_order = entries.Count;
            entries.Add(entry);
        }

        /// <summary>逆序结算：取出最后一个效果</summary>
        public ChainEntry Pop()
        {
            if (entries.Count == 0) return null;
            ChainEntry top = entries[entries.Count - 1];
            entries.RemoveAt(entries.Count - 1);
            return top;
        }

        /// <summary>获取当前连锁层数</summary>
        public int GetChainCount()
        {
            return entries.Count;
        }

        /// <summary>清空连锁堆叠</summary>
        public void Clear()
        {
            entries.Clear();
        }

        /// <summary>克隆连锁数据</summary>
        public static void Clone(ChainStack source, ChainStack dest)
        {
            dest.entries.Clear();
            foreach (ChainEntry entry in source.entries)
            {
                dest.entries.Add(ChainEntry.CloneNew(entry));
            }
        }
    }

    /// <summary>连锁堆叠中的单个效果条目</summary>
    [System.Serializable]
    public class ChainEntry
    {
        public string ability_id;
        public string caster_uid;
        public string triggerer_uid;
        public int chain_order;  //连锁顺序（0=第一个发动）

        public static ChainEntry CloneNew(ChainEntry source)
        {
            ChainEntry entry = new ChainEntry();
            entry.ability_id = source.ability_id;
            entry.caster_uid = source.caster_uid;
            entry.triggerer_uid = source.triggerer_uid;
            entry.chain_order = source.chain_order;
            return entry;
        }
    }
}