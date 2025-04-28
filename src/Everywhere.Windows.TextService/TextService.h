#pragma once
#include "pch.h"
#include "ComObject.h"

class TextService final :
    public ComObject<
        TextService,
        IServerMessageSink,
        ITfTextInputProcessor,
        ITfThreadMgrEventSink,
        ITfTextEditSink>
{
public:
    TextService() :
        clientId(0), rpc(nullptr), gitCookie(0),
        threadMgrEventSinkCookie(TF_INVALID_COOKIE), textEditSinkCookie(TF_INVALID_COOKIE)
    {
        DEBUG_LOG(L"TextService::TextService");
        if (FAILED(CoCreateFreeThreadedMarshaler(static_cast<IServerMessageSink *>(this), &pFtm)))
        {
            DEBUG_LOG(L"TextService::TextService, CoCreateFreeThreadedMarshaler failed");
        }
    }

    ~TextService() override
    {
        DEBUG_LOG(L"TextService::~TextService");
        if (rpc)
        {
            delete rpc;
            rpc = nullptr;
        }
    }

    // IUnknown
    BEGIN_INTERFACE_TABLE(TextService)
        INTERFACE_ENTRY_IUNKNOWN(ITfTextInputProcessor)
        INTERFACE_ENTRY(IServerMessageSink)
        INTERFACE_ENTRY(ITfTextInputProcessor)
        INTERFACE_ENTRY(ITfThreadMgrEventSink)
        INTERFACE_ENTRY(ITfTextEditSink)
        if (riid == IID_IMarshal && pFtm)
        {
            return pFtm->QueryInterface(riid, ppv);
        }
    END_INTERFACE_TABLE()

    // IServerMessageSink
    HRESULT OnServerMessage(const ServerMessage *msg) override;

    // ITfTextInputProcessor
    STDMETHODIMP Activate(ITfThreadMgr *ptim, TfClientId tid) override;
    STDMETHODIMP Deactivate() override;

    // ITfThreadMgrEventSink
    STDMETHODIMP OnInitDocumentMgr(ITfDocumentMgr *pDocMgr) override;
    STDMETHODIMP OnUninitDocumentMgr(ITfDocumentMgr *pDocMgr) override;
    STDMETHODIMP OnSetFocus(ITfDocumentMgr *pDocMgrFocus, ITfDocumentMgr *pDocMgrPrevFocus) override;
    STDMETHODIMP OnPushContext(ITfContext *pContext) override;
    STDMETHODIMP OnPopContext(ITfContext *pContext) override;

    // ITfTextEditSink
    STDMETHODIMP OnEndEdit(ITfContext *pContext, TfEditCookie ecReadOnly, ITfEditRecord *pEditRecord) override;

protected:
    class CEditSession final : public ComObject<CEditSession, ITfEditSession>
    {
    public:
        CEditSession(TextService *pTextService, ITfContext *pContext, const ServerMessage *msg)
            : pTextService(pTextService), pContext(pContext), msg(msg)
        {
            assert(pTextService != nullptr);
            assert(pContext != nullptr);
            assert(msg != nullptr);
        }

        // IUnknown
        BEGIN_INTERFACE_TABLE(TextService)
            INTERFACE_ENTRY_IUNKNOWN(ITfEditSession)
            INTERFACE_ENTRY(ITfEditSession)
        END_INTERFACE_TABLE()

        // ITfEditSession
        STDMETHODIMP DoEditSession(TfEditCookie ec) override;

    private:
        TextService *pTextService;
        ITfContext *pContext;
        const ServerMessage *msg;
    };

    HRESULT InitRpc();
    HRESULT InitThreadMgrEventSink();
    HRESULT InitTextEditSink(ITfDocumentMgr *pDocMgr);

    CComPtr<ITfThreadMgr> pThreadMgr;
    TfClientId clientId;
    CComPtr<IUnknown> pFtm;
    Rpc *rpc;
    DWORD gitCookie;
    DWORD threadMgrEventSinkCookie;
    DWORD textEditSinkCookie;
    std::map<CComPtr<ITfContext>, CComPtr<IUnknown>> contexts;
};
