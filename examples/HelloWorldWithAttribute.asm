
; Pointer isn't used directly by this script.
; However, pushvar pushes a Pointer to the stack, and the runtime requires it to be present.
; If it's not present, trying to call any function in the script will cause null deref.
.type primitive(Pointer) Pointer

; Declare types used by this script.
.type primitive(S32) S32
.type primitive(String) AsciiString
.type primitive(UnicodeString) UnicodeString
.type(export) primitive(U8) TMsgBoxType ; used internally by MsgBox
.type(export) primitive(U8) Boolean

; Declare an imported external function.
; function MsgBox(const Text: String; const Typ: TMsgBoxType; const Buttons: Integer): Integer;
.function(import) external internal returnsval MsgBox(__val __unknown,__val __unknown,__val __unknown)

; Declare the following function to have an Event attribute.
; See https://jrsoftware.org/ishelp/index.php?topic=scriptevents&anchor=eventattributes
[Event(AsciiString("InitializeUninstall"))] ; Called on uninstall start.
; Declare the script function. Does not need to be exported as it has an Event attribute.
.function Boolean init()
	pushtype S32 ; Declares a variable to store MsgBox's return value
	.alias MsgBoxRet Var1 ; Give that variable a name
	push S32(0) ; MB_OK ; Buttons argument for MsgBox()
	push TMsgBoxType(0) ; mbInformation ; Typ argument for MsgBox()
	push UnicodeString("Hello IFPS world!") ; Text argument for MsgBox()
	pushvar MsgBoxRet ; Pointer to return value
	call MsgBox ; Call MsgBox()
	; Callee should clear stack after a function call.
	; However, "ret" instruction will clear the stack for us.
	assign RetVal, Boolean(0) ; Prepare to return 0
	ret ; Return to caller.
