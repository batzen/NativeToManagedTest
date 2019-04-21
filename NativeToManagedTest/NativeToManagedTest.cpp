// NativeToManagedTest.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include "pch.h"

#include <iostream>
#include <filesystem>

#include "Windows.h"
#include <string>
#include <sstream>
#include "psapi.h"

#ifdef _UNICODE
#define COUT std::wcout
#else
#define COUT std::cout
#endif

//typedef LPTSTR(__stdcall *HelloFunc)(LPTSTR name);
typedef void(__stdcall *ManagedMethod)();

//Returns the last Win32 error, in string format. Returns an empty string if there is no error.
std::string GetLastErrorAsString()
{
	//Get the error message, if any.
	DWORD errorMessageID = ::GetLastError();
	if (errorMessageID == 0)
		return std::string(); //No error message has been recorded

	LPSTR messageBuffer = nullptr;
	size_t size = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
		NULL, errorMessageID, MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT), (LPSTR)&messageBuffer, 0, NULL);

	std::string message(messageBuffer, size);

	//Free the buffer.
	LocalFree(messageBuffer);

	return message;
}

int CallManagedMethod(LPCWSTR assembly)
{
	const auto methodName = "ManagedMethod";

    COUT << "#########################################" << std::endl;
	COUT << "Loading library " << assembly << std::endl;

	if (std::experimental::filesystem::exists(assembly) == false)
	{
		COUT << "Could not find library " << assembly << std::endl;
		COUT << "#########################################" << std::endl;

		return 1;
	}

	const auto libraryHandle = LoadLibrary(assembly);
	
	if (!libraryHandle)
	{
		COUT << GetLastErrorAsString().c_str() << std::endl;
		COUT << "#########################################" << std::endl;

		return GetLastError();
	}

	const auto managedMethod = reinterpret_cast<ManagedMethod>(GetProcAddress(libraryHandle, methodName));
	if (!managedMethod)
	{
		COUT << GetLastErrorAsString().c_str() << std::endl;
		COUT << "#########################################" << std::endl;

		return GetLastError();
	}

	try
	{
		COUT << "Calling method " << methodName << std::endl;

		managedMethod();
	}
	catch (std::exception &exception)
	{
		COUT << "Calling method failed" << std::endl;
		COUT << exception.what() << std::endl;
	}
	catch(...)
	{
	}

	FreeLibrary(libraryHandle);

	COUT << "#########################################" << std::endl;

	return 0;
}

int CallMessageHookProc(LPCWSTR assembly)
{
	const auto methodName = "MessageHookProc";

	COUT << "#########################################" << std::endl;
	COUT << "Loading library " << assembly << std::endl;

	if (std::experimental::filesystem::exists(assembly) == false)
	{
		COUT << "Could not find library " << assembly << std::endl;
		COUT << "#########################################" << std::endl;

		return 1;
	}

	const auto libraryHandle = LoadLibrary(assembly);

	if (!libraryHandle)
	{
		COUT << GetLastErrorAsString().c_str() << std::endl;
		COUT << "#########################################" << std::endl;

		return GetLastError();
	}

	const auto managedMethod = reinterpret_cast<HOOKPROC>(GetProcAddress(libraryHandle, methodName));
	if (!managedMethod)
	{
		COUT << GetLastErrorAsString().c_str() << std::endl;
		COUT << "#########################################" << std::endl;

		return GetLastError();
	}

	try
	{
		COUT << "Calling method " << methodName << std::endl;

		managedMethod(100, 200, 300);
	}
	catch (std::exception &exception)
	{
		COUT << "Calling method failed" << std::endl;
		COUT << exception.what() << std::endl;
	}
	catch (...)
	{
	}

	FreeLibrary(libraryHandle);

	COUT << "#########################################" << std::endl;

	return 0;
}

static unsigned int WM_GOBABYGO = ::RegisterWindowMessage(L"Injector_GOBABYGO!");

void Launch(HWND windowHandle, LPCWSTR assembly) //, System::String^ assembly, System::String^ className, System::String^ methodName)
{
	//System::String^ assemblyClassAndMethod = assembly + "$" + className + "$" + methodName;
	//pin_ptr<const wchar_t> acmLocal = PtrToStringChars(assemblyClassAndMethod);
	const auto acmLocal = L"SNOOP-DATA";

	HINSTANCE hinstDLL;

	//if (::GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS, (LPCTSTR)&MessageHookProc, &hinstDLL))
	if (!(hinstDLL = LoadLibrary(assembly)))
	{
		COUT << GetLastErrorAsString().c_str() << std::endl;
		return;
	}

	//LogMessage("GetModuleHandleEx successful", true);
	DWORD processID = 0;
	auto threadID = ::GetWindowThreadProcessId(windowHandle, &processID);

	if (processID)
	{
		//LogMessage("Got process id", true);
		auto hProcess = ::OpenProcess(PROCESS_ALL_ACCESS, FALSE, processID);
		if (hProcess)
		{
			//LogMessage("Got process handle", true);
			auto buffLen = (std::wcslen(acmLocal) + 1) * sizeof(wchar_t);
			void* acmRemote = ::VirtualAllocEx(hProcess, nullptr, buffLen, MEM_COMMIT, PAGE_READWRITE);

			if (acmRemote)
			{
				//LogMessage("VirtualAllocEx successful", true);
				::WriteProcessMemory(hProcess, acmRemote, acmLocal, buffLen, nullptr);

				const auto messageHookProc = reinterpret_cast<HOOKPROC>(GetProcAddress(hinstDLL, "MessageHookProc"));
				if (!messageHookProc)
				{
					COUT << GetLastErrorAsString().c_str() << std::endl;
					return;
					//return GetLastError();
				}

				const auto _messageHookHandle = ::SetWindowsHookEx(WH_CALLWNDPROC, messageHookProc, hinstDLL, threadID);

				if (_messageHookHandle)
				{
					//LogMessage("SetWindowsHookEx successful", true);
					COUT << "Sending message " << WM_GOBABYGO << std::endl;
					::SendMessage(windowHandle, WM_GOBABYGO, reinterpret_cast<WPARAM>(acmRemote), 0);
					::UnhookWindowsHookEx(_messageHookHandle);
				}

				::VirtualFreeEx(hProcess, acmRemote, 0, MEM_RELEASE);
			}

			::CloseHandle(hProcess);
		}
	}
	::FreeLibrary(hinstDLL);
}

std::wstring GetBitnessOfProcess(HWND windowHandle)
{
	DWORD processID = 0;
	auto threadID = ::GetWindowThreadProcessId(windowHandle, &processID);

	if (processID)
	{
		auto hProcess = ::OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processID);
		if (hProcess)
		{
			BOOL isWOW64Process;
			if (::IsWow64Process(hProcess, &isWOW64Process))
			{
				return isWOW64Process ? L"x86" : L"x64";
			}
		}
	}

	return L"";
}

bool IsDotNetCoreProcess(DWORD processID)
{
	HMODULE hMods[1024];
	HANDLE hProcess;
	DWORD cbNeeded;
	unsigned int i;

	// Print the process identifier.

	printf("\nProcess ID: %u\n", processID);

	// Get a handle to the process.

	hProcess = OpenProcess(PROCESS_QUERY_INFORMATION |
		PROCESS_VM_READ,
		FALSE, processID);
	if (NULL == hProcess)
		return false;

	bool isDotNetCoreProcess = false;

	// Get a list of all the modules in this process.

	if (::EnumProcessModules(hProcess, hMods, sizeof(hMods), &cbNeeded))
	{
		for (i = 0; i < (cbNeeded / sizeof(HMODULE)); i++)
		{
			TCHAR szModName[MAX_PATH];

			// Get the full path to the module's file.

			if (::GetModuleFileNameEx(hProcess, hMods[i], szModName,
				sizeof(szModName) / sizeof(TCHAR)))
			{
				std::wstring str(szModName);
				if (str.rfind(L"wpfgfx_cor3") != std::wstring::npos)
				{
					isDotNetCoreProcess = true;
					break;
				}

				//// Print the module name and handle value.

				//OutputDebugString(szModName);
				//OutputDebugString(L"\r\n");
			}
		}
	}

	// Release the handle to the process.

	CloseHandle(hProcess);

	return isDotNetCoreProcess;
}

int main()
{
	COUT << "Hello from Native!" << std::endl;

    /*
 After corflags:
.vtfixup [1] int32 fromunmanaged at VT_01
.data VT_01 = int32(0)

.vtfixup [1] int64 fromunmanaged at VT_01
.data VT_01 = int64(0)

     In Method:
.vtentry 1 : 1
.export [1] as ManagedMethod

.vtentry 1 : 1
.export [1] as MessageHookProc
     */

	//if (sizeof(size_t) == 8)
	//{
	//	COUT << "64 Bit" << std::endl;

 //       CallManagedMethod(L"ManagedWithDllExport.net462.x64.dll");

	//	COUT << std::endl;

 //       CallManagedMethod(L"ManagedWithDllExport.netcoreapp3.0.x64.dll");
	//}
	//else
	//{
	//	COUT << "32 Bit" << std::endl;

 //       CallManagedMethod(L"ManagedWithDllExport.net462.x86.dll");

	//	COUT << std::endl;

 //       CallManagedMethod(L"ManagedWithDllExport.netcoreapp3.0.x86.dll");
	//}

	//CallMessageHookProc(L"ManagedFullExported.dll");
	//CallMessageHookProc(L"ManagedCoreExported.dll");

	COUT << "Trying to find ControlzEx.Showcase..." << std::endl;

	const auto hwnd = FindWindow(nullptr, L"WPFTestApp");

	COUT << "Found hwnd = " << hwnd << std::endl;

	if (hwnd)
	{
		DWORD processID = 0;
		auto threadID = ::GetWindowThreadProcessId(hwnd, &processID);

		const auto framework = IsDotNetCoreProcess(processID) ? L"netcoreapp3.0" : L"net462";

		const auto bitness = GetBitnessOfProcess(hwnd);

		std::wstringstream stringStream;
		stringStream << "ManagedWithDllExport." << framework << "." << bitness << ".dll";
		std::wstring assemblyName = stringStream.str();

		Launch(hwnd, assemblyName.c_str());

		//Launch(hwnd, L"ManagedWithDllExport.net462.x64.dll");
		//Launch(hwnd, L"ManagedWithDllExport.net462.x86.dll");
		//Launch(hwnd,  L"ManagedWithDllExport.netcoreapp3.0.x64.dll");
		//Launch(hwnd, L"ManagedWithDllExport.netcoreapp3.0.x86.dll");
	}
}