using System.Collections.Generic;
using FourPersonExpeditions;
using FourPersonExpeditions.UI.Pagination;
using ModAPI.Reflection;
using UnityEngine;

namespace FourPersonExpeditions.MapPatches
{
    internal static class PartyMapPanelPaginationController
    {
        private const int ItemsPerPage = 2;
        private const int PagingDepth = 5000;

        public static void OnShow(PartyMapPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            FourPersonPartyLogic logic = GetOrAddLogic(panel);
            if (logic == null)
            {
                return;
            }

            if (!logic.isMapUIInitialized)
            {
                logic.mapScreenPage = 0;
            }

            if (HasPagingControls(logic))
            {
                Safe.InvokeMethod(panel, "UpdateUI");
                return;
            }

            InitializePagingControls(panel, logic);
        }

        public static bool UpdateUi(PartyMapPanel panel)
        {
            FPELog.Debug("PartyMapPanel.UpdateUI prefix entered.");

            try
            {
                if (panel == null)
                {
                    return true;
                }

                FourPersonPartyLogic logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
                if (logic == null)
                {
                    return true;
                }

                PartyMapPanelState state;
                if (!TryReadPanelState(panel, out state))
                {
                    return true;
                }

                if (!state.HasCurrentParty)
                {
                    SetMemberSlotsActive(state, false);
                    UpdatePagingControls(logic, 0);
                }
                else
                {
                    RenderCurrentPage(panel, logic, state.CurrentParty);
                }

                UpdatePartyCountLabel(panel, state);
                UpdateRecallState(panel, state);
                return false;
            }
            catch (System.Exception ex)
            {
                FPELog.Error(string.Format("PartyMapPanel: Exception in UpdateUI Prefix: {0}", ex));
                return true;
            }
        }

        public static void ResetPage(PartyMapPanel panel)
        {
            if (panel == null)
            {
                return;
            }

            FourPersonPartyLogic logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic != null)
            {
                logic.mapScreenPage = 0;
            }
        }

        private static FourPersonPartyLogic GetOrAddLogic(PartyMapPanel panel)
        {
            FourPersonPartyLogic logic = panel.gameObject.GetComponent<FourPersonPartyLogic>();
            if (logic == null)
            {
                logic = panel.gameObject.AddComponent<FourPersonPartyLogic>();
            }

            return logic;
        }

        private static bool HasPagingControls(FourPersonPartyLogic logic)
        {
            return logic != null
                && logic.isMapUIInitialized
                && logic.pageIndicatorLabel != null
                && logic.pageRightArrow != null;
        }

        private static void InitializePagingControls(PartyMapPanel panel, FourPersonPartyLogic logic)
        {
            FPELog.Debug("PartyMapPanel: Initializing paging UI components.");

            PartyMapPanelTemplates templates;
            if (!TryReadTemplates(panel, out templates))
            {
                return;
            }

            Transform parent = panel.transform.Find("UIElements");
            if (parent == null)
            {
                FPELog.Error("PartyMapPanel: Could not find 'UIElements' transform for parent anchoring. [Step 4]");
                return;
            }

            try
            {
                PartyMapPagingLayout layout = CalculateLayout(templates);

                UILabel indicator = PaginationControlFactory.CreateClonedIndicator(
                    templates.PartyCountLabel.gameObject,
                    parent,
                    "FPE_PageIndicator",
                    layout.IndicatorPosition);

                if (indicator == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to clone partyCountLabel template.");
                    return;
                }

                UISprite nextSprite = PaginationSpriteLookup.FindSpriteAtPathOrFallback(
                    panel.transform,
                    "UIElements/Party/next",
                    templates.NextPartyButton);
                UISprite previousSprite = PaginationSpriteLookup.FindSpriteAtPathOrFallback(
                    panel.transform,
                    "UIElements/Party/prev",
                    templates.PreviousPartyButton);

                if (!ValidateArrowSprite("next", nextSprite) || !ValidateArrowSprite("prev", previousSprite))
                {
                    return;
                }

                GameObject leftButton = CreateMapArrow(
                    "FPE_PageLeftArrow",
                    parent,
                    layout.LeftPosition,
                    Quaternion.Euler(0, 0, 180),
                    previousSprite,
                    delegate { logic.PreviousMapPage(); });

                if (leftButton == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to create left paging button.");
                    return;
                }

                GameObject rightButton = CreateMapArrow(
                    "FPE_PageRightArrow",
                    parent,
                    layout.RightPosition,
                    Quaternion.identity,
                    nextSprite,
                    delegate { logic.NextMapPage(); });

                if (rightButton == null)
                {
                    FPELog.Error("PartyMapPanel: Failed to create right paging button.");
                    return;
                }

                logic.pageIndicatorLabel = indicator;
                logic.pageLeftArrow = leftButton;
                logic.pageRightArrow = rightButton;
                logic.isMapUIInitialized = true;
                FPELog.Debug("PartyMapPanel: Paging UI initialized.");
            }
            catch (System.Exception ex)
            {
                FPELog.Error(string.Format("PartyMapPanel: Exception during paging UI initialization: {0}", ex));
            }
        }

        private static bool TryReadTemplates(PartyMapPanel panel, out PartyMapPanelTemplates templates)
        {
            templates = new PartyMapPanelTemplates();

            templates.NextPartyButton = GetTemplateGameObject(panel, "m_partySelectNext");
            FPELog.Debug(string.Format("PartyMapPanel: Template m_partySelectNext found={0}", templates.NextPartyButton != null));

            templates.PreviousPartyButton = GetTemplateGameObject(panel, "m_partySelectPrev");
            FPELog.Debug(string.Format("PartyMapPanel: Template m_partySelectPrev found={0}", templates.PreviousPartyButton != null));

            templates.PartyCountLabel = GetPartyCountLabel(panel);
            FPELog.Debug(string.Format("PartyMapPanel: Template partyCountLabel found={0}", templates.PartyCountLabel != null));

            if (templates.NextPartyButton == null || templates.PreviousPartyButton == null || templates.PartyCountLabel == null)
            {
                FPELog.Error(string.Format(
                    "PartyMapPanel: Essential templates missing. Next: {0}, Prev: {1}, Label: {2}",
                    templates.NextPartyButton != null,
                    templates.PreviousPartyButton != null,
                    templates.PartyCountLabel != null));
                return false;
            }

            return true;
        }

        private static GameObject GetTemplateGameObject(PartyMapPanel panel, string fieldName)
        {
            object value;
            if (!Safe.TryGetField(panel, fieldName, out value))
            {
                return null;
            }

            GameObject gameObject = value as GameObject;
            if (gameObject != null)
            {
                return gameObject;
            }

            Component component = value as Component;
            return component != null ? component.gameObject : null;
        }

        private static UILabel GetPartyCountLabel(PartyMapPanel panel)
        {
            object value;
            if (!Safe.TryGetField(panel, "partyCountLabel", out value))
            {
                Safe.TryGetField(panel, "m_partyCountLabel", out value);
            }

            UILabel label = value as UILabel;
            if (label != null)
            {
                return label;
            }

            GameObject gameObject = value as GameObject;
            return gameObject != null ? gameObject.GetComponent<UILabel>() : null;
        }

        private static PartyMapPagingLayout CalculateLayout(PartyMapPanelTemplates templates)
        {
            float topLabelX = templates.PartyCountLabel.transform.localPosition.x;
            float topLeftX = templates.PreviousPartyButton.transform.localPosition.x;
            float topRightX = templates.NextPartyButton.transform.localPosition.x;
            float leftOffset = topLabelX - topLeftX;
            float rightOffset = topRightX - topLabelX;
            float anchorY = FourPersonUIPositions.MapPagingAnchorY;
            float yNudge = FourPersonUIPositions.MapPagingYNudge;
            float indicatorX = FourPersonUIPositions.MapPageIndicatorX;

            return new PartyMapPagingLayout
            {
                IndicatorPosition = new Vector3(indicatorX, anchorY + yNudge, 0f),
                LeftPosition = new Vector3(indicatorX - leftOffset, anchorY + yNudge, 0f),
                RightPosition = new Vector3(indicatorX + rightOffset, anchorY + yNudge, 0f)
            };
        }

        private static bool ValidateArrowSprite(string label, UISprite sprite)
        {
            if (sprite == null)
            {
                FPELog.Error(string.Format("PartyMapPanel: UISprite not found on {0} arrow template.", label));
                return false;
            }

            if (sprite.atlas == null || string.IsNullOrEmpty(sprite.spriteName))
            {
                FPELog.Error(string.Format(
                    "PartyMapPanel: Atlas or SpriteName missing on {0} arrow. Atlas: {1}, Sprite: {2}",
                    label,
                    sprite.atlas != null,
                    sprite.spriteName));
                return false;
            }

            return true;
        }

        private static GameObject CreateMapArrow(
            string name,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            UISprite templateSprite,
            System.Action onClick)
        {
            PaginationArrowButtonOptions options = new PaginationArrowButtonOptions
            {
                Name = name,
                Parent = parent,
                LocalPosition = position,
                LocalRotation = rotation,
                TemplateSprite = templateSprite,
                Depth = PagingDepth,
                Color = templateSprite != null ? templateSprite.color : Color.white,
                CopyTemplateCollider = true,
                OnClick = onClick,
                ClickBinding = PaginationClickBinding.UIEventListener
            };

            return PaginationControlFactory.CreateSpriteArrow(options);
        }

        private static bool TryReadPanelState(PartyMapPanel panel, out PartyMapPanelState state)
        {
            state = new PartyMapPanelState();

            object partiesObject;
            if (Safe.TryGetField(panel, "m_allParties", out partiesObject))
            {
                state.AllParties = partiesObject as List<ExplorationParty>;
            }

            Safe.TryGetField(panel, "m_currentPartyIndex", out state.CurrentPartyIndex);

            object member1Object;
            if (Safe.TryGetField(panel, "member1", out member1Object))
            {
                state.Member1 = member1Object as GameObject;
            }

            object member2Object;
            if (Safe.TryGetField(panel, "member2", out member2Object))
            {
                state.Member2 = member2Object as GameObject;
            }

            return state.AllParties != null && state.Member1 != null && state.Member2 != null;
        }

        private static void SetMemberSlotsActive(PartyMapPanelState state, bool isActive)
        {
            if (state.Member1 != null) state.Member1.SetActive(isActive);
            if (state.Member2 != null) state.Member2.SetActive(isActive);
        }

        private static void RenderCurrentPage(PartyMapPanel panel, FourPersonPartyLogic logic, ExplorationParty party)
        {
            int memberCount = party != null ? party.membersCount : 0;
            logic.mapScreenPage = PaginationMath.ClampPage(logic.mapScreenPage, memberCount, ItemsPerPage);
            int startIndex = logic.mapScreenPage * ItemsPerPage;

            PartyMapMemberSlotRenderer.Update(panel, 0, startIndex < memberCount ? party.GetMember(startIndex) : null);
            PartyMapMemberSlotRenderer.Update(panel, 1, startIndex + 1 < memberCount ? party.GetMember(startIndex + 1) : null);
            UpdatePagingControls(logic, memberCount);
        }

        private static void UpdatePagingControls(FourPersonPartyLogic logic, int memberCount)
        {
            if (logic == null || !logic.isMapUIInitialized)
            {
                return;
            }

            PaginationControlSet controls = new PaginationControlSet(
                logic.pageLeftArrow,
                logic.pageRightArrow,
                logic.pageIndicatorLabel);
            controls.Update(logic.mapScreenPage, memberCount, ItemsPerPage, true);
        }

        private static void UpdatePartyCountLabel(PartyMapPanel panel, PartyMapPanelState state)
        {
            UILabel countLabel = GetPartyCountLabel(panel);
            if (countLabel == null)
            {
                return;
            }

            int partyCount = state.AllParties != null ? state.AllParties.Count : 0;
            if (partyCount <= 0)
            {
                countLabel.text = "0/0";
                return;
            }

            int displayIndex = Mathf.Clamp(state.CurrentPartyIndex + 1, 1, partyCount);
            countLabel.text = string.Format("{0}/{1}", displayIndex, partyCount);
        }

        private static void UpdateRecallState(PartyMapPanel panel, PartyMapPanelState state)
        {
            bool isRecalled = state.HasCurrentParty && state.CurrentParty.isRecalled;

            object recallButtonObject;
            if (Safe.TryGetField(panel, "m_recallButton", out recallButtonObject))
            {
                GameObject recallButton = recallButtonObject as GameObject;
                if (recallButton != null)
                {
                    recallButton.SetActive(!isRecalled && state.HasAnyParties);
                }
            }

            object recalledLabelObject;
            if (Safe.TryGetField(panel, "m_recalledLabel", out recalledLabelObject))
            {
                GameObject recalledLabel = recalledLabelObject as GameObject;
                if (recalledLabel != null)
                {
                    recalledLabel.SetActive(isRecalled);
                }
            }

            object legendObject;
            if (Safe.TryGetField(panel, "m_legend", out legendObject))
            {
                LegendContainer legend = legendObject as LegendContainer;
                if (legend != null)
                {
                    legend.SetButtonEnabled(LegendContainer.ButtonEnum.YButton, !isRecalled && state.HasAnyParties);
                }
            }
        }

        private sealed class PartyMapPanelTemplates
        {
            public GameObject NextPartyButton;
            public GameObject PreviousPartyButton;
            public UILabel PartyCountLabel;
        }

        private sealed class PartyMapPagingLayout
        {
            public Vector3 IndicatorPosition;
            public Vector3 LeftPosition;
            public Vector3 RightPosition;
        }

        private sealed class PartyMapPanelState
        {
            public List<ExplorationParty> AllParties;
            public int CurrentPartyIndex;
            public GameObject Member1;
            public GameObject Member2;

            public bool HasAnyParties
            {
                get { return AllParties != null && AllParties.Count > 0; }
            }

            public bool HasCurrentParty
            {
                get
                {
                    return HasAnyParties
                        && CurrentPartyIndex >= 0
                        && CurrentPartyIndex < AllParties.Count
                        && AllParties[CurrentPartyIndex] != null;
                }
            }

            public ExplorationParty CurrentParty
            {
                get { return HasCurrentParty ? AllParties[CurrentPartyIndex] : null; }
            }
        }
    }
}
