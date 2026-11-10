using System;
using System.Collections.Generic;

namespace CasualtiesSize.Tests
{
    public class AssertionFailed : Exception
    {
        public AssertionFailed(string message) : base(message) { }
    }

    public static class Check
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new AssertionFailed(message);
        }

        public static void False(bool condition, string message)
        {
            if (condition) throw new AssertionFailed(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new AssertionFailed(message + ": expected '" + expected + "' but was '" + actual + "'");
        }

        public static void Near(float expected, float actual, float tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new AssertionFailed(message + ": expected " + expected + " but was " + actual);
        }
    }

    public class TestSuite
    {
        private readonly List<KeyValuePair<string, Action>> cases = new List<KeyValuePair<string, Action>>();

        public void Add(string name, Action body)
        {
            cases.Add(new KeyValuePair<string, Action>(name, body));
        }

        public int Run()
        {
            int failed = 0;
            foreach (KeyValuePair<string, Action> testCase in cases)
            {
                try
                {
                    testCase.Value();
                    Console.WriteLine("ok    " + testCase.Key);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine("FAIL  " + testCase.Key + ": " + ex.Message);
                }
            }
            Console.WriteLine("ran " + cases.Count + " core tests, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }

    public static class Program
    {
        public static int Main()
        {
            TestSuite suite = new TestSuite();
            RelationalTests.Register(suite);
            RuleTests.Register(suite);
            CommandTests.Register(suite);
            WireTests.Register(suite);
            CaptureTests.Register(suite);
            GateTests.Register(suite);
            return suite.Run();
        }
    }
}
