using System.Collections.Generic;
using FourPersonExpeditions;
using FourPersonExpeditions.UI.Pagination;
using ModAPI.Reflection;
using UnityEngine;

namespace FourPersonExpeditions.UI
{
    public class CombatSummaryLogic : MonoBehaviour
    {
        private const int ItemsPerPage = 2;
        private const float AutoAdvanceDelay = 1.5f;
        private const float ArrowY = -180f;
        private const float ArrowXOffset = 180f;
        private const int ArrowSize = 68;
        private const int ArrowClickPadding = 20;
        private const int UiDepth = 200;

        private static readonly string[] PreferredArrowSprites =
        {
            "arrow_left",
            "arrowleft",
            "chevron_left",
            "arrow_back",
            "ArrowTab"
        };

        private readonly PaginationState _pagination = new PaginationState(ItemsPerPage);
        private MonoBehaviour _panel;
        private List<EncounterSummaryCharacter> _allSummaries;
        private int _validSummaryCount;
        private float _delayTimer;
        private bool _isAutoAdvancing = true;

        private GameObject _leftArrow;
        private GameObject _rightArrow;
        private UILabel _pageIndicator;
        private PaginationControlSet _paginationControls;

        public void Initialize(MonoBehaviour panel, List<EncounterSummaryCharacter> summaries)
        {
            _panel = panel;
            _allSummaries = summaries;
            _delayTimer = 0f;
            _isAutoAdvancing = true;
            _validSummaryCount = CountVisibleSummaries(_allSummaries);
            _pagination.Reset(_validSummaryCount);

            SetupUI();
            UpdatePageVisibility();
        }

        private static int CountVisibleSummaries(List<EncounterSummaryCharacter> summaries)
        {
            if (summaries == null)
            {
                return 0;
            }

            int visibleCount = 0;
            for (int i = 0; i < summaries.Count; i++)
            {
                if (summaries[i] != null && summaries[i].gameObject.activeSelf)
                {
                    visibleCount++;
                }
            }

            return visibleCount > 0 ? visibleCount : summaries.Count;
        }

        private void SetupUI()
        {
            FPELog.Debug("CombatSummaryLogic: SetupUI called");
            if (_leftArrow != null)
            {
                EnsureControlSet();
                return;
            }

            Transform parent = _panel != null ? _panel.transform : null;
            if (parent == null)
            {
                FPELog.Warn("CombatSummaryLogic: Cannot create pagination UI without a panel parent.");
                return;
            }

            PaginationSpriteChoice arrowSprite = PaginationSpriteLookup.FindInChildrenAtlas(parent, PreferredArrowSprites);
            if (arrowSprite == null)
            {
                FPELog.Warn("CombatSummaryLogic: No arrow sprite found in atlas, using text fallback");
            }
            else
            {
                FPELog.Debug(string.Format("CombatSummaryLogic: Using arrow sprite '{0}'", arrowSprite.SpriteName));
            }

            _leftArrow = CreateArrow(
                parent,
                "FPE_PageLeft",
                "<",
                new Vector3(-ArrowXOffset, ArrowY, 0f),
                Quaternion.Euler(0, 0, -90),
                arrowSprite,
                delegate { ChangePage(-1); });

            _rightArrow = CreateArrow(
                parent,
                "FPE_PageRight",
                ">",
                new Vector3(ArrowXOffset, ArrowY, 0f),
                Quaternion.Euler(0, 0, 90),
                arrowSprite,
                delegate { ChangePage(1); });

            _pageIndicator = PaginationControlFactory.CreateLabelIndicator(
                parent,
                "FPE_PageIndicator",
                new Vector3(0f, ArrowY, 0f),
                28,
                UiDepth,
                Color.white);

            EnsureControlSet();
            FPELog.Debug("CombatSummaryLogic: SetupUI complete");
        }

        private static GameObject CreateArrow(
            Transform parent,
            string name,
            string fallbackText,
            Vector3 position,
            Quaternion rotation,
            PaginationSpriteChoice arrowSprite,
            System.Action onClick)
        {
            if (arrowSprite != null)
            {
                PaginationArrowButtonOptions options = new PaginationArrowButtonOptions
                {
                    Name = name,
                    Parent = parent,
                    LocalPosition = position,
                    LocalRotation = rotation,
                    Atlas = arrowSprite.Atlas,
                    SpriteName = arrowSprite.SpriteName,
                    Width = ArrowSize,
                    Height = ArrowSize,
                    Depth = UiDepth,
                    Color = Color.white,
                    ColliderSize = new Vector3(ArrowSize + ArrowClickPadding, ArrowSize + ArrowClickPadding, 1f),
                    OnClick = onClick,
                    ClickBinding = PaginationClickBinding.UIButton
                };

                GameObject arrow = PaginationControlFactory.CreateSpriteArrow(options);
                if (arrow != null && FpeDebug.Enabled)
                {
                    UIDebug.TakeSnapshot(arrow, name);
                }

                return arrow;
            }

            return PaginationControlFactory.CreateTextArrow(
                parent,
                name,
                fallbackText,
                position,
                48,
                UiDepth,
                Color.white,
                new Vector3(60f, 60f, 1f),
                onClick,
                PaginationClickBinding.UIButton);
        }

        private void EnsureControlSet()
        {
            _paginationControls = new PaginationControlSet(_leftArrow, _rightArrow, _pageIndicator);
        }

        private void Update()
        {
            if (!_isAutoAdvancing) return;
            if (_allSummaries == null || _allSummaries.Count == 0) return;

            if (AreCurrentPageAnimationsComplete())
            {
                _delayTimer += Time.deltaTime;
                if (_delayTimer >= AutoAdvanceDelay)
                {
                    if (_pagination.HasNextPage)
                    {
                        ChangePage(1);
                        _delayTimer = 0f;
                    }
                    else
                    {
                        _isAutoAdvancing = false;
                    }
                }
            }
        }

        private bool AreCurrentPageAnimationsComplete()
        {
            for (int i = _pagination.StartIndex; i < _pagination.EndIndex; i++)
            {
                EncounterSummaryCharacter summary = _allSummaries[i];
                if (summary == null || !summary.gameObject.activeInHierarchy)
                {
                    continue;
                }

                System.Action updateAction = Safe.GetFieldOrDefault<System.Action>(summary, "m_update", null);
                if (updateAction != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void ChangePage(int delta)
        {
            if (!_pagination.Move(delta))
            {
                return;
            }

            _delayTimer = 0f;
            if (delta < 0)
            {
                _isAutoAdvancing = false;
            }

            UpdatePageVisibility();
        }

        private void UpdatePageVisibility()
        {
            if (_allSummaries == null)
            {
                return;
            }

            _pagination.SetItemCount(_validSummaryCount);
            FPELog.Debug(string.Format(
                "CombatSummaryLogic: UpdatePageVisibility - page {0}/{1}, showing {2} items",
                _pagination.CurrentPage + 1,
                _pagination.MaxPages,
                _pagination.ItemsPerPage));

            for (int i = 0; i < _allSummaries.Count; i++)
            {
                EncounterSummaryCharacter summary = _allSummaries[i];
                if (summary == null)
                {
                    continue;
                }

                bool shouldShow = i < _validSummaryCount
                    && i >= _pagination.StartIndex
                    && i < _pagination.StartIndex + _pagination.ItemsPerPage;
                summary.gameObject.SetActive(shouldShow);
            }

            if (_paginationControls != null)
            {
                _paginationControls.Update(
                    _pagination.CurrentPage,
                    _validSummaryCount,
                    _pagination.ItemsPerPage,
                    false);
            }

            UIGrid grid = Safe.GetFieldOrDefault<UIGrid>(_panel, "member_grid", null);
            if (grid != null)
            {
                grid.Reposition();
                FPELog.Debug("CombatSummaryLogic: Grid repositioned");
            }
            else
            {
                FPELog.Warn("CombatSummaryLogic: member_grid not found during UpdatePageVisibility");
            }
        }
    }
}
