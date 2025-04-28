#include "pch.h"
#include <chrono>

constexpr DWORD BufSize = 4096;

using namespace text_service;

Rpc::Rpc() : running(true)
{
    DEBUG_LOG(L"Rpc::Rpc");
    sendThread = std::thread(&Rpc::SendLoop, this);
    recvThread = std::thread(&Rpc::RecvLoop, this);
}

Rpc::~Rpc()
{
    DEBUG_LOG(L"Rpc::~Rpc");
    running = false;
    cv.notify_one();

    // cancel blocking IO and close pipe
    if (hPipe != INVALID_HANDLE_VALUE)
    {
        CancelIoEx(hPipe, nullptr);
        CloseHandle(hPipe);
        hPipe = INVALID_HANDLE_VALUE;
    }

    if (sendThread.joinable())
        sendThread.join();
    if (recvThread.joinable())
        recvThread.join();

    // empty send queue
    std::lock_guard lk(mutex);
    while (!sendQueue.empty())
    {
        delete sendQueue.front();
        sendQueue.pop();
    }
}

void Rpc::Send(ClientMessage *msg)
{
    {
        std::lock_guard lk(mutex);
        sendQueue.push(msg);
    }
    cv.notify_one();
}

void Rpc::Subscribe(const DWORD dwCookie)
{
    std::lock_guard lk(mutex);
    cookies.push_back(dwCookie);
}

void Rpc::SendLoop()
{
    DEBUG_LOG(L"Rpc::SendLoop");
    while (running)
    {
        ClientMessage *msg;

        // wait for new requests
        {
            std::unique_lock lk(mutex);
            cv.wait(lk, [&] { return !sendQueue.empty() || !running; });
            if (!running && sendQueue.empty())
                break;

            DEBUG_LOG(L"Rpc::SendLoop, msg arrived");
            msg = sendQueue.front();
            sendQueue.pop();
        }

        // host may not be available, so we need to check connection every time
        if (!TryConnect())
        {
            DEBUG_LOG(L"Rpc::SendLoop, TryConnect failed, discarding request");
            delete msg; // discard request
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
            continue;
        }

        const auto res = Write(msg);
        delete msg;
        if (!res) HandlePipeError();
    }
}

void Rpc::RecvLoop()
{
    DEBUG_LOG(L"Rpc::RecvLoop");

    HRESULT hr;
    if (FAILED(hr = CoInitializeEx(nullptr, COINIT_MULTITHREADED)))
    {
        DEBUG_LOG(L"Rpc::RecvLoop, CoInitializeEx failed: %08X", hr);
        return;
    }
    CComPtr<IGlobalInterfaceTable> pGit;
    if (FAILED(hr = CoCreateInstance(
        CLSID_StdGlobalInterfaceTable,
        nullptr,
        CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&pGit))))
    {
        DEBUG_LOG(L"Rpc::RecvLoop, CoCreateInstance failed: %08X", hr);
        return;
    }

    while (running)
    {
        if (!TryConnect())
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(3000));
            continue;
        }

        if (const auto msg = Read(); !msg)
        {
            if (!running) break; // if we are shutting down, break the loop
            HandlePipeError();
        }
        else
        {
            std::vector<DWORD> cookiesCopy;
            {
                std::lock_guard lk(mutex);
                cookiesCopy = cookies;
            }
            for (const auto cookie : cookiesCopy)
            {
                CComPtr<IServerMessageSink> pSink;
                if (FAILED(
                    pGit->GetInterfaceFromGlobal(cookie, IID_IServerMessageSink, reinterpret_cast<void **>(&pSink))))
                {
                    DEBUG_LOG(L"Rpc::RecvLoop, GetInterfaceFromGlobal failed: %08X", hr);
                    continue;
                }
                if (FAILED(pSink->OnServerMessage(msg)))
                {
                    DEBUG_LOG(L"Rpc::RecvLoop, OnServerMessage failed: %08X", hr);
                }
            }
            delete msg;
        }
    }
}

bool Rpc::TryConnect()
{
    {
        std::lock_guard lk(pipeMutex);
        if (hPipe != INVALID_HANDLE_VALUE)
            return true;

        DEBUG_LOG(L"Rpc::TryConnect.CreateFile");
        hPipe = CreateFile(
            LR"(\\.\pipe\everywhere_text_service)",
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED,
            nullptr);
    }

    auto ok = hPipe != INVALID_HANDLE_VALUE;
    if (ok)
    {
        DEBUG_LOG(L"Rpc::TryConnect.SetNamedPipeHandleState");
        DWORD dwMode = PIPE_READMODE_MESSAGE;
        ok = SetNamedPipeHandleState(
            hPipe,    // pipe handle
            &dwMode,  // new pipe mode
            nullptr,  // don't set maximum bytes
            nullptr); // don't set maximum time
    }
    else
    {
        DEBUG_LOG(L"Rpc::TryConnect, Error: %d", GetLastError());
    }

    if (ok)
    {
        const auto msg = new ClientMessage();
        const auto initialized = msg->mutable_initialized();
        initialized->set_pid(GetCurrentProcessId());
        const auto res = Write(msg); // send initialization message
        delete msg;
        if (!res) HandlePipeError();
    }

    return ok;
}

BOOL Rpc::Write(const ClientMessage *msg) const
{
    DEBUG_LOG(L"Rpc::Write");
    if (hPipe == INVALID_HANDLE_VALUE) return FALSE;

    std::string data; // 改为局部变量
    msg->SerializeToString(&data);

    OVERLAPPED ov{};
    ov.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    if (!ov.hEvent)
    {
        DEBUG_LOG(L"Rpc::Write, CreateEvent failed: %d", GetLastError());
        return TRUE;
    }

    BOOL result = WriteFile(
        hPipe,
        data.data(),
        static_cast<DWORD>(data.size()),
        nullptr, // 异步操作时设为NULL
        &ov);

    do
    {
        if (const DWORD err = result ? ERROR_SUCCESS : GetLastError(); err == ERROR_IO_PENDING)
        {
            if (const DWORD wr = WaitForSingleObject(ov.hEvent, 5000); wr != WAIT_OBJECT_0)
            {
                DEBUG_LOG(L"Rpc::Write, Wait failed: %d", GetLastError());
                result = FALSE;
                break;
            }

            DWORD cbWritten;
            if (GetOverlappedResult(hPipe, &ov, &cbWritten, FALSE))
            {
                DEBUG_LOG(L"Rpc::Write, Success: %d", cbWritten);
                result = TRUE;
                break;
            }

            DEBUG_LOG(L"Rpc::Write, GetOverlappedResult failed: %d", GetLastError());
            result = FALSE;
            break;
        }
        else if (err == ERROR_SUCCESS)
        {
            DWORD cbWritten;
            if (GetOverlappedResult(hPipe, &ov, &cbWritten, FALSE))
            {
                DEBUG_LOG(L"Rpc::Write, Success (sync): %d", cbWritten);
            }
            else
            {
                DEBUG_LOG(L"Rpc::Write, GetOverlappedResult sync error: %d", GetLastError());
                result = FALSE;
                break;
            }
        }
        else
        {
            DEBUG_LOG(L"Rpc::Write, Error: %d", err);
            result = FALSE;
            break;
        }
    }
    while (false);

    CloseHandle(ov.hEvent);
    return result;
}

ServerMessage *Rpc::Read() const
{
    DEBUG_LOG(L"Rpc::Read");
    if (hPipe == INVALID_HANDLE_VALUE) return nullptr;

    OVERLAPPED ov{};
    ov.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);
    if (!ov.hEvent)
    {
        DEBUG_LOG(L"Rpc::Read, CreateEvent failed: %d", GetLastError());
        return nullptr;
    }

    DWORD cbRead;
    DWORD err;
    std::string data;

    do
    {
        char buffer[BufSize];
        BOOL result = ReadFile(hPipe, buffer, sizeof(buffer), nullptr, &ov);
        err = result ? ERROR_SUCCESS : GetLastError();

        if (err == ERROR_IO_PENDING)
        {
            if (const DWORD wr = WaitForSingleObject(ov.hEvent, INFINITE); wr != WAIT_OBJECT_0)
            {
                DEBUG_LOG(L"Rpc::Read, Wait timeout: %d", GetLastError());
                return nullptr;
            }
            result = TRUE;
        }
        else if (err != ERROR_SUCCESS && err != ERROR_MORE_DATA)
        {
            if (err == ERROR_BROKEN_PIPE)
                DEBUG_LOG(L"Rpc::Read, Pipe closed");
            else
                DEBUG_LOG(L"Rpc::Read, ReadFile error: %d", err);
            return nullptr;
        }

        if (result || err == ERROR_MORE_DATA)
        {
            if (!GetOverlappedResult(hPipe, &ov, &cbRead, FALSE))
            {
                err = GetLastError();
                if (err != ERROR_MORE_DATA)
                {
                    DEBUG_LOG(L"Rpc::Read, Overlapped result error: %d", err);
                    return nullptr;
                }
            }
            data.append(buffer, cbRead);
        }

        ResetEvent(ov.hEvent); // 重置事件供下次使用
    }
    while (err == ERROR_MORE_DATA);

    CloseHandle(ov.hEvent);

    if (data.empty())
    {
        DEBUG_LOG(L"Rpc::Read, No data");
        return nullptr;
    }

    {
        const auto msg = new ServerMessage();
        if (!msg->ParseFromString(data))
        {
            DEBUG_LOG(L"Rpc::Read, Parse failed");
            delete msg;
            return nullptr;
        }

        DEBUG_LOG(L"Rpc::Read, Success");
        return msg;
    }
}

void Rpc::HandlePipeError()
{
    std::lock_guard lk(pipeMutex);
    if (hPipe != INVALID_HANDLE_VALUE)
    {
        CancelIo(hPipe);
        CloseHandle(hPipe);
        hPipe = INVALID_HANDLE_VALUE;
        DEBUG_LOG(L"Rpc: Pipe handle closed due to error");
    }
}
