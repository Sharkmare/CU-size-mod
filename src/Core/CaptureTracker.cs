using System;

namespace CasualtiesSize.Core
{
    public enum CaptureMode : byte
    {
        Held = 0,
        Swallowed = 1,
    }

    public enum CaptureEnd
    {
        None,
        BrokeFree,
        TimedOut,
    }

    public sealed class CaptureTracker
    {
        public const float StruggleDecayPerSecond = 1f;

        public readonly CaptureMode Mode;
        public float Elapsed { get; private set; }
        public float Struggles { get; private set; }

        public CaptureTracker(CaptureMode mode)
        {
            Mode = mode;
        }

        public void Struggle()
        {
            Struggles += 1f;
        }

        public CaptureEnd Tick(SizeRules rules, float deltaSeconds)
        {
            Elapsed += deltaSeconds;
            if (Struggles >= rules.EatEscapeStruggles) return CaptureEnd.BrokeFree;
            Struggles = Math.Max(0f, Struggles - StruggleDecayPerSecond * deltaSeconds);
            if (Mode == CaptureMode.Swallowed && Elapsed >= rules.EatMaxSeconds) return CaptureEnd.TimedOut;
            return CaptureEnd.None;
        }
    }
}
