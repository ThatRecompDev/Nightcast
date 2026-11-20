// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>
/// IMFAttributes (mfobjects.h): a store of values by GUID, which media types, samples and transforms all are. Every
/// slot is declared in the header's order so derived interfaces line up; the ones without parameters are never called.
/// Interface pointers travel as <see cref="nint"/>, so the caller owns and releases them.
/// </summary>
[GeneratedComInterface]
[Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
internal unsafe partial interface IMFAttributes
{
    void GetItem();

    void GetItemType();

    void CompareItem();

    void Compare();

    [PreserveSig]
    int GetUInt32(in Guid key, out uint value);

    [PreserveSig]
    int GetUInt64(in Guid key, out ulong value);

    void GetDouble();

    [PreserveSig]
    int GetGuid(in Guid key, out Guid value);

    [PreserveSig]
    int GetStringLength(in Guid key, out uint length);

    [PreserveSig]
    int GetString(in Guid key, char* value, uint size, out uint length);

    void GetAllocatedString();

    [PreserveSig]
    int GetBlobSize(in Guid key, out uint size);

    [PreserveSig]
    int GetBlob(in Guid key, byte* buffer, uint size, out uint written);

    void GetAllocatedBlob();

    void GetUnknown();

    void SetItem();

    void DeleteItem();

    void DeleteAllItems();

    [PreserveSig]
    int SetUInt32(in Guid key, uint value);

    [PreserveSig]
    int SetUInt64(in Guid key, ulong value);

    void SetDouble();

    [PreserveSig]
    int SetGuid(in Guid key, in Guid value);

    void SetString();

    [PreserveSig]
    int SetBlob(in Guid key, byte* buffer, uint size);

    [PreserveSig]
    int SetUnknown(in Guid key, nint value);

    void LockStore();

    void UnlockStore();

    void GetCount();

    void GetItemByIndex();

    void CopyAllItems();
}
