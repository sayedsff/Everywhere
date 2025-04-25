#include "pch.h"

HRESULT TextService::QueryInterface(REFIID riid, _Outptr_ void **ppvObj)
{
    WCHAR guidString[64];
    StringFromGUID2(riid, guidString, ARRAYSIZE(guidString));
    DEBUG_LOG(L"TextService::QueryInterface, riid: %s", guidString);

    if (ppvObj == nullptr)
    {
        return E_INVALIDARG;
    }

    if (IsEqualIID(riid, IID_IUnknown) ||
        IsEqualIID(riid, IID_ITfTextInputProcessor))
    {
        *ppvObj = static_cast<ITfTextInputProcessor *>(this);
    }
    else if (IsEqualIID(riid, IID_ITfTextInputProcessorEx))
    {
        *ppvObj = static_cast<ITfTextInputProcessorEx *>(this);
    }
    else if (IsEqualIID(riid, IID_ITfThreadMgrEventSink))
    {
        *ppvObj = static_cast<ITfThreadMgrEventSink *>(this);
    }
    else
    {
        *ppvObj = nullptr;
    }

    if (*ppvObj)
    {
        AddRef();
        return S_OK;
    }

    return E_NOINTERFACE;
}

ULONG TextService::AddRef()
{
    DEBUG_LOG(L"TextService::AddRef, refCount: %d", refCount);

    return ++refCount;
}

STDAPI_(ULONG) TextService::Release()
{
    DEBUG_LOG(L"TextService::Release, refCount: %d", refCount);

    const LONG cr = --refCount;
    assert(refCount >= 0);
    if (refCount == 0) delete this;
    return cr;
}

HRESULT TextService::Activate(ITfThreadMgr *ptim, const TfClientId tid)
{
    return this->ActivateEx(ptim, tid, 0);
}

HRESULT TextService::ActivateEx(ITfThreadMgr *ptim, const TfClientId tid, const DWORD dwFlags)
{
    const auto pid = GetCurrentProcessId();
    WCHAR processName[MAX_PATH];
    GetModuleFileName(nullptr, processName, ARRAYSIZE(processName));
    DEBUG_LOG(L"TextService::ActivateEx, tid: %u, dwFlags: %u, pid: %u, processName: %s", tid, dwFlags, pid, processName);

    pThreadMgr = ptim;
    pThreadMgr->AddRef();
    clientId = tid;
    dwActiveId = dwFlags;

    HRESULT hr;
    if (FAILED(hr = InitThreadMgrEventSink()))
    {
        goto Error;
    }

    return S_OK;

Error:
    DEBUG_LOG(L"TextService::ActivateEx, Error");
    Deactivate();
    return hr;
}

HRESULT TextService::Deactivate()
{
    DEBUG_LOG(L"TextService::Deactivate");

    dwActiveId = 0;
    clientId = TF_CLIENTID_NULL;

    if (pThreadMgr != nullptr)
    {
        pThreadMgr->Release();
        pThreadMgr = nullptr;
    }

    return S_OK;
}

HRESULT TextService::OnInitDocumentMgr(ITfDocumentMgr *pDocMgr)
{
    return S_OK;
}

HRESULT TextService::OnUninitDocumentMgr(ITfDocumentMgr *pDocMgr)
{
    return S_OK;
}

HRESULT TextService::OnSetFocus(ITfDocumentMgr *pDocMgrFocus, ITfDocumentMgr *pDocMgrPrevFocus)
{
    DEBUG_LOG(L"OnSetFocus");
    return S_OK;
}

HRESULT TextService::OnPushContext(ITfContext *pContext)
{
    return S_OK;
}

HRESULT TextService::OnPopContext(ITfContext *pContext)
{
    return S_OK;
}

HRESULT TextService::InitThreadMgrEventSink()
{
    DEBUG_LOG(L"TextService::InitThreadMgrEventSink");

    ITfSource* pSource = nullptr;
    HRESULT hr;

    if (FAILED(hr = pThreadMgr->QueryInterface(IID_ITfSource, reinterpret_cast<void **>(&pSource))))
    {
        return hr;
    }

    if (FAILED(hr = pSource->AdviseSink(
        IID_ITfThreadMgrEventSink, static_cast<ITfThreadMgrEventSink *>(this),
        &threadMgrEventSinkCookie)))
    {
        threadMgrEventSinkCookie = TF_INVALID_COOKIE;
    }

    pSource->Release();
    return hr;
}
