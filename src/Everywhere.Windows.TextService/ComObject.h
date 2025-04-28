#pragma once

#include "pch.h"
#include <Windows.h>

template <typename Derived, typename... Interfaces>
class ComObject : public Interfaces...
{
public:
    ComObject() noexcept = default;
    virtual ~ComObject() noexcept = default;

    STDMETHODIMP_(ULONG) AddRef() override
    {
        return refCount.fetch_add(1, std::memory_order_relaxed) + 1;
    }

    STDMETHODIMP_(ULONG) Release() override
    {
        const ULONG cnt = refCount.fetch_sub(1, std::memory_order_acq_rel) - 1;
        if (cnt == 0) delete static_cast<Derived *>(this);
        return cnt;
    }

private:
    std::atomic<ULONG> refCount{1};
};

#define BEGIN_INTERFACE_TABLE(ClassName) \
HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppv) override { \
if (!ppv) return E_POINTER;

#define INTERFACE_ENTRY(InterfaceName)      \
if (riid == __uuidof(InterfaceName)) {      \
*ppv = static_cast<InterfaceName *>(this);  \
AddRef();                                   \
return S_OK; \
}

#define INTERFACE_ENTRY_IUNKNOWN(InterfaceName) \
if (riid == __uuidof(IUnknown)) {               \
*ppv = static_cast<InterfaceName *>(this);      \
AddRef();                                       \
return S_OK;                                    \
}

#define END_INTERFACE_TABLE()   \
*ppv = nullptr;                 \
return E_NOINTERFACE;           \
}
