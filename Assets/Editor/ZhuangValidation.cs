using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs in an isolated empty scene in batch mode. It never writes save data or game scenes.
[InitializeOnLoad]
public static class ZhuangValidation
{
    public const string AssetRoot = "Assets/Data/Abilities/Zhuang_Abilities/";
    private const string Pending = "ZhuangValidation.Pending";
    public static int Assertions;
    public static readonly List<string> Results = new List<string>();
    static ZhuangValidation() { EditorApplication.playModeStateChanged += OnPlayMode; }

    [MenuItem("Tools/Rogue Sliders/Validate Zhuang Asset References")]
    public static void ValidateAssets()
    {
        string[] assets = AssetDatabase.FindAssets("t:AbilityDefinition", new[] { AssetRoot.TrimEnd('/') });
        Check(assets.Length == 13, "13 abilities including separate Side Step and Black Turtle Step");
        foreach (string id in assets)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            AbilityDefinition ability = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
            Check(ability != null && !ability.GetType().Name.Contains("Placeholder"), path + " has a concrete implementation");
            Check(ability.Icon != null, ability.name + " icon resolves");
            string expectedClip = ExpectedAnimation(ability.name);
            Check(expectedClip == null ? ability.AttackAnimationClip == null : ability.AttackAnimationClip != null && ability.AttackAnimationClip.name == expectedClip,
                ability.name + " uses the appropriate attack/cast animation, or movement only");
            SerializedObject serialized = new SerializedObject(ability);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Check(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0, ability.name + "." + property.propertyPath + " is not a missing reference");
                if (property.objectReferenceValue is AnimationClip clip)
                    Check(AssetDatabase.GetAssetPath(clip).StartsWith("Assets/Animations/Characters/Zhuang_Animations/"), ability.name + "." + property.name + " uses Zhuang's rig");
                if (property.objectReferenceValue is GameObject prefab && property.name.Contains("Sound"))
                {
                    SoundParameters sound = prefab.GetComponentInChildren<SoundParameters>(true);
                    Check(sound != null, ability.name + "." + property.name + " contains SoundParameters");
                    SerializedProperty clips = new SerializedObject(sound).FindProperty("possibleClips");
                    Check(clips.arraySize > 0 && Enumerable.Range(0, clips.arraySize).Any(i => clips.GetArrayElementAtIndex(i).objectReferenceValue != null), prefab.name + " contains playable audio");
                }
            }
            if (ability is ZhuangAbility zhuang)
                Check(zhuang.CastFxPrefab != null && zhuang.ImpactFxPrefab != null && zhuang.CastSoundParametersPrefab != null && zhuang.ImpactSoundParametersPrefab != null, ability.name + " cast/impact FX and audio resolve");
            foreach (AbilityUpgradeRewardDefinition upgrade in ability.LinkedUpgradeRewards)
                Check(upgrade != null && upgrade.Ability == ability, ability.name + " upgrade belongs to this ability");
        }
        GameObject column = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ObstaclePrefabs/ZhuangJadeColumn.prefab");
        Check(column != null && column.GetComponent<Rock>() != null, "Jade Column has Rock component");
        Character characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Chara-Zhuang.prefab").GetComponent<Character>();
        string placeholder = new SerializedObject(characterPrefab).FindProperty("attackPlaceholderClipName").stringValue;
        var controller = characterPrefab.GetComponentInChildren<Animator>(true).runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        var attack = controller.layers[0].stateMachine.states.Single(state => state.state.name == "Attack").state;
        Check(attack.motion.name == placeholder, "Zhuang overrides the clip actually played by the Attack state");
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Zhuang.asset");
        foreach (AbilityRewardDefinition reward in data.UnlockableAbilityRewards)
            Check(!reward.LockByDefault, reward.name + " is eligible without a progression unlock");
        Debug.Log("Zhuang assets: " + Assertions + " assertions passed.");
    }

    private static string ExpectedAnimation(string name)
    {
        switch (name)
        {
            case "ZhuangFauchard": case "ZhuangDragonGuandao": case "ZhuangSerpentHook": return "Attack_Front_Spear";
            case "ZhuangTigerFists": return "Attack_Front_fist";
            case "ZhuangPhoenixJian": return "Attack_Front_Sword";
            case "ZhuangCranePunishment": return "Attack_Spiral";
            case "ZhuangCelestialRings": case "ZhuangLionsRoar": case "ZhuangJadeColumn": return "Incantation";
            default: return null;
        }
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use batch mode for the isolated gameplay suite.");
        try
        {
            ValidateAssets();
            SessionState.SetBool(Pending, true);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Zhuang validation runner").AddComponent<ZhuangValidationRunner>();
    }

    public static AbilityDefinition Load(string name) => AssetDatabase.LoadAssetAtPath<AbilityDefinition>(AssetRoot + "Zhuang" + name + ".asset");
    public static void Set(object target, string field, object value)
    {
        Type type = target.GetType();
        FieldInfo info = null;
        while (type != null && info == null) { info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); type = type.BaseType; }
        if (info == null) throw new MissingFieldException(field);
        info.SetValue(target, value);
    }

    public static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Assertions++;
        Results.Add(message);
    }

    public static void Finish(Exception error)
    {
        string report = "Zhuang validation: " + (error == null ? "PASS" : "FAIL") + "\nAssertions: " + Assertions + "\n" + string.Join("\n", Results) + "\n" + error;
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "zhuang-validation.txt"), report);
        Debug.Log(report);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class ZhuangValidationRunner : MonoBehaviour
{
    private Fixture fixture;
    private Exception loggedError;
    private void Start() { Application.logMessageReceived += Log; StartCoroutine(Guard(Suite())); }
    private void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) loggedError = new Exception(message + "\n" + stack);
    }

    private IEnumerator Guard(IEnumerator suite)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(suite);
        Exception failure = null;
        while (stack.Count > 0)
        {
            object current = null;
            try
            {
                if (loggedError != null) throw loggedError;
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                current = stack.Peek().Current;
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
            }
            catch (Exception e) { failure = e; break; }
            yield return current;
        }
        fixture?.Dispose();
        Application.logMessageReceived -= Log;
        ZhuangValidation.Finish(failure);
    }

    private Fixture New(params string[] abilities)
    {
        fixture?.Dispose();
        fixture = new Fixture(abilities);
        return fixture;
    }

    private IEnumerator Idle()
    {
        float until = Time.realtimeSinceStartup + 12;
        yield return null;
        while (fixture.Character.IsBusy && Time.realtimeSinceStartup < until) yield return null;
        ZhuangValidation.Check(!fixture.Character.IsBusy, "action lock released");
    }

    private IEnumerator Suite()
    {
        ZhuangValidation.ValidateAssets();
        yield return ActiveItemRegression();
        RewardRegression();
        yield return AnimationRegression();
        Fixture f = New("Fauchard");
        Enemy target = f.Enemy(3, 4, 20);
        ZhuangValidation.Check(!f.Ability(0).CanActivateOnCell(f.Character, f.Runtime(0), new Vector2Int(3, 5)), "Fauchard rejects distant target");
        f.Use(0, 3, 4); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 15, "Fauchard deals 5 damage");

        f = New("DragonGuandao", "CranePunishment");
        target = f.Enemy(3, 5, 20);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 13 && target.IsAirborne, "Guandao distant damage and launch");
        ZhuangValidation.Check(f.Runtime(1).IsUsable(f.Character) && Time.timeScale < 1, "Crane opens during Momentum");
        f.Upgrade(AbilityUpgradeKey.ZhuangGraceOfTheCrane);
        ZhuangValidation.Set(f.Character, "currentHealth", 10);
        int movement = f.Character.RemainingMovementPoints;
        f.Use(1); yield return Idle();
        ZhuangValidation.Check(f.Character.RemainingMovementPoints == 0 && f.Character.CurrentHealth == 10 + movement, "Crane spends all movement and heals for Grace");
        ZhuangValidation.Check(target.CurrentHealth == 9 && Mathf.Approximately(Time.timeScale, 1), "Crane damage and clock restored");

        f = New("DragonGuandao", "CranePunishment");
        f.Upgrade(AbilityUpgradeKey.ZhuangDragonCloud);
        target = f.Enemy(3, 5, 7, 2);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(target == null || target.CurrentHealth == 0, "Dragon Cloud executes despite resistance");
        ZhuangValidation.Check(Mathf.Approximately(Time.timeScale, 1), "Automatic execute does not open Momentum");

        f = New("DragonGuandao", "CranePunishment");
        target = f.Enemy(3, 5, 30);
        f.Use(0, 3, 5); yield return Idle();
        yield return new WaitForSecondsRealtime(2.6f);
        ZhuangValidation.Check(!f.Runtime(1).IsUsable(f.Character) && Mathf.Approximately(Time.timeScale, 1), "Momentum expires after real time");

        f = New("TigerFists");
        target = f.Enemy(3, 4, 30);
        f.Rock(3, 5);
        f.Use(0, 3, 4); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 23 && target.GridPosition == new Vector2Int(3, 4) && target.IsAirborne, "Tiger wall collision is 5 + 2 and stays on its tile");

        f = New("TigerFists");
        f.Upgrade(AbilityUpgradeKey.ZhuangWhiteTiger);
        target = f.Enemy(3, 4, 30);
        f.Use(0, 3, 3);
        ZhuangValidation.Check(f.Runtime(0).RemainingUsesThisTurn == 1, "White Tiger transform grants unused temporary attack");
        yield return new WaitForSeconds(0.8f);
        int hp = f.Character.CurrentHealth;
        f.Use(0, 3, 4); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 23 && target.GridPosition == new Vector2Int(3, 6) && f.Character.CurrentHealth == hp - 2, "White Tiger +2 damage, 2-cell push, 2 HP cost");
        f.Character.HandlePlayerTurnEnded();
        ZhuangValidation.Check(!f.Runtime(0).GetState<ZhuangTigerFistsAbility.State>().White, "White Tiger expires at turn end");

        f = New("TigerFists");
        f.Upgrade(AbilityUpgradeKey.ZhuangTigerClaws);
        target = f.Enemy(2, 4, 20);
        Enemy stronger = f.Enemy(4, 4, 30);
        f.Use(0, 3, 4); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 15 && stronger.CurrentHealth == 30, "Tiger Claws pulls weakest adjacent enemy");

        f = New("TigerFists");
        f.Upgrade(AbilityUpgradeKey.ZhuangStoneTiger);
        Rock rock = f.Rock(3, 4);
        target = f.Enemy(3, 5, 20);
        f.Use(0, 3, 4); yield return Idle();
        ZhuangValidation.Check((rock == null || rock.IsDestroyed) && target.CurrentHealth == 15 && target.GridPosition.y == 6, "Stone Tiger destroys rock and strikes enemy behind it");

        f = New("PhoenixJian");
        f.Upgrade(AbilityUpgradeKey.ZhuangTongueOfFire);
        target = f.Enemy(3, 4, 6);
        Enemy side = f.Enemy(2, 4, 20);
        Enemy outside = f.Enemy(2, 3, 20);
        f.Use(0, 3, 4); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 2 && side.CurrentHealth == 16 && outside.CurrentHealth == 20, "Phoenix three-cell frontal sweep");
        target.GetComponent<ZhuangEnemyEffects>().EndEnemyTurn();
        f.Runtime(0).BeginTurn(f.Character);
        ZhuangValidation.Check(f.Runtime(0).RemainingUsesThisTurn == 2, "Burn kill grants next-turn Phoenix charge");

        f = New("JadeColumn", "CranePunishment");
        ZhuangValidation.Check(!f.Ability(0).CanActivateOnCell(f.Character, f.Runtime(0), new Vector2Int(3, 4)), "Jade rejects adjacent tile without Jade Eye");
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(f.Board.TryGetRock(new Vector2Int(3, 5), out rock) && !f.Board.IsCellWalkable(new Vector2Int(3, 5)), "Jade is a real blocking rock");
        f.Board.HandlePlayerTurnStarted(f.Character);
        ZhuangValidation.Check(f.Board.IsCellWalkable(new Vector2Int(3, 5)), "Jade clears occupancy at next player turn");

        f = New("JadeColumn", "CranePunishment");
        f.Upgrade(AbilityUpgradeKey.ZhuangImperialProjection);
        target = f.Enemy(3, 5, 20);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(f.Runtime(1).IsUsable(f.Character), "Jade projection leaves time to use Crane");
        f.Character.GetComponent<CombatSlowMotion>().Close();
        yield return new WaitForSeconds(0.7f);
        ZhuangValidation.Check(target.CurrentHealth == 19 && !f.Board.TryGetRock(new Vector2Int(3, 5), out _), "Projected Jade collapses then deals landing damage");

        f = New("LionsRoar", "Fauchard");
        f.Upgrade(AbilityUpgradeKey.ZhuangBeastlyFear);
        f.Upgrade(AbilityUpgradeKey.ZhuangLionStartle);
        target = f.Enemy(3, 9, 20, 3);
        outside = f.Enemy(3, 4, 20, 3);
        f.Use(0);
        ZhuangValidation.Check(target.Resistance == 1 && outside.Resistance == 3, "Lion affects distant enemies with enhanced far reduction");
        f.Character.DealDamageToEnemy(target, 5, true, true, DamageSoundType.Sword, f.Ability(1));
        ZhuangValidation.Check(target.CurrentHealth == 15 && target.IsAirborne, "Lion mark adds damage to the hit and launches");
        f.Character.HandlePlayerTurnEnded();
        ZhuangValidation.Check(target.Resistance == 3, "Lion resistance reduction expires with mark");

        f = New("SideStep");
        f.Use(0);
        int points = f.Character.RemainingMovementPoints;
        ZhuangValidation.Check(f.Ability(0).TryActivateFromSelectedCell(f.Character, f.Runtime(0), new Vector2Int(3, 4)), "Side Step selected adjacent cell");
        yield return Idle();
        ZhuangValidation.Check(f.Character.RemainingMovementPoints == points - 1, "Side Step spends movement");

        f = New("BlackTurtleStep");
        f.Upgrade(AbilityUpgradeKey.ZhuangSacredCarapace);
        f.Upgrade(AbilityUpgradeKey.ZhuangSpikedCarapace);
        f.Upgrade(AbilityUpgradeKey.ZhuangDoubleStep);
        target = f.Enemy(4, 5, 20);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(f.Character.Resistance == 1 && f.Character.GridPosition.y == 5, "Black Turtle range upgrade and base resistance");
        f.Character.TakeDamage(4, target);
        ZhuangValidation.Check(f.Character.Resistance == 2 && target.CurrentHealth == 19, "Turtle builds +1 resistance and retaliates on hit");
        f.Character.HandleEnemyTurnEnded();
        ZhuangValidation.Check(f.Character.Resistance == 0, "Turtle defense expires after enemy turn");

        f = New("MonkeyStrike");
        f.Upgrade(AbilityUpgradeKey.ZhuangGoldenMonkeyStrike);
        target = f.Enemy(3, 7, 2);
        f.Use(0, 3, 6); yield return Idle();
        ZhuangValidation.Check((target == null || target.CurrentHealth == 0) && f.Runtime(0).RemainingUsesThisTurn == 1 && f.Runtime(0).RemainingCooldown == 0, "Golden Monkey lethal landing refunds a usable attack");
        ZhuangValidation.Check(Vector3.Distance(f.Character.transform.position, f.Board.GridToWorldPosition(f.Character.GridPosition)) < 1f, "Leap lands at its grid destination");

        f = New("SerpentHook");
        f.Upgrade(AbilityUpgradeKey.ZhuangFlyingSerpent);
        f.Upgrade(AbilityUpgradeKey.ZhuangTwinSerpents);
        f.Runtime(0).BeginTurn(f.Character);
        target = f.Enemy(3, 7, 20);
        f.Use(0, 3, 7); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 19 && target.IsAirborne && f.Character.GridPosition.y == 6 && f.Runtime(0).RemainingUsesThisTurn == 1, "Serpent rush, damage, knock and second charge");

        f = New("CelestialRings");
        target = f.Enemy(3, 5, 20);
        ZhuangValidation.Check(!f.Runtime(0).IsUsable(f.Character) && f.Ability(0).GetCounterText(f.Runtime(0)) == "0", "Rings start empty");
        f.Upgrade(AbilityUpgradeKey.ZhuangDivineRings);
        for (int i = 0; i < 3; i++) { ZhuangAbilityUtils.Launch(f.Character, target); yield return null; }
        f.Runtime(0).BeginTurn(f.Character);
        ZhuangValidation.Check(f.Runtime(0).GetState<ZhuangCelestialRingsAbility.State>().Charges == 3 && f.Ability(0).GetDisplayName(f.Character, f.Runtime(0)) == "Divine Rings", "Knock charges persist and promote at 3");
        side = f.Enemy(4, 5, 20);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(target.CurrentHealth == 17 && side.CurrentHealth == 17 && f.Runtime(0).GetState<ZhuangCelestialRingsAbility.State>().Divine, "Divine ring hits main and adjacent target, keeps promotion at 2");

        f = New("CelestialRings");
        f.Upgrade(AbilityUpgradeKey.ZhuangCelestialRebound);
        target = f.Enemy(3, 5, 2);
        ZhuangAbilityUtils.Launch(f.Character, target);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(f.Runtime(0).GetState<ZhuangCelestialRingsAbility.State>().Charges == 1, "Lethal ring returns its charge on arrival");

        f = New("HeronWings");
        f.Upgrade(AbilityUpgradeKey.ZhuangHeronGlide);
        f.Upgrade(AbilityUpgradeKey.ZhuangDivineFeather);
        f.Upgrade(AbilityUpgradeKey.ZhuangFeatherWhirlwind);
        target = f.Enemy(2, 5, 20);
        side = f.Enemy(4, 5, 20);
        ZhuangValidation.Set(f.Character, "currentHealth", 10);
        ZhuangAbilityUtils.Launch(f.Character, target);
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(f.Character.CurrentHealth == 11 && target.CurrentHealth == 19 && side.CurrentHealth == 19 && f.Runtime(0).RemainingUsesThisTurn == 1, "Heron bonus charge, landing heal and radial damage");
        f.Runtime(0).BeginTurn(f.Character);
        ZhuangValidation.Check(f.Runtime(0).BonusUsesThisTurn == 0, "Heron bonus charge expires next turn");

        f = New("HeronWings");
        FireTileHazard crossedFire = new GameObject("Crossed fire").AddComponent<FireTileHazard>();
        crossedFire.Configure(f.Board, null, new Vector2Int(3, 4), 7, null, null, null);
        FireTileHazard landingFire = new GameObject("Landing fire").AddComponent<FireTileHazard>();
        landingFire.Configure(f.Board, null, new Vector2Int(3, 5), 3, null, null, null);
        int healthBeforeLeap = f.Character.CurrentHealth;
        f.Use(0, 3, 5); yield return Idle();
        ZhuangValidation.Check(f.Character.CurrentHealth == healthBeforeLeap - 3, "Leaps skip crossed ground hazards but trigger landing hazards");
    }

    private static AbilityDefinition Bonus(string name) => AssetDatabase.LoadAssetAtPath<AbilityDefinition>("Assets/Data/Abilities/Bonus_Abilities/" + name + ".asset");

    private GameTurnManager TurnManager(Fixture f)
    {
        GameTurnManager turns = f.Root.AddComponent<GameTurnManager>();
        turns.enabled = false;
        ZhuangValidation.Set(turns, "board", f.Board);
        return turns;
    }

    private IEnumerator ActiveItemRegression()
    {
        Fixture f = New("Fauchard");
        GameTurnManager turns = TurnManager(f);
        f.Character.TryAddBonusAbility(Bonus("PotionHeal"), false);
        f.Character.TryAddBonusAbility(Bonus("VoidGlove"), false);
        f.Character.TryAddBonusAbility(Bonus("RogueBow"), false);
        CharacterAbilityRuntime glove = f.Character.GetBonusAbility(1);
        CharacterAbilityRuntime bow = f.Character.GetBonusAbility(2);
        ZhuangValidation.Check(!turns.RequestBonusAbilityUse(1), "Void Glove disabled without a valid pull");
        Enemy target = f.Enemy(3, 4, 20);
        f.Enemy(2, 3, 30);
        ZhuangValidation.Check(turns.RequestBonusAbilityUse(1) && turns.PendingBonusAbilitySlot == 1 && glove.UsesThisTurn == 0, "Void Glove button enters targeting without spending its use");
        ZhuangValidation.Check(turns.RequestAbilityUse(0) && turns.PendingBonusAbilitySlot == -1 && turns.PendingCellTargetAbilityIndex == 0, "Switching to a character skill clears pending bonus targeting");
        ZhuangValidation.Check(turns.RequestBonusAbilityUse(1) && turns.PendingCellTargetAbilityIndex == -1, "Switching back to an active item clears skill targeting");
        ZhuangValidation.Check(!f.Character.TryUseBonusAbility(1, new Vector2Int(3, 4)) && glove.UsesThisTurn == 0, "Void Glove rejects occupied cells without spending a use");
        f.Enemy(3, 8, 10);
        ZhuangValidation.Check(!glove.Definition.TryActivate(f.Character, glove, new Vector2Int(3, 9)), "Void Glove rejects out-of-range pulls even via direct activation");

        Camera camera = new GameObject("Targeting camera").AddComponent<Camera>();
        camera.transform.SetParent(f.Root.transform);
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 10;
        camera.pixelRect = new Rect(0, 0, 800, 800);
        camera.transform.position = f.Board.transform.position + Vector3.up * 20;
        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
        Vector2 screen = camera.WorldToScreenPoint(f.Board.GridToWorldPosition(new Vector2Int(3, 5)));
        typeof(GameTurnManager).GetMethod("TryUseTargetedAbility", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(turns, new object[] { f.Character, screen });
        ZhuangValidation.Check(target.GridPosition == new Vector2Int(3, 5) && glove.UsesThisTurn == 1 && turns.PendingBonusAbilitySlot == -1, "Screen click commits Void Glove pull and clears targeting");
        ZhuangValidation.Check(!turns.RequestBonusAbilityUse(1), "Void Glove cannot be reused in the same turn");
        ZhuangValidation.Set(f.Character, "currentHealth", 10);
        ZhuangValidation.Check(turns.RequestBonusAbilityUse(0) && f.Character.CurrentHealth == 15 && f.Character.GetBonusAbility(0) == null, "Heal potion activates and consumes only its own slot");
        ZhuangValidation.Check(ReferenceEquals(glove, f.Character.GetBonusAbility(1)) && glove.UsesThisTurn == 1 && ReferenceEquals(bow, f.Character.GetBonusAbility(2)), "Consuming a potion preserves other active item runtimes and uses");
        glove.SetRemainingCooldown(3);
        f.Character.CompactBonusAbilitiesLeft();
        ZhuangValidation.Check(ReferenceEquals(glove, f.Character.GetBonusAbility(0)) && glove.RemainingCooldown == 3 && glove.UsesThisCombat == 1, "Slot compaction preserves cooldown and combat uses");
        f.Character.TryAddBonusAbility(Bonus("PotionHeal"), false);
        ZhuangValidation.Check(ReferenceEquals(glove, f.Character.GetBonusAbility(0)) && glove.RemainingCooldown == 3, "Adding a potion preserves active item state");
        f.Character.ResetTurn();
        ZhuangValidation.Check(glove.UsesThisTurn == 0 && glove.RemainingCooldown == 2 && glove.UsesThisCombat == 1, "Only a new turn resets turn uses and ticks cooldown once");

        f = New();
        turns = TurnManager(f);
        f.Character.TryAddBonusAbility(Bonus("VoidGlove"), false);
        Enemy strong = f.Enemy(2, 5, 30);
        Enemy weak = f.Enemy(4, 5, 10);
        glove = f.Character.GetBonusAbility(0);
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        for (int i = 0; i < 5; i++) glove.Definition.CanShowPotentialTargetCell(f.Character, glove, new Vector2Int(3, 5));
        ZhuangValidation.Check(UnityEngine.Random.state.Equals(randomState), "Void Glove previews do not perturb random rewards or target selection");
        ZhuangValidation.Check(f.Character.TryUseBonusAbility(0, new Vector2Int(3, 5)) && weak.GridPosition == new Vector2Int(3, 5) && strong.GridPosition == new Vector2Int(2, 5), "Void Glove pulls the weakest eligible enemy");

        foreach (string item in new[] { "RogueBow", "RogueKnife" })
        {
            f = New();
            turns = TurnManager(f);
            f.Character.TryAddBonusAbility(Bonus(item), false);
            target = f.Enemy(3, item == "RogueBow" ? 6 : 4, 20);
            ZhuangValidation.Check(turns.RequestBonusAbilityUse(0), item + " activates from its UI request with a single target");
            yield return new WaitForSeconds(0.8f);
            int damage = item == "RogueBow" ? 1 : 2;
            ZhuangValidation.Check(target.CurrentHealth == 20 - damage && !turns.RequestBonusAbilityUse(0), item + " deals damage and respects its turn limit");
            f.Character.ResetTurn();
            ZhuangValidation.Set(target, "currentHealth", 1);
            ZhuangValidation.Check(turns.RequestBonusAbilityUse(0), item + " is available on the next turn");
            yield return new WaitForSeconds(0.8f);
            ZhuangValidation.Check(f.Character.GetBonusAbilityPersistentValue(item + "_damage_bonus") == 1, item + " kill increases persistent damage");
        }

        foreach (string potion in new[] { "PotionStrength", "PotionResistance", "PotionMobility", "PotionFearAll" })
        {
            f = New();
            turns = TurnManager(f);
            target = f.Enemy(3, 5, 20);
            int movement = f.Character.RemainingMovementPoints;
            f.Character.ConsumeMovementPoint();
            f.Character.TryAddBonusAbility(Bonus(potion), false);
            ZhuangValidation.Check(turns.RequestBonusAbilityUse(0) && f.Character.GetBonusAbility(0) == null, potion + " activates and is consumed");
            if (potion == "PotionStrength") ZhuangValidation.Check(f.Character.BonusDamage == 3, "Strength potion grants 3 damage");
            if (potion == "PotionResistance") ZhuangValidation.Check(f.Character.Resistance == 3, "Resistance potion grants 3 resistance");
            if (potion == "PotionMobility") ZhuangValidation.Check(f.Character.RemainingMovementPoints == movement, "Mobility potion restores movement");
            if (potion == "PotionFearAll") ZhuangValidation.Check((int)typeof(Enemy).GetField("forcedFearTurnsRemaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target) == 1, "Fear potion applies fear to enemies");
        }
    }

    private void RewardRegression()
    {
        Fixture f = New("Fauchard", "SideStep");
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/Data/Characters/Zhuang.asset");
        ZhuangValidation.Set(f.Character, "characterData", data);
        ZhuangValidation.Set(f.Board, "runRewardState", f.Rewards);
        var ownRewards = new HashSet<RewardDefinition>(data.UnlockableAbilityRewards);
        for (int i = 0; i < 20; i++)
        {
            List<RewardOffer> choices = f.Board.GenerateRewardChoices();
            ZhuangValidation.Check(choices.Count == 3 && choices.All(offer => offer != null && ownRewards.Contains(offer.Definition)), "Level-end generation offers Zhuang skills (sample " + i + ")");
        }
        foreach (AbilityCategory category in new[] { AbilityCategory.BasicAttack, AbilityCategory.MobilitySkill, AbilityCategory.SpecialPower })
        {
            AbilityRewardDefinition reward = data.UnlockableAbilityRewards.First(r => r.Ability.Category == category);
            reward.Apply(f.Rewards);
        }
        List<RewardOffer> upgrades = f.Board.GenerateRewardChoices();
        ZhuangValidation.Check(upgrades.Count == 3 && upgrades.All(offer => offer.Definition is AbilityUpgradeRewardDefinition upgrade && f.Rewards.HasAbility(upgrade.Ability)), "After choosing each category, level-end rewards offer upgrades for equipped Zhuang skills");
    }

    private IEnumerator AnimationRegression()
    {
        Fixture f = New("SideStep");
        Character prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Chara-Zhuang.prefab").GetComponent<Character>();
        Animator animator = Object.Instantiate(prefab.GetComponentInChildren<Animator>(true), f.Character.transform);
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        ZhuangValidation.Set(f.Character, "characterAnimator", animator);
        ZhuangValidation.Set(f.Character, "attackPlaceholderClipName", new SerializedObject(prefab).FindProperty("attackPlaceholderClipName").stringValue);
        animator.Rebind();
        animator.Update(0);
        f.Use(0);
        animator.Update(0.15f);
        ZhuangValidation.Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") && !animator.IsInTransition(0), "Preparing Side Step stays idle instead of attacking");
        ZhuangValidation.Check(f.Ability(0).TryActivateFromSelectedCell(f.Character, f.Runtime(0), new Vector2Int(3, 4)), "Side Step move starts with the real Zhuang animator");
        animator.Update(0.01f);
        animator.Update(0.08f);
        ZhuangValidation.Check(animator.GetBool("Dashing") && animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Side Step uses the locomotion Dash state");
        yield return Idle();
        foreach (string name in new[] { "Fauchard", "DragonGuandao", "TigerFists", "PhoenixJian", "CranePunishment", "LionsRoar", "JadeColumn", "CelestialRings", "SerpentHook" })
        {
            animator.Rebind(); animator.Update(0);
            AnimationClip clip = ZhuangValidation.Load(name).AttackAnimationClip;
            f.Character.PlayAttackAnimation(clip);
            animator.Update(0.01f); animator.Update(0.12f);
            ZhuangValidation.Check(animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip == clip) || animator.GetNextAnimatorClipInfo(0).Any(info => info.clip == clip), name + " plays its assigned clip through the real controller");
        }
        animator.SetBool("Dashing", true);
        animator.Update(0.01f); animator.Update(0.12f);
        ZhuangValidation.Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"), "Serpent's pull transitions from casting to locomotion without waiting for the attack to finish");
    }

    private sealed class Fixture : IDisposable
    {
        public GameObject Root;
        public BoardManager Board;
        public Character Character;
        public PlayerRunRewardState Rewards;
        public Fixture(string[] names)
        {
            Root = new GameObject("Validation fixture");
            Board = Root.AddComponent<BoardManager>();
            BoardCell[,] cells = new BoardCell[Board.Width, Board.Height];
            for (int x = 0; x < Board.Width; x++)
            for (int y = 0; y < Board.Height; y++) cells[x, y] = new BoardCell { GridPosition = new Vector2Int(x, y), WorldPosition = new Vector3(x, 0, y) };
            ZhuangValidation.Set(Board, "cells", cells);
            Player player = Root.AddComponent<Player>();
            ZhuangValidation.Set(Board, "player", player);
            GameObject chara = new GameObject("Test Zhuang");
            chara.transform.SetParent(Root.transform);
            Character = chara.AddComponent<Character>();
            ZhuangValidation.Set(Character, "maxHealth", 50);
            Character.Assign(player, new Vector2Int(3, 3), Board);
            player.AssignCharacter(Character);
            cells[3, 3].SetOccupant(chara, BoardOccupantKind.PlayerCharacter);
            List<CharacterAbilityRuntime> runtimes = (List<CharacterAbilityRuntime>)Character.Abilities;
            runtimes.Clear();
            foreach (string name in names) runtimes.Add(new CharacterAbilityRuntime(ZhuangValidation.Load(name)));
            Rewards = new PlayerRunRewardState();
            Rewards.InitializeFrom(runtimes.Select(r => r.Definition).ToList(), 50);
            ZhuangValidation.Set(Character, "runRewardState", Rewards);
            Character.ResetTurn();
        }
        public AbilityDefinition Ability(int index) => Character.Abilities[index].Definition;
        public CharacterAbilityRuntime Runtime(int index) => Character.Abilities[index];
        public void Upgrade(AbilityUpgradeKey key) => Rewards.AddUpgrade(key);
        public void Use(int index, int? x = null, int? y = null)
        {
            ZhuangValidation.Check(Runtime(index).TryUse(Character, x.HasValue ? new Vector2Int(x.Value, y.Value) : (Vector2Int?)null), Ability(index).name + " activation succeeds");
        }
        public Enemy Enemy(int x, int y, int health, int resistance = 0)
        {
            GameObject obj = new GameObject("Test enemy");
            obj.transform.SetParent(Root.transform);
            Enemy enemy = obj.AddComponent<Enemy>();
            Transform body = new GameObject("Body").transform;
            body.SetParent(obj.transform, false);
            ZhuangValidation.Set(enemy, "enemyBody", body);
            ZhuangValidation.Set(enemy, "maxHealth", health);
            ZhuangValidation.Set(enemy, "resistance", resistance);
            enemy.Assign(new Vector2Int(x, y), Board);
            Board.Cells[x, y].SetOccupant(obj, BoardOccupantKind.Enemy);
            ((List<Enemy>)Board.SpawnedEnemies).Add(enemy);
            return enemy;
        }
        public Rock Rock(int x, int y)
        {
            GameObject obj = new GameObject("Test rock");
            obj.transform.SetParent(Root.transform);
            Rock rock = obj.AddComponent<Rock>();
            rock.Assign(Board, new Vector2Int(x, y));
            Board.Cells[x, y].SetStaticObstacle(obj);
            return rock;
        }
        public void Dispose() { Object.DestroyImmediate(Root); }
    }
}
