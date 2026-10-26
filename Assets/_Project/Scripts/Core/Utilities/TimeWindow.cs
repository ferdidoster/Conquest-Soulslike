using System;
using UnityEngine;

namespace Conquest.Core.Utilities
{
    /// <summary>A [start, end] range in normalized time (0..1) of an action.</summary>
    [Serializable]
    public struct TimeWindow
    {
        [Range(0f, 1f)] public float start;
        [Range(0f, 1f)] public float end;

        public TimeWindow(float start, float end)
        {
            this.start = start;
            this.end = end;
        }

        public bool Contains(float normalizedTime) => normalizedTime >= start && normalizedTime <= end;
        public bool HasEnded(float normalizedTime) => normalizedTime > end;
    }
}
