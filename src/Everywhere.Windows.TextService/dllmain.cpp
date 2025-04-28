#include "pch.h"
#include "TextServiceFactory.h"

BOOL APIENTRY DllMain(const HMODULE hModule, const DWORD ulReasonForCall, LPVOID lpReserved)
{
    switch (ulReasonForCall)
    {
        case DLL_PROCESS_ATTACH:
        {
            DEBUG_LOG(L"DLL_PROCESS_ATTACH, hModule: %p", hModule);
            globalDllHandle = hModule;

            if (!InitializeCriticalSectionAndSpinCount(&globalCriticalSection, 0))
            {
                DEBUG_LOG(L"InitializeCriticalSectionAndSpinCount failed, error: %d\n", GetLastError());
                return FALSE;
            }

            textServiceFactory = new TextServiceFactory();
            break;
        }
        case DLL_THREAD_ATTACH:
        case DLL_THREAD_DETACH:
        case DLL_PROCESS_DETACH:
        default:
            break;
    }

    return TRUE;
}
