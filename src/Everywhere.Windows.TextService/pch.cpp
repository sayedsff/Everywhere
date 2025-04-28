#include "pch.h"
#include "TextServiceFactory.h"

HMODULE globalDllHandle;
LONG globalDllRefCount = -1;
CRITICAL_SECTION globalCriticalSection;
TextServiceFactory* textServiceFactory = nullptr;

BOOL ClsidToString(REFGUID refGuid, _Out_writes_(39) WCHAR* pClsidString)
{
    WCHAR* pTemp = pClsidString;
    const auto pBytes = reinterpret_cast<const BYTE*>(&refGuid);

    DWORD j = 0;
    pTemp[j++] = L'{';
    for (int i = 0; i < sizeof(GuidSymbols) && j < CLSID_STRLEN - 2; i++)
    {
        if (GuidSymbols[i] == '-')
        {
            pTemp[j++] = L'-';
        }
        else
        {
            pTemp[j++] = HexDigits[(pBytes[GuidSymbols[i]] & 0xF0) >> 4];
            pTemp[j++] = HexDigits[(pBytes[GuidSymbols[i]] & 0x0F)];
        }
    }

    pTemp[j++] = L'}';
    pTemp[j] = L'\0';

    return TRUE;
}

LONG DllAddRef()
{
    DEBUG_LOG("DllAddRef, globalDllRefCount: %d\n", globalDllRefCount);

    return InterlockedIncrement(&globalDllRefCount);
}

LONG DllRelease()
{
    DEBUG_LOG("DllRelease, globalDllRefCount: %d\n", globalDllRefCount);

    const auto refCount = InterlockedDecrement(&globalDllRefCount);
    if (refCount < 0)
    {
        EnterCriticalSection(&globalCriticalSection);
        if (textServiceFactory != nullptr)
        {
            delete textServiceFactory;
            textServiceFactory = nullptr;
        }
        LeaveCriticalSection(&globalCriticalSection);

        assert(globalDllRefCount == -1);
    }
    return refCount;
}