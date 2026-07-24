using System;
using UnityEngine;

namespace ContextStage
{
    [Serializable]
    public struct CrowdCompositionSnapshot : IEquatable<CrowdCompositionSnapshot>
    {
        [SerializeField, Min(0)] int chillCount;
        [SerializeField, Min(0)] int singalongCount;
        [SerializeField, Min(0)] int moshCount;

        public CrowdCompositionSnapshot(int chillCount, int singalongCount, int moshCount)
        {
            this.chillCount = chillCount;
            this.singalongCount = singalongCount;
            this.moshCount = moshCount;
        }

        public int ChillCount => chillCount;
        public int SingalongCount => singalongCount;
        public int MoshCount => moshCount;
        public int TotalCount => chillCount + singalongCount + moshCount;

        public int GetCount(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillCount;
                case CrowdPreference.Singalong: return singalongCount;
                case CrowdPreference.Mosh: return moshCount;
                default: return 0;
            }
        }

        public float GetRatio(CrowdPreference preference) =>
            TotalCount > 0 ? (float)GetCount(preference) / TotalCount : 0f;

        public bool IsValid(int expectedTotal) =>
            chillCount >= 0 &&
            singalongCount >= 0 &&
            moshCount >= 0 &&
            TotalCount == expectedTotal;

        public bool Equals(CrowdCompositionSnapshot other) =>
            chillCount == other.chillCount &&
            singalongCount == other.singalongCount &&
            moshCount == other.moshCount;

        public override bool Equals(object obj) =>
            obj is CrowdCompositionSnapshot other && Equals(other);

        public override int GetHashCode() =>
            ((chillCount * 397) ^ singalongCount) * 397 ^ moshCount;

        public static bool operator ==(CrowdCompositionSnapshot left, CrowdCompositionSnapshot right) =>
            left.Equals(right);

        public static bool operator !=(CrowdCompositionSnapshot left, CrowdCompositionSnapshot right) =>
            !left.Equals(right);
    }

    [Serializable]
    public struct CrowdCompositionPreset
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] CrowdCompositionSnapshot composition;

        public CrowdCompositionPreset(string id, string displayName, CrowdCompositionSnapshot composition)
        {
            this.id = id;
            this.displayName = displayName;
            this.composition = composition;
        }

        public string Id => id;
        public string DisplayName => displayName;
        public CrowdCompositionSnapshot Composition => composition;
    }
}
