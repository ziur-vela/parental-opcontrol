// Copyright (c) 2026 Ziur / OpControl IT Solutions
// SPDX-License-Identifier: PolyForm-Noncommercial-1.0.0

using OpControl.Parental.Contracts;

namespace OpControl.Parental.Server.Services;

public static class DevicePresentation
{
    public static string OsLabel(DevicePlatform platform) => platform.ToString().ToUpperInvariant();

    public static bool IsPhone(DevicePlatform platform) => platform == DevicePlatform.Android;

    public static bool CanStartRemote(bool online) => online;
}
