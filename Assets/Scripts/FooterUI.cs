using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FooterUI : MonoBehaviour
{
    private static readonly Color PathFutureColor = Color.white;
    private static readonly Color PathCompletedColor = new Color(1f, 1f, 1f, 0f);
    private const int ShopStepIndex = 7;
    private const float PathBounceDuration = 8f;
    private const float PathBounceCycleDuration = 0.3f;
    private static readonly Vector3 PathBounceScale = new Vector3(1.16f, 1.16f, 1f);

    [SerializeField] private RectTransform abilitiesBar;
    [SerializeField] private AbilityButtonUI abilityButton1;
    [SerializeField] private AbilityButtonUI abilityButton2;
    [SerializeField] private AbilityButtonUI abilityButton3;
    [SerializeField] private RectTransform itemsList;
    [SerializeField] private TMP_Text characterNameLabel;
    [SerializeField] private Image characterPortraitImage;
    [SerializeField] private Button portraitButton;
    [SerializeField] private TMP_Text arenaCountLabel;
    [SerializeField] private RectTransform travelContainer;

    private readonly List<Image> travelStepImages = new List<Image>();
    private readonly List<Sprite> travelStepSprites = new List<Sprite>();
    private int lastTravelStepIndex = -1;
    private RectTransform activeTravelStepRect;
    private Coroutine travelStepBounceCoroutine;

    public RectTransform AbilitiesBar => abilitiesBar;
    public AbilityButtonUI AbilityButton1 => abilityButton1;
    public AbilityButtonUI AbilityButton2 => abilityButton2;
    public AbilityButtonUI AbilityButton3 => abilityButton3;
    public RectTransform ItemsList => itemsList;
    public TMP_Text CharacterNameLabel => characterNameLabel;
    public Image CharacterPortraitImage => characterPortraitImage;
    public Button PortraitButton => portraitButton;
    public TMP_Text ArenaCountLabel => arenaCountLabel;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        ResetActiveTravelStepScale();
    }

    public void RefreshCharacter(Character character)
    {
        CacheReferences();

        if (characterNameLabel != null)
        {
            characterNameLabel.text = character != null ? character.CharacterName : string.Empty;
        }

        if (characterPortraitImage != null)
        {
            Sprite portrait = character != null ? character.CharacterPortrait : null;
            characterPortraitImage.sprite = portrait;
            characterPortraitImage.enabled = portrait != null;
        }
    }

    public void RefreshArenaCount(int arenaCount)
    {
        CacheReferences();
        if (arenaCountLabel != null)
        {
            arenaCountLabel.text = $"ARENA {Mathf.Max(1, arenaCount)}";
        }
    }

    public void ShowShopLabel()
    {
        CacheReferences();
        if (arenaCountLabel != null)
        {
            arenaCountLabel.text = "SHOP";
        }
    }

    public void RefreshTravelPath(Character character, int arenaCount, bool isShopOpen)
    {
        CacheReferences();
        CacheTravelSteps();

        if (travelStepImages.Count == 0)
        {
            return;
        }

        int currentStepIndex = ResolveTravelStepIndex(arenaCount, isShopOpen, travelStepImages.Count);
        Sprite characterPathIcon = character != null ? character.CharacterPathIcon : null;

        for (int index = 0; index < travelStepImages.Count; index++)
        {
            Image stepImage = travelStepImages[index];
            if (stepImage == null)
            {
                continue;
            }

            Sprite originalSprite = index < travelStepSprites.Count ? travelStepSprites[index] : stepImage.sprite;
            stepImage.sprite = originalSprite;
            stepImage.color = index < currentStepIndex ? PathCompletedColor : PathFutureColor;
        }

        if (currentStepIndex < 0 || currentStepIndex >= travelStepImages.Count)
        {
            return;
        }

        Image currentStepImage = travelStepImages[currentStepIndex];
        if (currentStepImage == null)
        {
            return;
        }

        currentStepImage.sprite = characterPathIcon != null ? characterPathIcon : travelStepSprites[currentStepIndex];
        currentStepImage.color = Color.white;

        RectTransform currentStepRect = currentStepImage.rectTransform;
        if (lastTravelStepIndex != currentStepIndex)
        {
            ResetActiveTravelStepScale();
            activeTravelStepRect = currentStepRect;
            if (activeTravelStepRect != null && gameObject.activeInHierarchy)
            {
                travelStepBounceCoroutine = StartCoroutine(BounceTravelStep(activeTravelStepRect));
            }
        }
        else if (activeTravelStepRect == null)
        {
            activeTravelStepRect = currentStepRect;
        }

        lastTravelStepIndex = currentStepIndex;
    }

    private void CacheReferences()
    {
        if (abilitiesBar == null)
        {
            Transform abilitiesTransform = transform.Find("Abilities");
            if (abilitiesTransform != null)
            {
                abilitiesBar = abilitiesTransform as RectTransform;
            }
        }

        if (abilityButton1 == null && abilitiesBar != null)
        {
            abilityButton1 = GetOrAddAbilityButton("BAbility (1)")
                ?? GetOrAddAbilityButton("BAbility1")
                ?? GetOrAddAbilityButton("BAbility");
        }

        if (abilityButton2 == null && abilitiesBar != null)
        {
            abilityButton2 = GetOrAddAbilityButton("BAbility (2)")
                ?? GetOrAddAbilityButton("BAbility2");
        }

        if (abilityButton3 == null && abilitiesBar != null)
        {
            abilityButton3 = GetOrAddAbilityButton("BAbility (3)")
                ?? GetOrAddAbilityButton("BAbility3");
        }

        if (itemsList == null)
        {
            Transform itemsTransform = transform.Find("ItemsList");
            if (itemsTransform != null)
            {
                itemsList = itemsTransform as RectTransform;
            }
        }

        if (characterNameLabel == null)
        {
            characterNameLabel = transform.Find("Chara-Name")?.GetComponent<TMP_Text>();
        }

        if (characterPortraitImage == null)
        {
            characterPortraitImage = transform.Find("iChara")?.GetComponent<Image>();
        }

        if (portraitButton == null)
        {
            portraitButton = transform.Find("BPortrait")?.GetComponent<Button>();
        }

        if (arenaCountLabel == null)
        {
            arenaCountLabel = transform.Find("iArenaCount/tArenaCount")?.GetComponent<TMP_Text>();
            if (arenaCountLabel == null)
            {
                arenaCountLabel = transform.Find("tArenaCount")?.GetComponent<TMP_Text>();
            }
        }

        if (travelContainer == null)
        {
            travelContainer = transform.Find("Travel") as RectTransform;
        }
    }

    private void CacheTravelSteps()
    {
        if (travelContainer == null)
        {
            travelStepImages.Clear();
            travelStepSprites.Clear();
            return;
        }

        if (travelStepImages.Count == travelContainer.childCount
            && travelStepSprites.Count == travelContainer.childCount)
        {
            return;
        }

        travelStepImages.Clear();
        travelStepSprites.Clear();

        for (int index = 0; index < travelContainer.childCount; index++)
        {
            Image stepImage = travelContainer.GetChild(index).GetComponent<Image>();
            if (stepImage == null)
            {
                continue;
            }

            travelStepImages.Add(stepImage);
            travelStepSprites.Add(stepImage.sprite);
        }
    }

    private static int ResolveTravelStepIndex(int arenaCount, bool isShopOpen, int stepCount)
    {
        if (stepCount <= 0)
        {
            return 0;
        }

        if (isShopOpen)
        {
            return Mathf.Clamp(ShopStepIndex, 0, stepCount - 1);
        }

        int normalizedArenaCount = Mathf.Max(1, arenaCount);
        int stepIndex = normalizedArenaCount <= ShopStepIndex
            ? normalizedArenaCount - 1
            : normalizedArenaCount;

        return Mathf.Clamp(stepIndex, 0, stepCount - 1);
    }

    private System.Collections.IEnumerator BounceTravelStep(RectTransform targetRect)
    {
        if (targetRect == null)
        {
            yield break;
        }

        float elapsed = 0f;
        Vector3 baseScale = Vector3.one;

        while (elapsed < PathBounceDuration && targetRect == activeTravelStepRect)
        {
            float cycleProgress = Mathf.PingPong(elapsed / PathBounceCycleDuration, 1f);
            float easedProgress = Mathf.SmoothStep(0f, 1f, cycleProgress);
            targetRect.localScale = Vector3.Lerp(baseScale, PathBounceScale, easedProgress);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (targetRect != null)
        {
            targetRect.localScale = baseScale;
        }

        if (travelStepBounceCoroutine != null && targetRect == activeTravelStepRect)
        {
            travelStepBounceCoroutine = null;
        }
    }

    private void ResetActiveTravelStepScale()
    {
        if (travelStepBounceCoroutine != null)
        {
            StopCoroutine(travelStepBounceCoroutine);
            travelStepBounceCoroutine = null;
        }

        if (activeTravelStepRect != null)
        {
            activeTravelStepRect.localScale = Vector3.one;
        }
    }

    private AbilityButtonUI GetOrAddAbilityButton(string childName)
    {
        if (abilitiesBar == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        Transform buttonTransform = abilitiesBar.Find(childName);
        if (buttonTransform == null)
        {
            return null;
        }

        AbilityButtonUI abilityButton = buttonTransform.GetComponent<AbilityButtonUI>();
        if (abilityButton == null)
        {
            abilityButton = buttonTransform.gameObject.AddComponent<AbilityButtonUI>();
        }

        return abilityButton;
    }
}
