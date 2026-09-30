using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Topoda.RLS.Observer
{
    public sealed partial class ObserverApplicationController
    {
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle primaryButtonStyle;
        private GUIStyle filterActiveButtonStyle;
        private GUIStyle panelStyle;
        private GUIStyle fieldStyle;
        private static readonly Dictionary<string, Texture2D> SolidTextures = new Dictionary<string, Texture2D>();

        private void OnGUI()
        {
            if (toolkitRoot != null) return;
            BuildStyles();
            if (state != ApplicationState.Observer)
            {
                Fill(new Rect(0f, 0f, Screen.width, Screen.height), highContrast ? Color.black : ObserverPalette.AppBg);
            }

            switch (state)
            {
                case ApplicationState.Splash: DrawSplash(); break;
                case ApplicationState.MainMenu: DrawMainMenu(); break;
                case ApplicationState.Loading: DrawLoading(); break;
                case ApplicationState.Observer: DrawObserver(); break;
                case ApplicationState.Settings: DrawSettings(); break;
                case ApplicationState.Credits: DrawCredits(); break;
                case ApplicationState.Exiting: DrawExiting(); break;
            }

            if (!string.IsNullOrWhiteSpace(plainLanguageError))
            {
                DrawErrorBanner();
            }
        }

        private void DrawSplash()
        {
            float size = Mathf.Min(Screen.width, Screen.height) * 0.42f;
            if (logo != null)
            {
                GUI.DrawTexture(new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.45f, size, size), logo, ScaleMode.ScaleToFit, true);
            }
            Label(new Rect(0f, Screen.height * 0.75f, Screen.width, 36f), "A GAIA OBSERVER", headingStyle, ObserverPalette.SemanticInfo, TextAnchor.MiddleCenter);
        }

        private void DrawMainMenu()
        {
            DrawBrandHeader("RLS Observer", MenuCopyWatchWorldGrow + " — no campaign, no management commands.");
            float width = Mathf.Min(620f, Screen.width * 0.62f);
            float left = (Screen.width - width) * 0.5f;
            float top = Screen.height * 0.36f;

            if (Button(new Rect(left, top, width, 72f), "Start January 1, 2425", primaryButtonStyle)) BeginObserver(ObserverMode.WatchPreSimulation);
            top += 84f;
            if (Button(new Rect(left, top, width, 56f), MenuCopyOpenHandoff + "\nGenerate the deterministic handoff and open it paused", buttonStyle)) BeginObserver(ObserverMode.OpenHandoff);
            top += 68f;
            if (Button(new Rect(left, top, width, 46f), MenuCopyLoadSnapshot, buttonStyle))
            {
                showSnapshotPicker = true;
                snapshotList = snapshots.List();
            }
            top += 56f;
            if (Button(new Rect(left, top, (width - 12f) * 0.5f, 44f), MenuCopySettings, buttonStyle)) state = ApplicationState.Settings;
            if (Button(new Rect(left + (width + 12f) * 0.5f, top, (width - 12f) * 0.5f, 44f), MenuCopyCredits, buttonStyle)) state = ApplicationState.Credits;
            top += 54f;
            if (Button(new Rect(left, top, width, 40f), MenuCopyExit, buttonStyle)) ExitCleanly();

            Label(new Rect(left, top + 58f, width, 24f), "Offline-ready  •  Local snapshots  •  Generic eight-label validation seed", smallStyle, ObserverPalette.Ink500, TextAnchor.MiddleCenter);
            if (showSnapshotPicker) DrawSnapshotPicker();
        }

        private void DrawLoading()
        {
            DrawBrandHeader("PREPARING THE SANDBOX", loadingMessage);
            float width = Mathf.Min(760f, Screen.width * 0.72f);
            Rect track = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.53f, width, 18f);
            Fill(track, ObserverPalette.SurfaceRaised);
            Fill(new Rect(track.x, track.y, track.width * Mathf.Clamp01(loadingProgress), track.height), ObserverPalette.LabelCoral);
            Label(new Rect(track.x, track.y + 30f, track.width, 30f), Mathf.RoundToInt(loadingProgress * 100f) + "%", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleCenter);
            Label(new Rect(track.x, track.y + 72f, track.width, 28f), "Five-minute clock  •  ordered events  •  canonical digest", smallStyle, ObserverPalette.Ink500, TextAnchor.MiddleCenter);
        }

        private void DrawObserver()
        {
            EnsureObserverLayoutRects();
            DrawTopBar();
            DrawWorldHud(observerWorldGuiRect);
            DrawInspector(observerInspectorGuiRect);
            DrawEvents(observerEventsGuiRect);
        }

        private void DrawTopBar()
        {
            Fill(new Rect(0f, 0f, Screen.width, 78f), TopBarOverlay());
            Label(new Rect(18f, 10f, 250f, 28f), "Central Bloomville", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft);
            Label(new Rect(18f, 40f, 250f, 22f), mode == ObserverMode.OpenHandoff ? "Aug 31 handoff" : "Watch the world grow", smallStyle, ObserverPalette.Ink500, TextAnchor.MiddleLeft);

            string date = simulation == null ? "—" : simulation.World.Clock.CurrentUtc.ToString("MMM d, yyyy  HH:mm") + " GST";
            Label(new Rect(Screen.width * 0.29f, 8f, 280f, 34f), date, headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleCenter);
            Label(new Rect(Screen.width * 0.29f, 42f, 280f, 20f), simulation != null && simulation.World.Clock.Paused ? "PAUSED" : "OBSERVING ×" + simulation.World.Clock.Speed, smallStyle, simulation != null && simulation.World.Clock.Paused ? ObserverPalette.SemanticWarning : ObserverPalette.SemanticInfo, TextAnchor.MiddleCenter);

            float x = Screen.width - 560f;
            if (Button(new Rect(x, 18f, 76f, 40f), simulation.World.Clock.Paused ? "Play" : "Pause", buttonStyle)) TogglePause();
            x += 84f;
            if (Button(new Rect(x, 18f, 72f, 40f), "Step", buttonStyle)) { SafeAutosave("before single step"); simulation.SingleStep(); statusMessage = "Advanced one five-minute step"; }
            x += 70f;
            if (Button(new Rect(x, 18f, 62f, 40f), "×1", buttonStyle)) SetSpeed(1);
            x += 66f;
            if (Button(new Rect(x, 18f, 62f, 40f), "×4", buttonStyle)) SetSpeed(4);
            x += 66f;
            if (Button(new Rect(x, 18f, 62f, 40f), "×16", buttonStyle)) SetSpeed(16);
            x += 66f;
            if (Button(new Rect(x, 18f, 76f, 40f), "Menu", buttonStyle)) ReturnToMenu();
        }

        private void DrawWorldHud(Rect area)
        {
            DrawShadowLabel(new Rect(area.x + 16f, area.y + 12f, 340f, 28f), "Central Bloomville", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft);
            DrawShadowLabel(new Rect(area.xMax - 360f, area.y + 14f, 344f, 24f), "WASD pan  •  wheel zoom  •  Q/E orbit  •  click HQ", smallStyle, ObserverPalette.Ink900, TextAnchor.MiddleRight);
        }

        private void DrawInspector(Rect area)
        {
            Fill(area, CardOverlay());
            GUI.BeginGroup(area);
            Label(new Rect(16f, 14f, area.width - 32f, 28f), "Read-only Inspector", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft);
            LabelRecord label = simulation == null ? null : simulation.FindLabel(selectedLabelId);
            if (label == null)
            {
                Label(new Rect(16f, 64f, area.width - 32f, 48f), "Select a label on the map to inspect it.", bodyStyle, ObserverPalette.Ink500, TextAnchor.UpperLeft);
                GUI.EndGroup();
                return;
            }

            float y = 58f;
            Fill(new Rect(16f, y, 2f, 36f), ObserverPalette.NationAccent(label.Nation));
            Color chipBackground = ObserverPalette.NationAccent(label.Nation);
            chipBackground.a = 0.22f;
            Fill(new Rect(20f, y + 4f, 52f, 22f), chipBackground);
            Label(new Rect(20f, y + 4f, 52f, 22f), label.MarketCode, smallStyle, ObserverPalette.Ink900, TextAnchor.MiddleCenter);
            Label(new Rect(78f, y, area.width - 94f, 36f), label.Name, titleStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft); y += 42f;
            Label(new Rect(16f, y, area.width - 32f, 44f), label.Nation + "\n" + label.Archetype + " (non-canon validation tuning)", bodyStyle, ObserverPalette.Ink500, TextAnchor.UpperLeft); y += 56f;
            DrawMetric(area.width, ref y, "Cash", "$" + label.Cash.ToString("N0"));
            DrawMetric(area.width, ref y, "Reputation", label.Reputation.ToString());
            DrawMetric(area.width, ref y, "Catalog value", label.CatalogValue.ToString("N0"));
            DrawMetric(area.width, ref y, "Chart score", label.ChartScore.ToString("N0"));
            DrawMetric(area.width, ref y, "Release cadence", ObserverDisplayFormat.Preference(label.ReleaseCadencePreference));
            DrawMetric(area.width, ref y, "Quality target", ObserverDisplayFormat.Preference(label.QualityPreference));
            DrawMetric(area.width, ref y, "Promotion spend", ObserverDisplayFormat.Preference(label.PromotionPreference));
            DrawMetric(area.width, ref y, "Cash reserve", ObserverDisplayFormat.Preference(label.CashReservePreference));
            DrawMetric(area.width, ref y, "Audience growth", ObserverDisplayFormat.Preference(label.AudienceGrowthPreference));
            DrawMetric(area.width, ref y, "Trend response", ObserverDisplayFormat.Preference(label.TrendResponsePreference));
            ActRecord act = simulation.World.Acts.FirstOrDefault(item => item.LabelId == label.Id);
            StructureRecord structure = simulation.World.Structures.FirstOrDefault(item => item.LabelId == label.Id);
            DrawMetric(area.width, ref y, "Act", act == null ? "—" : act.Name);
            DrawMetric(area.width, ref y, "Audience", act == null ? "—" : act.Audience.ToString("N0"));
            DrawMetric(area.width, ref y, "Structure slots", structure == null ? "—" : structure.OccupiedSlots + " / " + structure.SlotCount);

            y += 12f;
            Label(new Rect(16f, y, area.width - 32f, 24f), "Released catalog", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft); y += 30f;
            foreach (CreativeWorkRecord work in simulation.World.Works.Where(item => item.LabelId == label.Id).OrderByDescending(item => item.CreatedTicks).Take(4))
            {
                Label(new Rect(16f, y, area.width - 32f, 38f), work.Title + "\n" + ObserverDisplayFormat.StageLabel(work), smallStyle, work.Stage == WorkStage.Track ? ObserverPalette.SemanticSuccess : ObserverPalette.Ink500, TextAnchor.UpperLeft);
                y += 42f;
            }

            y = Mathf.Max(y + 8f, area.height - 232f);
            Label(new Rect(16f, y, area.width - 32f, 22f), "Snapshots", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft); y += 30f;
            snapshotName = GUI.TextField(new Rect(16f, y, area.width - 32f, 34f), snapshotName, fieldStyle); y += 42f;
            if (Button(new Rect(16f, y, area.width - 32f, 38f), "Create named snapshot", primaryButtonStyle)) SaveNamedSnapshot(); y += 46f;
            if (Button(new Rect(16f, y, (area.width - 42f) * 0.5f, 36f), "Skip 1 day", buttonStyle)) Skip(TimeSpan.FromDays(1));
            if (Button(new Rect(26f + (area.width - 42f) * 0.5f, y, (area.width - 42f) * 0.5f, 36f), "Skip 1 week", buttonStyle)) Skip(TimeSpan.FromDays(7)); y += 44f;
            if (Button(new Rect(16f, y, (area.width - 42f) * 0.5f, 36f), MenuCopyLoadSnapshot, buttonStyle)) { showSnapshotPicker = true; snapshotList = snapshots.List(); }
            if (Button(new Rect(26f + (area.width - 42f) * 0.5f, y, (area.width - 42f) * 0.5f, 36f), "Reset", buttonStyle)) ResetWorld(); y += 44f;
            Label(new Rect(16f, y, area.width - 32f, 40f), statusMessage + "\n" + cloud.Status, smallStyle, ObserverPalette.Ink500, TextAnchor.UpperLeft);
            GUI.EndGroup();
            if (showSnapshotPicker) DrawSnapshotPicker();
        }

        private void DrawEvents(Rect area)
        {
            Fill(area, CardOverlay());
            GUI.BeginGroup(area);
            Label(new Rect(14f, 10f, 160f, 24f), "World Events", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft);
            string[] filters = { "All", "Creation", "Production", "Release", "Promotion", "Chart" };
            float filterWidth = Mathf.Max(72f, (area.width - 188f) / filters.Length);
            float filterX = 172f;
            foreach (string filter in filters)
            {
                if (Button(new Rect(filterX, 8f, filterWidth - 4f, 28f), filter, filter == eventFilter ? filterActiveButtonStyle : buttonStyle)) eventFilter = filter;
                filterX += filterWidth;
            }

            IList<WorldEventRecord> events = simulation == null ? new List<WorldEventRecord>() : simulation.RecentEvents(40, eventFilter);
            Rect view = new Rect(12f, 44f, area.width - 24f, area.height - 54f);
            Rect content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height, events.Count * 54f));
            eventScroll = GUI.BeginScrollView(view, eventScroll, content);
            float y = 0f;
            foreach (WorldEventRecord item in events)
            {
                DateTime time = new DateTime(item.OccurredTicks, DateTimeKind.Utc);
                Label(new Rect(0f, y, 112f, 48f), time.ToString("MMM d\nHH:mm"), smallStyle, ObserverPalette.Ink500, TextAnchor.UpperLeft);
                Label(new Rect(112f, y, content.width - 112f, 48f), item.Headline + "\n" + item.Explanation, smallStyle, ObserverPalette.Ink900, TextAnchor.UpperLeft);
                y += 54f;
            }
            GUI.EndScrollView();
            GUI.EndGroup();
        }

        private void DrawSnapshotPicker()
        {
            Rect overlay = new Rect(Screen.width * 0.18f, Screen.height * 0.14f, Screen.width * 0.64f, Screen.height * 0.72f);
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f));
            Fill(overlay, highContrast ? Color.black : ObserverPalette.SurfaceCard);
            GUI.BeginGroup(overlay);
            Label(new Rect(24f, 18f, overlay.width - 120f, 36f), "Load a local snapshot", titleStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft);
            if (Button(new Rect(overlay.width - 98f, 18f, 74f, 34f), "Close", buttonStyle)) showSnapshotPicker = false;
            Label(new Rect(24f, 58f, overlay.width - 48f, 28f), "Loading never deletes your current world; an autosave is made first.", smallStyle, ObserverPalette.Ink500, TextAnchor.MiddleLeft);
            Rect view = new Rect(24f, 96f, overlay.width - 48f, overlay.height - 124f);
            Rect content = new Rect(0f, 0f, view.width - 18f, Mathf.Max(view.height, snapshotList.Count * 70f));
            snapshotScroll = GUI.BeginScrollView(view, snapshotScroll, content);
            float y = 0f;
            if (snapshotList.Count == 0)
            {
                Label(new Rect(0f, 0f, content.width, 50f), "No snapshots yet. Start observing and create one when a moment matters.", bodyStyle, ObserverPalette.Ink500, TextAnchor.UpperLeft);
            }
            foreach (SnapshotMetadata item in snapshotList)
            {
                Fill(new Rect(0f, y, content.width, 62f), ObserverPalette.SurfaceRaised);
                Label(new Rect(12f, y + 8f, content.width - 180f, 46f), item.DisplayName + "\n" + ObserverDisplayFormat.SnapshotDate(item) + "  •  " + item.Compatibility, smallStyle, item.IntegrityValid ? ObserverPalette.Ink900 : ObserverPalette.SemanticDanger, TextAnchor.UpperLeft);
                if (Button(new Rect(content.width - 150f, y + 12f, 138f, 38f), "Load snapshot", buttonStyle)) LoadSnapshot(item.FileName);
                y += 70f;
            }
            GUI.EndScrollView();
            GUI.EndGroup();
        }

        private void DrawSettings()
        {
            DrawBrandHeader("SETTINGS", "Presentation choices never change simulation truth.");
            float width = Mathf.Min(620f, Screen.width * 0.65f);
            float left = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.38f;
            Label(new Rect(left, y, width, 28f), "Text size: " + Mathf.RoundToInt(textScale * 100f) + "%", headingStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft); y += 38f;
            textScale = GUI.HorizontalSlider(new Rect(left, y, width, 28f), textScale, 0.9f, 1.35f); y += 54f;
            reducedMotion = GUI.Toggle(new Rect(left, y, width, 34f), reducedMotion, " Reduced motion", buttonStyle); y += 46f;
            highContrast = GUI.Toggle(new Rect(left, y, width, 34f), highContrast, " High contrast", buttonStyle); y += 62f;
            Label(new Rect(left, y, width, 54f), "Keyboard: Enter confirms focused controls. Escape returns. In the observer, Space pauses; 1/2/3 change speed; N steps; arrows or WASD pan.", bodyStyle, ObserverPalette.Ink500, TextAnchor.UpperLeft); y += 76f;
            if (Button(new Rect(left, y, width, 44f), "Back", buttonStyle)) state = ApplicationState.MainMenu;
        }

        private void DrawCredits()
        {
            DrawBrandHeader("CREDITS", "Record Label Simulator — Observer 0.1.0");
            float width = Mathf.Min(680f, Screen.width * 0.68f);
            float left = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.40f;
            Label(new Rect(left, y, width, 150f), "Created by JL / Tópoda Charts Studios\n\nGaia observer simulation and implementation support by Codex / OpenAI\n\nThis validation roster is generic. The named Hann/ARL3 roster remains reserved for the future default campaign.", bodyStyle, ObserverPalette.Ink900, TextAnchor.UpperCenter);
            if (Button(new Rect(left, y + 176f, width, 44f), "Back", buttonStyle)) state = ApplicationState.MainMenu;
        }

        private void DrawExiting()
        {
            DrawBrandHeader("SAVING YOUR WORLD", "Closing cleanly…");
        }

        private Color CardOverlay()
        {
            Color color = highContrast ? ObserverPalette.SurfaceCard : ObserverPalette.SurfaceCard;
            color.a = highContrast ? 0.94f : 0.88f;
            return color;
        }

        private Color TopBarOverlay()
        {
            Color color = highContrast ? ObserverPalette.SurfaceCard : ObserverPalette.SurfaceCard;
            color.a = highContrast ? 0.94f : 0.92f;
            return color;
        }

        private static void DrawShadowLabel(Rect rect, string text, GUIStyle style, Color color, TextAnchor alignment)
        {
            Color shadow = ObserverPalette.Ink900;
            shadow.a = 0.75f;
            Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style, shadow, alignment);
            Label(rect, text, style, color, alignment);
        }

        private void DrawBrandHeader(string title, string subtitle)
        {
            float logoSize = Mathf.Min(110f, Screen.height * 0.14f);
            Rect logoRect = new Rect((Screen.width - logoSize) * 0.5f, Screen.height * 0.07f, logoSize, logoSize);
            Fill(new Rect(logoRect.x - 8f, logoRect.y - 8f, logoRect.width + 16f, logoRect.height + 16f), ObserverPalette.SurfaceRaised);
            if (logo != null)
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.28f, 0.24f, 0.22f, 1f);
                GUI.DrawTexture(logoRect, logo, ScaleMode.ScaleToFit, true);
                GUI.color = previous;
            }
            Label(new Rect(Screen.width * 0.1f, Screen.height * 0.22f, Screen.width * 0.8f, 58f), title, titleStyle, ObserverPalette.Ink900, TextAnchor.MiddleCenter);
            Label(new Rect(Screen.width * 0.1f, Screen.height * 0.29f, Screen.width * 0.8f, 34f), subtitle, bodyStyle, ObserverPalette.Ink500, TextAnchor.MiddleCenter);
        }

        private void DrawMetric(float width, ref float y, string label, string value)
        {
            Fill(new Rect(16f, y, width - 32f, 32f), ObserverPalette.SurfaceRaised);
            Label(new Rect(26f, y, width * 0.43f, 32f), label, smallStyle, ObserverPalette.Ink500, TextAnchor.MiddleLeft);
            Label(new Rect(width * 0.44f, y, width * 0.50f, 32f), value, smallStyle, ObserverPalette.Ink900, TextAnchor.MiddleRight);
            y += 38f;
        }

        private void DrawErrorBanner()
        {
            Rect banner = new Rect(Screen.width * 0.16f, Screen.height - 100f, Screen.width * 0.68f, 76f);
            Color bannerColor = ObserverPalette.SemanticDanger;
            bannerColor.a = 0.92f;
            Fill(banner, bannerColor);
            Label(new Rect(banner.x + 16f, banner.y + 10f, banner.width - 112f, 56f), plainLanguageError, smallStyle, ObserverPalette.Ink900, TextAnchor.MiddleLeft);
            if (Button(new Rect(banner.xMax - 86f, banner.y + 19f, 70f, 38f), "Okay", buttonStyle)) plainLanguageError = string.Empty;
        }

        private void BuildStyles()
        {
            float scale = Screen.height <= 0 ? 1f : Screen.height / 1080f;
            int baseSize = Mathf.RoundToInt(16f * scale * textScale);
            titleStyle = MakeStyle(Mathf.RoundToInt(26f * scale * textScale), FontStyle.Bold, ObserverPalette.Ink900);
            headingStyle = MakeStyle(Mathf.RoundToInt(17f * scale * textScale), FontStyle.Bold, ObserverPalette.Ink900);
            bodyStyle = MakeStyle(baseSize, FontStyle.Normal, ObserverPalette.Ink900);
            bodyStyle.fontStyle = FontStyle.Bold;
            bodyStyle.wordWrap = true;
            smallStyle = MakeStyle(Mathf.RoundToInt(13f * scale * textScale), FontStyle.Normal, ObserverPalette.Ink900);
            smallStyle.wordWrap = true;
            panelStyle = MakeStyle(baseSize, FontStyle.Normal, ObserverPalette.Ink900);
            buttonStyle = MakeButtonStyle(ObserverPalette.SurfaceRaised, ObserverPalette.Ink900, ObserverPalette.SurfaceCard);
            primaryButtonStyle = MakeButtonStyle(ObserverPalette.LabelCoral, ObserverPalette.Ink900, ObserverPalette.LabelCoralHover, ObserverPalette.LabelCoralActive);
            filterActiveButtonStyle = MakeButtonStyle(ObserverPalette.SemanticInfo, ObserverPalette.Ink900, ObserverPalette.SurfaceCard, ObserverPalette.SemanticInfo);
            fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = baseSize,
                padding = new RectOffset(10, 10, 6, 6),
                normal = { background = SolidTexture(ObserverPalette.SurfaceRaised), textColor = ObserverPalette.Ink900 },
                focused = { background = SolidTexture(ObserverPalette.SurfaceRaised), textColor = ObserverPalette.Ink900 }
            };
        }

        private GUIStyle MakeButtonStyle(Color normal, Color text, Color hover, Color active)
        {
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.Max(13, Mathf.RoundToInt(17f * textScale)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(12, 12, 5, 5)
            };
            style.normal.background = SolidTexture(normal);
            style.normal.textColor = text;
            style.hover.background = SolidTexture(hover);
            style.hover.textColor = ObserverPalette.Ink900;
            style.active.background = SolidTexture(active);
            style.active.textColor = ObserverPalette.Ink900;
            style.focused.background = SolidTexture(active);
            style.focused.textColor = ObserverPalette.Ink900;
            return style;
        }

        private GUIStyle MakeStyle(int size, FontStyle fontStyle, Color color)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, alignment = TextAnchor.MiddleLeft, normal = { textColor = color } };
        }

        private GUIStyle MakeButtonStyle(Color normal, Color text, Color hover)
        {
            return MakeButtonStyle(normal, text, hover, ObserverPalette.LabelCoralActive);
        }

        private static bool Button(Rect rect, string text, GUIStyle style) { return GUI.Button(rect, text, style); }

        private static void Label(Rect rect, string text, GUIStyle style, Color color, TextAnchor alignment)
        {
            Color prior = style.normal.textColor;
            TextAnchor priorAlignment = style.alignment;
            style.normal.textColor = color;
            style.alignment = alignment;
            GUI.Label(rect, text, style);
            style.normal.textColor = prior;
            style.alignment = priorAlignment;
        }

        private static void Fill(Rect rect, Color color) { Color prior = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = prior; }

        private static Texture2D SolidTexture(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color);
            Texture2D texture;
            if (SolidTextures.TryGetValue(key, out texture) && texture != null)
            {
                return texture;
            }
            texture = new Texture2D(1, 1);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            SolidTextures[key] = texture;
            return texture;
        }
    }
}
