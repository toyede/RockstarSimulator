using UnityEngine;

namespace ContextStage
{
    public static class CardVFXPalette
    {
        public static readonly Color Chill = Hex(0x30E1B9);
        public static readonly Color Singalong = Hex(0x9B5DE5);
        public static readonly Color Mosh = Hex(0xFB4B1D);
        public static readonly Color Utility = Hex(0xF9C22B);

        public static Color ResolveFamily(CardRole role, HeatStage stage)
        {
            if (role == CardRole.Utility) return Utility;

            switch (stage)
            {
                case HeatStage.Chill: return Chill;
                case HeatStage.Singalong: return Singalong;
                case HeatStage.Mosh: return Mosh;
                default: return Color.white;
            }
        }

        public static Color ResolveTrail(
            CardRole role,
            HeatStage stage,
            Color cardColor)
        {
            bool isUnsetWhite =
                cardColor.r > 0.9f &&
                cardColor.g > 0.9f &&
                cardColor.b > 0.9f;
            if (!isUnsetWhite &&
                cardColor.a > 0.05f &&
                Mathf.Max(cardColor.r, cardColor.g, cardColor.b) > 0.08f)
            {
                cardColor.a = 1f;
                return cardColor;
            }

            return ResolveFamily(role, stage);
        }

        static Color Hex(int rgb) =>
            new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
    }
}
