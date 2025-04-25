#pragma once
#include "pch.h"

class TextServiceFactory final : public IClassFactory
{
public:
    TextServiceFactory() = default;

    // IUnknown methods
    STDMETHODIMP QueryInterface(REFIID riid, _Outptr_ void **ppvObj) override;
    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;

    // IClassFactory methods
    STDMETHODIMP CreateInstance(_In_opt_ IUnknown *pUnkOuter, _In_ REFIID riid, _COM_Outptr_ void **ppvObj) override;
    STDMETHODIMP LockServer(BOOL fLock) override;
};
