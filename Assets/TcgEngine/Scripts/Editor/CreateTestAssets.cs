using UnityEngine;
using UnityEditor;
using System.IO;
using TcgEngine;

namespace TcgEngine.EditorTool
{
    /// <summary>
    /// Creates all test card and ability assets for the TCG test suite.
    /// Run from Unity menu: TcgEngine > Create Test Assets
    /// </summary>
    public static class CreateTestAssets
    {
        private const string abilityPath = "Assets/TcgEngine/Resources/Abilities/test/";
        private const string effectPath = "Assets/TcgEngine/Resources/Effects/test/";
        private const string cardPath = "Assets/TcgEngine/Resources/Cards/Test/";
        private const string deckPath = "Assets/TcgEngine/Resources/Decks/";

        [MenuItem("TcgEngine/Create Test Assets")]
        public static void CreateAll()
        {
            CreateEffectAssets();
            CreateAbilityAssets();
            CreateCardAssets();
            CreateTestDeck();
            AssetDatabase.Refresh();
            Debug.Log("All test assets created successfully!");
        }

        static void EnsureDir(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            EnsureDir(path);
            T asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static int ToEnumIndex<T>(T value) where T : System.Enum
        {
            string name = value.ToString();
            string[] names = System.Enum.GetNames(typeof(T));
            return System.Array.IndexOf(names, name);
        }

        // ---- Effect Assets ----
        static EffectData effectDraw;
        static EffectData effectDamage;
        static EffectData effectAddAttack;
        static EffectData effectAddHP;
        static EffectData effectRefresh;
        static EffectData effectReturnToHand;
        static EffectData effectExhaust;

        static void CreateEffectAssets()
        {
            effectDraw = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_draw.asset");
            if (effectDraw == null)
            {
                effectDraw = CreateAsset<EffectDraw>(effectPath + "effect_draw.asset");
            }

            effectDamage = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_damage_test.asset");
            if (effectDamage == null)
            {
                effectDamage = CreateAsset<EffectDamage>(effectPath + "effect_damage_test.asset");
            }

            effectAddAttack = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_add_attack.asset");
            if (effectAddAttack == null)
            {
                effectAddAttack = CreateAsset<EffectAddStat>(effectPath + "effect_add_attack.asset");
                var so = new SerializedObject(effectAddAttack);
                so.FindProperty("type").enumValueIndex = 1; // Attack
                so.ApplyModifiedProperties();
            }

            effectAddHP = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_add_hp.asset");
            if (effectAddHP == null)
            {
                effectAddHP = CreateAsset<EffectAddStat>(effectPath + "effect_add_hp.asset");
                var so = new SerializedObject(effectAddHP);
                so.FindProperty("type").enumValueIndex = 2; // HP
                so.ApplyModifiedProperties();
            }

            effectRefresh = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_refresh.asset");
            if (effectRefresh == null)
            {
                effectRefresh = CreateAsset<EffectRefresh>(effectPath + "effect_refresh.asset");
            }

            effectReturnToHand = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_return_to_hand.asset");
            if (effectReturnToHand == null)
            {
                effectReturnToHand = CreateAsset<EffectReturnToHand>(effectPath + "effect_return_to_hand.asset");
            }

            effectExhaust = AssetDatabase.LoadAssetAtPath<EffectData>(effectPath + "effect_exhaust.asset");
            if (effectExhaust == null)
            {
                effectExhaust = CreateAsset<EffectExhaust>(effectPath + "effect_exhaust.asset");
                var so = new SerializedObject(effectExhaust);
                so.FindProperty("exhausted").boolValue = true;
                so.ApplyModifiedProperties();
            }
        }

        // ---- Ability Assets ----
        static void CreateAbilityAssets()
        {
            // talent_draw: [横置] 抽1张卡
            CreateAbility("talent_draw", "抽卡",
                AbilityTrigger.Activate, AbilityTarget.None,
                new EffectData[] { effectDraw }, null,
                exhaust: true, mana_cost: 0, value: 1);

            // talent_kill: [横置] 破坏1只生物
            CreateAbility("talent_kill", "破坏",
                AbilityTrigger.Activate, AbilityTarget.SelectTarget,
                new EffectData[] { effectDamage }, null,
                exhaust: true, mana_cost: 0, value: 999);

            // talent_buff_lv2: 己方生物获得+1+1 (Ongoing)
            CreateAbility("talent_buff_lv2", "强化光环",
                AbilityTrigger.Ongoing, AbilityTarget.AllCardsBoard,
                new EffectData[] { effectAddAttack, effectAddHP }, null,
                exhaust: false, mana_cost: 0, value: 1);

            // talent_reset_lv3: [横置] 重置一张卡
            CreateAbility("talent_reset_lv3", "重置",
                AbilityTrigger.Activate, AbilityTarget.SelectTarget,
                new EffectData[] { effectRefresh }, null,
                exhaust: true, mana_cost: 0, value: 0);

            // graveyard_return: 墓地从墓地回手
            CreateAbility("graveyard_return", "墓地回手",
                AbilityTrigger.Activate, AbilityTarget.Self,
                new EffectData[] { effectReturnToHand }, null,
                exhaust: false, mana_cost: 0, value: 0);

            // discard_end: 回合结束墓地丢弃
            CreateAbility("discard_end", "终末咒印",
                AbilityTrigger.EndOfTurn, AbilityTarget.PlayerSelf,
                new EffectData[] { effectReturnToHand }, null,
                exhaust: false, mana_cost: 0, value: 1);
        }

        static void CreateAbility(string id, string title,
            AbilityTrigger trigger, AbilityTarget target,
            EffectData[] effects, StatusData[] statuses,
            bool exhaust, int mana_cost, int value)
        {
            string path = abilityPath + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<AbilityData>(path) != null)
                return;

            // Create instance first, set id before saving to disk
            EnsureDir(path);
            AbilityData ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.id = id;
            AssetDatabase.CreateAsset(ability, path);

            var so = new SerializedObject(ability);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("title").stringValue = title;
            so.FindProperty("trigger").enumValueIndex = ToEnumIndex(trigger);
            so.FindProperty("target").enumValueIndex = ToEnumIndex(target);
            so.FindProperty("value").intValue = value;
            so.FindProperty("exhaust").boolValue = exhaust;
            so.FindProperty("mana_cost").intValue = mana_cost;

            if (effects != null)
            {
                var effectsProp = so.FindProperty("effects");
                effectsProp.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++)
                    effectsProp.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];
            }

            if (statuses != null)
            {
                var statusProp = so.FindProperty("status");
                statusProp.arraySize = statuses.Length;
                for (int i = 0; i < statuses.Length; i++)
                    statusProp.GetArrayElementAtIndex(i).objectReferenceValue = statuses[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ability);
        }

        // ---- Card Assets ----
        static CardData templateCharacter; // wolf_baby or similar
        static CardData templateSpell;     // any spell card

        static CardData FindTemplate(CardType type)
        {
            var guids = AssetDatabase.FindAssets("t:CardData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (card == null) continue;
                if (card.type == type && card.art_full != null
                    && !path.Contains("Test/") && !path.Contains("test"))
                    return card;
            }
            return null;
        }

        static void CreateCardAssets()
        {
            templateCharacter = FindTemplate(CardType.Character);
            templateSpell = FindTemplate(CardType.Spell);
            Debug.Log($"Template char: {templateCharacter?.id}, spell: {templateSpell?.id}");

            // Talent Lv1 — clone from character template
            CreateTalent("talent_test_lv1", "试验天赋 Lv1",
                2, 0, 0,
                new string[] { "talent_draw", "talent_kill" },
                "空城[横置]抽1张卡\n驻守[横置]破坏1只生物");

            // Talent Lv2
            CreateTalent("talent_test_lv1_lv2", "试验天赋 Lv2",
                0, 0, 0,
                new string[] { "talent_buff_lv2" },
                "空城-己方生物获得+1+1\n驻守-无效果");

            // Talent Lv3
            CreateTalent("talent_test_lv1_lv2_lv3", "试验天赋 Lv3",
                0, 0, 0,
                new string[] { "talent_reset_lv3" },
                "空城与驻守效果都为[横置]重置一张卡");

            // Creature with graveyard recursion — clone from character template
            CreateCard("test_graveyard_creature", "墓守灵", CardType.Character,
                2, 1, 2,
                new string[] { "graveyard_return" },
                "这张卡在墓地存在时，墓地的这张卡回到手卡", templateCharacter);

            // Spell with discard end-of-turn effect — clone from spell template
            CreateCard("test_discard_spell", "终末咒印", CardType.Spell,
                1, 0, 0,
                new string[] { "discard_end" },
                "回合结束时，如果这张卡在墓地存在，丢弃1张手卡", templateSpell);
        }

        static void CreateTalent(string id, string title,
            int mana, int attack, int hp, string[] abilityIds, string text)
        {
            // Talents are special — clone from character template but set Talent type
            CreateCard(id, title, CardType.Talent, mana, attack, hp, abilityIds, text, templateCharacter);
        }

        static CardData CreateCard(string id, string title, CardType type,
            int mana, int attack, int hp, string[] abilityIds, string text, CardData template)
        {
            string path = cardPath + id + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
            }

            // Clone from template to get all visual references (art, team, rarity, frame, etc.)
            CardData card;
            if (template != null)
            {
                card = ScriptableObject.Instantiate(template);
                card.name = id;
                card.id = id; // Must set before AssetDatabase.CreateAsset to avoid dictionary key conflicts
                AssetDatabase.CreateAsset(card, path);
            }
            else
            {
                card = CreateAsset<CardData>(path);
            }

            var so = new SerializedObject(card);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("title").stringValue = title;
            so.FindProperty("type").enumValueIndex = ToEnumIndex(type);
            so.FindProperty("mana").intValue = mana;
            so.FindProperty("attack").intValue = attack;
            so.FindProperty("hp").intValue = hp;
            so.FindProperty("text").stringValue = text;
            so.FindProperty("deckbuilding").boolValue = true;

            // Set abilities
            var abilitiesProp = so.FindProperty("abilities");
            abilitiesProp.arraySize = abilityIds.Length;
            for (int i = 0; i < abilityIds.Length; i++)
            {
                string abPath = abilityPath + abilityIds[i] + ".asset";
                var ab = AssetDatabase.LoadAssetAtPath<AbilityData>(abPath);
                abilitiesProp.GetArrayElementAtIndex(i).objectReferenceValue = ab;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(card);
            Debug.Log($"Created card: {id} (template: {template?.id ?? "none"})");
            return card;
        }

        // ---- Test Deck ----
        static void CreateTestDeck()
        {
            string deckPathFull = deckPath + "test_deck_tcg.asset";
            if (AssetDatabase.LoadAssetAtPath<DeckData>(deckPathFull) != null)
            {
                Debug.Log("Test deck already exists, skipping.");
                return;
            }

            DeckData deck = CreateAsset<DeckData>(deckPathFull);
            var so = new SerializedObject(deck);
            so.FindProperty("id").stringValue = "test_deck_tcg";
            so.FindProperty("title").stringValue = "TCG Test Deck";

            // Hero
            var heroes = AssetDatabase.FindAssets("t:CardData hero");
            if (heroes.Length > 0)
            {
                var hero = AssetDatabase.LoadAssetAtPath<CardData>(
                    AssetDatabase.GUIDToAssetPath(heroes[0]));
                so.FindProperty("hero").objectReferenceValue = hero;
            }

            // Populate cards: 3x talent Lv1, 3x Lv2, 3x Lv3, 4x creature, 4x spell + filler
            var cardsList = new System.Collections.Generic.List<CardData>();
            AddCardCopies(cardsList, cardPath + "talent_test_lv1.asset", 3);
            AddCardCopies(cardsList, cardPath + "talent_test_lv1_lv2.asset", 3);
            AddCardCopies(cardsList, cardPath + "talent_test_lv1_lv2_lv3.asset", 3);
            AddCardCopies(cardsList, cardPath + "test_graveyard_creature.asset", 4);
            AddCardCopies(cardsList, cardPath + "test_discard_spell.asset", 4);

            // Fill remaining slots with existing cards
            var allCards = AssetDatabase.FindAssets("t:CardData");
            foreach (var guid in allCards)
            {
                if (cardsList.Count >= 30) break;
                var cpath = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<CardData>(cpath);
                if (c != null && c.deckbuilding && c.type != CardType.Hero
                    && !cpath.Contains("Test/"))
                {
                    cardsList.Add(c);
                }
            }

            var cardsProp = so.FindProperty("cards");
            cardsProp.arraySize = cardsList.Count;
            for (int i = 0; i < cardsList.Count; i++)
                cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardsList[i];

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(deck);
            Debug.Log($"Created test deck with {cardsList.Count} cards.");

            // Update GameplayData
            UpdateGameplayData();
        }

        static void AddCardCopies(System.Collections.Generic.List<CardData> list, string path, int count)
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card != null)
            {
                for (int i = 0; i < count; i++)
                    list.Add(card);
            }
        }

        static void UpdateGameplayData()
        {
            var guids = AssetDatabase.FindAssets("t:GameplayData");
            if (guids.Length > 0)
            {
                var gd = AssetDatabase.LoadAssetAtPath<GameplayData>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                var deck = AssetDatabase.LoadAssetAtPath<DeckData>(deckPath + "test_deck_tcg.asset");

                var so = new SerializedObject(gd);
                so.FindProperty("test_deck").objectReferenceValue = deck;
                so.FindProperty("test_deck_ai").objectReferenceValue = deck;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(gd);
                Debug.Log("Updated GameplayData.test_deck reference.");
            }
        }
    }
}
