// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using System.Text.Json;
using OpControl.Parental.Contracts;

namespace OpControl.Parental.Helper;

internal static class MessageThreadCache
{
    public static List<ChildMessageDto>? Read()
    {
        try
        {
            if (!File.Exists(HelperPaths.MessageThreadFile)) return null;
            return JsonSerializer.Deserialize<List<ChildMessageDto>>(
                File.ReadAllText(HelperPaths.MessageThreadFile), HelperPaths.JsonOptions);
        }
        catch { return null; }
    }
}
