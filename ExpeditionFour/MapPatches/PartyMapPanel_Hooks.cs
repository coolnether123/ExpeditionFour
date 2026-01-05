using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.UI;

namespace FourPersonExpeditions.MapPatches
{
    // Patches the PartyMapPanel to support paging for more than two members.
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnShow))]
    public static class PartyMapPanel_OnShow_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) logic = __instance.gameObject.AddComponent<FourPersonPartyLogic>();

            logic.mapScreenPage = 0;

            if (logic.isMapUIInitialized)
            {
                Traverse.Create(__instance).Method("UpdateUI").GetValue();
                return;
            }

            FPELog.Info("PartyMapPanel Patch: Initializing paging UI for the first time.");

            var tr = Traverse.Create(__instance);
            GameObject nextPartyButtonTemplate = tr.Field("m_partySelectNext").GetValue<GameObject>();
            GameObject prevPartyButtonTemplate = tr.Field("m_partySelectPrev").GetValue<GameObject>();
            UILabel partyCountLabelTemplate = tr.Field("partyCountLabel").GetValue<UILabel>();

            Transform parentTransform = __instance.transform.Find("UIElements");
            if (parentTransform == null || nextPartyButtonTemplate == null || prevPartyButtonTemplate == null || partyCountLabelTemplate == null)
            {
                FPELog.Warn("Could not find template UI objects. Aborting.");
                return;
            }

            // ---------- 1) Find the chevron sprites (TRYING NOT RB/LB) ----------
            UISprite PickChevron(GameObject go)
            {
                var sprites = go.GetComponentsInChildren<UISprite>(true);
                foreach (var s in sprites)
                {
                    var n = (s.spriteName ?? s.name ?? "").ToLowerInvariant();
                    // skip platform bumper / dpad icons
                    if (n.Contains("rb") || n.Contains("lb") || n.Contains("rt") || n.Contains("lt") ||
                        n.Contains("dpad") || n.Contains("button_a") || n.Contains("button_b") ||
                        n.Contains("button_x") || n.Contains("button_y"))
                        continue;
                    // prefer names that look like arrows/chevrons
                    if (n.Contains("arrow") || n.Contains("chevron"))
                        return s;
                }
                // fallback: widest non-square sprite (usually the chevron background glyph)
                UISprite best = null;
                foreach (var s in sprites)
                    if (best == null || (s.width > best.width && s.width != s.height)) best = s;
                return best ?? (sprites.Length > 0 ? sprites[0] : null);
            }

            var chevronRight = PickChevron(nextPartyButtonTemplate);
            var chevronLeft = PickChevron(prevPartyButtonTemplate) ?? chevronRight;
            if (chevronRight == null)
            {
                FPELog.Warn("CRITICAL: Could not find a chevron sprite on template buttons.");
                return;
            }

            // ---------- 2) Measure vanilla top-row spacing so we match it ----------
            float topLabelX = partyCountLabelTemplate.transform.localPosition.x;
            float topLeftX = prevPartyButtonTemplate.transform.localPosition.x;
            float topRightX = nextPartyButtonTemplate.transform.localPosition.x;
            float leftOffset = topLabelX - topLeftX;     // px between label and left chevron
            float rightOffset = topRightX - topLabelX;    // px between label and right chevron

            // ---------- 3) Set a hardcoded anchor Y for the new paging arrows ----------
            float anchorY = -330f;
            float yNudge = -10f;   

            // Anchor the X position to the Party count label:
            float indicatorX = -464.0f;                // X position of PartyMember1Portrait
            Vector3 indicatorPos = new Vector3(indicatorX, anchorY + yNudge, 0f);
            Vector3 leftPos = new Vector3(indicatorX - leftOffset, anchorY + yNudge, 0f);
            Vector3 rightPos = new Vector3(indicatorX + rightOffset, anchorY + yNudge, 0f);

            // ---------- 4) Create label and clean arrows ----------
            var indicatorGO = UIUtil.CloneAndReposition(partyCountLabelTemplate.gameObject, Vector3.zero, parentTransform);
            indicatorGO.transform.localPosition = indicatorPos;
            indicatorGO.name = "FPE_PageIndicator";
            logic.pageIndicatorLabel = indicatorGO.GetComponent<UILabel>();
            if (logic.pageIndicatorLabel != null) { logic.pageIndicatorLabel.text = "1/1"; logic.pageIndicatorLabel.depth = 30; }

            // Get the atlas from one of the existing UI elements
            UIAtlas sharedAtlas = nextPartyButtonTemplate.GetComponentInChildren<UISprite>(true)?.atlas;
            if (sharedAtlas == null)
            {
                FPELog.Warn("CRITICAL: Could not find a shared UIAtlas from template buttons. Aborting.");
                return;
            }

            // Get the actual next/prev arrow GameObjects
            GameObject actualNextArrowGO = __instance.transform.Find("UIElements/Party/next")?.gameObject;
            GameObject actualPrevArrowGO = __instance.transform.Find("UIElements/Party/prev")?.gameObject;

            if (actualNextArrowGO == null || actualPrevArrowGO == null)
            {
                FPELog.Warn("Could not find actual next/prev arrow GameObjects. Aborting.");
                return;
            }

            UISprite actualNextArrowSprite = actualNextArrowGO.GetComponentInChildren<UISprite>(true);
            UISprite actualPrevArrowSprite = actualPrevArrowGO.GetComponentInChildren<UISprite>(true);

            if (actualNextArrowSprite == null || actualPrevArrowSprite == null)
            {
                FPELog.Warn("Could not find UISprite on actual next/prev arrow GameObjects. Aborting.");
                return;
            }

            // Use the actual arrow sprites as templates
            logic.pageLeftArrow = CreateCleanArrow("FPE_PageLeftArrow", actualPrevArrowSprite, parentTransform, leftPos, Quaternion.Euler(0, 0, 180));
            logic.pageRightArrow = CreateCleanArrow("FPE_PageRightArrow", actualNextArrowSprite, parentTransform, rightPos, Quaternion.identity);

            // Clicks
            UIEventListener.Get(logic.pageLeftArrow).onClick += (go) => logic.PreviousMapPage();
            UIEventListener.Get(logic.pageRightArrow).onClick += (go) => logic.NextMapPage();

            logic.isMapUIInitialized = true;
        }

        /// <summary>
        /// Creates a new, clean GameObject for an arrow button.
        /// It adds only the necessary components and copies visual data from the template sprite.
        /// This avoids cloning any unwanted scripts like UI_PlatformButton.
        /// </summary>
        private static GameObject CreateCleanArrow(string name, UISprite templateSprite, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            // 1. Create an empty GameObject
            GameObject arrowGO = new GameObject(name);
            arrowGO.transform.SetParent(parent, false);
            arrowGO.layer = parent.gameObject.layer;

            // 2. Add the UISprite component for visuals
            UISprite arrowSprite = arrowGO.AddComponent<UISprite>();
            arrowSprite.atlas = templateSprite.atlas; // Use templateSprite's atlas
            arrowSprite.spriteName = templateSprite.spriteName; // Use templateSprite's spriteName
            arrowSprite.depth = 9999; // Increased depth to ensure visibility TODO find a proper value
            arrowSprite.MakePixelPerfect(); // Set size based on sprite data in atlas

            // 3. Add a BoxCollider for clicking
            var col = arrowGO.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(arrowSprite.width, arrowSprite.height, 0); // Match collider to sprite size

            // 4. Add the UIButton component for interaction effects (hover, press color changes)
            arrowGO.AddComponent<UIButton>();

            // 5. Position and rotate the final object
            arrowGO.transform.localPosition = localPosition;
            arrowGO.transform.localRotation = localRotation;
            arrowGO.transform.localScale = new Vector3(3.0f, 3.0f, 1.0f); // Scale up by 3

            return arrowGO;
        }
    }

    // The UpdateUI_Patch 
    [HarmonyPatch(typeof(PartyMapPanel), "UpdateUI")]
    public static class PartyMapPanel_UpdateUI_Patch
    {
        public static bool Prefix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return true;

            var tr = Traverse.Create(__instance);
            var allParties = tr.Field("m_allParties").GetValue<List<ExplorationParty>>();
            int currentPartyIndex = tr.Field("m_currentPartyIndex").GetValue<int>();

            var member1_go = tr.Field("member1").GetValue<GameObject>();
            var member2_go = tr.Field("member2").GetValue<GameObject>();

            if (allParties.Count == 0 || currentPartyIndex >= allParties.Count)
            {
                member1_go.SetActive(false);
                member2_go.SetActive(false);
                if (logic.isMapUIInitialized)
                {
                    logic.pageLeftArrow.SetActive(false);
                    logic.pageRightArrow.SetActive(false);
                    logic.pageIndicatorLabel.gameObject.SetActive(false);
                }
            }
            else
            {
                var party = allParties[currentPartyIndex];
                int startIndex = logic.mapScreenPage * 2;

                UpdateMemberSlot(__instance, 0, party.GetMember(startIndex));
                UpdateMemberSlot(__instance, 1, party.GetMember(startIndex + 1));

                if (logic.isMapUIInitialized)
                {
                    int memberCount = party.membersCount;
                    int maxPages = Mathf.Max(1, Mathf.CeilToInt(memberCount / 2.0f));

                    logic.pageIndicatorLabel.text = $"{logic.mapScreenPage + 1}/{maxPages}";

                    bool showPaging = maxPages > 1;
                    logic.pageLeftArrow.SetActive(showPaging && logic.mapScreenPage > 0);
                    logic.pageRightArrow.SetActive(showPaging && logic.mapScreenPage < maxPages - 1);
                    logic.pageIndicatorLabel.gameObject.SetActive(showPaging);
                }
            }

            tr.Field("partyCountLabel").GetValue<UILabel>().text = $"{currentPartyIndex + 1}/{allParties.Count}";
            bool isRecalled = (allParties.Count > 0) && allParties[currentPartyIndex].isRecalled;
            tr.Field("m_recallButton").GetValue<GameObject>().SetActive(!isRecalled && allParties.Count > 0);
            tr.Field("m_recalledLabel").GetValue<GameObject>().SetActive(isRecalled);
            tr.Field("m_legend").GetValue<LegendContainer>().SetButtonEnabled(LegendContainer.ButtonEnum.YButton, !isRecalled && allParties.Count > 0);

            return false;
        }

        private static void UpdateMemberSlot(PartyMapPanel panel, int slotIndex, PartyMember member)
        {
            var tr = Traverse.Create(panel);
            GameObject memberGO = (slotIndex == 0) ? tr.Field("member1").GetValue<GameObject>() : tr.Field("member2").GetValue<GameObject>();

            if (member == null || member.person == null)
            {
                memberGO.SetActive(false);
                return;
            }

            memberGO.SetActive(true);
            var person = member.person;

            UI2DSprite image = (slotIndex == 0) ? tr.Field("m_member1Image").GetValue<UI2DSprite>() : tr.Field("m_member2Image").GetValue<UI2DSprite>();
            UILabel name = (slotIndex == 0) ? tr.Field("m_member1Name").GetValue<UILabel>() : tr.Field("m_member2Name").GetValue<UILabel>();
            UIProgressBar health = (slotIndex == 0) ? tr.Field("m_member1HealthBar").GetValue<UIProgressBar>() : tr.Field("m_member2HealthBar").GetValue<UIProgressBar>();
            GameObject bleedIcon = (slotIndex == 0) ? tr.Field("m_member1BleedIcon").GetValue<GameObject>() : tr.Field("m_member2BleedIcon").GetValue<GameObject>();

            person.ColorizeAvatarSprite(image);
            name.text = person.firstName;
            if (person.maxHealth > 0)
                health.value = (float)person.health / person.maxHealth;
            if (bleedIcon != null)
                bleedIcon.SetActive(person.illness.bleeding.isActive);
        }
    }
}