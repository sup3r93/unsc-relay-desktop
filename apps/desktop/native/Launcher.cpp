#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <string>
#include <vector>

// Native GUI entry point: the complete .NET application lives in Librerie.
// No shell, interpreter, elevated process, or console window is involved.
int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR arguments, int)
{
    std::vector<wchar_t> buffer(32768);
    DWORD size = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (size == 0 || size >= buffer.size()) return 1;
    std::wstring directory(buffer.data(), size);
    directory.resize(directory.find_last_of(L"\\/"));
    const std::wstring application = directory + L"\\Librerie\\UNSC Relay.exe";
    if (GetFileAttributesW(application.c_str()) == INVALID_FILE_ATTRIBUTES)
    {
        MessageBoxW(nullptr, L"I file di UNSC Relay non sono disponibili nella cartella Librerie. Reinstalla il programma con l'installer firmato.", L"UNSC Relay", MB_OK | MB_ICONERROR);
        return 1;
    }
    std::wstring command = L"\"" + application + L"\"";
    if (arguments && *arguments) { command += L" "; command += arguments; }
    STARTUPINFOW startup{}; startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(application.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, directory.c_str(), &startup, &process))
    {
        MessageBoxW(nullptr, L"Impossibile avviare UNSC Relay. Verifica i file installati nella cartella Librerie.", L"UNSC Relay", MB_OK | MB_ICONERROR);
        return 1;
    }
    CloseHandle(process.hThread); CloseHandle(process.hProcess);
    return 0;
}
