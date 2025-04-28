#include "pch.h"
#include "TextService.h"

#include <codecvt>
#include <locale>

HRESULT TextService::OnServerMessage(const ServerMessage *msg)
{
    return S_OK;

    HRESULT hr;
    switch (msg->data_case())
    {
        case ServerMessage::kGetFocusText:
        case ServerMessage::kSetFocusText:
        {
            CComPtr<ITfDocumentMgr> pDocMgr;
            if (FAILED(hr = pThreadMgr->GetFocus(&pDocMgr)))
            {
                DEBUG_LOG(L"TextService::OnServerMessage, GetFocus failed: %08X", hr);
                break;
            }
            CComPtr<ITfContext> pContext;
            if (FAILED(hr = pDocMgr->GetBase(&pContext)))
            {
                DEBUG_LOG(L"TextService::OnServerMessage, GetBase failed: %08X", hr);
                break;
            }

            const auto pEditSession = new CEditSession(this, pContext, msg); // new CEditSession, refCount = 1
            CComPtr<ITfEditSession> pce;
            hr = pEditSession->QueryInterface(IID_ITfEditSession, reinterpret_cast<void **>(&pce)); // refCount = 2
            if (FAILED(hr))
            {
                DEBUG_LOG(L"TextService::OnServerMessage, QueryInterface failed: %08X", hr);
                break;
            }
            HRESULT inner;
            if (FAILED(hr = pContext->RequestEditSession(clientId, pce, TF_ES_SYNC | TF_ES_READWRITE, &inner)) ||
                FAILED(hr = inner))
            {
                DEBUG_LOG(L"TextService::OnServerMessage, RequestEditSession failed: %08X", hr);
            }
            pEditSession->Release(); // refCount = 1
            break; // pce will be released here, causing refCount - 1 = 0
        }
    }

    return S_OK;
}

HRESULT TextService::Activate(ITfThreadMgr *ptim, const TfClientId tid)
{
    WCHAR processName[MAX_PATH];
    GetModuleFileName(nullptr, processName, ARRAYSIZE(processName));
    DEBUG_LOG(
        L"TextService::Activate, ptim: %X, tid: %u, processName: %s",
        ptim, tid, processName);

    if (pThreadMgr) return S_OK;

    pThreadMgr = ptim;
    clientId = tid;

    HRESULT hr;
    do
    {
        if (FAILED(hr = InitRpc()))
        {
            DEBUG_LOG(L"TextService::Activate, InitRpc failed: %08X", hr);
            break;
        }
        if (FAILED(hr = InitThreadMgrEventSink()))
        {
            DEBUG_LOG(L"TextService::Activate, InitThreadMgrEventSink failed: %08X", hr);
            break;
        }
        CComPtr<ITfDocumentMgr> pDocMgrFocus;
        if (FAILED(hr = pThreadMgr->GetFocus(&pDocMgrFocus)) && pDocMgrFocus != nullptr)
        {
            DEBUG_LOG(L"TextService::Activate, GetFocus failed: %08X", hr);
            break;
        }
        if (FAILED(hr = InitTextEditSink(pDocMgrFocus)))
        {
            DEBUG_LOG(L"TextService::Activate, InitTextEditSink failed: %08X", hr);
            break;
        }
    }
    while (false);

    if (FAILED(hr)) Deactivate();
    return hr;
}

HRESULT TextService::Deactivate()
{
    DEBUG_LOG(L"TextService::Deactivate");

    // we don't actually deactivate the text service so that it can continue to receive messages
    return S_OK;
}

HRESULT TextService::OnInitDocumentMgr(ITfDocumentMgr *pDocMgr)
{
    DEBUG_LOG(L"TextService::OnInitDocumentMgr, pDocMgr: %p", pDocMgr);
    if (!pDocMgr) return E_INVALIDARG;

    HRESULT hr;
    CComPtr<ITfContext> pContext;
    if (FAILED(hr = pDocMgr->GetBase(&pContext)))
    {
        DEBUG_LOG(L"TextService::OnInitDocumentMgr, GetBase failed: %08X", hr);
        return E_FAIL;
    }
    CComPtr<IUnknown> ctx;
    if (FAILED(hr = pContext->QueryInterface(IID_IUnknown, reinterpret_cast<void **>(&ctx))))
    {
        DEBUG_LOG(L"TextService::OnInitDocumentMgr, QueryInterface failed: %08X", hr);
        return E_FAIL;
    }
    contexts.insert({pContext, ctx});
    return S_OK;
}

HRESULT TextService::OnUninitDocumentMgr(ITfDocumentMgr *pDocMgr)
{
    DEBUG_LOG(L"TextService::OnUninitDocumentMgr, pDocMgr: %p", pDocMgr);
    if (!pDocMgr) return E_INVALIDARG;

    CComPtr<ITfContext> pContext;
    if (HRESULT hr; FAILED(hr = pDocMgr->GetBase(&pContext)))
    {
        DEBUG_LOG(L"TextService::OnUninitDocumentMgr, GetBase failed: %08X", hr);
        return E_FAIL;
    }
    if (const auto it = contexts.find(pContext); it != contexts.end())
    {
        contexts.erase(it);
    }
    else
    {
        DEBUG_LOG(L"OnUninitDocumentMgr, context not found");
    }
    return S_OK;
}

HRESULT TextService::OnSetFocus(ITfDocumentMgr *pDocMgrFocus, ITfDocumentMgr *pDocMgrPrevFocus)
{
    const auto processDocMgr = [this](ITfDocumentMgr *pDocMgr, IUnknown **pUnknown, HWND *hWnd, RECT *rect)
    {
        if (pDocMgr == nullptr)
        {
            *pUnknown = nullptr;
            *hWnd = nullptr;
            return S_OK;
        }

        HRESULT hr;
        CComPtr<ITfContext> pContext;
        if (FAILED(hr = pDocMgr->GetBase(&pContext))) return hr;
        const auto it = contexts.find(pContext);
        *pUnknown = it != contexts.end() ? it->second.p : nullptr;

        CComPtr<ITfContextView> pView;
        if (FAILED(hr = pContext->GetActiveView(&pView))) return hr;
        if (FAILED(hr = pView->GetWnd(hWnd))) return hr;

        if (rect && FAILED(hr = pView->GetScreenExt(rect))) return hr;
        return S_OK;
    };

    InitTextEditSink(pDocMgrFocus);

    IUnknown *pContext, *pPrevContext;
    HWND hWnd, prevHWnd;
    RECT screenRect{};
    if (FAILED(processDocMgr(pDocMgrFocus, &pContext, &hWnd, &screenRect)) ||
        FAILED(processDocMgr(pDocMgrPrevFocus, &pPrevContext, &prevHWnd, nullptr)))
    {
        return S_OK;
    }

    DEBUG_LOG(
        L"TextService::OnSetFocus, pDocMgrFocus: %p, pDocMgrPrevFocus: %p, hWnd: %p, prevHWnd: %p",
        pDocMgrFocus, pDocMgrPrevFocus, hWnd, prevHWnd);

    const auto msg = new ClientMessage();
    const auto focus = msg->mutable_focus_changed();
    focus->set_pid(GetCurrentProcessId());
    focus->set_ctx(reinterpret_cast<uint64_t>(pContext));
    focus->set_hwnd(reinterpret_cast<uint64_t>(hWnd));
    focus->set_prev_ctx(reinterpret_cast<uint64_t>(pPrevContext));
    focus->set_prev_hwnd(reinterpret_cast<uint64_t>(prevHWnd));
    focus->mutable_screen_rect()->set_left(screenRect.left);
    focus->mutable_screen_rect()->set_top(screenRect.top);
    focus->mutable_screen_rect()->set_right(screenRect.right);
    focus->mutable_screen_rect()->set_bottom(screenRect.bottom);
    rpc->Send(msg);
    return S_OK;
}

HRESULT TextService::OnPushContext(ITfContext *pContext)
{
    DEBUG_LOG(L"TextService::OnPushContext, pContext: %p", pContext);
    return S_OK;
}

HRESULT TextService::OnPopContext(ITfContext *pContext)
{
    DEBUG_LOG(L"TextService::OnPopContext, pContext: %p", pContext);
    return S_OK;
}

HRESULT TextService::OnEndEdit(ITfContext *pContext, const TfEditCookie ecReadOnly, ITfEditRecord *pEditRecord)
{
    DEBUG_LOG(L"TextService::OnEndEdit, pic: %p, ecReadOnly: %u, pEditRecord: %p", pContext, ecReadOnly, pEditRecord);

    const auto msg = new ClientMessage();
    const auto endEdit = msg->mutable_end_edit();
    endEdit->set_pid(GetCurrentProcessId());
    const auto it = contexts.find(pContext);
    const auto pUnknown = it != contexts.end() ? it->second.p : nullptr;
    endEdit->set_ctx(reinterpret_cast<uint64_t>(pUnknown));
    rpc->Send(msg);
    return S_OK;
}

HRESULT TextService::CEditSession::DoEditSession(const TfEditCookie ec)
{
    DEBUG_LOG(L"TextService::CEditSession::DoEditSession, ec: %u", ec);

    HRESULT hr;
    switch (msg->data_case())
    {
        case ServerMessage::kGetFocusText:
        {
            CComPtr<ITfRange> pRange;
            std::wstring text;
            if (msg->get_focus_text().selection_only())
            {
                TF_SELECTION selections[4];
                ULONG fetched = 0;

                if (FAILED(hr = pContext->GetSelection(
                    ec,
                    TF_DEFAULT_SELECTION,
                    ARRAYSIZE(selections),
                    selections,
                    &fetched)))
                {
                    DEBUG_LOG(L"DoEditSession::GetFocusText, GetSelection failed: %08X", hr);
                    return hr;
                }
                if (fetched > 0) pRange = selections[0].range;
            }
            else
            {
                CComPtr<ITfRange> pStart, pEnd;
                if (FAILED(hr = pContext->GetStart(ec, &pStart)) || FAILED(hr = pContext->GetEnd(ec, &pEnd)))
                {
                    DEBUG_LOG(L"DoEditSession::GetFocusText, GetStart/GetEnd failed: %08X", hr);
                    return hr;
                }
                if (FAILED(hr = pStart->Clone(&pRange)) ||
                    FAILED(hr = pRange->ShiftEndToRange(ec, pEnd, TF_ANCHOR_END)))
                {
                    DEBUG_LOG(L"DoEditSession::GetFocusText, Clone/ShiftEndToRange failed: %08X", hr);
                    return hr;
                }
            }

            if (pRange)
            {
                ULONG cch = 0;
                if (FAILED(hr = pRange->GetText(pTextService->threadMgrEventSinkCookie, 0, nullptr, 0, &cch)))
                {
                    DEBUG_LOG(L"DoEditSession::GetFocusText, pRange->GetText failed: %08X", hr);
                    return hr;
                }
                text.resize(cch);
                if (FAILED(hr = pRange->GetText(
                    pTextService->threadMgrEventSinkCookie, 0, text.data(), cch + 1, &cch)))
                {
                    DEBUG_LOG(L"DoEditSession::GetFocusText, pRange->GetText failed: %08X", hr);
                    return hr;
                }
            }

            const auto send = new ClientMessage();
            const auto focus = send->mutable_focus_text();
            focus->set_text(text.empty() ? "" : WideToUtf8(text));
            pTextService->rpc->Send(send);
            return S_OK;
        }
        case ServerMessage::kSetFocusText:
        {
            CComPtr<ITfRange> pRange;
            if (FAILED(hr = pContext->GetEnd(ec, &pRange)))
            {
                DEBUG_LOG(L"DoEditSession::SetFocusText, GetEnd failed: %08X", hr);
                return hr;
            }
            if (FAILED(hr = pRange->SetGravity(ec, TF_GRAVITY_FORWARD, TF_GRAVITY_FORWARD)))
            {
                DEBUG_LOG(L"DoEditSession::SetFocusText, SetGravity failed: %08X", hr);
                return hr;
            }
            if (!msg->set_focus_text().append())
            {
                // we need to get a range of the whole text and clear it
                CComPtr<ITfRange> pStart;
                if (FAILED(hr = pContext->GetStart(ec, &pStart)))
                {
                    DEBUG_LOG(L"DoEditSession::SetFocusText, GetStart failed: %08X", hr);
                    return hr;
                }
                if (FAILED(hr = pRange->ShiftStartToRange(ec, pStart, TF_ANCHOR_START)))
                {
                    DEBUG_LOG(L"DoEditSession::SetFocusText, ShiftStartToRange failed: %08X", hr);
                    return hr;
                }
                if (FAILED(hr = pRange->SetText(ec, 0, L"", 0))) // clear the range
                {
                    DEBUG_LOG(L"DoEditSession::SetFocusText, SetText failed: %08X", hr);
                    return hr;
                }
            }

            const auto text = Utf8ToWide(msg->set_focus_text().text());
            if (FAILED(hr = pRange->SetText(ec, 0, text.c_str(), static_cast<ULONG>(text.size()))))
            {
                DEBUG_LOG(L"DoEditSession::SetFocusText, SetText failed: %08X", hr);
                return hr;
            }
            return S_OK;
        }
    }

    return S_OK;
}

HRESULT TextService::InitRpc()
{
    DEBUG_LOG(L"TextService::InitRpc");

    CComPtr<IGlobalInterfaceTable> pGit;
    HRESULT hr;
    if (FAILED(hr = CoCreateInstance(
        CLSID_StdGlobalInterfaceTable,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&pGit)
    )))
    {
        DEBUG_LOG(L"TextService::InitRpc, CoCreateInstance failed: %08X", hr);
        return hr;
    }

    if (FAILED(pGit->RegisterInterfaceInGlobal(
        static_cast<IServerMessageSink *>(this),
        IID_IServerMessageSink,
        &gitCookie)))
    {
        DEBUG_LOG(L"TextService::InitRpc, RegisterInterfaceInGlobal failed: %08X", hr);
        return hr;
    }

    rpc = new Rpc();
    rpc->Subscribe(gitCookie);

    return hr;
}

HRESULT TextService::InitThreadMgrEventSink()
{
    DEBUG_LOG(L"TextService::InitThreadMgrEventSink");

    CComPtr<ITfSource> pSource;
    HRESULT hr;
    if (FAILED(hr = pThreadMgr->QueryInterface(IID_ITfSource, reinterpret_cast<void **>(&pSource))))
    {
        DEBUG_LOG(L"TextService::InitThreadMgrEventSink, QueryInterface failed: %08X", hr);
        return hr;
    }
    if (FAILED(hr = pSource->AdviseSink(
        IID_ITfThreadMgrEventSink, static_cast<ITfThreadMgrEventSink *>(this),
        &threadMgrEventSinkCookie)))
    {
        DEBUG_LOG(L"TextService::InitThreadMgrEventSink, AdviseSink failed: %08X", hr);
        threadMgrEventSinkCookie = TF_INVALID_COOKIE;
    }

    return hr;
}

HRESULT TextService::InitTextEditSink(ITfDocumentMgr *pDocMgr)
{
    DEBUG_LOG(L"TextService::InitTextEditSink");

    static CComPtr<ITfContext> pTextEditSinkContext;
    CComPtr<ITfSource> pSource;
    HRESULT hr = S_OK;

    // clear out any previous sink first
    if (pTextEditSinkContext)
    {
        if (SUCCEEDED(hr = pTextEditSinkContext->QueryInterface(IID_ITfSource, reinterpret_cast<void **>(&pSource))))
        {
            hr = pSource->UnadviseSink(textEditSinkCookie);
            pSource = nullptr;
        }

        pTextEditSinkContext = nullptr;
        textEditSinkCookie = TF_INVALID_COOKIE;
    }

    if (!pDocMgr)
    {
        return hr; // caller just wanted to clear the previous sink
    }

    if (FAILED(hr = pDocMgr->GetTop(&pTextEditSinkContext)))
    {
        return hr;
    }

    if (pTextEditSinkContext == nullptr)
    {
        return hr; // empty document, no sink possible
    }

    if (SUCCEEDED(hr = pTextEditSinkContext->QueryInterface(IID_ITfSource, reinterpret_cast<void **>(&pSource))))
    {
        if (FAILED(
            hr = pSource->AdviseSink(IID_ITfTextEditSink, static_cast<ITfTextEditSink *>(this), &textEditSinkCookie)))
        {
            textEditSinkCookie = TF_INVALID_COOKIE;
        }
    }

    if (hr != S_OK)
    {
        pTextEditSinkContext = nullptr;
    }

    return hr;
}
