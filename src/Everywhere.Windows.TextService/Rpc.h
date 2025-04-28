#pragma once
#include "pch.h"
#include <thread>
#include <mutex>
#include <condition_variable>
#include <queue>
#include <vector>
#include <functional>
#include <atomic>

#include "TextService.pb.h"
using namespace text_service;

class Rpc
{
public:
    explicit Rpc();
    ~Rpc();

    void Send(ClientMessage* msg);
    void Subscribe(DWORD dwCookie);

private:
    void SendLoop();
    void RecvLoop();
    bool TryConnect();
    BOOL Write(const ClientMessage *msg) const;
    ServerMessage* Read() const;
    void HandlePipeError();

    std::thread                 sendThread;
    std::thread                 recvThread;
    std::mutex                  mutex;
    std::mutex                  pipeMutex;
    std::condition_variable     cv;
    std::queue<ClientMessage*>  sendQueue;
    std::vector<DWORD>          cookies;
    std::atomic<bool>           running;
    HANDLE                      hPipe = INVALID_HANDLE_VALUE;
};
