using System;
using System.Collections.Generic;
using System.Reflection;
using CasualtiesSize.Core;
using HarmonyLib;

namespace CasualtiesSize
{
    public static class ConsoleOutput
    {
        private static readonly MethodInfo LogMethod = AccessTools.Method(typeof(ConsoleScript), "LogToConsole");

        public static void Write(string text)
        {
            if (string.IsNullOrEmpty(text) || ConsoleScript.instance == null) return;
            LogMethod.Invoke(ConsoleScript.instance, new object[] { text });
        }
    }

    [HarmonyPatch(typeof(ConsoleScript), nameof(ConsoleScript.RegisterAllCommands))]
    public static class RegisterCommandsPatch
    {
        private const string TargetShortDescription = "player target";

        private static void Postfix()
        {
            Register(new Command("size", "Set a body size: size <target> <factor|reset>. Sizes multiply the character's own stature.",
                arguments => Run(arguments), null,
                (TargetShortDescription, "@m yourself, @a everyone, @r random, @c at the cursor, id:N, or a name"),
                ("float factor", "2, x0.5, 150% or reset")));
            Register(new Command("sizes", "Lists every body that is not size 1.", arguments => Run(arguments), null));
            Register(new Command("sizerule", "Shows or sets a size rule (host only in multiplayer): sizerule [name [value]]",
                arguments => Run(arguments), null, ("string rule", "rule name"), ("float value", "new value")));
            Register(new Command("eat", "A much larger body swallows a smaller one: eat <eater> <target>",
                arguments => Run(arguments), null, (TargetShortDescription, "who eats"), (TargetShortDescription, "who is eaten")));
            Register(new Command("release", "Lets a held or swallowed body go: release <target>",
                arguments => Run(arguments), null, (TargetShortDescription, "who is let go")));
        }

        private static void Register(Command command)
        {
            ConsoleScript.Commands.RemoveAll(existing => existing.name == command.name);
            ConsoleScript.Commands.Add(command);
        }

        private static void Run(string[] arguments)
        {
            SizeCommand command;
            string error;
            if (!SizeCommandParser.TryParse(arguments, out command, out error)) throw new Exception(error);
            string reply = Plugin.Dispatch(command);
            ConsoleOutput.Write(reply);
        }
    }
}
