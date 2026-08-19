#pragma once
#include <windows.h>

typedef HRESULT (__stdcall *MarkdownCreateWpfViewProc)(
	HWND parentWindow,
	const wchar_t* filename,
	const wchar_t* extensions,
	BOOL darkMode,
	HWND* viewWindow
);

typedef HRESULT (__stdcall *MarkdownLoadWpfViewProc)(
	HWND viewWindow,
	const wchar_t* filename,
	const wchar_t* extensions,
	BOOL darkMode
);

typedef void (__stdcall *MarkdownDestroyWpfViewProc)(HWND viewWindow);
typedef void (__stdcall *MarkdownFocusWpfViewProc)(HWND viewWindow);
typedef void (__stdcall *MarkdownCommandWpfViewProc)(HWND viewWindow, int command);
typedef HRESULT (__stdcall *MarkdownSearchWpfViewProc)(
	HWND viewWindow,
	const wchar_t* searchText,
	int searchFlags
);

#ifdef MAKEDLL
extern "C" HRESULT __stdcall MarkdownCreateWpfView(
	HWND parentWindow,
	const wchar_t* filename,
	const wchar_t* extensions,
	BOOL darkMode,
	HWND* viewWindow
);
extern "C" HRESULT __stdcall MarkdownLoadWpfView(
	HWND viewWindow,
	const wchar_t* filename,
	const wchar_t* extensions,
	BOOL darkMode
);
extern "C" void __stdcall MarkdownDestroyWpfView(HWND viewWindow);
extern "C" void __stdcall MarkdownFocusWpfView(HWND viewWindow);
extern "C" void __stdcall MarkdownCommandWpfView(HWND viewWindow, int command);
extern "C" HRESULT __stdcall MarkdownSearchWpfView(
	HWND viewWindow,
	const wchar_t* searchText,
	int searchFlags
);
#endif
