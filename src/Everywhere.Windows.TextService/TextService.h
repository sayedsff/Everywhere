#pragma once
#include "pch.h"

class TextService final :
    public ITfTextInputProcessorEx,
    public ITfThreadMgrEventSink
{
public:
    TextService(): refCount(0), pThreadMgr(nullptr), clientId(0), dwActiveId(0), threadMgrEventSinkCookie(0)
    {
        AddRef();
    }

    // IUnknown
    STDMETHODIMP QueryInterface(REFIID riid, _Outptr_ void **ppvObj) override;
    STDMETHODIMP_(ULONG) AddRef() override;
    STDMETHODIMP_(ULONG) Release() override;

    // ITfTextInputProcessor
    STDMETHODIMP Activate(ITfThreadMgr *ptim, TfClientId tid) override;
    STDMETHODIMP ActivateEx(ITfThreadMgr *ptim, TfClientId tid, DWORD dwFlags) override;
    STDMETHODIMP Deactivate() override;

    // ITfThreadMgrEventSink
    STDMETHODIMP OnInitDocumentMgr(_In_ ITfDocumentMgr *pDocMgr) override;
    STDMETHODIMP OnUninitDocumentMgr(_In_ ITfDocumentMgr *pDocMgr) override;
    STDMETHODIMP OnSetFocus(_In_ ITfDocumentMgr *pDocMgrFocus, _In_ ITfDocumentMgr *pDocMgrPrevFocus) override;
    STDMETHODIMP OnPushContext(_In_ ITfContext *pContext) override;
    STDMETHODIMP OnPopContext(_In_ ITfContext *pContext) override;

private:
    HRESULT InitThreadMgrEventSink();

private:
    LONG refCount;

    ITfThreadMgr *pThreadMgr;
    TfClientId clientId;
    DWORD dwActiveId;

    DWORD threadMgrEventSinkCookie;
};
