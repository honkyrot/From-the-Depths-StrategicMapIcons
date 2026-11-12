using UnityEngine;

namespace StrategicMapIcons
{
    /// <summary>
    /// Central configuration and threshold values for role classification, combat domains, and map display.
    /// Backed at runtime by <see cref="SMI_ModSettingsConfiguration"/>.
    /// </summary>
    public static class SMI_ModSettings
    {
        // --- Master Controls ---

        /// <summary>
        /// Master toggle to enable or disable custom Strategic Map vertex icons.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        /// <summary>
        /// Controls whether non-critical resource/supply text overlays (creator, cargo, -filling up...-, etc.) are drawn on the map.
        /// Critical "low mats" / "no mats" warnings are always kept visible.
        /// </summary>
        public static bool ShowResourceLabels { get; set; } = true;
        /// <summary>
        /// Enables drawing when zoomed in past the strategic map view.
        /// </summary>
        public static bool EnableOnZoom { get; set; } = false;

        /// <summary>
        /// Minimum world-space horizontal span (meters) used when drawing custom craft icons in Tactical Map view (EnableOnZoom),
        /// ensuring small drones and probes remain clearly recognizable.
        /// </summary>
        public static float MinTacticalIconWorldSpanMeters { get; set; } = 24f;

        // visuals
        public static bool SeparateOverlappingIcons { get; set; } = true;
        public static bool ShowFleetCostTiers { get; set; } = true;
        public static bool ScaleByMaterials { get; set; } = true;
        public static float ScalingExponent { get; set; } = 2f;
        public static float IconScaleMultiplier { get; set; } = 1.0f;
        public static bool OutlineIcons { get; set; } = true;
        public static float OutlineWidth { get; set; } = 2.0f;
        
        // UI layout adjustments (default off)
        /// <summary>
        /// Dynamically resizes the top-left fleet and force list panels on the Strategic &amp; Tactical Maps to fit long craft names.
        /// </summary>
        public static bool ExpandUiPanels { get; set; } = false;

        /// <summary>
        /// Dynamically widens the top-right Build / Favourites bar when open.
        /// </summary>
        public static bool ExpandFavouritesBar { get; set; } = false;

        // other

        public static bool EasterEggsEnabled { get; set; } = true;


        // BELOW ARE NOT EXPOSED TO THE USER
        // --- Combat Domain Thresholds (VertexIconShapes.GetCombatDomain) ---

        /// <summary>
        /// Maximum top speed (m/s) for a non-airborne, non-land craft to be classified as a stationary Fortress.
        /// </summary>
        public static float FortressMaxTopSpeed { get; set; } = 4f; // If a craft is out of play, ANY craft has a minimum speed of 3.

        /// <summary>
        /// Minimum recorded max altitude (m) required to classify a craft as Orbital rather than Air.
        /// High-altitude jets often operate around 500-800m, while spacecraft operate above 900m.
        /// </summary>
        public static float OrbitalMinAltitude { get; set; } = 900f;

        /// <summary>
        /// Minimum recorded max altitude (m) to classify a craft as Air if not already flagged by travel restrictions.
        /// </summary>
        public static float AirMinAltitude { get; set; } = 50f;


        // --- Role Classification Thresholds (IconRoleManager) ---

        /// <summary>
        /// Maximum firepower for a craft with repair tentacles or blueprint spawners to be auto-classified as Support.
        /// Above this threshold, it is treated as a Combat craft.
        /// </summary>
        public static float SupportMaxFirepower { get; set; } = 8f;

        /// <summary>
        /// Firepower threshold at or below which a craft is considered effectively unarmed.
        /// </summary>
        public static float UnarmedMaxFirepower { get; set; } = 1f;

        /// <summary>
        /// Maximum firepower for a fast, high-flying, or radar-equipped craft to be auto-classified as a Scout.
        /// </summary>
        public static float ScoutMaxFirepower { get; set; } = 8f;

        /// <summary>
        /// Minimum antenna height (m) to qualify a lightly armed craft as a Scout.
        /// </summary>
        public static float ScoutMinAntennaHeight { get; set; } = 10f;

        /// <summary>
        /// Minimum max altitude (m) to qualify a lightly armed craft as a Scout.
        /// </summary>
        public static float ScoutMinAltitude { get; set; } = 300f;

        /// <summary>
        /// Minimum top speed (m/s) to qualify a lightly armed craft as a Scout.
        /// </summary>
        public static float ScoutMinTopSpeed { get; set; } = 45f;

        /// <summary>
        /// Minimum combined fleet firepower for an entire fleet to be classified as a Combat fleet
        /// rather than taking on a utility role (Harvester/Cargo/Support/Scout).
        /// </summary>
        public static float CombatFleetMinFirepower { get; set; } = 10f;


        // --- Fleet Cost Tier Thresholds (StrategicMapFleetsPatch) ---

        public static float Tier1MinMaterialCost { get; set; } = 25_000f;
        public static float Tier2MinMaterialCost { get; set; } = 100_000f;
        public static float Tier3MinMaterialCost { get; set; } = 400_000f;
        public static float Tier4MinMaterialCost { get; set; } = 1_000_000f;
        public static float Tier5MinMaterialCost { get; set; } = 1_500_000f;


        // --- Strategic Map & Fleet UI Panel Layout (StrategicMapUiPatch) ---

        /// <summary>
        /// Maximum number of row-heights the top-left Fleet/Force panel can expand to before scrolling (vanilla Strategic Map was 8f).
        /// </summary>
        public static float MaxForcePanelRowMultiplier { get; set; } = 26f;

        /// <summary>
        /// Maximum fraction of screen height the top-left Fleet/Force panel can occupy (vanilla was 0.5f).
        /// </summary>
        public static float MaxForcePanelScreenHeightFraction { get; set; } = 0.9f;

        /// <summary>
        /// Absolute maximum width (px) for the dynamically sized top-left Fleet/Force panel.
        /// </summary>
        public static float MaxForcePanelWidth { get; set; } = 560f;

        /// <summary>
        /// Maximum fraction of screen width the top-left Fleet/Force panel can occupy.
        /// </summary>
        public static float MaxForcePanelScreenWidthFraction { get; set; } = 0.35f;

        /// <summary>
        /// Minimum width multiplier applied to the top-right Build / Favourites bar when open, relative to its closed header width.
        /// </summary>
        public static float OpenFavouritesBarMinWidthMultiplier { get; set; } = 1.35f;

        /// <summary>
        /// Absolute maximum width (px) for the dynamically sized top-right Build / Favourites bar when open.
        /// </summary>
        public static float MaxFavouritesBarWidth { get; set; } = 420f;

        /// <summary>
        /// Maximum fraction of screen width the top-right Build / Favourites bar can occupy when open.
        /// </summary>
        public static float MaxFavouritesBarScreenWidthFraction { get; set; } = 0.35f;

        /// <summary>
        /// Colors used for your team
        /// </summary>
        public static Color TeamColor { get; set; } = new Color(0.00f, 1.00f, 1.00f, 1.00f); // cyan blue
    }
}
