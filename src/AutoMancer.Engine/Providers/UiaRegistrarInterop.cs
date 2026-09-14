// Copyright (c) AutoMancer Contributors. Licensed under the Apache License, Version 2.0.
using System.Runtime.InteropServices;
using AutoMancer.Engine.Core;

namespace AutoMancer.Engine.Providers;

// This whole file is more complex than average since it's leveraging GUIDs and code from Microsoft's uiautomationcore.h. Apologies.

// Hand-declared COM interop for IUIAutomationRegistrar — Interop.UIAutomationClient 10.19041.0 omits this interface entirely, so custom-property GUID resolution can't go through the generated wrapper.
internal static class UiaRegistrarInterop
{
    // CLSID_CUIAutomationRegistrar picks which native COM class to activate, distinct from the IID below which picks which interface to call on it once created.
    private static readonly Guid ClsidCUIAutomationRegistrar = new("6e29fabf-9977-42d1-8d0e-ca7e61ad87e6");

    // Activates the native CUIAutomationRegistrar COM object once and reuses it — in-proc inside uiautomationcore.dll, same as CUIAutomation8Class; it's stateless plumbing to a session-wide OS service, so one shared instance is as correct as a fresh one per call.
    // The ! marks assume this CLSID is always registered on Windows — a framework guarantee, not something worth a runtime null check.
    private static readonly IUIAutomationRegistrar Registrar =
        (IUIAutomationRegistrar)Activator.CreateInstance(Type.GetTypeFromCLSID(ClsidCUIAutomationRegistrar)!)!;

    // Resolves a custom UIA property's stable GUID to this session's numeric PropertyId — Windows allocates that id per session, not as a fixed constant.
    internal static Task<int> RegisterCustomPropertyAsync(Guid propertyGuid, string programmaticName, UiaAutomationType type, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var info = new UiaAutomationPropertyInfo { Guid = propertyGuid, ProgrammaticName = programmaticName, Type = (int)type };
            Registrar.RegisterProperty(ref info, out var propertyId);
            return propertyId;
        }, ct);
    }
}

// Mirrors the native IUIAutomationRegistrar COM interface (uiautomationcore.h)
// Only RegisterProperty is declared since it's the only member AutoMancer calls, and it's the first vtable slot after IUnknown so nothing earlier is skipped (which would cause issues).
[ComImport]
// IID_IUIAutomationRegistrar — the interface id QueryInterface uses to hand back this interface's vtable, distinct from the CLSID above which picks the object itself.
[Guid("8609c4ec-4a1a-4d88-a357-5a66e060e1cf")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IUIAutomationRegistrar
{
    void RegisterProperty(ref UiaAutomationPropertyInfo property, out int propertyId);
}

// Mirrors the native UIAutomationPropertyInfo struct (uiautomationcore.h) passed to IUIAutomationRegistrar.RegisterProperty.
[StructLayout(LayoutKind.Sequential)]
internal struct UiaAutomationPropertyInfo
{
    public Guid Guid;
    // LPWStr marshals this as a null-terminated UTF-16 string pointer, matching the native LPCWSTR field exactly — the .NET default could pick a different encoding and corrupt the string.
    [MarshalAs(UnmanagedType.LPWStr)] public string ProgrammaticName;
    public int Type;
}
