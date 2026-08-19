#include <windows.h>
#include <objbase.h>
#include <algorithm>
#include <cwctype>
#include <string>
#include <vector>

#include "listerplugin.h"
#include "Markdown/markdown.h"

namespace
{
constexpr wchar_t WindowClassName[] = L"MarkdownViewWpfHostWindow";
constexpr wchar_t StateProperty[] = L"MarkdownView.WpfState";
constexpr wchar_t DefaultMarkdownExtensions[] =
    L"md;markdown;mdown;mdtext;mdtxt;mdwn;mk;mkd;mkdn;mkdown";
constexpr wchar_t DefaultRendererExtensions[] =
    L"common+advanced+emojis+mathematics+tasklists";

HINSTANCE instance = nullptr;
HMODULE markdown_module = nullptr;
INIT_ONCE configuration_once = INIT_ONCE_STATIC_INIT;
INIT_ONCE markdown_runtime_once = INIT_ONCE_STATIC_INIT;
DWORD markdown_load_error = ERROR_SUCCESS;
std::wstring markdown_extensions;
std::wstring renderer_extensions;

MarkdownCreateWpfViewProc markdown_create_wpf_view = nullptr;
MarkdownLoadWpfViewProc markdown_load_wpf_view = nullptr;
MarkdownDestroyWpfViewProc markdown_destroy_wpf_view = nullptr;
MarkdownFocusWpfViewProc markdown_focus_wpf_view = nullptr;
MarkdownCommandWpfViewProc markdown_command_wpf_view = nullptr;
MarkdownSearchWpfViewProc markdown_search_wpf_view = nullptr;

struct MarkdownWpfState
{
    HWND view_window = nullptr;
    HWND lister_parent = nullptr;
    std::wstring filename;
    int show_flags = 0;
    bool ole_initialized = false;
};

std::wstring GetModulePath()
{
    std::vector<wchar_t> buffer(MAX_PATH);
    for(;;)
    {
        DWORD length = GetModuleFileNameW(instance, buffer.data(), static_cast<DWORD>(buffer.size()));
        if(length == 0)
            return std::wstring();
        if(length < buffer.size() - 1)
            return std::wstring(buffer.data(), length);
        buffer.resize(buffer.size() * 2);
    }
}

std::wstring GetIniPath()
{
    std::wstring path = GetModulePath();
    size_t separator = path.find_last_of(L"\\/");
    size_t dot = path.find_last_of(L'.');
    if(dot == std::wstring::npos || separator != std::wstring::npos && dot < separator)
        path += L".ini";
    else
        path.replace(dot, std::wstring::npos, L".ini");
    return path;
}

std::wstring ReadIniString(const wchar_t* section, const wchar_t* key, const wchar_t* fallback)
{
    std::vector<wchar_t> buffer(4096);
    GetPrivateProfileStringW(section, key, fallback, buffer.data(),
        static_cast<DWORD>(buffer.size()), GetIniPath().c_str());
    return std::wstring(buffer.data());
}

BOOL CALLBACK InitConfiguration(PINIT_ONCE, PVOID, PVOID*)
{
    markdown_extensions = ReadIniString(
        L"Extensions", L"MarkdownExtensions", DefaultMarkdownExtensions);
    renderer_extensions = ReadIniString(
        L"Renderer", L"Extensions", DefaultRendererExtensions);
    return TRUE;
}

void EnsureConfiguration()
{
    InitOnceExecuteOnce(&configuration_once, InitConfiguration, nullptr, nullptr);
}

std::wstring Lower(std::wstring value)
{
    std::transform(value.begin(), value.end(), value.begin(),
        [](wchar_t character) { return static_cast<wchar_t>(towlower(character)); });
    return value;
}

bool ExtensionListContains(const std::wstring& list, const std::wstring& extension)
{
    const std::wstring normalized = Lower(extension);
    size_t position = 0;
    while(position < list.size())
    {
        position = list.find_first_not_of(L"; ,\t\r\n", position);
        if(position == std::wstring::npos)
            break;
        size_t end = list.find_first_of(L"; ,\t\r\n", position);
        std::wstring item = list.substr(position,
            end == std::wstring::npos ? std::wstring::npos : end - position);
        if(Lower(item) == normalized)
            return true;
        if(end == std::wstring::npos)
            break;
        position = end + 1;
    }
    return false;
}

bool IsMarkdownFile(const wchar_t* filename)
{
    if(!filename || !*filename)
        return false;
    DWORD attributes = GetFileAttributesW(filename);
    if(attributes != INVALID_FILE_ATTRIBUTES && (attributes & FILE_ATTRIBUTE_DIRECTORY))
        return false;
    const wchar_t* separator = wcsrchr(filename, L'\\');
    const wchar_t* alternate_separator = wcsrchr(filename, L'/');
    if(!separator || alternate_separator && alternate_separator > separator)
        separator = alternate_separator;
    const wchar_t* dot = wcsrchr(filename, L'.');
    if(!dot || separator && dot < separator || !dot[1])
        return false;
    EnsureConfiguration();
    return ExtensionListContains(markdown_extensions, dot + 1);
}

BOOL CALLBACK InitMarkdownRuntime(PINIT_ONCE, PVOID, PVOID*)
{
    std::wstring runtime_path = GetModulePath();
    size_t separator = runtime_path.find_last_of(L"\\/");
    if(separator == std::wstring::npos)
    {
        markdown_load_error = ERROR_PATH_NOT_FOUND;
        return FALSE;
    }
    runtime_path.resize(separator + 1);
#ifdef _WIN64
    runtime_path += L"runtime\\x64\\Markdown-x64.dll";
#else
    runtime_path += L"runtime\\x86\\Markdown-x86.dll";
#endif

    markdown_module = LoadLibraryExW(runtime_path.c_str(), nullptr,
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if(!markdown_module)
    {
        markdown_load_error = GetLastError();
        return FALSE;
    }

    markdown_create_wpf_view = reinterpret_cast<MarkdownCreateWpfViewProc>(
        GetProcAddress(markdown_module, "MarkdownCreateWpfView"));
    markdown_load_wpf_view = reinterpret_cast<MarkdownLoadWpfViewProc>(
        GetProcAddress(markdown_module, "MarkdownLoadWpfView"));
    markdown_destroy_wpf_view = reinterpret_cast<MarkdownDestroyWpfViewProc>(
        GetProcAddress(markdown_module, "MarkdownDestroyWpfView"));
    markdown_focus_wpf_view = reinterpret_cast<MarkdownFocusWpfViewProc>(
        GetProcAddress(markdown_module, "MarkdownFocusWpfView"));
    markdown_command_wpf_view = reinterpret_cast<MarkdownCommandWpfViewProc>(
        GetProcAddress(markdown_module, "MarkdownCommandWpfView"));
    markdown_search_wpf_view = reinterpret_cast<MarkdownSearchWpfViewProc>(
        GetProcAddress(markdown_module, "MarkdownSearchWpfView"));

    if(!markdown_create_wpf_view || !markdown_load_wpf_view || !markdown_destroy_wpf_view ||
        !markdown_focus_wpf_view || !markdown_command_wpf_view || !markdown_search_wpf_view)
    {
        markdown_load_error = ERROR_PROC_NOT_FOUND;
        FreeLibrary(markdown_module);
        markdown_module = nullptr;
        return FALSE;
    }
    markdown_load_error = ERROR_SUCCESS;
    return TRUE;
}

MarkdownWpfState* GetState(HWND window)
{
    return reinterpret_cast<MarkdownWpfState*>(GetPropW(window, StateProperty));
}

void ResizeView(HWND window)
{
    MarkdownWpfState* state = GetState(window);
    if(!state || !state->view_window)
        return;
    RECT client = {};
    GetClientRect(window, &client);
    MoveWindow(state->view_window, 0, 0, client.right, client.bottom, TRUE);
}

void ShowRendererError(HWND owner, HRESULT result)
{
    wchar_t error_code[32] = {};
    swprintf_s(error_code, L"0x%08X", static_cast<unsigned int>(result));
    std::wstring message =
        L"The .NET Framework 4.8 WPF Markdown renderer could not be loaded.\n\n"
        L"Check that all files in runtime\\x86 or runtime\\x64 are present.\n"
        L"Renderer error: ";
    message += error_code;
    MessageBoxW(owner, message.c_str(), L"MarkdownView", MB_OK | MB_ICONERROR);
}

std::wstring AnsiToWide(const char* value)
{
    if(!value)
        return std::wstring();
    int length = MultiByteToWideChar(CP_ACP, 0, value, -1, nullptr, 0);
    if(length <= 0)
        return std::wstring();
    std::vector<wchar_t> buffer(static_cast<size_t>(length));
    if(!MultiByteToWideChar(CP_ACP, 0, value, -1, buffer.data(), length))
        return std::wstring();
    return std::wstring(buffer.data());
}

LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam)
{
    switch(message)
    {
        case WM_DESTROY:
        {
            MarkdownWpfState* state = GetState(window);
            RemovePropW(window, StateProperty);
            if(state)
            {
                if(markdown_destroy_wpf_view && state->view_window)
                    markdown_destroy_wpf_view(state->view_window);
                if(state->ole_initialized)
                    OleUninitialize();
                delete state;
            }
            return 0;
        }
        case WM_SIZE:
            ResizeView(window);
            return 0;
        case WM_SETFOCUS:
        {
            MarkdownWpfState* state = GetState(window);
            if(state && markdown_focus_wpf_view)
                markdown_focus_wpf_view(state->view_window);
            return 0;
        }
        case WM_ERASEBKGND:
            return 1;
    }
    return DefWindowProcW(window, message, wparam, lparam);
}
}

int __stdcall ListLoadNextW(HWND parent_window, HWND plugin_window, WCHAR* filename, int show_flags)
{
    MarkdownWpfState* state = GetState(plugin_window);
    if(!state || !IsMarkdownFile(filename) || !markdown_load_wpf_view)
        return LISTPLUGIN_ERROR;
    HRESULT result = markdown_load_wpf_view(state->view_window, filename,
        renderer_extensions.c_str(), (show_flags & lcp_darkmode) != 0);
    if(FAILED(result))
        return LISTPLUGIN_ERROR;
    state->filename = filename;
    state->lister_parent = parent_window;
    state->show_flags = show_flags;
    return LISTPLUGIN_OK;
}

int __stdcall ListLoadNext(HWND parent_window, HWND plugin_window, char* filename, int show_flags)
{
    std::wstring wide_filename = AnsiToWide(filename);
    return wide_filename.empty()
        ? LISTPLUGIN_ERROR
        : ListLoadNextW(parent_window, plugin_window, &wide_filename[0], show_flags);
}

HWND __stdcall ListLoadW(HWND parent_window, WCHAR* filename, int show_flags)
{
    if(!IsMarkdownFile(filename))
        return nullptr;

    HRESULT ole_result = OleInitialize(nullptr);
    if(FAILED(ole_result))
        return nullptr;

    if(!InitOnceExecuteOnce(&markdown_runtime_once, InitMarkdownRuntime, nullptr, nullptr) ||
        !markdown_create_wpf_view)
    {
        ShowRendererError(parent_window, HRESULT_FROM_WIN32(markdown_load_error));
        OleUninitialize();
        return nullptr;
    }

    RECT client = {};
    GetClientRect(parent_window, &client);
    HWND plugin_window = CreateWindowExW(WS_EX_CONTROLPARENT, WindowClassName, L"MarkdownView",
        WS_VISIBLE | WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS,
        0, 0, client.right, client.bottom, parent_window, nullptr, instance, nullptr);
    if(!plugin_window)
    {
        OleUninitialize();
        return nullptr;
    }

    HWND view_window = nullptr;
    HRESULT result = markdown_create_wpf_view(plugin_window, filename,
        renderer_extensions.c_str(), (show_flags & lcp_darkmode) != 0, &view_window);
    if(FAILED(result) || !view_window)
    {
        ShowRendererError(plugin_window, result);
        DestroyWindow(plugin_window);
        OleUninitialize();
        return nullptr;
    }

    MarkdownWpfState* state = new MarkdownWpfState;
    state->view_window = view_window;
    state->lister_parent = parent_window;
    state->filename = filename;
    state->show_flags = show_flags;
    state->ole_initialized = true;
    SetPropW(plugin_window, StateProperty, state);
    ResizeView(plugin_window);
    return plugin_window;
}

HWND __stdcall ListLoad(HWND parent_window, char* filename, int show_flags)
{
    std::wstring wide_filename = AnsiToWide(filename);
    return wide_filename.empty() ? nullptr : ListLoadW(parent_window, &wide_filename[0], show_flags);
}

int __stdcall ListSendCommand(HWND plugin_window, int command, int)
{
    MarkdownWpfState* state = GetState(plugin_window);
    if(!state || !markdown_command_wpf_view)
        return LISTPLUGIN_ERROR;
    if(command == lc_selectall)
        markdown_command_wpf_view(state->view_window, 1);
    else if(command == lc_copy)
        markdown_command_wpf_view(state->view_window, 2);
    else
        return LISTPLUGIN_ERROR;
    return LISTPLUGIN_OK;
}

int __stdcall ListSearchTextW(HWND plugin_window, WCHAR* search, int search_flags)
{
    MarkdownWpfState* state = GetState(plugin_window);
    if(!state || !search || !markdown_search_wpf_view)
        return LISTPLUGIN_ERROR;
    return markdown_search_wpf_view(state->view_window, search, search_flags) == S_OK
        ? LISTPLUGIN_OK
        : LISTPLUGIN_ERROR;
}

int __stdcall ListSearchText(HWND plugin_window, char* search, int search_flags)
{
    std::wstring wide_search = AnsiToWide(search);
    return wide_search.empty()
        ? LISTPLUGIN_ERROR
        : ListSearchTextW(plugin_window, &wide_search[0], search_flags);
}

void __stdcall ListCloseWindow(HWND plugin_window)
{
    if(plugin_window)
        DestroyWindow(plugin_window);
}

int __stdcall ListPrint(HWND, char*, char*, int, RECT*)
{
    return LISTPLUGIN_ERROR;
}

int __stdcall ListPrintW(HWND, WCHAR*, WCHAR*, int, RECT*)
{
    return LISTPLUGIN_ERROR;
}

BOOL APIENTRY DllMain(HANDLE module, DWORD reason, LPVOID)
{
    if(reason == DLL_PROCESS_ATTACH)
    {
        instance = static_cast<HINSTANCE>(module);
        DisableThreadLibraryCalls(instance);
        WNDCLASSW window_class = {};
        window_class.lpfnWndProc = WindowProc;
        window_class.hInstance = instance;
        window_class.hCursor = LoadCursor(nullptr, IDC_ARROW);
        window_class.lpszClassName = WindowClassName;
        if(!RegisterClassW(&window_class) && GetLastError() != ERROR_CLASS_ALREADY_EXISTS)
            return FALSE;
    }
    return TRUE;
}
