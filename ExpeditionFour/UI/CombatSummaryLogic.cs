using UnityEngine;
using System.Collections.Generic;
using ModAPI.Reflection;
using System.Collections;
using System.Linq;
using ModAPI.UI;
using FourPersonExpeditions;

namespace FourPersonExpeditions.UI
{
    public class CombatSummaryLogic : MonoBehaviour
    {
        private MonoBehaviour _panel;
        private List<EncounterSummaryCharacter> _allSummaries;
        private int _validSummaryCount;
        private int _currentPage = 0;
        private int _itemsPerPage = 2;
        private float _autoAdvanceDelay = 1.5f;
        private float _delayTimer = 0f;
        private bool _isAutoAdvancing = true;

        // UI Elements
        private GameObject _leftArrow;
        private GameObject _rightArrow;
        private UILabel _pageIndicator;

        public void Initialize(MonoBehaviour panel, List<EncounterSummaryCharacter> summaries)
        {
            _panel = panel;
            _allSummaries = summaries;
            _currentPage = 0;
            _isAutoAdvancing = true;
            _delayTimer = 0f;
            _validSummaryCount = 0;

            if (_allSummaries != null)
            {
                for (int i = 0; i < _allSummaries.Count; i++)
                {
                    if (_allSummaries[i] != null && _allSummaries[i].gameObject.activeSelf)
                        _validSummaryCount++;
                }
            }

            if (_validSummaryCount <= 0)
                _validSummaryCount = _allSummaries != null ? _allSummaries.Count : 0;

            SetupUI();
            UpdatePageVisibility();
        }

        /// <summary>
        /// Creates the pagination UI elements (left/right arrows and page indicator).
        /// 
        /// Implementation notes:
        /// - Arrows are parented to the PANEL (not the grid) to avoid disrupting UIGrid's layout.
        ///   UIGrid positions its direct children automatically, so adding our arrows there would break it.
        /// - We use the panel's transform for positioning, which places elements relative to the panel's center.
        /// - Arrow sprites use width/height sizing (not transform.scale) because NGUI widgets use those properties.
        /// - ArrowTab sprite in MenuAtlas points DOWN by default, so we rotate:
        ///   - Left arrow: 90 degrees (clockwise from down -> pointing left)
        ///   - Right arrow: -90 degrees (counter-clockwise from down -> pointing right)
        /// - The page indicator is centered between the arrows.
        /// </summary>
        private void SetupUI()
        {
            FPELog.Debug("CombatSummaryLogic: SetupUI called");
            if (_leftArrow != null) return;
            
            // ============================================================
            // STEP 1: Determine the correct parent transform
            // ============================================================
            // IMPORTANT: We parent to the PANEL, not the grid.
            // The grid (member_grid) auto-arranges its children, so adding arrows there breaks layout.
            // The panel provides a stable coordinate system for our UI additions.
            Transform parent = _panel.transform;
            FPELog.Debug($"CombatSummaryLogic: UI Parent is {parent.name}");

            // ============================================================
            // STEP 2: Find a reference sprite to get the atlas
            // ============================================================
            // We need an atlas reference to create our own sprites.
            // We search in the panel's children for any existing sprite.
            var existingSprite = parent.GetComponentInChildren<UISprite>(true);
            UIAtlas atlas = existingSprite != null ? existingSprite.atlas : null;
            string arrowSpriteName = null;

            if (atlas != null)
            {
                FPELog.Debug($"CombatSummaryLogic: Found atlas '{atlas.name}' with {atlas.spriteList.Count} sprites");
                
                // Search for arrow sprites in priority order
                var sprites = atlas.spriteList;
                
                // First pass: look for standard directional arrow names
                string[] preferredNames = { "arrow_left", "arrowleft", "chevron_left", "arrow_back", "ArrowTab" };
                foreach (var preferred in preferredNames)
                {
                    foreach (var s in sprites)
                    {
                        if (s.name.Equals(preferred, System.StringComparison.OrdinalIgnoreCase))
                        {
                            arrowSpriteName = s.name;
                            break;
                        }
                    }
                    if (arrowSpriteName != null) break;
                }
                
                // Fallback: any sprite containing "arrow" or "chevron"
                if (arrowSpriteName == null)
                {
                    foreach (var s in sprites)
                    {
                        string n = s.name.ToLowerInvariant();
                        if (n.Contains("arrow") || n.Contains("chevron"))
                        {
                            arrowSpriteName = s.name;
                            FPELog.Debug($"CombatSummaryLogic: Found fallback arrow sprite '{arrowSpriteName}'");
                            break;
                        }
                    }
                }
            }

            if (arrowSpriteName == null) 
            {
                FPELog.Warn("CombatSummaryLogic: No arrow sprite found in atlas, using text fallback");
            }
            else 
            {
                FPELog.Debug($"CombatSummaryLogic: Using arrow sprite '{arrowSpriteName}'");
            }

            // ============================================================
            // STEP 3: Define positioning constants
            // ============================================================
            // These positions are relative to the panel's center (0,0).
            // The combat summary panel is ~480 pixels tall with characters in the middle.
            // We place navigation at Y=-180 to be below the character bars but above the panel edge.
            float arrowY = -180f;           // Vertical position (negative = below center)
            float arrowXOffset = 180f;      // Horizontal offset from center
            int arrowSize = 68;             // Arrow sprite size in pixels (increased 30%)
            int uiDepth = 200;              // Depth within the panel (above most elements, below popups)
            
            FPELog.Debug($"CombatSummaryLogic: UI Positions - Y={arrowY}, X offset={arrowXOffset}, Size={arrowSize}, Depth={uiDepth}");

            // ============================================================
            // STEP 4: Create LEFT ARROW
            // ============================================================
            System.Action onLeft = () => ChangePage(-1);
            
            if (arrowSpriteName != null && atlas != null)
            {
                // Create sprite-based arrow
                var leftGO = new GameObject("FPE_PageLeft");
                leftGO.layer = parent.gameObject.layer;
                leftGO.transform.parent = parent;
                leftGO.transform.localPosition = new Vector3(-arrowXOffset, arrowY, 0);
                leftGO.transform.localScale = Vector3.one;
                
                // ArrowTab points DOWN by default.
                // In Unity 2D, positive Z is counter-clockwise.
                // Rotate -90 degrees (clockwise) to point LEFT.
                leftGO.transform.localRotation = Quaternion.Euler(0, 0, -90);
                
                var leftSprite = leftGO.AddComponent<UISprite>();
                leftSprite.atlas = atlas;
                leftSprite.spriteName = arrowSpriteName;
                leftSprite.width = arrowSize;
                leftSprite.height = arrowSize;
                leftSprite.depth = uiDepth;
                leftSprite.color = Color.white;
                
                var leftCollider = leftGO.AddComponent<BoxCollider>();
                leftCollider.size = new Vector3(arrowSize + 20, arrowSize + 20, 1); // Slightly larger for easier clicking
                
                var leftButton = leftGO.AddComponent<UIButton>();
                leftButton.tweenTarget = leftGO;
                EventDelegate.Add(leftButton.onClick, new EventDelegate(() => onLeft()));
                
                _leftArrow = leftGO;
                FPELog.Debug($"CombatSummaryLogic: Left arrow created (sprite-based, size={arrowSize})");
                if (FpeDebug.Enabled) UIDebug.TakeSnapshot(_leftArrow, "Left Arrow");
            }
            else
            {
                // Fallback: text-based arrow "<"
                var btn = UIHelper.CreateLabel(parent, "<", 48);
                btn.depth = uiDepth;
                btn.transform.localPosition = new Vector3(-arrowXOffset, arrowY, 0);
                btn.color = Color.white;
                var box = btn.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(60, 60, 1);
                var interact = btn.gameObject.AddComponent<UIButton>();
                EventDelegate.Add(interact.onClick, new EventDelegate(() => onLeft()));
                _leftArrow = btn.gameObject;
                FPELog.Info("CombatSummaryLogic: Left arrow created (text fallback)");
            }

            // ============================================================
            // STEP 5: Create RIGHT ARROW
            // ============================================================
            System.Action onRight = () => ChangePage(1);
            
            if (arrowSpriteName != null && atlas != null)
            {
                // Create sprite-based arrow
                var rightGO = new GameObject("FPE_PageRight");
                rightGO.layer = parent.gameObject.layer;
                rightGO.transform.parent = parent;
                rightGO.transform.localPosition = new Vector3(arrowXOffset, arrowY, 0);
                rightGO.transform.localScale = Vector3.one;
                
                // ArrowTab points DOWN by default.
                // In Unity 2D, positive Z is counter-clockwise.
                // Rotate +90 degrees (counter-clockwise) to point RIGHT.
                rightGO.transform.localRotation = Quaternion.Euler(0, 0, 90);
                
                var rightSprite = rightGO.AddComponent<UISprite>();
                rightSprite.atlas = atlas;
                rightSprite.spriteName = arrowSpriteName;
                rightSprite.width = arrowSize;
                rightSprite.height = arrowSize;
                rightSprite.depth = uiDepth;
                rightSprite.color = Color.white;
                
                var rightCollider = rightGO.AddComponent<BoxCollider>();
                rightCollider.size = new Vector3(arrowSize + 20, arrowSize + 20, 1);
                
                var rightButton = rightGO.AddComponent<UIButton>();
                rightButton.tweenTarget = rightGO;
                EventDelegate.Add(rightButton.onClick, new EventDelegate(() => onRight()));
                
                _rightArrow = rightGO;
                FPELog.Debug($"CombatSummaryLogic: Right arrow created (sprite-based, size={arrowSize})");
                if (FpeDebug.Enabled) UIDebug.TakeSnapshot(_rightArrow, "Right Arrow");
            }
            else
            {
                // Fallback: text-based arrow ">"
                var btn = UIHelper.CreateLabel(parent, ">", 48);
                btn.depth = uiDepth;
                btn.transform.localPosition = new Vector3(arrowXOffset, arrowY, 0);
                btn.color = Color.white;
                var box = btn.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(60, 60, 1);
                var interact = btn.gameObject.AddComponent<UIButton>();
                EventDelegate.Add(interact.onClick, new EventDelegate(() => onRight()));
                _rightArrow = btn.gameObject;
                FPELog.Info("CombatSummaryLogic: Right arrow created (text fallback)");
            }

            // ============================================================
            // STEP 6: Create PAGE INDICATOR (e.g., "1/2")
            // ============================================================
            // Centered between the arrows at the same Y position.
            _pageIndicator = UIHelper.CreateLabel(parent, "1/1", 28, TextAnchor.MiddleCenter);
            if (_pageIndicator != null)
            {
                _pageIndicator.gameObject.name = "FPE_PageIndicator";
                _pageIndicator.transform.localPosition = new Vector3(0, arrowY, 0);
                _pageIndicator.depth = uiDepth;
                _pageIndicator.color = Color.white;
                FPELog.Debug($"CombatSummaryLogic: Page indicator created at {_pageIndicator.transform.localPosition}");
            }
            
            FPELog.Debug("CombatSummaryLogic: SetupUI complete");
        }

        private void Update()
        {
            if (!_isAutoAdvancing) return;
            if (_allSummaries == null || _allSummaries.Count == 0) return;

            // Check if animations on current page are complete
            if (AreCurrentPageAnimationsComplete())
            {
                _delayTimer += Time.deltaTime;
                if (_delayTimer >= _autoAdvanceDelay)
                {
                    if (HasNextPage())
                    {
                        ChangePage(1);
                        _delayTimer = 0f;
                    }
                    else
                    {
                        _isAutoAdvancing = false; // Stop when we reach the end
                    }
                }
            }
        }

        private bool AreCurrentPageAnimationsComplete()
        {
            int start = _currentPage * _itemsPerPage;
            int end = Mathf.Min(start + _itemsPerPage, _validSummaryCount);

            for (int i = start; i < end; i++)
            {
                var summary = _allSummaries[i];
                if (summary == null || !summary.gameObject.activeInHierarchy) continue;

                // Check internal state using Reflection
                // field: private System.Action m_update;
                var updateAction = Safe.GetFieldOrDefault<System.Action>(summary, "m_update", null);
                if (updateAction != null)
                {
                    return false; // Still animating
                }
            }
            return true;
        }

        private void ChangePage(int delta)
        {
            int maxPages = Mathf.Max(1, Mathf.CeilToInt((float)_validSummaryCount / _itemsPerPage));
            int newPage = Mathf.Clamp(_currentPage + delta, 0, maxPages - 1);

            if (newPage != _currentPage)
            {
                _currentPage = newPage;
                _delayTimer = 0f;
                // If user manually changes page, we can choose to stop auto-advancing or keep it.
                // Usually manual interaction overrides auto.
                if (delta < 0) _isAutoAdvancing = false; // Stop auto if going back
                
                UpdatePageVisibility();
            }
        }

        private bool HasNextPage()
        {
            int maxPages = Mathf.Max(1, Mathf.CeilToInt((float)_validSummaryCount / _itemsPerPage));
            return _currentPage < maxPages - 1;
        }

        private void UpdatePageVisibility()
        {
            if (_allSummaries == null) return;

            int maxPages = Mathf.Max(1, Mathf.CeilToInt((float)_validSummaryCount / _itemsPerPage));
            
            FPELog.Debug($"CombatSummaryLogic: UpdatePageVisibility - page {_currentPage + 1}/{maxPages}, showing {_itemsPerPage} items");
            
            int start = _currentPage * _itemsPerPage;
            int end = start + _itemsPerPage;

            for (int i = 0; i < _allSummaries.Count; i++)
            {
                bool shouldShow = (i < _validSummaryCount) && (i >= start && i < end);
                _allSummaries[i].gameObject.SetActive(shouldShow);
            }

            // Update Arrows
            bool leftActive = _currentPage > 0;
            bool rightActive = _currentPage < maxPages - 1;
            
            if (_leftArrow != null)
            {
                _leftArrow.SetActive(leftActive);
                FPELog.Debug($"CombatSummaryLogic: Left arrow set to {leftActive}");
            }
            else
            {
                FPELog.Warn("CombatSummaryLogic: Left arrow is null during UpdatePageVisibility");
            }
            
            if (_rightArrow != null)
            {
                _rightArrow.SetActive(rightActive);
                FPELog.Debug($"CombatSummaryLogic: Right arrow set to {rightActive}");
            }
            else
            {
                FPELog.Warn("CombatSummaryLogic: Right arrow is null during UpdatePageVisibility");
            }
            
            // Update Indicator
            if (_pageIndicator != null)
            {
                _pageIndicator.text = $"{_currentPage + 1}/{maxPages}";
                FPELog.Debug($"CombatSummaryLogic: Page indicator updated to '{_pageIndicator.text}'");
            }
            
            // Ensure Grid Repositions
             // We need to access the grid via reflection or known field
            var grid = Safe.GetFieldOrDefault<UIGrid>(_panel, "member_grid", null);
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
