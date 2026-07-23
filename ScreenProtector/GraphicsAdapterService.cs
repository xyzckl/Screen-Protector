using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ScreenProtector;

public static class GraphicsAdapterService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTPUT_DESC
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
        public RECT DesktopCoordinates;
        [MarshalAs(UnmanagedType.Bool)]
        public bool AttachedToDesktop;
        public int Rotation;
        public IntPtr Monitor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [ComImport]
    [Guid("7B7166EC-21C7-44AE-B21A-C9AE321AE369")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory6
    {
        [PreserveSig] int SetPrivateData();
        [PreserveSig] int SetPrivateDataInterface();
        [PreserveSig] int GetPrivateData();
        [PreserveSig] int GetParent();
        [PreserveSig] int EnumAdapters(uint adapter, out IntPtr ppAdapter);
        [PreserveSig] int MakeWindowAssociation();
        [PreserveSig] int GetWindowAssociation();
        [PreserveSig] int CreateSwapChain();
        [PreserveSig] int CreateSoftwareAdapter();
        [PreserveSig] int EnumAdapterByLuid();
        [PreserveSig] int EnumWarpAdapter();
        [PreserveSig] int EnumAdapterByGpuPreference(uint adapter, int gpuPreference, ref Guid riid, out IntPtr ppvAdapter);
    }

    [ComImport]
    [Guid("29038F61-3839-4626-91FD-086879011A05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        [PreserveSig] int SetPrivateData();
        [PreserveSig] int SetPrivateDataInterface();
        [PreserveSig] int GetPrivateData();
        [PreserveSig] int GetParent();
        [PreserveSig] int EnumOutputs(uint output, out IntPtr ppOutput);
        [PreserveSig] int GetDesc(out DXGI_ADAPTER_DESC1 desc);
    }

    [ComImport]
    [Guid("AE02EEDB-C735-4690-8D52-5A8DC20213AA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIOutput
    {
        [PreserveSig] int SetPrivateData();
        [PreserveSig] int SetPrivateDataInterface();
        [PreserveSig] int GetPrivateData();
        [PreserveSig] int GetParent();
        [PreserveSig] int GetDesc(out DXGI_OUTPUT_DESC desc);
    }

    [DllImport("dxgi.dll")]
    private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

    private const int DXGI_GPU_PREFERENCE_UNSPECIFIED = 0;
    private const int DXGI_ERROR_NOT_FOUND = unchecked((int)0x887A0002);

    public static IReadOnlyList<GraphicsAdapterInfo> GetAdapters()
    {
        var adapters = new List<GraphicsAdapterInfo>();
        Guid factoryGuid = typeof(IDXGIFactory6).GUID;
        IntPtr factoryPtr = IntPtr.Zero;
        object? factoryComObject = null;

        try
        {
            int factoryHr = CreateDXGIFactory1(ref factoryGuid, out factoryPtr);
            if (factoryHr < 0 || factoryPtr == IntPtr.Zero)
            {
                return adapters;
            }

            var factory = (IDXGIFactory6)Marshal.GetObjectForIUnknown(factoryPtr);
            factoryComObject = factory;
            try
            {
                uint index = 0;
                while (true)
                {
                    Guid adapterGuid = typeof(IDXGIAdapter1).GUID;
                    int hr = factory.EnumAdapterByGpuPreference(index, DXGI_GPU_PREFERENCE_UNSPECIFIED, ref adapterGuid, out IntPtr adapterPtr);
                    if (hr == DXGI_ERROR_NOT_FOUND)
                    {
                        break;
                    }

                    if (hr < 0 || adapterPtr == IntPtr.Zero)
                    {
                        index++;
                        continue;
                    }

                    try
                    {
                        var adapter = (IDXGIAdapter1)Marshal.GetObjectForIUnknown(adapterPtr);
                        try
                        {
                            adapter.GetDesc(out DXGI_ADAPTER_DESC1 desc);

                            bool isDefaultOutputAdapter = HasPrimaryDesktopOutput(adapter);
                            string name = string.IsNullOrWhiteSpace(desc.Description)
                                ? $"GPU {index}"
                                : desc.Description.Trim();

                            adapters.Add(new GraphicsAdapterInfo
                            {
                                Id = desc.AdapterLuid.ToString(),
                                Name = isDefaultOutputAdapter ? $"{name} (当前显示输出)" : name,
                                IsDefaultOutputAdapter = isDefaultOutputAdapter
                            });
                        }
                        catch
                        {
                            // Ignore one bad adapter and continue so startup cannot hang on enumeration.
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(adapter);
                        }
                    }
                    finally
                    {
                        Marshal.Release(adapterPtr);
                    }

                    index++;
                }
            }
            finally
            {
                if (factoryComObject != null)
                {
                    Marshal.ReleaseComObject(factoryComObject);
                }
            }
        }
        catch
        {
            return adapters;
        }
        finally
        {
            if (factoryPtr != IntPtr.Zero)
            {
                Marshal.Release(factoryPtr);
            }
        }

        return adapters;
    }

    public static string? GetDefaultAdapterId(IReadOnlyList<GraphicsAdapterInfo> adapters)
    {
        foreach (var adapter in adapters)
        {
            if (adapter.IsDefaultOutputAdapter)
            {
                return adapter.Id;
            }
        }

        return adapters.Count > 0 ? adapters[0].Id : null;
    }

    private static bool HasPrimaryDesktopOutput(IDXGIAdapter1 adapter)
    {
        for (uint outputIndex = 0; ; outputIndex++)
        {
            int hr = adapter.EnumOutputs(outputIndex, out IntPtr outputPtr);
            if (hr == DXGI_ERROR_NOT_FOUND)
            {
                return false;
            }

            if (hr < 0 || outputPtr == IntPtr.Zero)
            {
                continue;
            }

            try
            {
                var output = (IDXGIOutput)Marshal.GetObjectForIUnknown(outputPtr);
                try
                {
                    output.GetDesc(out DXGI_OUTPUT_DESC outputDesc);
                    if (outputDesc.AttachedToDesktop && outputDesc.DesktopCoordinates.Left == 0 && outputDesc.DesktopCoordinates.Top == 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(output);
                }
            }
            finally
            {
                Marshal.Release(outputPtr);
            }
        }
    }
}