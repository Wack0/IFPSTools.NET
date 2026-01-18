# IFPSTools.NET
Successor to IFPSTools; for working with RemObject PascalScript compiled bytecode files.

Written in C#, libraries target .NET Standard 2.0 and console applications target .NET Framework.

Contains the following:

## IFPSLib

Library for reading, modifying, creating, writing and disassembling compiled IFPS scripts. Saving a loaded script without modification is expected to output an identical binary, not doing so is considered a bug.

The API is modeled on dnlib's.

A known bug is that the `Extended` primitive in IFPSLib will always refer internally to an 80-bit floating point value; any compiled IFPS script intended for an architecture that is not x86 (32-bit) will use 64-bit floating point values.

## IFPSAsmLib

Library implementing an assembler for IFPS scripts.

Depends on IFPSLib.

Assembling the output of IFPSLib's disassembler is expected to output an identical binary, not doing so is considered a bug.

## ifpsdasm

Wrapper around IFPSLib's disassember functionality.

Usage: `ifpsdasm CompiledCode.bin` will disassemble `CompiledCode.bin` into `CompiledCode.txt`.

## ifpsasm

Wrapper around IFPSAsmLib.

Usage: `ifpsasm file.txt` will assemble `file.txt` into `file.bin`

## LibIFPSCC

Implements an ANSI C compiler targeting PascalScript bytecode. Fork of [phisiart's C compiler](https://github.com/phisiart/C-Compiler).

Further details below.

## ifpscc

Wrapper around LibIFPSCC.

Usage: `ifpscc [-A|--disassemble] [-O=out.bin|--output=out.bin] files...` - where `--disassemble` writes the disassembly of the compiler output to stdout, and if `--output` is not passed, compiled script will be written to `[first C file passed].bin`.

## uninno

The Inno Setup Uninstaller Configuration Creator.

This tool creates an Inno Setup uninstaller `*.dat` file (ie, `unins000.dat`) from a compiled IFPS script.

This allows the usage of the Inno Setup uninstaller as a lolbin; most systems would have several versions already on the system, and signed samples can be found quite easily (even MS signed a few: Skype, VSCode, Azure Storage Explorer...)

See `uninno --help` for usage instructions.

`innounp` does not extract the uninstaller, but for a signed sample, the uninstaller equals the setup core executable; so the uninstaller can be dumped by running an Inno Setup installer under a debugger and setting a breakpoint on `CreateProcessW`, then copying it out of `%TEMP%`.

After dumping an uninstaller, the required version number can be obtained by getting xref to the string `"Install was done in 64-bit mode but not running 64-bit Windows now"`; further up should be `mov ecx, <version constant> ; mov dx, 0x20` or similar.

Bit 31 means Unicode; `0x86000500` means `6.0.5u`; `0x06000500` means `6.0.5`.

## Differences from earlier IFPSTools

Compared to the earlier IFPSTools, IFPSTools.NET implements file saving, modifying, assembling...

The disassembler also includes additional functionality that was not implemented in the earlier IFPSTools, for example COM vtable functions and function/type attributes all disassemble correctly.

## IFPS assembly format

Quoted string literals are similar to C# regular string literals, except `\U` is not allowed, and `\x` specifies exactly one byte (two nibbles).

Declarations:
 - `.version <int32>` - script file version. If not present, will be set to `VERSION_HIGHEST` (`23`) by default.
 - `.entry <funcname>` - specifies the entry point of the script, which must have the declaration `.function(export) void name()`.
 - `.type <declaration> <name>` - declares a named type (which can be exported if `.type(export)` is used), of which the following are allowed:
   - `primitive(<prim>)` - primitive type, of which `prim` can be `U8` (unsigned 8-bit integer), `S8` (signed 8-bit integer) , `U16` (unsigned 16-bit integer), `S16` (signed 16-bit integer), `U32` (unsigned 32-bit integer), `S32` (signed 32-bit integer), `S64` (signed 64-bit integer), `Single` (32-bit float), `Double` (64-bit float), `Currency` (OLE Automation tagCY), `Extended` (80-bit float), `String` (ASCII string), `Pointer`, `PChar` , `Variant` (OLE Automation VARIANT), `Char` (ASCII character), `UnicodeString`, `WideString` (same as `UnicodeString`), or `WideChar` (UTF-16 character).
   - `array(<type>)` - variable length array, where `type` is the name of a previously declared type.
   - `array(<type>,<length>,<startindex>)` - static array, where `type` is the name of a previously declared type; `length` is the number of elements in the array; and `startindex` is the base index of the array.
   - `class(<internalname>)` - an internally-defined class, where `internalname` is the internal name of that class (for example `class(TMainForm)`).
   - `interface("guidstring")` - a COM interface, of a GUID that is specified as a string; example: `interface("00000000-0000-0000-C000-000000000046")` is IUnknown.
   - `funcptr(<declaration>)` - a function pointer, where `declaration` is an external function declaration: `void|returnsval (__in|__out,...)`
   - `record(...)` - a record (ie, structure), where the body is the list of element types which must be previously-defined: for example `record(U8,U8,U16)` would be the equivalent of `struct { U8; U8; U16; }`. Elements have no name and are accessed by their element index.
   - `set(<bitsize>)` - a set (ie, bit array), of `bitsize` bits in length.
- `.global <type> <name>` - declares a global variable, which can be exported if `.global(export)` is used, of a previously declared type and specified name.
- `.function(export) external <externaltype> void|returnsval name(externalargs...)` - declares an external function, which in practise must always be exported. External functions do not store argument types, as such an external argument must be declared in the form `__in|__out|__val|__ref __unknown` (`__ref` is equivalent to `__out` and `__val` is equivalent to `__in`). The following types of external functions are allowed:
    -  `com(<vtableindex>) callingconvention` - a COM vtable function, where `vtableindex` is the vtable index (not offset) of the function to call, for example `com(1) __stdcall` would refer to `IUnknown::AddRef`.
    - `class(<classname>, <funcname>) callingconvention` - an internally implemented class function or property, for example `class(Class, CastToType) __pascal` or `class(TControl, Left, property) __pascal`.
    - `dll("dllname", "procname") callingconvention` - an exported function from a DLL, for example `dll("kernelbase.dll", "ExitProcess") __stdcall`. Additional `delayload` and `alteredsearchpath` arguments are also supported.
    - `internal` - an internally implemented function, for example `MsgBox()` in Inno Setup.
    - `nodecl` - an internally implemented function, that includes no argument declarations.
    - The allowed calling conventions are `__stdcall`, `__cdecl`, `__pascal` and `__fastcall`.
- `.function(export) void|<type> name(...)` - declares a function implemented as PascalScript bytecode, which must be followed by at least one instruction. The specified return type must have been previously declared. Parameters are specified as `__in|__out|__val|__ref type name`, for example `__in TControl Arg1`. (`__ref` is equivalent to `__out` and `__val` is equivalent to `__in`)
- `.alias <name> <varname>` - must be specified as part of a PascalScript bytecode function. Declares an alias of a variable name, valid until the end of the function. Local variables are normally numbered as `Var1` (etc); declaring an alias allows a local variable to be given a name.
- `.define <immediate> <name>` - defines a name to any immediate operand to be used in any instruction. Example: `.define U32(0xC0000001) STATUS_UNSUCCESSFUL`.

Attributes in the form of `[attribute(...)]` are allowed to be specified before functions and types. They only have meaning to the host application; Inno Setup uses exactly one attribute - functions can have an `event(UnicodeString)` attribute, see the documentation. An example of that would be: `[event(UnicodeString("InitializeUninstall"))]`.

Labels are allowed as part of a PascalScript bytecode function, in the form of `label:`. Labels only exist for a single function, and can be used as an operand in branch instructions. A label can not have the name `null`.

Instruction operands have several forms:
- Operands that refer to an instruction do so via a label. For the `starteh` instruction, `null` is allowed to specify an empty operand.
- Operands that refer to a type (like in the `pushtype` instruction) do so via the type name.
- Operands that refer to a function (like the `call` instruction) do so via the function name.
- Operands that refer to a variable only (the obsolete `setstacktype` instruction) do so via the variable name.
- Other operands can refer to a variable (via its name), a constant (via the syntax `type(value)`; function pointers use the function name, strings use quoted literals); or an array or record variable indexed by integer constant (`RecordOrArrayVar[0]`) or by variable name (`RecordOrArrayVar[IndexVar]`).

`import` can be used as a synonym for `export`; in some cases it makes more sense to use one over the other (for example, external functions, types used by internally-implemented functions, and global variables initialised by the host application like `WizardForm` in Inno Setup).

## Examples

The `examples` directory contains a few example scripts, intended to be used with uninno:
- `HelloWorld.asm` is a simple hello world example.
- `HelloWorldWithAttribute.asm` demonstrates an initialisation function using an `Event` attribute.
- `DllImportExample.asm` demonstrates importing a function from a DLL.

## IFPS runtime considerations

It is possible to assemble or save a script which would be considered invalid by the runtime.

Notably, even if it is not used by your script, a compiled script without the `primitive(Pointer)` type included is invalid; the runtime expects it to be present and calling any function in a script without such a type present leads to a null pointer dereference.

Opcodes involving COM variants and COM interfaces can specifically compare the type name against `"IDISPATCH"`.

Passing arrays to imported functions may require the type name of the array to start with `!OPENARRAY`.

## LibIFPSCC details

This compiler specifically targets PascalScript for x86 Windows (for Inno Setup). It has been tested for some time, but please report any bugs you find.

A preprocessor is not implemented (might happen in the future, not sure).

Static functions or typedefs will not be exported unless needed.

No inbuilt runtime types or functions are imported by default, you will need to specify prototypes yourself. For a "generic" function like GetArrayLength/SetArrayLength, do not provide an argument list in the prototype.

Unbounded arrays are allowed, as the runtime supports them.

Initialiser lists are supported in function calls, where the type of the initialised object is the type of that function parameter.

Additional types:
- `__int64` becomes `primitive(S64)`
- `__String` becomes `primitive(String)`
- `unsigned __String` becomes `primitive(UnicodeString)`
- `__variant` becomes `primitive(Variant)`
- `__interface("guid")` specifies a COM interface
- `void*` (and arrays of pointer, pointer to pointer, ...) becomes `primitive(U32)` internally.

Because this is intended for use with `uninno` / etc, `class()` types are not (yet) supported.

Casting to and from pointers are fully supported, thanks to some tricks.
- `__fastcall` calling convention is Delphi style (on x86, first argument in `eax`). This means that calling an exported function from a DLL that performs no operation allows simple casting from reference to integer.
  - `ntdll!RtlDebugPrintTimes` is used for this. On free builds of NT this is a no-op (and checked builds aren't a thing anymore)
- For casting to a reference type, a `memcpy` (actually `ntdll!RtlMoveMemory`) is done to a null pointer.
  - The runtime doesn't support pointer to pointer, we work around this by using `Pointer[1]`. This also works around null derefs in the runtime when doing almost anything to a null pointer.
  - PascalScript objects have a pointer to the type information located before the object value, which pointers are always obtained to.
  - Pointers additionally have an additional pointer afterwards, to the type being referenced: `struct Pointer { TypeHeader hdr; void* pValue; void* pType; }`
  - Therefore, casting to a reference type is effectively done by: `void CreateValidPointer(u32 ptr, u32 pObjForType, ArrayOfPointer* outPtr) { memcpy(&outPtr[0]->pValue, &ptr, sizeof(ptr)); pObjForType -= sizeof(ptr); memcpy(&outPtr[0]->pType, &pObjForType, sizeof(ptr)); }`
  - pObjForType is obtained by creating a new instance of the wanted type on the stack.

Unions are supported by emitting a byte array of sizeof(union) and casting to the correct pointer type when needed.

Types and functions support additional attributes. For example:

- `typedef __variant __attribute(__open) OpenArrayOfVariant[];` - unbounded array of COM variants, which will be marshaled to a C array when passed to an imported function.
- `typedef void * PVOID ; typedef PVOID __attribute(__open) OpenArrayOfConst[];` - unbounded array of any object, which will be marshaled correctly when passed to an imported function (only used for Format() function).
- `typedef __interface("bca8b44d-aad6-3a86-8ab7-03349f4f2da2") __attribute(__dispatch) IClrType;` - COM interface; expected to be an IDispatch when in a variant.
- `static IntPtr __attribute(__stdcall) __attribute(__dll("kernel32.dll", "LoadLibraryW")) LoadLibraryW(PWSTR filename);` - stdcall calling convention function, imported from kernel32.dll!LoadLibraryW
- `static void __attribute(__internal) OleCheck(HRESULT hr);` - function implemented internally by the PascalScript runtime
- `static HRESULT __attribute(__com(7)) __attribute(__stdcall) IClrObject_ToString(IClrMethodInfo this, IntPtr* ret);` - stdcall calling convention function, the 7th entry in the vtable of a COM object

Initialised global variables are supported by emitting the initialisation instructions in a separate function (with an additional initialisation global boolean) and having all non-static functions call it first.  
The script's entry point is also set to the initialiser function.

An `__attribute(__open)` array of any pointer will be emitted as array of const (that is, open array of pointer).  
"Implicit cast to pointer" for array-of-const element assignment will emit `cpval` instruction (that is, copy constructor), like the official PascalScript compiler.

Because the PascalScript runtime is an interpreter, the compiler has a few optimisations for size (that is, number of instructions):

- Removing un-needed nops where possible
- Removing pops directly before ret (return instruction fixes up the stack itself)
- Immediate operands are only spilled to stack when needed.
  - For example, emitting `add Var1, UnicodeString("ABCD")` instead of (what the official PascalScript compiler does) `pushtype UnicodeString ; assign Var2, Var1 ; add Var2, UnicodeString("ABCD") ; assign Var1, Var2 ; pop`, saving 4 instructions.

