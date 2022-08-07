
; Pointer isn't used directly by this script.
; However, pushvar pushes a Pointer to the stack, and the runtime requires it to be present.
; If it's not present, trying to call any function in the script will cause null deref.
.type primitive(Pointer) Pointer

; Declare types used by this script.
.type primitive(S32) S32
.type primitive(U32) U32
.type primitive(UnicodeString) UnicodeString
.type(export) primitive(U8) TMsgBoxType ; used internally by MsgBox
.type(export) primitive(U8) Boolean

; Declare an imported external function.
; function MsgBox(const Text: String; const Typ: TMsgBoxType; const Buttons: Integer): Integer;
.function(import) external internal returnsval MsgBox(__val __unknown,__val __unknown,__val __unknown)

; Import from a DLL.
.function(import) external dll("kernelbase.dll", "ExitProcess") __stdcall void ExitProcess(__val __unknown)

; Define the ntstatus code.
.define U32(0xC0000001) STATUS_UNSUCCESSFUL

; Declare the exported script function.
.function(export) Boolean InitializeUninstall()
	pushtype S32 ; Declares a variable to store MsgBox's return value
	.alias MsgBoxRet Var1 ; Give that variable a name
	push S32(0) ; MB_OK ; Buttons argument for MsgBox()
	push TMsgBoxType(0) ; mbInformation ; Typ argument for MsgBox()
	push UnicodeString("Hello IFPS world! Let's call ExitProcess()!") ; Text argument for MsgBox()
	pushvar MsgBoxRet ; Push pointer to return value
	call MsgBox ; Call MsgBox()
	; Callee should clear stack after a function call.
        ; We are however about to call ExitProcess which will not return.
        push STATUS_UNSUCCESSFUL ; Make you cry
	call ExitProcess ; Say goodbye
	; PascalScript runtime interprets VM bytecode. Execution will not reach here!
