// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using Xunit;

namespace OpControl.Parental.Tests;

/// <summary>Headless Avalonia tests share one in-process application, so they must not overlap.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class HeadlessUiCollection
{
    public const string Name = "Headless UI";
}
