using CasualtiesSize.Core;

namespace CasualtiesSize.Tests
{
    public static class CaptureTests
    {
        public static void Register(TestSuite suite)
        {
            suite.Add("enough struggles break a hold free", () =>
            {
                SizeRules rules = new SizeRules();
                CaptureTracker tracker = new CaptureTracker(CaptureMode.Swallowed);
                for (int press = 0; press < rules.EatEscapeStruggles; press++) tracker.Struggle();
                Check.Equal(CaptureEnd.BrokeFree, tracker.Tick(rules, 0.02f), "struggled free");
            });

            suite.Add("one press per second decays as fast as it builds and never frees", () =>
            {
                SizeRules rules = new SizeRules();
                CaptureTracker tracker = new CaptureTracker(CaptureMode.Swallowed);
                for (int second = 0; second < 30; second++)
                {
                    tracker.Struggle();
                    Check.Equal(CaptureEnd.None, tracker.Tick(rules, 1f), "second " + second);
                }
            });

            suite.Add("a swallowed body is released at the time limit, a held body is not", () =>
            {
                SizeRules rules = new SizeRules();
                CaptureTracker swallowed = new CaptureTracker(CaptureMode.Swallowed);
                CaptureTracker held = new CaptureTracker(CaptureMode.Held);
                CaptureEnd swallowedEnd = CaptureEnd.None;
                CaptureEnd heldEnd = CaptureEnd.None;
                for (float time = 0f; time < rules.EatMaxSeconds + 1f; time += 1f)
                {
                    swallowedEnd = swallowed.Tick(rules, 1f);
                    heldEnd = held.Tick(rules, 1f);
                    if (swallowedEnd != CaptureEnd.None) break;
                }
                Check.Equal(CaptureEnd.TimedOut, swallowedEnd, "swallowed times out");
                Check.Equal(CaptureEnd.None, heldEnd, "held keeps holding");
            });

            suite.Add("pickup command parses", () =>
            {
                SizeCommand command;
                string error;
                Check.True(SizeCommandParser.TryParse(new[] { "pickup", "@m", "id:2" }, out command, out error), "parse");
                Check.Equal(SizeVerb.Pickup, command.Verb, "verb");
                Check.Equal("id:2", command.Secondary, "target");
            });
        }
    }
}
