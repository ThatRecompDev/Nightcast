// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Tests.Live;
using Phantom.Relay.Media.Windows.Interop;
using Phantom.Relay.Nvidia.Interop;

namespace Phantom.Relay.Nvidia.Tests.Live;

/// <summary>
/// The owner's FRUC folder for the live tests (FRUC_SDK_DIR when set, else the newest SDK at a drive's root) and a test
/// device on the NVIDIA GPU (a PC may have an integrated GPU first).
/// </summary>
internal static class FrucSdk
{
    public static string Folder()
    {
        var roots = DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady).Select(drive => drive.RootDirectory.FullName);
        var folder = NvidiaFrucLocator.Find(Environment.GetEnvironmentVariable("FRUC_SDK_DIR"), roots);
        Assert.True(folder is not null, "NvOFFRUC.dll was not found; set FRUC_SDK_DIR to the SDK's NvOFFRUC\\NvOFFRUCSample\\bin\\win64");

        return folder;
    }

    public static D3D11TestDevice NvidiaDevice()
    {
        var adapter = NvidiaAdapters.FindNvidia();
        Assert.True(adapter != 0, "no NVIDIA GPU");
        var device = new D3D11TestDevice(adapter);
        ComObjects.Release(ref adapter);

        return device;
    }
}
