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
	private TaskCompletionSource<string> _lineInputTaskSource = new(); // Renamed for clarity
	private TaskCompletionSource<int> _charReadTaskSource; // For single character reads

	// Called by UI for each character typed
	public void SubmitChar( char c )
	{
		// Check if a Read() operation is waiting for a single character
		var currentCharReadTcs = _charReadTaskSource;
		if ( currentCharReadTcs != null && !currentCharReadTcs.Task.IsCompleted )
		{
			// A Read() call is waiting, so fulfill it.
			currentCharReadTcs.SetResult( (int)c );
			// The character is consumed by Read(), so we don't process it for line input or other Read() calls.
			// _charReadTaskSource will be set to null by the Read() method after consumption.
			return;
		}

		// Otherwise, process for ReadLine()
		if ( c == '\r' || c == '\n' ) // Assuming '\r' or '\n' signifies end of line
		{
			string line = _lineBuilder.ToString();
			_lineBuilder.Clear();

			var currentLineInputTcs = _lineInputTaskSource;
			// Fulfill the TaskCompletionSource for ReadLine.
			// ReadLine creates a new TCS for each call, so this should target the correct one.
			currentLineInputTcs.SetResult( line );
		}
		else if ( c == '\b' ) // Handle backspace
		{
			if ( _lineBuilder.Length > 0 )
			{
				_lineBuilder.Length--;
			}
		}
		else
		{
			_lineBuilder.Append( c );
		}
	}

	public override int Read()
	{
		var tcs = new TaskCompletionSource<int>();
		_charReadTaskSource = tcs;

		try
		{
			// Blocking wait for a character to be submitted.
			// SubmitChar will call tcs.SetResult() when a character is typed.
			int charCode = tcs.Task.Result; // .Result will block until the task is completed
			return charCode;
		}
		finally
		{
			// Ensure that _charReadTaskSource is cleared only if it's the one we set for this call.
			// This helps prevent race conditions if Read() could be called from multiple threads,
			// though typical TextReader usage is serial.
			if ( _charReadTaskSource == tcs )
			{
				_charReadTaskSource = null;
			}
		}
	}

	public override string ReadLine()
	{
		// Ensure a fresh TaskCompletionSource for this ReadLine operation.
		_lineInputTaskSource = new TaskCompletionSource<string>();

		// Wait for input to be submitted (i.e., Enter pressed)
		Task<string> task = _lineInputTaskSource.Task;
		task.Wait(); // Blocking wait

		string line = task.Result;
		// _lineInputTaskSource is reset at the beginning of the next ReadLine call.
		return line;
	}
}
