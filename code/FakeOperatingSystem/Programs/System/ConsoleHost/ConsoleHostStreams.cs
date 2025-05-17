using System;
using System.Collections.Generic; // For List
using System.IO;
using System.Linq; // For LastOrDefault
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
	private TaskCompletionSource<string> _lineInputTaskSource = new();
	private TaskCompletionSource<int> _charReadTaskSource;

	public int CaretPositionInLine { get; private set; } = 0;

	private bool _expectingScanCode = false;
	private const char EXTENDED_KEY_INTRO = (char)0x00;

	private const char SCAN_CODE_UP = (char)0x48;
	private const char SCAN_CODE_DOWN = (char)0x50;
	private const char SCAN_CODE_LEFT = (char)0x4B;
	private const char SCAN_CODE_RIGHT = (char)0x4D;
	private const char SCAN_CODE_HOME = (char)0x47;
	private const char SCAN_CODE_END = (char)0x4F;
	private const char SCAN_CODE_DELETE = (char)0x53;

	// --- Internal Command History ---
	private List<string> _commandHistory = new List<string>();
	private int _currentHistoryIndex = -1; // -1 indicates not navigating, or use _commandHistory.Count
	private string _userInputBeforeHistoryNav = null;
	private const int MAX_HISTORY_ITEMS = 50; // Max number of commands to store
	private bool _isNavigatingHistory = false; // True if the current line is from history and not yet edited


	public void SubmitChar( char c )
	{
		var currentCharReadTcs = _charReadTaskSource;
		if ( currentCharReadTcs != null && !currentCharReadTcs.Task.IsCompleted )
		{
			currentCharReadTcs.SetResult( (int)c );
			_expectingScanCode = false;
			_isNavigatingHistory = false; // Any direct Read() char clears history navigation state
			return;
		}

		if ( _expectingScanCode )
		{
			_expectingScanCode = false; // Consume the expectation
			switch ( c )
			{
				case SCAN_CODE_LEFT:
					if ( CaretPositionInLine > 0 ) CaretPositionInLine--;
					_isNavigatingHistory = false; // User is editing
					break;
				case SCAN_CODE_RIGHT:
					if ( CaretPositionInLine < _lineBuilder.Length ) CaretPositionInLine++;
					_isNavigatingHistory = false; // User is editing
					break;
				case SCAN_CODE_HOME:
					CaretPositionInLine = 0;
					_isNavigatingHistory = false; // User is editing
					break;
				case SCAN_CODE_END:
					CaretPositionInLine = _lineBuilder.Length;
					_isNavigatingHistory = false; // User is editing
					break;
				case SCAN_CODE_DELETE:
					if ( CaretPositionInLine < _lineBuilder.Length )
					{
						_lineBuilder.Remove( CaretPositionInLine, 1 );
					}
					_isNavigatingHistory = false; // User is editing
					break;
				case SCAN_CODE_UP:
					if ( _commandHistory.Count > 0 )
					{
						if ( !_isNavigatingHistory || _currentHistoryIndex == -1 || _currentHistoryIndex == _commandHistory.Count )
						{
							// Starting navigation or was at the "new input" line
							_userInputBeforeHistoryNav = _lineBuilder.ToString();
							_currentHistoryIndex = _commandHistory.Count - 1;
						}
						else if ( _currentHistoryIndex > 0 )
						{
							_currentHistoryIndex--;
						}
						// else _currentHistoryIndex is 0, stay at the oldest

						_lineBuilder.Clear().Append( _commandHistory[_currentHistoryIndex] );
						CaretPositionInLine = _lineBuilder.Length;
						_isNavigatingHistory = true;
					}
					break;
				case SCAN_CODE_DOWN:
					if ( _isNavigatingHistory && _currentHistoryIndex != -1 )
					{
						if ( _currentHistoryIndex < _commandHistory.Count - 1 )
						{
							_currentHistoryIndex++;
							_lineBuilder.Clear().Append( _commandHistory[_currentHistoryIndex] );
						}
						else // Reached end of history, restore user's original input or clear
						{
							_currentHistoryIndex = _commandHistory.Count; // Indicate we're at the "new input" line
							_lineBuilder.Clear().Append( _userInputBeforeHistoryNav ?? "" );
							_isNavigatingHistory = false; // No longer strictly navigating a past item
						}
						CaretPositionInLine = _lineBuilder.Length;
					}
					break;
				default:
					// Unknown scan code, user is editing if they were navigating
					_isNavigatingHistory = false;
					break;
			}
			return;
		}

		if ( c == EXTENDED_KEY_INTRO )
		{
			_expectingScanCode = true;
			return;
		}

		// If any other key (printable, backspace, enter) is pressed,
		// and it's not starting an extended key sequence,
		// then the user is editing the current line.
		if ( c != EXTENDED_KEY_INTRO )
		{
			_isNavigatingHistory = false;
		}


		if ( c == '\r' || c == '\n' )
		{
			string lineToSubmit = _lineBuilder.ToString();

			// Add to history
			if ( !string.IsNullOrWhiteSpace( lineToSubmit ) )
			{
				// Avoid adding consecutive duplicates
				if ( _commandHistory.Count == 0 || _commandHistory.Last() != lineToSubmit )
				{
					_commandHistory.Add( lineToSubmit );
				}
				if ( _commandHistory.Count > MAX_HISTORY_ITEMS )
				{
					_commandHistory.RemoveAt( 0 );
				}
			}

			_lineBuilder.Clear();
			CaretPositionInLine = 0;
			_currentHistoryIndex = -1; // Reset history navigation index
			_userInputBeforeHistoryNav = null;
			_isNavigatingHistory = false;

			var currentLineInputTcs = _lineInputTaskSource;
			currentLineInputTcs.SetResult( lineToSubmit );
		}
		else if ( c == '\b' )
		{
			if ( CaretPositionInLine > 0 )
			{
				_lineBuilder.Remove( CaretPositionInLine - 1, 1 );
				CaretPositionInLine--;
			}
		}
		else if ( !char.IsControl( c ) )
		{
			if ( CaretPositionInLine < _lineBuilder.Length )
			{
				_lineBuilder.Insert( CaretPositionInLine, c );
			}
			else
			{
				_lineBuilder.Append( c );
			}
			CaretPositionInLine++;
		}
	}

	public override int Read()
	{
		var tcs = new TaskCompletionSource<int>();
		_charReadTaskSource = tcs;
		_expectingScanCode = false;
		_isNavigatingHistory = false; // Reset history state for direct Read()

		try
		{
			int charCode = tcs.Task.Result;
			return charCode;
		}
		finally
		{
			if ( _charReadTaskSource == tcs )
			{
				_charReadTaskSource = null;
			}
		}
	}

	public override string ReadLine()
	{
		_lineInputTaskSource = new TaskCompletionSource<string>();
		// Don't reset _currentHistoryIndex here, as user might be typing a new command
		// _userInputBeforeHistoryNav is handled by up/down arrow logic
		// _isNavigatingHistory is reset by typing or submitting
		Task<string> task = _lineInputTaskSource.Task;
		task.Wait();

		string line = task.Result;
		return line;
	}
}
