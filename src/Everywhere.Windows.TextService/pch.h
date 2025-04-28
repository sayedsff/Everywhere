#pragma once

#include <windows.h>
#include <msctf.h>
#include <strsafe.h>
#include <cassert>
#include <atlbase.h>
#include "Define.h"
#include "TextService.pb.h"

class TextServiceFactory;

extern HMODULE globalDllHandle;
extern LONG globalDllRefCount;
extern CRITICAL_SECTION globalCriticalSection;
extern TextServiceFactory *textServiceFactory;

#ifdef _DEBUG
#define DEBUG_LOG(fmt, ...) \
    do { \
        WCHAR buffer[1024]; \
        StringCchPrintf(buffer, ARRAYSIZE(buffer), L"[Everywhere] " fmt L"\n", __VA_ARGS__); \
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

// {0D000721-E638-7C3E-EFD2-AD2DF039499B}
static const GUID IID_IServerMessageSink =
{
    0x0d000721,
    0xe638,
    0x7c3e,
    {0xef, 0xd2, 0xad, 0x2d, 0xf0, 0x39, 0x49, 0x9b}
};

MIDL_INTERFACE("0D000721-E638-7C3E-EFD2-AD2DF039499B")
IServerMessageSink : IUnknown
{
    virtual HRESULT STDMETHODCALLTYPE OnServerMessage(const ServerMessage *msg) = 0;
};

typedef interface IServerMessageSink IServerMessageSink;

constexpr BYTE GuidSymbols[] =
{
    3, 2, 1, 0, '-', 5, 4, '-', 7, 6, '-', 8, 9, '-', 10, 11, 12, 13, 14, 15
};

constexpr WCHAR HexDigits[] = L"0123456789ABCDEF";

BOOL ClsidToString(REFGUID refGuid, _Out_writes_(39) WCHAR *pClsidString);

LONG DllAddRef();
LONG DllRelease();

inline std::string WideToUtf8(const std::wstring &wide)
{
    if (wide.empty()) return {};

    const int sizeNeeded = WideCharToMultiByte(
        CP_UTF8, 0, wide.data(), static_cast<int>(wide.size()), nullptr, 0, nullptr, nullptr);
    if (sizeNeeded <= 0)
    {
        DEBUG_LOG(L"WideToUtf8, WideCharToMultiByte failed, sizeNeeded <= 0: %d", GetLastError());
        return {};
    }

    std::string utf8(sizeNeeded, 0);
    const int result = WideCharToMultiByte(
        CP_UTF8, 0, wide.data(), static_cast<int>(wide.size()), utf8.data(), sizeNeeded, nullptr, nullptr);
    if (result <= 0)
    {
        DEBUG_LOG(L"WideToUtf8, WideCharToMultiByte failed, result <= 0: %d", GetLastError());
        return {};
    }

    return utf8;
}

inline std::wstring Utf8ToWide(const std::string &utf8)
{
    if (utf8.empty()) return {};

    const int sizeNeeded = MultiByteToWideChar(
        CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), nullptr, 0);
    if (sizeNeeded <= 0)
    {
        DEBUG_LOG(L"UTF8ToWide, MultiByteToWideChar failed, size_needed <= 0: %d", GetLastError());
        return {};
    }

    std::wstring wide(sizeNeeded, 0);
    const int result = MultiByteToWideChar(
        CP_UTF8, 0, utf8.data(), static_cast<int>(utf8.size()), wide.data(), sizeNeeded);
    if (result <= 0)
    {
        DEBUG_LOG(L"UTF8ToWide, MultiByteToWideChar failed, result <= 0: %d", GetLastError());
        return {};
    }

    return wide;
}
