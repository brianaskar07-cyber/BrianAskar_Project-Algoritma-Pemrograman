using System;
using System.Collections.Generic;

namespace FIRE;

public class NlpCommandService
{
    private Dictionary<string[], string> _commands;

    public NlpCommandService()
    {
        _commands =
            new Dictionary<string[], string>()
        {
            {
                new[]
                {
                    "cek kondisi lingkungan",
                    "kondisi lingkungan",
                    "status lingkungan",
                    "cek ruangan"
                },

                "CEK_LINGKUNGAN"
            }
        };
    }

    public string AnalyzeCommand(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText))
            return "TIDAK_DIKENAL";

        string lower =
            inputText.ToLower().Trim();

        foreach (var cmd in _commands)
        {
            foreach (var keyword in cmd.Key)
            {
                if (lower.Contains(keyword))
                {
                    return cmd.Value;
                }
            }
        }

        return "TIDAK_DIKENAL";
    }
}