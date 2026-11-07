using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMFixes
{
    /// <summary>
    /// Scouting quality of life (Kyle's request, 11/07/26):
    ///   1. Browse the scouting list from a person's detail screen. When you open a driver, designer
    ///      or mechanic from Scouting, the game's own left/right arrows beside the status label
    ///      (normally only for people in a team, and hidden for free agents) step through the
    ///      scouting list exactly as it was filtered and sorted. The left/right arrow keys do the same.
    ///      Back still returns straight to Scouting.
    ///   2. A Marketability column for drivers, in place of Nationality, sortable by clicking the header.
    ///      Unscouted drivers show "?" and sort to the bottom. Designers and mechanics keep Nationality.
    /// </summary>
    internal static class ScoutBrowse
    {
        internal static readonly List<Person> List = new List<Person>();
        internal static bool Active;          // the current detail screen was opened from Scouting
        private static bool sPending;         // set just before a scouting-originated screen change
        private static bool sPopBackStack;    // our own arrow navigation: drop the extra back-stack entry

        internal static void ArmFromScouting(UIScoutingStaffEntry entry)
        {
            List.Clear();
            try
            {
                UIScoutingSearchResultsWidget w = entry.screen != null ? entry.screen.searchWidget : null;
                List<Person> src = w != null ? Traverse.Create(w).Field("mList").GetValue<List<Person>>() : null;
                if (src != null)
                {
                    List.AddRange(src);
                }
            }
            catch (Exception e)
            {
                Main.LogOnce("qol-arm", "Scouting browse: could not read the list. " + e.Message);
            }
            sPending = List.Count > 1;
        }

        /// <summary>Called as a detail screen enters: decide whether the arrows are ours this time.</summary>
        internal static void OnDetailScreenEnter()
        {
            Active = sPending;
            sPending = false;
            if (sPopBackStack)
            {
                sPopBackStack = false;
                try
                {
                    Stack back = Traverse.Create(UIManager.instance).Field("mBackStack").GetValue<Stack>();
                    if (back != null && back.Count > 0)
                    {
                        back.Pop(); // the person we just stepped away from; Back should go to Scouting
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        internal static void Step(Person current, int dir)
        {
            if (!Active || List.Count < 2)
            {
                return;
            }
            int i = List.IndexOf(current);
            if (i < 0)
            {
                return;
            }
            int n = (i + dir + List.Count) % List.Count;
            Person next = List[n];
            try
            {
                scSoundManager.Instance.PlaySound(SoundID.Button_Select);
            }
            catch (Exception)
            {
            }
            sPending = true;
            sPopBackStack = true;
            string screen = next is Driver ? "DriverScreen" : "StaffDetailsScreen";
            UIManager.instance.ChangeScreen(screen, next);
        }

        internal static void Wire(Button left, Button right, Person person)
        {
            if (!Main.Enabled || !Active || person == null || !List.Contains(person) || left == null || right == null)
            {
                return;
            }
            GameUtility.SetActive(left.gameObject, inIsActive: true);
            GameUtility.SetActive(right.gameObject, inIsActive: true);
            left.onClick.RemoveAllListeners();
            right.onClick.RemoveAllListeners();
            left.onClick.AddListener(() => Step(person, -1));
            right.onClick.AddListener(() => Step(person, 1));
            Main.LogOnce("qol-wired", "Scouting browse: arrows active on the detail screen (" + List.Count + " people in the list).");
        }

        internal static Person CurrentPerson()
        {
            UIScreen s = UIManager.instance != null ? UIManager.instance.currentScreen : null;
            if (s is DriverScreen ds)
            {
                return ds.driver;
            }
            if (s is StaffDetailsScreen ss)
            {
                return ss.person;
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(UIScoutingStaffEntry), "OnButton")]
    internal static class ScoutEntryClickPatch
    {
        private static void Prefix(UIScoutingStaffEntry __instance)
        {
            if (Main.Enabled)
            {
                ScoutBrowse.ArmFromScouting(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(DriverScreen), "OnEnter")]
    internal static class DriverScreenEnterPatch
    {
        private static void Prefix()
        {
            ScoutBrowse.OnDetailScreenEnter();
        }
    }

    [HarmonyPatch(typeof(StaffDetailsScreen), "OnEnter")]
    internal static class StaffScreenEnterPatch
    {
        private static void Prefix()
        {
            ScoutBrowse.OnDetailScreenEnter();
        }
    }

    [HarmonyPatch(typeof(UIDriverScreenInfoWidget), "Setup")]
    internal static class DriverInfoArrowsPatch
    {
        private static void Postfix(UIDriverScreenInfoWidget __instance, Driver inDriver)
        {
            try
            {
                ScoutBrowse.Wire(__instance.leftDriverButton, __instance.rightDriverButton, inDriver);
            }
            catch (Exception e)
            {
                Main.LogOnce("qol-d", "Scouting browse (driver) failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(UIStaffDetailsScreenPersonInfoWidget), "Setup")]
    internal static class StaffInfoArrowsPatch
    {
        private static void Postfix(UIStaffDetailsScreenPersonInfoWidget __instance, Person inPerson)
        {
            try
            {
                ScoutBrowse.Wire(__instance.leftButton, __instance.rightButton, inPerson);
            }
            catch (Exception e)
            {
                Main.LogOnce("qol-s", "Scouting browse (staff) failed: " + e.Message);
            }
        }
    }

    /// <summary>Left/right arrow keys while browsing from Scouting.</summary>
    internal class ScoutBrowseKeys : MonoBehaviour
    {
        private void Update()
        {
            if (!Main.Enabled || !ScoutBrowse.Active)
            {
                return;
            }
            bool left = Input.GetKeyDown(KeyCode.LeftArrow);
            bool right = Input.GetKeyDown(KeyCode.RightArrow);
            if (!left && !right)
            {
                return;
            }
            try
            {
                if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    GameObject sel = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                    if (sel != null && (sel.GetComponent<InputField>() != null || sel.GetComponent<TMP_InputField>() != null))
                    {
                        return; // typing in a text box
                    }
                }
                Person p = ScoutBrowse.CurrentPerson();
                if (p != null && ScoutBrowse.List.Contains(p))
                {
                    ScoutBrowse.Step(p, left ? -1 : 1);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    // ----------------------------------------------------------------------
    // Marketability column
    // ----------------------------------------------------------------------

    internal static class MarketCol
    {
        private const string LabelName = "MMFixesMarketability";
        private static readonly Dictionary<TextMeshProUGUI, string> sOriginalHeader = new Dictionary<TextMeshProUGUI, string>();

        internal static bool DriversShown(UIScoutingSearchResultsWidget w)
        {
            return w != null && w.filterJobRole == UIScoutingFilterJobRole.Filter.Drivers;
        }

        internal static float Value(Driver d)
        {
            return d.CanShowStats() ? d.GetDriverStats().marketability : -1f;
        }

        internal static void SetEntry(UIScoutingStaffEntry e)
        {
            if (e == null || e.flag == null || e.age == null)
            {
                return;
            }
            Transform cell = e.flag.transform;
            Transform existing = cell.parent != null ? cell.parent.Find(LabelName) : null;
            Driver d = e.person as Driver;
            UIScoutingSearchResultsWidget w = e.screen != null ? e.screen.searchWidget : null;
            bool show = Main.Enabled && d != null && DriversShown(w);
            if (!show)
            {
                if (existing != null)
                {
                    existing.gameObject.SetActive(false);
                }
                e.flag.gameObject.SetActive(true);
                return;
            }
            TextMeshProUGUI label;
            if (existing == null)
            {
                GameObject go = (GameObject)UnityEngine.Object.Instantiate(e.age.gameObject);
                go.transform.SetParent(cell.parent, false);
                go.name = LabelName;
                RectTransform src = (RectTransform)cell;
                RectTransform dst = (RectTransform)go.transform;
                dst.anchorMin = src.anchorMin;
                dst.anchorMax = src.anchorMax;
                dst.pivot = src.pivot;
                dst.anchoredPosition = src.anchoredPosition;
                dst.sizeDelta = new Vector2(Mathf.Max(src.sizeDelta.x, 90f), Mathf.Max(src.sizeDelta.y, 24f));
                dst.SetSiblingIndex(cell.GetSiblingIndex());
                label = go.GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.enableWordWrapping = false;
            }
            else
            {
                existing.gameObject.SetActive(true);
                label = existing.GetComponent<TextMeshProUGUI>();
            }
            float m = Value(d);
            label.text = m < 0f ? "?" : GameUtility.GetPercentageText(m, 1f);
            e.flag.gameObject.SetActive(false);
        }

        internal static void SetHeader(UIScoutingSearchResultsWidget w)
        {
            if (w == null || w.tableFilters == null)
            {
                return;
            }
            for (int i = 0; i < w.tableFilters.Length; i++)
            {
                UIScoutingFilter f = w.tableFilters[i];
                if (f == null || f.filterType != UIScoutingFilter.Type.Nationality)
                {
                    continue;
                }
                TextMeshProUGUI t = f.GetComponentInChildren<TextMeshProUGUI>(true);
                if (t == null)
                {
                    continue;
                }
                if (Main.Enabled && DriversShown(w))
                {
                    if (t.text != "Marketability")
                    {
                        sOriginalHeader[t] = t.text;
                    }
                    t.text = "Marketability";
                    t.enableAutoSizing = true;
                    t.fontSizeMin = 9f;
                    t.fontSizeMax = Mathf.Max(t.fontSize, 14f);
                }
                else if (t.text == "Marketability")
                {
                    string original;
                    t.enableAutoSizing = false;
                    t.text = sOriginalHeader.TryGetValue(t, out original) ? original : "Nationality";
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIScoutingStaffEntry), "SetupEntry")]
    internal static class MarketEntryPatch
    {
        private static void Postfix(UIScoutingStaffEntry __instance)
        {
            try
            {
                MarketCol.SetEntry(__instance);
            }
            catch (Exception e)
            {
                Main.LogOnce("mk-entry", "Marketability column (row) failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(UIScoutingStaffEntry), "UpdateEntry")]
    internal static class MarketEntryUpdatePatch
    {
        private static void Postfix(UIScoutingStaffEntry __instance)
        {
            try
            {
                MarketCol.SetEntry(__instance);
            }
            catch (Exception)
            {
            }
        }
    }

    [HarmonyPatch(typeof(UIScoutingSearchResultsWidget), "Refresh")]
    internal static class MarketHeaderPatch
    {
        private static void Postfix(UIScoutingSearchResultsWidget __instance)
        {
            try
            {
                MarketCol.SetHeader(__instance);
            }
            catch (Exception e)
            {
                Main.LogOnce("mk-head", "Marketability column (header) failed: " + e.Message);
            }
        }
    }

    /// <summary>The Nationality sort becomes a marketability sort while drivers are listed.
    /// First click: highest first. Unscouted drivers always at the bottom.</summary>
    [HarmonyPatch(typeof(UIScoutingSearchResultsWidget), "SortByFilter")]
    internal static class MarketSortPatch
    {
        private static bool Prefix(UIScoutingSearchResultsWidget __instance)
        {
            try
            {
                Traverse t = Traverse.Create(__instance);
                if (!Main.Enabled || !MarketCol.DriversShown(__instance)
                    || t.Field("mFilter").GetValue<UIScoutingFilter.Type>() != UIScoutingFilter.Type.Nationality)
                {
                    return true;
                }
                bool highFirst = t.Field("mFilterAsc").GetValue<bool>();
                List<Person> list = t.Field("mList").GetValue<List<Person>>();
                list.Sort((a, b) =>
                {
                    float ma = a is Driver da ? MarketCol.Value(da) : -1f;
                    float mb = b is Driver db ? MarketCol.Value(db) : -1f;
                    if (ma < 0f || mb < 0f)
                    {
                        int known = (mb < 0f ? 0 : 1) - (ma < 0f ? 0 : 1);
                        if (known != 0)
                        {
                            return known; // scouted first
                        }
                        return string.Compare(a.name, b.name, StringComparison.Ordinal);
                    }
                    int c = highFirst ? mb.CompareTo(ma) : ma.CompareTo(mb);
                    return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.Ordinal);
                });
                Main.LogOnce("mk-sort", "Marketability column: sorting drivers by marketability.");
                return false;
            }
            catch (Exception e)
            {
                Main.LogOnce("mk-sort-err", "Marketability sort failed, using the game's sort: " + e.Message);
                return true;
            }
        }
    }
}
