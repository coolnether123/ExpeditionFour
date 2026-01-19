using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using ModAPI.UI;
using ModAPI.Reflection;
using FourPersonExpeditions;

namespace FourPersonExpeditions.MapPatches
{
    /// <summary>
    /// Patches the PartyMapPanel to support paging for parties with more than two members.
    /// This allows the user to cycle through all party members on the map screen.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnShow))]
    public static class PartyMapPanel_OnShow_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) logic = __instance.gameObject.AddComponent<FourPersonPartyLogic>();

            // On first initialization, reset to page 0
            // When returning from sub-panels (like inventory), preserve the current page
            if (!logic.isMapUIInitialized)
            {
                logic.mapScreenPage = 0;
            }
            
            if (logic.isMapUIInitialized)
            {
                // If already initialized, just refresh the UI
                Safe.InvokeMethod(__instance, "UpdateUI");
                return;
            }

            FPELog.Info("PartyMapPanel: Initializing paging UI components.");

            // Use Safe API to retrieve template objects for cloning
            if (!Safe.TryGetField(__instance, "m_partySelectNext", out GameObject nextPartyButtonTemplate)) return;
            if (!Safe.TryGetField(__instance, "m_partySelectPrev", out GameObject prevPartyButtonTemplate)) return;
            if (!Safe.TryGetField(__instance, "partyCountLabel", out UILabel partyCountLabelTemplate)) return;

            Transform parentTransform = __instance.transform.Find("UIElements");
            if (parentTransform == null)
            {
                FPELog.Warn("PartyMapPanel: Could not find UIElements transform for parent anchoring.");
                return;
            }

            // ---------- 1) Identify suitable chevron sprites for paging ----------
            UISprite PickChevron(GameObject go)
            {
                var sprites = go.GetComponentsInChildren<UISprite>(true);
                foreach (var s in sprites)
                {
                    var n = (s.spriteName ?? s.name ?? "").ToLowerInvariant();
                    // Exclude platform-specific bumper/dpad icons
                    if (n.Contains("rb") || n.Contains("lb") || n.Contains("rt") || n.Contains("lt") ||
                        n.Contains("dpad") || n.Contains("button_a") || n.Contains("button_b") ||
                        n.Contains("button_x") || n.Contains("button_y"))
                        continue;
                    
                    if (n.Contains("arrow") || n.Contains("chevron"))
                        return s;
                }
                
                // Fallback: widest non-square sprite
                UISprite best = null;
                foreach (var s in sprites)
                    if (best == null || (s.width > best.width && s.width != s.height)) best = s;
                return best ?? (sprites.Length > 0 ? sprites[0] : null);
            }

            var chevronRight = PickChevron(nextPartyButtonTemplate);
            var chevronLeft = PickChevron(prevPartyButtonTemplate) ?? chevronRight;
            if (chevronRight == null)
            {
                FPELog.Warn("PartyMapPanel: Failed to find chevron sprite for paging buttons.");
                return;
            }

            // ---------- 2) Calculate positioning based on vanilla layout ----------
            float topLabelX = partyCountLabelTemplate.transform.localPosition.x;
            float topLeftX = prevPartyButtonTemplate.transform.localPosition.x;
            float topRightX = nextPartyButtonTemplate.transform.localPosition.x;
            float leftOffset = topLabelX - topLeftX;
            float rightOffset = topRightX - topLabelX;

            float anchorY = FourPersonUIPositions.MapPagingAnchorY;
            float yNudge = FourPersonUIPositions.MapPagingYNudge;

            float indicatorX = FourPersonUIPositions.MapPageIndicatorX;
            Vector3 indicatorPos = new Vector3(indicatorX, anchorY + yNudge, 0f);
            Vector3 leftPos = new Vector3(indicatorX - leftOffset, anchorY + yNudge, 0f);
            Vector3 rightPos = new Vector3(indicatorX + rightOffset, anchorY + yNudge, 0f);

            // ---------- 3) Instantiate and configure paging UI elements ----------
            // Create the page indicator text using UIUtil
            var indicatorGO = UIUtil.CloneAndReposition(partyCountLabelTemplate.gameObject, Vector3.zero, parentTransform);
            indicatorGO.transform.localPosition = indicatorPos;
            indicatorGO.name = "FPE_PageIndicator";
            logic.pageIndicatorLabel = indicatorGO.GetComponent<UILabel>();
            if (logic.pageIndicatorLabel != null) 
            { 
                logic.pageIndicatorLabel.text = "1/1"; 
            }

            // Retrieve the shared atlas/sprite info
            GameObject actualNextArrowGO = __instance.transform.Find("UIElements/Party/next")?.gameObject;
            GameObject actualPrevArrowGO = __instance.transform.Find("UIElements/Party/prev")?.gameObject;
            if (actualNextArrowGO == null || actualPrevArrowGO == null) return;

            UISprite nextS = actualNextArrowGO.GetComponentInChildren<UISprite>(true);
            UISprite prevS = actualPrevArrowGO.GetComponentInChildren<UISprite>(true);
            if (nextS == null || prevS == null) return;

            // Create clean arrow buttons using UIFactory
            // Pass Atlas:Sprite to ensure UIFactory finds the correct sprite and sizes it correctly
            var leftOpts = new UIElementOptions { Name = "FPE_PageLeftArrow", Scale = new Vector3(3f, 3f, 1f), Rotation = Quaternion.Euler(0, 0, 180) };
            var rightOpts = new UIElementOptions { Name = "FPE_PageRightArrow", Scale = new Vector3(3f, 3f, 1f) };

            string leftPath = (prevS.atlas != null ? prevS.atlas.name + ":" : "") + prevS.spriteName;
            string rightPath = (nextS.atlas != null ? nextS.atlas.name + ":" : "") + nextS.spriteName;

            var leftEl = UIFactory.CreateInteractiveElement(parentTransform, leftPath, leftPos, () => logic.PreviousMapPage(), leftOpts);
            leftEl.Sprite.atlas = prevS.atlas; 
            // Force dimensions to match request (52x52)
            leftEl.Sprite.width = 52;
            leftEl.Sprite.height = 52;
            leftEl.Collider.size = new Vector3(52, 52, 0);
            // Ensure scale is normalized if we are sizing by dimensions
            leftEl.GameObject.transform.localScale = Vector3.one; 
            logic.pageLeftArrow = leftEl.GameObject;

            var rightEl = UIFactory.CreateInteractiveElement(parentTransform, rightPath, rightPos, () => logic.NextMapPage(), rightOpts);
            rightEl.Sprite.atlas = nextS.atlas;
            rightEl.Sprite.width = 52;
            rightEl.Sprite.height = 52;
            rightEl.Collider.size = new Vector3(52, 52, 0);
            rightEl.GameObject.transform.localScale = Vector3.one;
            logic.pageRightArrow = rightEl.GameObject;

            // Debug Sizing
            FPELog.Info($"[MapPaging] Left Arrow: {leftEl.Sprite.width}x{leftEl.Sprite.height} Scale={leftEl.GameObject.transform.localScale}");
            FPELog.Info($"[MapPaging] Right Arrow: {rightEl.Sprite.width}x{rightEl.Sprite.height} Scale={rightEl.GameObject.transform.localScale}");
            FPELog.Info($"[MapPaging] Template Arrow: {prevS.width}x{prevS.height} Scale={prevS.transform.localScale}");

            // Ensure all paging elements are at a high depth
            UIHelper.SetChildDepths(indicatorGO.transform, 5000);
            UIHelper.SetChildDepths(leftEl.GameObject.transform, 5000);
            UIHelper.SetChildDepths(rightEl.GameObject.transform, 5000);

            logic.isMapUIInitialized = true;
        }
    }

    /// <summary>
    /// Overrides the UpdateUI logic to handle paging for the member list.
    /// Only displays members belonging to the current page.
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), "UpdateUI")]
    public static class PartyMapPanel_UpdateUI_Patch
    {
        public static bool Prefix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null) return true;

            // Safely retrieve internal lists and indices
            if (!Safe.TryGetField(__instance, "m_allParties", out List<ExplorationParty> allParties)) return true;
            if (!Safe.TryGetField(__instance, "m_currentPartyIndex", out int currentPartyIndex)) return true;

            if (!Safe.TryGetField(__instance, "member1", out GameObject member1_go)) return true;
            if (!Safe.TryGetField(__instance, "member2", out GameObject member2_go)) return true;

            if (allParties.Count == 0 || currentPartyIndex >= allParties.Count)
            {
                member1_go.SetActive(false);
                member2_go.SetActive(false);
                if (logic.isMapUIInitialized)
                {
                    logic.pageLeftArrow?.SetActive(false);
                    logic.pageRightArrow?.SetActive(false);
                    logic.pageIndicatorLabel?.gameObject.SetActive(false);
                }
            }
            else
            {
                var party = allParties[currentPartyIndex];
                int startIndex = logic.mapScreenPage * 2;

                // Update the two available slots on the panel
                UpdateMemberSlot(__instance, 0, party.GetMember(startIndex));
                UpdateMemberSlot(__instance, 1, party.GetMember(startIndex + 1));

                if (logic.isMapUIInitialized)
                {
                    int memberCount = party.membersCount;
                    int maxPages = Mathf.Max(1, Mathf.CeilToInt(memberCount / 2.0f));

                    if (logic.pageIndicatorLabel != null)
                        logic.pageIndicatorLabel.text = $"{logic.mapScreenPage + 1}/{maxPages}";

                    bool showPaging = maxPages > 1;
                    logic.pageLeftArrow?.SetActive(showPaging && logic.mapScreenPage > 0);
                    logic.pageRightArrow?.SetActive(showPaging && logic.mapScreenPage < maxPages - 1);
                    logic.pageIndicatorLabel?.gameObject.SetActive(showPaging);
                }
            }

            // Update party count indicator
            if (Safe.TryGetField(__instance, "partyCountLabel", out UILabel countLabel)) 
                countLabel.text = $"{currentPartyIndex + 1}/{allParties.Count}";
            
            bool isRecalled = (allParties.Count > 0) && allParties[currentPartyIndex].isRecalled;
            
            // Update recall and legend status
            if (Safe.TryGetField(__instance, "m_recallButton", out GameObject recallBtn))
                recallBtn.SetActive(!isRecalled && allParties.Count > 0);
            
            if (Safe.TryGetField(__instance, "m_recalledLabel", out GameObject recalledLbl))
                recalledLbl.SetActive(isRecalled);
            
            if (Safe.TryGetField(__instance, "m_legend", out LegendContainer legend))
                legend.SetButtonEnabled(LegendContainer.ButtonEnum.YButton, !isRecalled && allParties.Count > 0);

            return false; // Suppress original UI logic
        }

        /// <summary>
        /// Updates a single member slot in the PartyMapPanel UI.
        /// </summary>
        private static void UpdateMemberSlot(PartyMapPanel panel, int slotIndex, PartyMember member)
        {
            GameObject memberGO = (slotIndex == 0) 
                ? Safe.GetFieldOrDefault<GameObject>(panel, "member1", null) 
                : Safe.GetFieldOrDefault<GameObject>(panel, "member2", null);

            if (member == null || member.person == null || memberGO == null)
            {
                if (memberGO != null) memberGO.SetActive(false);
                return;
            }

            memberGO.SetActive(true);
            var person = member.person;

            // Retrieve UI components for the specific slot
            UI2DSprite image = (slotIndex == 0) ? Safe.GetFieldOrDefault<UI2DSprite>(panel, "m_member1Image", null) : Safe.GetFieldOrDefault<UI2DSprite>(panel, "m_member2Image", null);
            UILabel name = (slotIndex == 0) ? Safe.GetFieldOrDefault<UILabel>(panel, "m_member1Name", null) : Safe.GetFieldOrDefault<UILabel>(panel, "m_member2Name", null);
            UIProgressBar health = (slotIndex == 0) ? Safe.GetFieldOrDefault<UIProgressBar>(panel, "m_member1HealthBar", null) : Safe.GetFieldOrDefault<UIProgressBar>(panel, "m_member2HealthBar", null);
            GameObject bleedIcon = (slotIndex == 0) ? Safe.GetFieldOrDefault<GameObject>(panel, "m_member1BleedIcon", null) : Safe.GetFieldOrDefault<GameObject>(panel, "m_member2BleedIcon", null);

            person.ColorizeAvatarSprite(image);
            name.text = person.firstName;
            if (person.maxHealth > 0)
                health.value = (float)person.health / person.maxHealth;
            if (bleedIcon != null)
                bleedIcon.SetActive(person.illness.bleeding.isActive);
        }
    }

    /// <summary>
    /// Patch for party switching - resets page when switching to a different party
    /// </summary>
    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnTabRight))]
    public static class PartyMapPanel_OnTabRight_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic != null)
            {
                // Reset to first page when switching parties
                logic.mapScreenPage = 0;
            }
        }
    }

    [HarmonyPatch(typeof(PartyMapPanel), nameof(PartyMapPanel.OnTabLeft))]
    public static class PartyMapPanel_OnTabLeft_Patch
    {
        public static void Postfix(PartyMapPanel __instance)
        {
            var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic != null)
            {
                // Reset to first page when switching parties
                logic.mapScreenPage = 0;
            }
        }
    }
}
