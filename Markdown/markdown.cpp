#pragma once
#include <objbase.h>

#define MAKEDLL TRUE
#include "markdown.h"

#ifdef _WIN64
#pragma comment(linker, "/EXPORT:MarkdownCreateWpfView")
#pragma comment(linker, "/EXPORT:MarkdownLoadWpfView")
#pragma comment(linker, "/EXPORT:MarkdownDestroyWpfView")
#pragma comment(linker, "/EXPORT:MarkdownFocusWpfView")
#pragma comment(linker, "/EXPORT:MarkdownCommandWpfView")
#pragma comment(linker, "/EXPORT:MarkdownSearchWpfView")
#else
#pragma comment(linker, "/EXPORT:MarkdownCreateWpfView=_MarkdownCreateWpfView@20")
#pragma comment(linker, "/EXPORT:MarkdownLoadWpfView=_MarkdownLoadWpfView@16")
#pragma comment(linker, "/EXPORT:MarkdownDestroyWpfView=_MarkdownDestroyWpfView@4")
#pragma comment(linker, "/EXPORT:MarkdownFocusWpfView=_MarkdownFocusWpfView@4")
#pragma comment(linker, "/EXPORT:MarkdownCommandWpfView=_MarkdownCommandWpfView@8")
#pragma comment(linker, "/EXPORT:MarkdownSearchWpfView=_MarkdownSearchWpfView@12")
#endif

using namespace System;
using namespace System::IO;
using namespace System::Reflection;
using namespace System::Runtime::InteropServices;

private ref class WpfBridgeState abstract sealed
{
public:
	static Type^ HostType = nullptr;
	static String^ RuntimeDirectory = nullptr;

	static Assembly^ ResolveAssembly(Object^, ResolveEventArgs^ arguments)
	{
		String^ simpleName = (gcnew AssemblyName(arguments->Name))->Name;
		String^ path = Path::Combine(RuntimeDirectory, simpleName + ".dll");
		return File::Exists(path) ? Assembly::LoadFrom(path) : nullptr;
	}
};

static Type^ GetWpfHostType()
{
	if(WpfBridgeState::HostType == nullptr)
	{
		String^ bridgeDirectory = Path::GetDirectoryName(Assembly::GetExecutingAssembly()->Location);
		WpfBridgeState::RuntimeDirectory = bridgeDirectory;
		AppDomain::CurrentDomain->AssemblyResolve +=
			gcnew ResolveEventHandler(&WpfBridgeState::ResolveAssembly);
		String^ viewerPath = Path::Combine(bridgeDirectory, "Markdown.Wpf.dll");
		Assembly^ viewerAssembly = Assembly::LoadFrom(viewerPath);
		WpfBridgeState::HostType = viewerAssembly->GetType("MarkdownView.Wpf.WpfViewerHost", true);
	}
	return WpfBridgeState::HostType;
}

static Object^ InvokeWpfHost(String^ method, array<Object^>^ arguments)
{
	return GetWpfHostType()->InvokeMember(method,
		BindingFlags::Public | BindingFlags::Static | BindingFlags::InvokeMethod,
		nullptr, nullptr, arguments);
}

static HRESULT ExceptionToHResult(Exception^ exception)
{
	while(exception->InnerException != nullptr && dynamic_cast<TargetInvocationException^>(exception) != nullptr)
		exception = exception->InnerException;
	return Marshal::GetHRForException(exception);
}

extern "C" HRESULT __stdcall MarkdownCreateWpfView(
	HWND parentWindow,
	const wchar_t* filename,
	const wchar_t* extensions,
	BOOL darkMode,
	HWND* viewWindow)
{
	if(!parentWindow || !filename || !extensions || !viewWindow)
		return E_INVALIDARG;
	*viewWindow = NULL;
	try
	{
		IntPtr handle = safe_cast<IntPtr>(InvokeWpfHost("Create", gcnew array<Object^> {
			IntPtr(parentWindow), gcnew String(filename), gcnew String(extensions), darkMode != FALSE
		}));
		*viewWindow = static_cast<HWND>(handle.ToPointer());
		return *viewWindow ? S_OK : E_FAIL;
	}
	catch(Exception^ exception)
	{
		return ExceptionToHResult(exception);
	}
}

extern "C" HRESULT __stdcall MarkdownLoadWpfView(
	HWND viewWindow,
	const wchar_t* filename,
	const wchar_t* extensions,
	BOOL darkMode)
{
	if(!viewWindow || !filename || !extensions)
		return E_INVALIDARG;
	try
	{
		bool loaded = safe_cast<bool>(InvokeWpfHost("Load", gcnew array<Object^> {
			IntPtr(viewWindow), gcnew String(filename), gcnew String(extensions), darkMode != FALSE
		}));
		return loaded ? S_OK : E_FAIL;
	}
	catch(Exception^ exception)
	{
		return ExceptionToHResult(exception);
	}
}

extern "C" void __stdcall MarkdownDestroyWpfView(HWND viewWindow)
{
	if(!viewWindow)
		return;
	try
	{
		InvokeWpfHost("Destroy", gcnew array<Object^> { IntPtr(viewWindow) });
	}
	catch(Exception^)
	{
	}
}

extern "C" void __stdcall MarkdownFocusWpfView(HWND viewWindow)
{
	if(!viewWindow)
		return;
	try
	{
		InvokeWpfHost("Focus", gcnew array<Object^> { IntPtr(viewWindow) });
	}
	catch(Exception^)
	{
	}
}

extern "C" void __stdcall MarkdownCommandWpfView(HWND viewWindow, int command)
{
	if(!viewWindow)
		return;
	try
	{
		if(command == 1)
			InvokeWpfHost("SelectAll", gcnew array<Object^> { IntPtr(viewWindow) });
		else if(command == 2)
			InvokeWpfHost("Copy", gcnew array<Object^> { IntPtr(viewWindow) });
		else if(command == 3)
			InvokeWpfHost("Zoom", gcnew array<Object^> { IntPtr(viewWindow), 10 });
		else if(command == 4)
			InvokeWpfHost("Zoom", gcnew array<Object^> { IntPtr(viewWindow), -10 });
		else if(command == 5)
			InvokeWpfHost("Zoom", gcnew array<Object^> { IntPtr(viewWindow), safe_cast<Object^>(Int32(0)) });
	}
	catch(Exception^)
	{
	}
}

extern "C" HRESULT __stdcall MarkdownSearchWpfView(
	HWND viewWindow,
	const wchar_t* searchText,
	int searchFlags)
{
	if(!viewWindow || !searchText)
		return E_INVALIDARG;
	try
	{
		return safe_cast<bool>(InvokeWpfHost("Find", gcnew array<Object^> {
			IntPtr(viewWindow), gcnew String(searchText), searchFlags
		})) ? S_OK : S_FALSE;
	}
	catch(Exception^ exception)
	{
		return ExceptionToHResult(exception);
	}
}
