// Define the primitives/builtins.
typedef char s8;
typedef unsigned char u8;
typedef short s16;
typedef unsigned short u16;
typedef int s32;
typedef unsigned int u32;
typedef __int64 s64;
typedef unsigned __int64 u64;

typedef __String PSTR;
typedef unsigned __String PWSTR;

typedef u8 BYTE;
typedef u16 WORD;
typedef u32 DWORD;
typedef u64 QWORD;
typedef u8 BOOLEAN;

typedef u32 NTSTATUS;
typedef s32 HRESULT;

typedef void * PVOID;
typedef u32 IntPtr;

typedef PVOID __attribute(__open) ArrOfConst[]; // array of const

enum {
	false = 0,
	true = 1
};

enum {
	MB_OK = 0,
	mbInformation = 0
};

enum {
	STATUS_UNSUCCESSFUL = 0xC0000001
};

// Define imported external functions.
// Displays a message box.
static s32 __attribute(__internal) MsgBox(PWSTR Text, s32 Type, s32 Buttons);
// Ends the calling process and all its threads.
static void __attribute(__dll("kernelbase.dll", "ExitProcess")) __attribute(__stdcall) ExitProcess(u32 uExitCode);

BOOLEAN InitializeUninstall() {
	MsgBox(L"Hello IFPS world! Let's call ExitProcess()!", MB_OK, mbInformation);
	ExitProcess(STATUS_UNSUCCESSFUL);
	return false;
}