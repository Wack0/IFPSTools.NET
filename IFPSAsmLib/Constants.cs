using System;
using System.Collections.Generic;
using System.Text;

namespace IFPSAsmLib
{
    internal static class Constants
    {
        internal const char START_BODY = '(';
        internal const char NEXT_BODY = ',';
        internal const char END_BODY = ')';
        internal const char START_ARRAY_INDEX = '[';
        internal const char END_ARRAY_INDEX = ']';
        internal const char START_ATTRIBUTE = '[';
        internal const char END_ATTRIBUTE = ']';

        internal const char STRING_CHAR = '"';
        internal const char COMMENT_START = ';';
        internal const char ELEMENT_START = '.';
        internal const char LABEL_SPECIFIER = ':';

        internal const char BINARY_FALSE = '0';
        internal const char BINARY_TRUE = '1';

        internal const string ELEMENT_OBFUSCATE = ".obfuscate";
        internal const string ELEMENT_VERSION = ".version";
        internal const string ELEMENT_ENTRYPOINT = ".entry";
        internal const string ELEMENT_TYPE = ".type";
        internal const string ELEMENT_ALIAS = ".alias";
        internal const string ELEMENT_DEFINE = ".define";
        internal const string ELEMENT_GLOBALVARIABLE = ".global";
        internal const string ELEMENT_FUNCTION = ".function";

        internal const string ELEMENT_BODY_EXPORTED = "export";
        internal const string ELEMENT_BODY_IMPORTED = "import";

        internal const string FUNCTION_EXTERNAL = "external";

        internal const string FUNCTION_EXTERNAL_INTERNAL = "internal";
        internal const string FUNCTION_EXTERNAL_DLL = "dll";
        internal const string FUNCTION_EXTERNAL_COM = "com";
        internal const string FUNCTION_EXTERNAL_CLASS = "class";

        internal const string FUNCTION_EXTERNAL_CLASS_PROPERTY = "property";

        internal const string FUNCTION_EXTERNAL_DLL_DELAYLOAD = "delayload";
        internal const string FUNCTION_EXTERNAL_DLL_ALTEREDSEARCHPATH = "alteredsearchpath";

        internal const string FUNCTION_FASTCALL = "__fastcall";
        internal const string FUNCTION_PASCAL = "__pascal";
        internal const string FUNCTION_CDECL = "__cdecl";
        internal const string FUNCTION_STDCALL = "__stdcall";

        internal const string FUNCTION_RETURN_VOID = "void";
        internal const string FUNCTION_RETURN_VAL = "returnsval";

        internal const string FUNCTION_ARG_IN = "__in";
        internal const string FUNCTION_ARG_OUT = "__out";
        internal const string FUNCTION_ARG_VAL = "__val"; // same as in
        internal const string FUNCTION_ARG_REF = "__ref"; // same as out

        internal const string TYPE_PRIMITIVE = "primitive";
        internal const string TYPE_ARRAY = "array";
        internal const string TYPE_CLASS = "class";
        internal const string TYPE_COM_INTERFACE = "interface";
        internal const string TYPE_FUNCTION_POINTER = "funcptr";
        internal const string TYPE_RECORD = "record";
        internal const string TYPE_SET = "set";

        internal const string INTEGER_BINARY = "0b";

        internal const string VARIABLE_RET = "retval";
        internal const string VARIABLE_LOCAL_PREFIX = "var";

        internal const string LABEL_NULL = "null";
    }
}
