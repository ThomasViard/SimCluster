using System.Text;

namespace Common;

public class PrefixedConsoleWriter(TextWriter originalWriter, string prefix) : TextWriter
{
    private readonly TextWriter _originalWriter = originalWriter;
    private readonly string _prefix = prefix;
    private bool _isNewLine = true;

    public override Encoding Encoding => _originalWriter.Encoding;

    public override void Write(char value)
    {
        if (_isNewLine && value != '\n' && value != '\r')
        {
            _originalWriter.Write(_prefix);
            _isNewLine = false;
        }

        _originalWriter.Write(value);

        if (value == '\n')
        {
            _isNewLine = true;
        }
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        if (_isNewLine)
        {
            _originalWriter.Write(_prefix);
            _isNewLine = false;
        }

        _originalWriter.Write(value);

        if (value.EndsWith('\n'))
        {
            _isNewLine = true;
        }
    }

    public override void WriteLine(string? value)
    {
        if (_isNewLine)
        {
            _originalWriter.Write(_prefix);
        }

        _originalWriter.WriteLine(value);
        _isNewLine = true;
    }

    public override void WriteLine()
    {
        _originalWriter.WriteLine();
        _isNewLine = true;
    }
}
