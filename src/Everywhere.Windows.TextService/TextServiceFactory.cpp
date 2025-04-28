#include "pch.h"
#include "TextService.h"
#include "TextServiceFactory.h"

STDAPI TextServiceFactory::QueryInterface(REFIID riid, _Outptr_ void **ppvObj)
{
    DEBUG_LOG(L"TextServiceFactory::QueryInterface");

    if (IsEqualIID(riid, IID_IClassFactory) || IsEqualIID(riid, IID_IUnknown))
    {
        *ppvObj = this;
        DllAddRef();
        return NOERROR;
    }

    *ppvObj = nullptr;
    return E_NOINTERFACE;
}

STDAPI_(ULONG) TextServiceFactory::AddRef()
{
    DEBUG_LOG(L"TextServiceFactory::AddRef");

    return DllAddRef();
}

STDAPI_(ULONG) TextServiceFactory::Release()
{
    DEBUG_LOG(L"TextServiceFactory::Release");

    return DllRelease();
}

STDAPI TextServiceFactory::CreateInstance(_In_opt_ IUnknown *pUnkOuter, _In_ REFIID riid, _COM_Outptr_ void **ppvObj)
{
    DEBUG_LOG(L"TextServiceFactory::CreateInstance");

    if (ppvObj == nullptr)
    {
        return E_INVALIDARG;
    }
    if (pUnkOuter != nullptr)
    {
        return CLASS_E_NOAGGREGATION;
    }

    const auto pTextService = new TextService();
    const auto hr = pTextService->QueryInterface(riid, ppvObj);
    pTextService->Release();
    return hr;
}

STDAPI TextServiceFactory::LockServer(const BOOL fLock)
{
    DEBUG_LOG(L"TextServiceFactory::LockServer, fLock: %d", fLock);

    if (fLock)
    {
        DllAddRef();
    }
    else
    {
        DllRelease();
    }

    return S_OK;
}