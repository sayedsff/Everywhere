#pragma once

#include <msctf.h>
#include <windows.h>
#include <strsafe.h>
#include <cassert>
#include "Define.h"
#include "TextService.h"
#include "TextServiceFactory.h"

extern HMODULE globalDllHandle;
extern LONG globalDllRefCount;
extern CRITICAL_SECTION globalCriticalSection;
extern TextServiceFactory *textServiceFactory;

#ifdef _DEBUG
#define DEBUG_LOG(fmt, ...) \
    do { \
        WCHAR buffer[1024]; \
        StringCchPrintf(buffer, ARRAYSIZE(buffer), L"[Everywhere] " fmt, __VA_ARGS__); \
        OutputDebugString(buffer); \
    } while (0)
#else
#define DEBUG_LOG(fmt, ...) do {} while (0)
#endif

// {00114514-E638-7C3E-EFD2-AD2DF039499B}
constexpr CLSID CLSID_TextService =
{
    0x00114514,
    0xe638,
    0x7c3e,
    {0xef, 0xd2, 0xad, 0x2d, 0xf0, 0x39, 0x49, 0x9b}
};

// {01919810-E638-7C3E-EFD2-AD2DF039499B}
constexpr GUID GUID_Profile =
{
    0x01919810,
    0xe638,
    0x7c3e,
    {0xef, 0xd2, 0xad, 0x2d, 0xf0, 0x39, 0x49, 0x9b}
};

constexpr BYTE GuidSymbols[] =
{
    3, 2, 1, 0, '-', 5, 4, '-', 7, 6, '-', 8, 9, '-', 10, 11, 12, 13, 14, 15
};

constexpr WCHAR HexDigits[] = L"0123456789ABCDEF";

inline BOOL ClsidToString(REFGUID refGuid, _Out_writes_(39) WCHAR *pClsidString)
{
    WCHAR *pTemp = pClsidString;
    const auto pBytes = reinterpret_cast<const BYTE *>(&refGuid);

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

LONG DllAddRef();
LONG DllRelease();
