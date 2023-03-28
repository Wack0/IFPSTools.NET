using System;
using System.Collections;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

public interface ILineInfo
{
    int Line { get; }
    int Column { get; }

}

public interface IStoredLineInfo : ILineInfo
{
    void Copy(ILineInfo lineInfo);
}

public class ExceptionWithLineInfo : Exception
{
    private readonly Exception exception;
    private readonly ILineInfo lineInfo;

    public override string Message => string.Format("{0} [line {1}, column {2}]", exception.Message, lineInfo.Line, lineInfo.Column);

    public override IDictionary Data => exception.Data;
    public override Exception GetBaseException()
    {
        return exception;
    }
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        exception.GetObjectData(info, context);
    }

    public override string HelpLink { get => exception.HelpLink; set => exception.HelpLink = value; }

    public override string Source { get => exception.Source; set => exception.Source = value; }

    public override string StackTrace => exception.StackTrace;

    public Exception BaseException => exception;

    public ExceptionWithLineInfo(Exception exception, ILineInfo lineInfo)
    {
        this.exception = exception;
        this.lineInfo = lineInfo;
    }
}

public static class ExceptionWithLineInfoExtensions
{
    public static ExceptionWithLineInfo Attach(this Exception exception, ILineInfo lineInfo)
    {
        return new ExceptionWithLineInfo(exception, lineInfo);
    }
}