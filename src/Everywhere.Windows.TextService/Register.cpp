#include "pch.h"
#include <olectl.h>

static constexpr WCHAR RegInfoPrefixClsid[] = L"CLSID\\";
static constexpr WCHAR RegInfoKeyInProSvr32[] = L"InProcServer32";
static constexpr WCHAR RegInfoKeyThreadModel[] = L"ThreadingModel";
static constexpr WCHAR TextServiceDesc[] = L"Everywhere";

HMODULE globalDllHandle;
LONG globalDllRefCount = -1;
CRITICAL_SECTION globalCriticalSection;
TextServiceFactory *textServiceFactory = nullptr;

// https://learn.microsoft.com/zh-cn/windows/win32/api/msctf/ns-msctf-tf_inputprocessorprofile
static const GUID SupportCategories[] =
{
    GUID_TFCAT_TIPCAP_SECUREMODE,
    GUID_TFCAT_TIPCAP_COMLESS,
    GUID_TFCAT_TIPCAP_IMMERSIVESUPPORT,
};

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

static LONG RecurseDeleteKey(_In_ const HKEY hParentKey, _In_ const LPCTSTR lpszKey)
{
    HKEY regKeyHandle = nullptr;
    FILETIME time;
    WCHAR stringBuffer[256] = {};
    DWORD size = ARRAYSIZE(stringBuffer);

    if (RegOpenKey(hParentKey, lpszKey, &regKeyHandle) != ERROR_SUCCESS)
    {
        return ERROR_SUCCESS;
    }

    LONG res = ERROR_SUCCESS;
    while (RegEnumKeyEx(regKeyHandle, 0, stringBuffer, &size, nullptr, nullptr, nullptr, &time) == ERROR_SUCCESS)
    {
        stringBuffer[ARRAYSIZE(stringBuffer) - 1] = '\0';
        res = RecurseDeleteKey(regKeyHandle, stringBuffer);
        if (res != ERROR_SUCCESS)
        {
            break;
        }
        size = ARRAYSIZE(stringBuffer);
    }
    RegCloseKey(regKeyHandle);

    return res == ERROR_SUCCESS ? RegDeleteKey(hParentKey, lpszKey) : res;
}

static BOOL RegisterServer()
{
    DEBUG_LOG(L"RegisterServer Start");

    DWORD copiedStringLen = 0;
    HKEY regKeyHandle = nullptr;
    HKEY regSubKeyHandle = nullptr;
    BOOL ret = FALSE;
    WCHAR imeKey[ARRAYSIZE(RegInfoPrefixClsid) + CLSID_STRLEN] = {};
    WCHAR dllFilePath[MAX_PATH] = {};

    if (!ClsidToString(CLSID_TextService, imeKey + ARRAYSIZE(RegInfoPrefixClsid) - 1))
    {
        return FALSE;
    }

    memcpy(imeKey, RegInfoPrefixClsid, sizeof(RegInfoPrefixClsid) - sizeof(WCHAR));

    if (RegCreateKeyEx(
        HKEY_CLASSES_ROOT, imeKey, 0, nullptr,
        REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr,
        &regKeyHandle, &copiedStringLen) == ERROR_SUCCESS)
    {
        if (RegSetValueEx(
            regKeyHandle, nullptr, 0, REG_SZ, reinterpret_cast<const BYTE *>(TextServiceDesc),
            (_countof(TextServiceDesc)) * sizeof(WCHAR)) != ERROR_SUCCESS)
        {
            goto Exit;
        }

        if (RegCreateKeyEx(
            regKeyHandle, RegInfoKeyInProSvr32, 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_WRITE, nullptr,
            &regSubKeyHandle, &copiedStringLen) == ERROR_SUCCESS)
        {
            copiedStringLen = GetModuleFileNameW(globalDllHandle, dllFilePath, ARRAYSIZE(dllFilePath));
            DEBUG_LOG(L"globalDllHandle: %p, DLL Path: %s", globalDllHandle, dllFilePath);
            copiedStringLen = copiedStringLen >= MAX_PATH - 1 ? MAX_PATH : ++copiedStringLen;
            if (RegSetValueEx(
                regSubKeyHandle, nullptr, 0, REG_SZ, reinterpret_cast<const BYTE *>(dllFilePath),
                copiedStringLen * sizeof(WCHAR)) != ERROR_SUCCESS)
            {
                goto Exit;
            }
            if (RegSetValueEx(
                regSubKeyHandle, RegInfoKeyThreadModel, 0, REG_SZ,
                reinterpret_cast<const BYTE *>(TEXTSERVICE_MODEL),
                (_countof(TEXTSERVICE_MODEL)) * sizeof(WCHAR)) != ERROR_SUCCESS)
            {
                goto Exit;
            }

            ret = TRUE;
        }
    }

Exit:
    if (regSubKeyHandle)
    {
        RegCloseKey(regSubKeyHandle);
        regSubKeyHandle = nullptr;
    }
    if (regKeyHandle)
    {
        RegCloseKey(regKeyHandle);
        regKeyHandle = nullptr;
    }

    return ret;
}

static void UnregisterServer()
{
    WCHAR achImeKey[ARRAYSIZE(RegInfoPrefixClsid) + CLSID_STRLEN] = {};
    if (!ClsidToString(CLSID_TextService, achImeKey + ARRAYSIZE(RegInfoPrefixClsid) - 1)) return;
    memcpy(achImeKey, RegInfoPrefixClsid, sizeof(RegInfoPrefixClsid) - sizeof(WCHAR));
    RecurseDeleteKey(HKEY_CLASSES_ROOT, achImeKey);
}

static BOOL RegisterProfiles()
{
    DEBUG_LOG(L"RegisterProfiles Start");

    // https://github.com/ChineseInputMethod/Interface/blob/master/TSFmanager/ITfInputProcessorProfileMgr.md
    HRESULT hr;
    ITfInputProcessorProfileMgr *pITfInputProcessorProfileMgr = nullptr;
    if (FAILED(hr = CoCreateInstance(
        CLSID_TF_InputProcessorProfiles,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_ITfInputProcessorProfileMgr,
        reinterpret_cast<void **>(&pITfInputProcessorProfileMgr))))
    {
        return FALSE;
    }

    // https://learn.microsoft.com/zh-cn/windows/win32/api/msctf/nf-msctf-itfinputprocessorprofilemgr-registerprofile
    hr = pITfInputProcessorProfileMgr->RegisterProfile(
        CLSID_TextService, TEXTSERVICE_LANGID, GUID_Profile,
        TextServiceDesc, sizeof(TextServiceDesc) / sizeof(WCHAR),
        nullptr, 0, 0, nullptr, 0,
        TRUE, TF_RP_HIDDENINSETTINGUI);

    if (pITfInputProcessorProfileMgr)
    {
        pITfInputProcessorProfileMgr->Release();
    }

    return hr == S_OK;
}

void UnregisterProfiles()
{
    ITfInputProcessorProfileMgr *pITfInputProcessorProfileMgr = nullptr;
    const auto hr = CoCreateInstance(
        CLSID_TF_InputProcessorProfiles, nullptr, CLSCTX_INPROC_SERVER,
        IID_ITfInputProcessorProfileMgr, reinterpret_cast<void **>(&pITfInputProcessorProfileMgr));
    if (SUCCEEDED(hr))
    {
        pITfInputProcessorProfileMgr->UnregisterProfile(CLSID_TextService, TEXTSERVICE_LANGID, GUID_Profile, 0);
    }

    if (pITfInputProcessorProfileMgr)
    {
        pITfInputProcessorProfileMgr->Release();
    }
}

static BOOL RegisterCategories()
{
    DEBUG_LOG(L"RegisterCategories Start");

    // https://github.com/ChineseInputMethod/Interface/blob/master/TSFmanager/ITfCategoryMgr.md
    ITfCategoryMgr *pCategoryMgr = nullptr;

    auto hr = CoCreateInstance(
        CLSID_TF_CategoryMgr, nullptr, CLSCTX_INPROC_SERVER,
        IID_ITfCategoryMgr, reinterpret_cast<void **>(&pCategoryMgr));
    if (FAILED(hr))
    {
        return FALSE;
    }

    // https://learn.microsoft.com/zh-cn/windows/win32/api/msctf/nf-msctf-itfcategorymgr-registercategory
    for (GUID guid : SupportCategories)
    {
        hr = pCategoryMgr->RegisterCategory(CLSID_TextService, guid, CLSID_TextService);
    }

    pCategoryMgr->Release();
    return hr == S_OK;
}

static void UnregisterCategories()
{
    ITfCategoryMgr *pCategoryMgr = nullptr;
    const auto hr = CoCreateInstance(
        CLSID_TF_CategoryMgr, nullptr, CLSCTX_INPROC_SERVER,
        IID_ITfCategoryMgr, reinterpret_cast<void **>(&pCategoryMgr));
    if (FAILED(hr))
    {
        return;
    }

    for (GUID guid : SupportCategories)
    {
        pCategoryMgr->UnregisterCategory(CLSID_TextService, guid, CLSID_TextService);
    }

    pCategoryMgr->Release();
}

_Check_return_
STDAPI DllGetClassObject(
    _In_ REFCLSID rclsid,
    _In_ REFIID riid,
    _Outptr_ void** ppv)
{
    if ((IsEqualIID(riid, IID_IClassFactory) ||
        IsEqualIID(riid, IID_IUnknown)) &&
        IsEqualGUID(rclsid, CLSID_TextService))
    {
        *ppv = static_cast<IClassFactory *>(textServiceFactory);
        DllAddRef();
        return NOERROR;
    }

    *ppv = nullptr;
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow(void)
{
    if (globalDllRefCount >= 0)
    {
        return S_FALSE;
    }

    return S_OK;
}

STDAPI DllRegisterServer(void)
{
    if (!RegisterServer() || !RegisterProfiles() || !RegisterCategories())
    {
        DllUnregisterServer();
        return E_FAIL;
    }

    DEBUG_LOG(L"DllRegisterServer End");
    return S_OK;
}

STDAPI DllUnregisterServer(void)
{
    UnregisterServer();
    UnregisterProfiles();
    UnregisterCategories();
    return S_OK;
}
