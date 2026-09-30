using System;
using UnityEngine;

namespace Topoda.RLS.Observer
{
    internal static class ObserverPalette
    {
        public static readonly Color AppBg = Hex("FAF7F2");
        public static readonly Color SurfaceCard = Hex("F2EDE6");
        public static readonly Color SurfaceRaised = Hex("E8E2DA");
        public static readonly Color Ink900 = Hex("3F3A36");
        public static readonly Color Ink500 = Hex("9C928A");
        public static readonly Color Ink300 = Hex("D8D1C8");
        public static readonly Color LabelCoral = Hex("E88B7D");
        public static readonly Color SemanticSuccess = Hex("8FB996");
        public static readonly Color SemanticWarning = Hex("E8B86D");
        public static readonly Color SemanticDanger = Hex("E07A7A");
        public static readonly Color SemanticInfo = Hex("8FBEE0");
        public static readonly Color NationAnnglora = Hex("CC99FF");
        public static readonly Color NationByteria = Hex("3333FF");
        public static readonly Color NationCrownia = Hex("FFD700");
        public static readonly Color LabelCoralHover = Hex("F0A396");
        public static readonly Color LabelCoralActive = Hex("D9776A");

        public static Color NationAccent(string nation)
        {
            if (string.Equals(nation, "Byteria", StringComparison.Ordinal))
            {
                return NationByteria;
            }

            if (string.Equals(nation, "Crownia", StringComparison.Ordinal))
            {
                return NationCrownia;
            }

            return NationAnnglora;
        }

        public static Color Hex(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString("#" + value, out color) ? color : Color.white;
        }
    }
}
