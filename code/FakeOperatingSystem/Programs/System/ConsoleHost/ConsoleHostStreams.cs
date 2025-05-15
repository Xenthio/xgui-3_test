using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FakeOperatingSystem.Console;

public class ConsoleHostWriter : TextWriter
{
	private readonly Action<char> _writeAction;
	public override Encoding Encoding => Encoding.UTF8;

	public ConsoleHostWriter( Action<char> writeAction )
	{
		_writeAction = writeAction;
	}

	public override void Write( char value )
	{
		_writeAction?.Invoke( value );
	}
}

public class ConsoleHostReader : TextReader
{
	public string CurrentLine => _lineBuilder.ToString();
	private StringBuilder _lineBuilder = new();
	private TaskCompletionSource<string> _inputTaskSource = new();

	// Called by UI for each character typed
	public void SubmitChar( char c )
	{
		if ( c == '\r' || c == '\n' )
		{
			string line = _lineBuilder.ToString();
			_lineBuilder.Clear();
			_inputTaskSource.SetResult( line );
		}
		else if ( c == '\b' )
		{
			// Handle backspace
			if ( _lineBuilder.Length > 0 )
				_lineBuilder.Length--;
		}
		else
		{
			_lineBuilder.Append( c );
		}
	}

	public override string ReadLine()
	{
		// Wait for input to be submitted
		Task<string> task = _inputTaskSource.Task;
		task.Wait(); // Blocking wait

		string line = task.Result;
		_inputTaskSource = new TaskCompletionSource<string>(); // Reset for next input
		return line;
	}
}
