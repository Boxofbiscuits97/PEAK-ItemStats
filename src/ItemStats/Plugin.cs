using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using ExitGames.Client.Photon.StructWrapping;
using HarmonyLib;
using Peak.Afflictions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ItemStats;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    private static ConfigEntry<bool>? showPercentageSign;

    private void Awake()
    {
        Log = Logger;

        showPercentageSign = Config.Bind("Display", "ShowPercentageSign", true, "Whether to show a percentage sign (%) after numeric values.");
        if (!showPercentageSign.Value) ItemStats.precentSign = "";

        Harmony.CreateAndPatchAll(typeof(ItemStats));
        
        Log.LogInfo($"Plugin {Name} is loaded!");
    }
}



public static class ItemStats
{
    private static InventoryItemUI instance = new();
    private static GameObject? templateStat;
    private static float rainbowHue = 0f;
    private static float colorLerp = -1f;
    private static int index = 0;

    public static string precentSign = "%";

    [HarmonyPrefix]
    [HarmonyPatch(typeof(InventoryItemUI), nameof(InventoryItemUI.SetItem))]
    private static void SetItemPatch(InventoryItemUI __instance)
    {
        instance = __instance;
        GameObject slotGameObject = __instance.fuelBar.transform.parent.gameObject;

        if (__instance.isBackpack) return;

        Initialize(out GameObject hungerStat, out GameObject hungerText, out TextMeshProUGUI hungerTMP);

        index = 0;
        AddNewStat(out hungerStat, out hungerTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Hunger/Icon", "hungerStat", CharacterAfflictions.STATUSTYPE.Hunger);
        AddNewStat(out GameObject extraStaminaStat, out TextMeshProUGUI extraStaminaTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/ExtraStaminaBar/Icon", "extraStaminaStat");
        AddNewStat(out GameObject coldStat, out TextMeshProUGUI coldTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Cold/Icon", "coldStat", CharacterAfflictions.STATUSTYPE.Cold);
        AddNewStat(out GameObject injuryStat, out TextMeshProUGUI injuryTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Injury/Icon", "injuryStat", CharacterAfflictions.STATUSTYPE.Injury);
        AddNewStat(out GameObject sleepyStat, out TextMeshProUGUI sleepyTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Sleepy/Icon", "sleepyStat", CharacterAfflictions.STATUSTYPE.Drowsy);
        AddNewStat(out GameObject heatStat, out TextMeshProUGUI heatTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Heat/Icon", "heatStat", CharacterAfflictions.STATUSTYPE.Hot);
        AddNewStat(out GameObject curseStat, out TextMeshProUGUI curseTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Curse/Icon", "curseStat", CharacterAfflictions.STATUSTYPE.Curse);
        AddNewStat(out GameObject sporesStat, out TextMeshProUGUI SporesTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Spores/Icon", "sporesStat", CharacterAfflictions.STATUSTYPE.Spores);
        AddNewStat(out GameObject infiniteStaminaStat, out TextMeshProUGUI infiniteStaminaTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/ExtraStaminaBar/Icon", "infiniteStaminaStat");
        AddNewStat(out GameObject fasterBoiStat, out TextMeshProUGUI fasterBoiTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/ExtraStaminaBar/Icon", "fasterBoiStat");
        AddNewStat(out GameObject invincibilityStat, out TextMeshProUGUI invincibilityTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/OutlineMask/Outline/Shield/ShieldIcon", "invincibilityStat");

        AddNewStat(out GameObject poisonStat, out TextMeshProUGUI poisonTMP, "GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Poison/Icon", "poisonStat", CharacterAfflictions.STATUSTYPE.Poison);


        Item item = Character.localCharacter.data.currentItem;
        if (item == null) return;

        if (!slotGameObject.transform.Find("Name").GetComponent<TextMeshProUGUI>().enabled) return;

        Action_InflictPoison inflictPoisonComponent = item.gameObject.GetComponent<Action_InflictPoison>();
        if (inflictPoisonComponent && inflictPoisonComponent.enabled)
        {
            int incriment = 1;
            float value = inflictPoisonComponent.poisonPerSecond * inflictPoisonComponent.inflictionTime * 100;
            if (poisonTMP.text != "0")
            {
                value += float.Parse(poisonTMP.text.Replace(precentSign, ""));
                incriment = 0;
            }
            string poisonPercentage = "+" + Mathf.Round(value).ToString() + precentSign; ;
            poisonTMP.text = poisonPercentage;
            UpdateStats(ref poisonStat, ref index, incriment);
        }

        Action_RestoreHunger restoreHungerComponent = item.gameObject.GetComponent<Action_RestoreHunger>();
        if (restoreHungerComponent && restoreHungerComponent.restorationAmount != 0)
        {
            float value = restoreHungerComponent.restorationAmount * 100;
            string restorationPercentage = "-" + Mathf.Round(value).ToString() + precentSign;
            hungerTMP.text = restorationPercentage;
            UpdateStats(ref hungerStat, ref index);
        }

        Action_GiveExtraStamina extraStaminaComponent = item.gameObject.GetComponent<Action_GiveExtraStamina>();
        if (extraStaminaComponent && extraStaminaComponent.amount != 0)
        {
            string staminaPercentage = "+" + (extraStaminaComponent.amount * 100).ToString() + precentSign; ;
            extraStaminaTMP.text = staminaPercentage;
            UpdateStats(ref extraStaminaStat, ref index);
        }

        Action_ApplyAffliction[] applyAfflictionComponents = item.gameObject.GetComponents<Action_ApplyAffliction>();
        foreach (Action_ApplyAffliction applyAfflictionComponent in applyAfflictionComponents)
        {
            if (!applyAfflictionComponent.enabled) continue;

            Affliction.AfflictionType afflictionType = applyAfflictionComponent.affliction.GetAfflictionType();

            if (afflictionType == Affliction.AfflictionType.InfiniteStamina)
            {
                Image infiniteStaminaImage = infiniteStaminaStat.GetComponent<Image>();
                rainbowHue += Time.deltaTime * 0.1f;
                if (rainbowHue > 1f) rainbowHue -= 1f;
                Color color = Color.HSVToRGB(rainbowHue, 1f, 1f);
                infiniteStaminaImage.color = color;
                infiniteStaminaTMP.color = color;

                Affliction_InfiniteStamina infiniteStamina = (Affliction_InfiniteStamina)applyAfflictionComponent.affliction;
                Affliction_AdjustDrowsyOverTime adjustDrowsyOverTime = (Affliction_AdjustDrowsyOverTime)infiniteStamina.drowsyAffliction;
                float drowsyTime = infiniteStamina.drowsyAffliction.totalTime * adjustDrowsyOverTime.statusPerSecond;

                string drowsyPercentage = "+" + Mathf.Round(drowsyTime * 100).ToString() + precentSign; ;
                sleepyTMP.text = drowsyPercentage;
                UpdateStats(ref sleepyStat, ref index);

                string staminaTime = applyAfflictionComponent.affliction.totalTime.ToString() + "sec"; ;
                infiniteStaminaTMP.text = staminaTime;
                UpdateStats(ref infiniteStaminaStat, ref index);
            }

            if (afflictionType == Affliction.AfflictionType.FasterBoi)
            {
                Image fasterBoiImage = fasterBoiStat.GetComponent<Image>();
                colorLerp += Time.deltaTime * 2f;
                if (colorLerp > 1f) colorLerp -= 2f;
                Color color = Color.Lerp(extraStaminaTMP.color, coldTMP.color, Mathf.Abs(colorLerp));
                fasterBoiImage.color = color;
                fasterBoiTMP.color = color;

                Affliction_FasterBoi fasterBoi = (Affliction_FasterBoi)applyAfflictionComponent.affliction;

                string drowsyPercentage = "+" + Mathf.Round(fasterBoi.drowsyOnEnd * 100).ToString() + precentSign;
                sleepyTMP.text = drowsyPercentage;
                UpdateStats(ref sleepyStat, ref index);

                string staminaTime = fasterBoi.totalTime.ToString() + "sec"; ;
                fasterBoiTMP.text = staminaTime;
                UpdateStats(ref fasterBoiStat, ref index);
            }

            if (afflictionType == Affliction.AfflictionType.Invincibility)
            {
                string invincibilityTime = applyAfflictionComponent.affliction.totalTime.ToString() + "sec";
                invincibilityTMP.text = invincibilityTime;
                UpdateStats(ref invincibilityStat, ref index);
            }
        }
    }



    private static void Initialize(out GameObject hungerIcon, out GameObject hungerText, out TextMeshProUGUI hungerTMP)
    {
        GameObject sourceHungerIcon = GameObject.Find("GAME/GUIManager/Canvas_HUD/BarGroup/Bar/LayoutGroup/Hunger/Icon");

        GameObject slotGameObject = instance.fuelBar.transform.parent.gameObject;
        Transform hungerIconTransform = slotGameObject.transform.Find("hungerStat");
        if (hungerIconTransform == null)
        {
            hungerIcon = GameObject.Instantiate(sourceHungerIcon, slotGameObject.transform);
            hungerIcon.name = "hungerStat";
            hungerIcon.transform.localPosition = new Vector3(55f, -20f, 0f);
            hungerIcon.transform.localScale = new Vector3(0.66f, 0.66f, 0.66f);

            Outline hungerOutline = hungerIcon.AddComponent<Outline>();
            hungerOutline.effectDistance = new Vector2(1.5f, -1.5f);
        }
        else
        {
            hungerIcon = hungerIconTransform.gameObject;
        }

        Transform hungerTextTransform = hungerIcon.transform.Find("Text");
        if (hungerTextTransform == null)
        {
            GameObject sourceName = slotGameObject.transform.Find("Name").gameObject;
            hungerText = GameObject.Instantiate(sourceName, hungerIcon.transform);
            hungerText.name = "Text";
            hungerText.transform.localPosition = new Vector3(-75f, -25f, 0f);
            hungerTMP = hungerText.GetComponent<TextMeshProUGUI>();
            hungerTMP.text = "-100%";
            hungerTMP.outlineWidth = 0.1f;
            hungerTMP.alignment = TMPro.TextAlignmentOptions.Right;
            hungerTMP.enabled = true;

            Image image = sourceHungerIcon.GetComponent<Image>();
            hungerTMP.color = image.color;
        }
        else
        {
            hungerText = hungerTextTransform.gameObject;
            hungerTMP = hungerText.GetComponent<TextMeshProUGUI>();
        }

        templateStat = hungerIcon;
    }



    private static void AddNewStat(out GameObject objectIcon, out TextMeshProUGUI objectTMP, string name, string iconName, CharacterAfflictions.STATUSTYPE? status = null)
    {
        if (templateStat == null)
        {
            objectIcon = new GameObject();
            objectTMP = objectIcon.AddComponent<TextMeshProUGUI>();
            return;
        }

        Item item = Character.localCharacter.data.currentItem;

        GameObject slotGameObject = instance.fuelBar.transform.parent.gameObject;
        Transform extraStaminaIconTransform = slotGameObject.transform.Find(iconName);
        if (extraStaminaIconTransform == null)
        {
            GameObject sourceExtraStaminaIcon = GameObject.Find(name);

            objectIcon = GameObject.Instantiate(templateStat, slotGameObject.transform);
            objectIcon.name = iconName;
            Image extraStaminaIconImage = objectIcon.GetComponent<Image>();
            Image sourceExtraStaminaIconImage = sourceExtraStaminaIcon.GetComponent<Image>();

            extraStaminaIconImage.sprite = sourceExtraStaminaIconImage.sprite;
            extraStaminaIconImage.color = sourceExtraStaminaIconImage.color;

            objectTMP = objectIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            objectTMP.color = sourceExtraStaminaIconImage.color;
        }
        else
        {
            objectIcon = extraStaminaIconTransform.gameObject;
        }

        objectTMP = objectIcon.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        objectTMP.text = "0";

        objectIcon.SetActive(false);

        if (item == null || status == null) return;

        if (!slotGameObject.transform.Find("Name").GetComponent<TextMeshProUGUI>().enabled) return;

        Action_ModifyStatus[] statusComponents = item.gameObject.GetComponents<Action_ModifyStatus>();
        foreach (Action_ModifyStatus statusComponent in statusComponents)
        {
            if (!statusComponent.enabled) continue;

            float value = statusComponent.changeAmount * 100;
            if (value == 0) continue;
            string changePrecent = Mathf.Round(value).ToString() + precentSign;
            var statusType = statusComponent.statusType;

            if (statusType == status)
            {
                string sign = "";
                if (value > 0) sign = "+";
                objectTMP.text = sign + changePrecent;
                UpdateStats(ref objectIcon, ref index);
            }
        }
    }



    private static void UpdateStats(ref GameObject icon, ref int index, int incriment = 1)
    {
        Vector3 position;
        icon.SetActive(true);
        index += incriment;
        
        if (!instance.isTemporarySlot) position = new Vector3(60f, 80f + 20 * index, 0f);
        else position = new Vector3(20f, 120f + 20 * index, 0f);

        icon.transform.localPosition = position;
    } 
}