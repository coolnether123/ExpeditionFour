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
            
            if (logic.isMapUIInitialized && logic.pageIndicatorLabel != null && logic.pageRightArrow != null)
            {
                // If already initialized and references are valid, just refresh the UI
                Safe.InvokeMethod(__instance, "UpdateUI");
                return;
            }

            // If we get here, either we haven't initialized, or references were lost (Missing)
            FPELog.Debug("PartyMapPanel: Initializing paging UI components.");

            // Use object retrieval to prevent cast exceptions inside TryGetField
            GameObject nextPartyButtonTemplate = null;
            GameObject prevPartyButtonTemplate = null;
            UILabel partyCountLabelTemplate = null;

            if (Safe.TryGetField(__instance, "m_partySelectNext", out object nextObj))
            {
                nextPartyButtonTemplate = nextObj as GameObject;
                if (nextPartyButtonTemplate == null && nextObj is Component c) nextPartyButtonTemplate = c.gameObject;
            }
            FPELog.Debug($"PartyMapPanel: Template m_partySelectNext found={nextPartyButtonTemplate != null}");

            if (Safe.TryGetField(__instance, "m_partySelectPrev", out object prevObj))
            {
                prevPartyButtonTemplate = prevObj as GameObject;
                if (prevPartyButtonTemplate == null && prevObj is Component c) prevPartyButtonTemplate = c.gameObject;
            }
            FPELog.Debug($"PartyMapPanel: Template m_partySelectPrev found={prevPartyButtonTemplate != null}");

            // Check both partyCountLabel and m_partyCountLabel
            if (!Safe.TryGetField(__instance, "partyCountLabel", out object countObj))
            {
                Safe.TryGetField(__instance, "m_partyCountLabel", out countObj);
            }
            
            if (countObj != null)
            {
                partyCountLabelTemplate = countObj as UILabel;
                if (partyCountLabelTemplate == null && countObj is GameObject go) partyCountLabelTemplate = go.GetComponent<UILabel>();
            }
            FPELog.Debug($"PartyMapPanel: Template partyCountLabel found={partyCountLabelTemplate != null}");

            if (nextPartyButtonTemplate == null || prevPartyButtonTemplate == null || partyCountLabelTemplate == null)
            {
                FPELog.Error($"PartyMapPanel: Essential templates missing. Next: {nextPartyButtonTemplate != null}, Prev: {prevPartyButtonTemplate != null}, Label: {partyCountLabelTemplate != null}");
                return;
            }

            Transform parentTransform = __instance.transform.Find("UIElements");
            if (parentTransform == null)
            {
                FPELog.Error("PartyMapPanel: Could not find 'UIElements' transform for parent anchoring. [Step 4]");
                return;
            }
            FPELog.Debug("PartyMapPanel: Found UIElements.");

            try
            {
                void SetLayerRecursively(GameObject root, int layer)
                {
                    if (root == null) return;
                    root.layer = layer;
                    foreach (Transform child in root.transform)
                    {
                        if (child != null) SetLayerRecursively(child.gameObject, layer);
                    }
                }

                // ---------- 1) Identify suitable chevron sprites for paging ----------
                UISprite PickChevron(GameObject go)
                {
                    if (go == null) return null;
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

                FPELog.Debug("PartyMapPanel: Picking chevrons.");
                var chevronRight = PickChevron(nextPartyButtonTemplate);
                var chevronLeft = PickChevron(prevPartyButtonTemplate) ?? chevronRight;
                if (chevronRight == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to find any suitable chevron sprite for paging buttons.");
                    return;
                }

                // ---------- 2) Calculate positioning based on vanilla layout ----------
                FPELog.Debug("PartyMapPanel: Calculating positions.");
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
                FPELog.Debug("PartyMapPanel: Cloning indicator.");
                var indicatorGO = UIUtil.CloneAndReposition(partyCountLabelTemplate.gameObject, Vector3.zero, parentTransform);
                if (indicatorGO == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to clone partyCountLabel template.");
                    return;
                }

                indicatorGO.transform.localPosition = indicatorPos;
                indicatorGO.name = "FPE_PageIndicator";
                SetLayerRecursively(indicatorGO, parentTransform.gameObject.layer);
                logic.pageIndicatorLabel = indicatorGO.GetComponent<UILabel>();
                if (logic.pageIndicatorLabel != null) 
                { 
                    logic.pageIndicatorLabel.text = "1/1"; 
                }

                FPELog.Debug("PartyMapPanel: Finding arrow sprites.");
                GameObject actualNextArrowGO = __instance.transform.Find("UIElements/Party/next")?.gameObject ?? nextPartyButtonTemplate;
                GameObject actualPrevArrowGO = __instance.transform.Find("UIElements/Party/prev")?.gameObject ?? prevPartyButtonTemplate;

                UISprite nextS = (actualNextArrowGO != null) ? actualNextArrowGO.GetComponentInChildren<UISprite>(true) : null;
                UISprite prevS = (actualPrevArrowGO != null) ? actualPrevArrowGO.GetComponentInChildren<UISprite>(true) : null;
                
                if (nextS == null || prevS == null)
                {
                    FPELog.Error($"PartyMapPanel: UISprite not found on arrows templates. nextS={(nextS != null ? "found" : "null")}, prevS={(prevS != null ? "found" : "null")}");
                    return;
                }

                if (prevS.atlas == null || string.IsNullOrEmpty(prevS.spriteName) || nextS.atlas == null || string.IsNullOrEmpty(nextS.spriteName))
                {
                    FPELog.Error($"PartyMapPanel: Atlas or SpriteName missing. PrevAtlas: {prevS.atlas != null}, PrevSprite: {prevS.spriteName}, NextAtlas: {nextS.atlas != null}, NextSprite: {nextS.spriteName}");
                    return;
                }

                FPELog.Debug("PartyMapPanel: Creating interactive elements.");

                // --- Helper to create a button manually to avoid UIFactory crashes ---
                GameObject CreatePageButton(string name, Vector3 pos, UISprite templateSprite, System.Action onClickAction, bool flip = false)
                {
                    GameObject go = new GameObject(name);
                    go.transform.parent = parentTransform;
                    go.transform.localPosition = pos;
                    go.transform.localScale = Vector3.one;
                    if (flip) go.transform.localRotation = Quaternion.Euler(0, 0, 180);
                    SetLayerRecursively(go, parentTransform.gameObject.layer);

                    // Setup Sprite
                    UISprite spr = go.AddComponent<UISprite>();
                    spr.atlas = templateSprite.atlas;
                    spr.spriteName = templateSprite.spriteName;
                    spr.depth = 5000;
                    spr.width = Mathf.Max(1, templateSprite.width);
                    spr.height = Mathf.Max(1, templateSprite.height);
                    spr.type = templateSprite.type; // Copy sprite type
                    spr.flip = templateSprite.flip;
                    spr.color = templateSprite.color;

                    // Setup Collider
                    BoxCollider box = go.AddComponent<BoxCollider>();
                    BoxCollider templateCollider = templateSprite.GetComponent<BoxCollider>();
                    if (templateCollider == null) templateCollider = templateSprite.GetComponentInParent<BoxCollider>();
                    if (templateCollider != null)
                    {
                        box.size = new Vector3(
                            Mathf.Max(1f, Mathf.Abs(templateCollider.size.x)),
                            Mathf.Max(1f, Mathf.Abs(templateCollider.size.y)),
                            Mathf.Max(1f, Mathf.Abs(templateCollider.size.z)));
                        box.center = templateCollider.center;
                    }
                    else
                    {
                        box.size = new Vector3(Mathf.Max(1f, spr.width), Mathf.Max(1f, spr.height), 1f);
                        box.center = Vector3.zero;
                    }

                    // Setup Interaction (UIEventListener is sufficient, no UIButton needed)
                    UIEventListener listener = UIEventListener.Get(go);
                    listener.onClick = (clickedGo) => {
                        if (onClickAction != null)
                        {
                            onClickAction.Invoke();
                        }
                    };

                    // Activate the GameObject
                    go.SetActive(true);

                    return go;
                }

                // Create Left Button
                GameObject leftBtn = CreatePageButton("FPE_PageLeftArrow", leftPos, prevS, () => logic.PreviousMapPage(), true);
                if (leftBtn == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to create left paging button.");
                    return;
                }
                logic.pageLeftArrow = leftBtn;

                // Create Right Button
                GameObject rightBtn = CreatePageButton("FPE_PageRightArrow", rightPos, nextS, () => logic.NextMapPage(), false);
                if (rightBtn == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to create right paging button.");
                    return;
                }
                logic.pageRightArrow = rightBtn;

                FPELog.Debug("PartyMapPanel: Setting depths.");
                // Removed separate SetChildDepths since we set depth during creation
                // UIHelper.SetChildDepths(indicatorGO.transform, 5000);
                // UIHelper.SetChildDepths(leftBtn.transform, 5000);
                // UIHelper.SetChildDepths(rightBtn.transform, 5000);
                // UIHelper.SetChildDepths(rightEl.GameObject.transform, 5000); // Cleared

                logic.isMapUIInitialized = true;
                FPELog.Debug("PartyMapPanel: Paging UI initialized.");
            }
            catch (System.Exception ex)
            {
                FPELog.Error($"PartyMapPanel: Exception during paging UI initialization: {ex}");
            }
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
            FPELog.Debug("PartyMapPanel.UpdateUI prefix entered.");
            try
            {
                var logic = __instance.gameObject.GetComponent<FourPersonPartyLogic>();
                if (logic == null) return true;

                // Safely retrieve internal lists and indices using object fetch
                List<ExplorationParty> allParties = null;
                if (Safe.TryGetField(__instance, "m_allParties", out object partiesObj)) allParties = partiesObj as List<ExplorationParty>;
                
                int currentPartyIndex = 0;
                Safe.TryGetField(__instance, "m_currentPartyIndex", out currentPartyIndex);

                GameObject member1_go = null;
                if (Safe.TryGetField(__instance, "member1", out object m1Obj)) member1_go = m1Obj as GameObject;
                
                GameObject member2_go = null;
                if (Safe.TryGetField(__instance, "member2", out object m2Obj)) member2_go = m2Obj as GameObject;

                if (allParties == null || member1_go == null || member2_go == null)
                {
                    return true;
                }

                if (allParties.Count == 0 || currentPartyIndex >= allParties.Count || allParties[currentPartyIndex] == null)
                {
                    member1_go.SetActive(false);
                    member2_go.SetActive(false);
                    if (logic.isMapUIInitialized)
                    {
                        if (logic.pageLeftArrow != null) logic.pageLeftArrow.SetActive(false);
                        if (logic.pageRightArrow != null) logic.pageRightArrow.SetActive(false);
                        if (logic.pageIndicatorLabel != null) logic.pageIndicatorLabel.gameObject.SetActive(false);
                    }
                }
                else
                {
                    var party = allParties[currentPartyIndex];
                    int startIndex = logic.mapScreenPage * 2;

                    // Update the two available slots on the panel with bounds checking
                    UpdateMemberSlot(__instance, 0, (party != null && startIndex < party.membersCount) ? party.GetMember(startIndex) : null);
                    UpdateMemberSlot(__instance, 1, (party != null && startIndex + 1 < party.membersCount) ? party.GetMember(startIndex + 1) : null);

                    if (logic.isMapUIInitialized)
                    {
                        int memberCount = party.membersCount;
                        int maxPages = Mathf.Max(1, Mathf.CeilToInt(memberCount / 2.0f));

                        if (logic.pageIndicatorLabel != null)
                            logic.pageIndicatorLabel.text = $"{logic.mapScreenPage + 1}/{maxPages}";

                        bool showPaging = maxPages > 1;
                        if (logic.pageLeftArrow != null) logic.pageLeftArrow.SetActive(showPaging && logic.mapScreenPage > 0);
                        if (logic.pageRightArrow != null) logic.pageRightArrow.SetActive(showPaging && logic.mapScreenPage < maxPages - 1);
                        if (logic.pageIndicatorLabel != null) logic.pageIndicatorLabel.gameObject.SetActive(showPaging);
                    }
                }

                // Update party count indicator
                if (Safe.TryGetField(__instance, "partyCountLabel", out object lblObj) || Safe.TryGetField(__instance, "m_partyCountLabel", out lblObj))
                {
                    if (lblObj is UILabel countLabel)
                    {
                        int partyCount = allParties != null ? allParties.Count : 0;
                        if (partyCount <= 0)
                        {
                            countLabel.text = "0/0";
                        }
                        else
                        {
                            int displayIndex = Mathf.Clamp(currentPartyIndex + 1, 1, partyCount);
                            countLabel.text = $"{displayIndex}/{partyCount}";
                        }
                    }
                }
                
                bool isRecalled = (allParties.Count > 0 && currentPartyIndex < allParties.Count) && allParties[currentPartyIndex].isRecalled;
                
                // Update recall and legend status
                if (Safe.TryGetField(__instance, "m_recallButton", out object rbObj) && rbObj is GameObject recallBtn)
                    recallBtn.SetActive(!isRecalled && allParties.Count > 0);
                
                if (Safe.TryGetField(__instance, "m_recalledLabel", out object rlObj) && rlObj is GameObject recalledLbl)
                    recalledLbl.SetActive(isRecalled);
                
                if (Safe.TryGetField(__instance, "m_legend", out object legObj) && legObj is LegendContainer legend)
                    legend.SetButtonEnabled(LegendContainer.ButtonEnum.YButton, !isRecalled && allParties.Count > 0);

                return false; // Suppress original UI logic
            }
            catch (System.Exception ex)
            {
                FPELog.Error($"PartyMapPanel: Exception in UpdateUI Prefix: {ex}");
                return true; // Fallback to vanilla on error
            }
        }

        /// <summary>
        /// Updates a single member slot in the PartyMapPanel UI.
        /// </summary>
        private static void UpdateMemberSlot(PartyMapPanel panel, int slotIndex, PartyMember member)
        {
            try
            {
                string suffix = (slotIndex == 0) ? "1" : "2";
                GameObject memberGO = null;
                if (Safe.TryGetField(panel, "member" + suffix, out object goObj)) memberGO = goObj as GameObject;

                if (member == null || member.person == null || memberGO == null)
                {
                    if (memberGO != null) memberGO.SetActive(false);
                    return;
                }

                memberGO.SetActive(true);
                var person = member.person;

                // Retrieve UI components for the specific slot using ultra-safe object fetching
                // We use object and manual cast to handle different NGUI versions (UI2DSprite vs UISprite)
                if (Safe.TryGetField(panel, "m_member" + suffix + "Image", out object imgObj))
                {
                    if (imgObj is UI2DSprite image) person.ColorizeAvatarSprite(image);
                }

                if (Safe.TryGetField(panel, "m_member" + suffix + "Name", out object nameObj))
                {
                    if (nameObj is UILabel nameLabel) nameLabel.text = person.firstName;
                }

                if (Safe.TryGetField(panel, "m_member" + suffix + "HealthBar", out object barObj))
                {
                    if (barObj is UIProgressBar healthBar && person.maxHealth > 0)
                        healthBar.value = (float)person.health / person.maxHealth;
                }

                if (Safe.TryGetField(panel, "m_member" + suffix + "BleedIcon", out object bleedObj))
                {
                    if (bleedObj is GameObject bleedIcon)
                        bleedIcon.SetActive(person.illness != null && person.illness.bleeding != null && person.illness.bleeding.isActive);
                }
            }
            catch (System.Exception ex)
            {
                 // Trace error but don't crash
                 FPELog.Debug($"UpdateMemberSlot Slot {slotIndex} error: {ex.Message}");
            }
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
