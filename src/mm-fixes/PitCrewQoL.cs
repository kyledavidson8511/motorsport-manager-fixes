using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MMFixes
{
    /// <summary>
    /// QoL (1.3.0): a "Renew All" button on the Pit Crew screen, beside Auto Manage Option.
    /// It renews every crew member the game's own Renew button would allow (not retiring, not past
    /// peak age, contract not already full), using the same call (ContractPitCrew.RenewContract).
    /// </summary>
    internal static class RenewAll
    {
        private const string ButtonName = "MMFixesRenewAll";

        private static bool Eligible(PitCrewMember m)
        {
            return m != null && m.pitCrewContract != null && !m.WantsToRetire() && !m.HasPassedPeakAge() && m.pitCrewContract.canRenewContract;
        }

        private static List<PitCrewMember> Crew()
        {
            try
            {
                return Game.instance.player.team.pitCrewController.pitCrewTeamMembers;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static void Ensure(PitCrewScreen screen)
        {
            if (screen == null || screen.automanageButton == null)
            {
                return;
            }
            Transform anchor = screen.automanageButton.transform;
            Transform parent = anchor.parent;
            Transform existing = parent.Find(ButtonName);
            GameObject go;
            if (existing == null)
            {
                go = (GameObject)UnityEngine.Object.Instantiate(screen.automanageButton.gameObject);
                go.transform.SetParent(parent, false);
                go.name = ButtonName;
                // Labels that re-localise themselves on enable would overwrite our text.
                foreach (UILocaliseLabel l in go.GetComponentsInChildren<UILocaliseLabel>(true))
                {
                    UnityEngine.Object.Destroy(l);
                }
                LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
                le.ignoreLayout = true;

                RectTransform src = (RectTransform)anchor;
                RectTransform dst = (RectTransform)go.transform;
                dst.anchorMin = src.anchorMin;
                dst.anchorMax = src.anchorMax;
                dst.pivot = src.pivot;
                float w = src.rect.width;
                float newW = Mathf.Max(120f, w * 0.62f);
                float gap = Mathf.Max(12f, w * 0.15f);
                dst.sizeDelta = new Vector2(newW, src.rect.height);
                float x = src.anchoredPosition.x - src.pivot.x * w - gap - (1f - src.pivot.x) * newW;
                dst.anchoredPosition = new Vector2(x, src.anchoredPosition.y);

                Button b = go.GetComponent<Button>();
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(OnClick);
                Main.LogOnce("renew-all-ui", string.Format("QoL (renew all): button added beside '{0}' under '{1}'.", anchor.name, parent.name));
            }
            else
            {
                go = existing.gameObject;
            }
            go.SetActive(Main.Enabled);
            Refresh(go);
        }

        private static void Refresh(GameObject go)
        {
            int n = 0;
            List<PitCrewMember> crew = Crew();
            for (int i = 0; crew != null && i < crew.Count; i++)
            {
                if (Eligible(crew[i])) n++;
            }
            Button b = go.GetComponent<Button>();
            if (b != null) b.interactable = n > 0;
            TextMeshProUGUI t = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null)
            {
                t.enableWordWrapping = false;
                t.text = n > 0 ? "Renew All (" + n + ")" : "Renew All";
            }
        }

        private static void OnClick()
        {
            try
            {
                List<PitCrewMember> crew = Crew();
                PitCrewScreen screen = UIManager.instance.GetScreen<PitCrewScreen>();
                List<PitCrewMember> done = new List<PitCrewMember>();
                int skipped = 0;
                for (int i = 0; crew != null && i < crew.Count; i++)
                {
                    PitCrewMember m = crew[i];
                    if (Eligible(m))
                    {
                        m.pitCrewContract.RenewContract();
                        done.Add(m);
                    }
                    else if (m != null && m.pitCrewContract != null && m.pitCrewContract.canRenewContract)
                    {
                        skipped++;
                    }
                }
                for (int i = 0; screen != null && i < done.Count; i++)
                {
                    screen.OnContractRenewed(done[i]);
                }
                Main.Logger.Log(string.Format("QoL (renew all): renewed {0} pit crew contract(s){1}.", done.Count,
                    skipped > 0 ? "; " + skipped + " retiring or past peak age left alone" : ""));
                FeedbackPopup.Open("Contracts renewed", done.Count + " pit crew contract(s) renewed for 12 races." + (skipped > 0 ? " " + skipped + " retiring or past peak age were left alone." : ""));
                if (screen != null)
                {
                    Ensure(screen);
                }
            }
            catch (Exception e)
            {
                Main.Logger.Error("Renew all failed: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(PitCrewScreen), "OnEnter")]
    internal static class PitCrewScreenRenewAllPatch
    {
        private static void Postfix(PitCrewScreen __instance)
        {
            try
            {
                RenewAll.Ensure(__instance);
            }
            catch (Exception e)
            {
                Main.LogOnce("renew-all-err", "QoL (renew all) failed to add the button: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(PitCrewScreen), "OnContractRenewed")]
    internal static class PitCrewRenewedPatch
    {
        private static void Postfix(PitCrewScreen __instance)
        {
            try
            {
                RenewAll.Ensure(__instance);
            }
            catch (Exception)
            {
            }
        }
    }
}
