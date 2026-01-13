namespace FourPersonExpeditions
{
    /// <summary>
    /// Centralized repository for UI positioning and depth constants used throughout the four-person expedition mod.
    /// This ensures consistent layouts across different screens and paging states.
    /// </summary>
    public static class FourPersonUIPositions
    {
        // --- Map Paging UI Layout ---
        public const float MapPagingAnchorY = -330f;
        public const float MapPagingYNudge = -10f;
        public const float MapPageIndicatorX = -464f;

        // --- Party Setup Character Avatar Depth ---
        // Used to bring the currently selected slot to the visual foreground
        public const int CombatUIDepthHigh = 20;
        public const int CombatUIDepthLow = 10;

        // --- Expedition Return Summary Grid Layout ---
        public const float EncounterSummaryLeftX = -210f;
        public const float EncounterSummaryRightX = 110f;
        public const float EncounterSummaryTopY = 65f;
        public const float EncounterSummaryVerticalSpacing = 160f;
        public const float EncounterSummaryXOffset = 0f;
    }
}
