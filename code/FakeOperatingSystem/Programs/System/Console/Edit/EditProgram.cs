using FakeOperatingSystem;
using FakeOperatingSystem.OSFileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class EditProgram : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Windows/System32/edit.exe";
	public override bool ConsoleApp => true;

	// Key Codes based on ConsoleHostReader and ConsolePanel
	private const int KEY_NULL_INTRO = 0x00;

	private const int SCAN_CODE_UP = 0x48;
	private const int SCAN_CODE_DOWN = 0x50;
	private const int SCAN_CODE_LEFT = 0x4B;
	private const int SCAN_CODE_RIGHT = 0x4D;
	private const int SCAN_CODE_HOME = 0x47;
	private const int SCAN_CODE_END = 0x4F;
	private const int SCAN_CODE_DELETE = 0x53;
	// Note: PageUp/PageDown are not uniquely identifiable with current ConsolePanel, they send ESC

	private const int KEY_ENTER = '\n';       // ConsolePanel sends \n for enter button
	private const int KEY_BACKSPACE = '\b';
	private const int KEY_ESCAPE = 27;
	private const int KEY_TAB = '\t';
	// Ctrl+S, Ctrl+O etc. are not directly supported by ConsolePanel -> ConsoleHostReader yet.

	// ANSI Escape Codes
	private const string ANSI_CLEAR_SCREEN = "\x1B[2J";
	private const string ANSI_CURSOR_HOME = "\x1B[H";
	// private const string ANSI_HIDE_CURSOR = "\x1B[?25l"; // ConsolePanel consumes but doesn't act
	// private const string ANSI_SHOW_CURSOR = "\x1B[?25h"; // ConsolePanel consumes but doesn't act

	private List<StringBuilder> _buffer = new List<StringBuilder>();
	private string _currentFilePath = "UNTITLED1";
	private bool _isUntitled = true;
	private bool _isDirty = false;

	private int _cursorBufferRow = 0; // Cursor's current line in the _buffer
	private int _cursorBufferCol = 0; // Cursor's current column in the _buffer line

	private int _viewTopRow = 0;      // First line of _buffer visible in the editor window
	private int _viewLeftCol = 0;     // First column of _buffer lines visible (for horizontal scroll)

	// Editor display dimensions (could be made dynamic if console size was known)
	// Assuming a standard 80x25 console, minus menu/borders/status
	private int _textDisplayWidth = 78; // Width for text content (inside borders)
	private int _textDisplayHeight = 20; // Height for text content

	private string _statusMessage = "";
	private DateTime _statusMessageTime;

	public override void Main( NativeProcess process, Win32LaunchOptions launchOptions = null )
	{
		string filePathArgument = launchOptions?.Arguments?.Trim();
		if ( !string.IsNullOrWhiteSpace( filePathArgument ) )
		{
			_currentFilePath = filePathArgument;
			_isUntitled = false;
			LoadFile();
		}
		else
		{
			_buffer.Add( new StringBuilder() ); // Start with one empty line for new file
		}
		_isDirty = false; // Reset dirty flag

		// process.StandardOutput.Write(ANSI_HIDE_CURSOR); // Attempt to hide ConsolePanel's default caret

		bool running = true;
		while ( running )
		{
			DrawScreen( process.StandardOutput );
			int keyCode = process.StandardInput.Read();

			if ( keyCode == -1 ) // EOF or error
			{
				running = false;
				break;
			}
			running = ProcessKeyInput( process, keyCode );
		}

		// process.StandardOutput.Write(ANSI_SHOW_CURSOR); // Restore ConsolePanel's caret (if it worked)
		process.StandardOutput.Write( ANSI_CLEAR_SCREEN ); // Clear screen on exit
		process.StandardOutput.Write( ANSI_CURSOR_HOME );
		process.StandardOutput.WriteLine( "Exited edit.com." ); // Final message
	}

	private void LoadFile()
	{
		_buffer.Clear();
		if ( !_isUntitled && VirtualFileSystem.Instance.FileExists( _currentFilePath ) )
		{
			try
			{
				string[] lines = VirtualFileSystem.Instance.ReadAllText( _currentFilePath )
												.Replace( "\r\n", "\n" ) // Normalize newlines
												.Split( '\n' );
				foreach ( var line in lines )
				{
					_buffer.Add( new StringBuilder( line ) );
				}
				if ( _buffer.Count == 0 ) _buffer.Add( new StringBuilder() ); // Ensure at least one line
				SetStatusMessage( $"Loaded '{_currentFilePath}'" );
			}
			catch ( Exception e )
			{
				_buffer.Add( new StringBuilder() ); // Start fresh
				SetStatusMessage( $"Error loading: {e.Message.Split( '\n' ).FirstOrDefault()}" );
			}
		}
		else
		{
			_buffer.Add( new StringBuilder() );
			if ( !_isUntitled ) SetStatusMessage( $"New file: '{_currentFilePath}'" );
		}
		_cursorBufferRow = 0;
		_cursorBufferCol = 0;
		_viewTopRow = 0;
		_viewLeftCol = 0;
		_isDirty = false;
	}

	private void SaveFile( string path = null )
	{
		string savePath = path ?? _currentFilePath;
		if ( string.IsNullOrWhiteSpace( savePath ) || savePath.Equals( "UNTITLED1", StringComparison.OrdinalIgnoreCase ) )
		{
			// In a real version, we'd prompt for a filename here.
			// This would require a sub-loop for input on the status line.
			SetStatusMessage( "Save As: Filename needed (not implemented). Press ESC." );
			return;
		}

		try
		{
			List<string> linesToSave = _buffer.Select( sb => sb.ToString() ).ToList();
			VirtualFileSystem.Instance.WriteAllText( savePath, string.Join( "\r\n", linesToSave ) );
			_currentFilePath = savePath;
			_isUntitled = false;
			_isDirty = false;
			SetStatusMessage( $"File saved: {savePath}" );
		}
		catch ( Exception e )
		{
			SetStatusMessage( $"Error saving: {e.Message.Split( '\n' ).FirstOrDefault()}" );
		}
	}

	private bool ProcessKeyInput( NativeProcess process, int keyCode )
	{
		_statusMessage = ""; // Clear old status message on new input

		if ( keyCode == KEY_NULL_INTRO ) // Extended key
		{
			keyCode = process.StandardInput.Read(); // Read the actual scan code
			switch ( keyCode )
			{
				case SCAN_CODE_UP:
					if ( _cursorBufferRow > 0 ) _cursorBufferRow--;
					break;
				case SCAN_CODE_DOWN:
					if ( _cursorBufferRow < _buffer.Count - 1 ) _cursorBufferRow++;
					break;
				case SCAN_CODE_LEFT:
					if ( _cursorBufferCol > 0 ) _cursorBufferCol--;
					else if ( _cursorBufferRow > 0 ) // Wrap to end of previous line
					{
						_cursorBufferRow--;
						_cursorBufferCol = _buffer[_cursorBufferRow].Length;
					}
					break;
				case SCAN_CODE_RIGHT:
					if ( _cursorBufferCol < CurrentLineLength() ) _cursorBufferCol++;
					else if ( _cursorBufferRow < _buffer.Count - 1 ) // Wrap to start of next line
					{
						_cursorBufferRow++;
						_cursorBufferCol = 0;
					}
					break;
				case SCAN_CODE_HOME:
					_cursorBufferCol = 0;
					break;
				case SCAN_CODE_END:
					_cursorBufferCol = CurrentLineLength();
					break;
				case SCAN_CODE_DELETE:
					if ( _cursorBufferCol < CurrentLineLength() )
					{
						_buffer[_cursorBufferRow].Remove( _cursorBufferCol, 1 );
						_isDirty = true;
					}
					else if ( _cursorBufferRow < _buffer.Count - 1 ) // Delete at end of line, merge with next
					{
						_buffer[_cursorBufferRow].Append( _buffer[_cursorBufferRow + 1].ToString() );
						_buffer.RemoveAt( _cursorBufferRow + 1 );
						_isDirty = true;
					}
					break;
				default: // Unknown scan code
					break;
			}
		}
		else // Normal key
		{
			switch ( keyCode )
			{
				case KEY_ENTER:
					StringBuilder currentLine = _buffer[_cursorBufferRow];
					string remainder = currentLine.ToString().Substring( _cursorBufferCol );
					currentLine.Remove( _cursorBufferCol, currentLine.Length - _cursorBufferCol );
					_buffer.Insert( _cursorBufferRow + 1, new StringBuilder( remainder ) );
					_cursorBufferRow++;
					_cursorBufferCol = 0;
					_isDirty = true;
					break;
				case KEY_BACKSPACE:
					if ( _cursorBufferCol > 0 )
					{
						_buffer[_cursorBufferRow].Remove( _cursorBufferCol - 1, 1 );
						_cursorBufferCol--;
						_isDirty = true;
					}
					else if ( _cursorBufferRow > 0 ) // Backspace at start of line, merge with previous
					{
						string lineToAppend = _buffer[_cursorBufferRow].ToString();
						_buffer.RemoveAt( _cursorBufferRow );
						_cursorBufferRow--;
						_cursorBufferCol = CurrentLineLength();
						_buffer[_cursorBufferRow].Append( lineToAppend );
						_isDirty = true;
					}
					break;
				case KEY_TAB:
					// Insert spaces for tab, typically 4 or 8
					const int tabSize = 4;
					string tabSpaces = new string( ' ', tabSize );
					_buffer[_cursorBufferRow].Insert( _cursorBufferCol, tabSpaces );
					_cursorBufferCol += tabSize;
					_isDirty = true;
					break;
				case KEY_ESCAPE:
					// For now, ESC could be a way to quit if dirty (after a confirmation, not implemented yet)
					// Or to clear a status message / cancel a mode.
					// A simple quit for now, later could add "Save changes? Y/N"
					if ( _isDirty )
					{
						SetStatusMessage( "Unsaved changes! Press ESC again to quit without saving, or F2 to Save." );
						// This needs a state machine or a sub-loop to handle confirmation, which is complex here.
						// For now, let's make it a direct quit if ESC is pressed again.
						// This is a placeholder for a more robust exit confirmation.
						int nextKey = process.StandardInput.Read();
						if ( nextKey == KEY_ESCAPE ) return false; // Quit without saving
						else if ( nextKey == KEY_NULL_INTRO && process.StandardInput.Read() == 0x3C ) // F2 Scan Code (placeholder)
						{
							SaveFile();
							if ( !_isDirty ) return false; // Quit if save was successful
						}
						// else, continue editing
					}
					else
					{
						return false; // Exit if not dirty
					}
					break;
				default:
					if ( keyCode >= 32 && keyCode <= 126 ) // Printable ASCII
					{
						_buffer[_cursorBufferRow].Insert( _cursorBufferCol, (char)keyCode );
						_cursorBufferCol++;
						_isDirty = true;
					}
					break;
			}
		}
		EnsureCursorInBounds();
		ScrollToViewCursor();
		return true; // Continue running
	}

	private int CurrentLineLength()
	{
		if ( _cursorBufferRow >= 0 && _cursorBufferRow < _buffer.Count )
		{
			return _buffer[_cursorBufferRow].Length;
		}
		return 0;
	}

	private void EnsureCursorInBounds()
	{
		_cursorBufferRow = Math.Max( 0, Math.Min( _cursorBufferRow, _buffer.Count - 1 ) );
		_cursorBufferCol = Math.Max( 0, Math.Min( _cursorBufferCol, CurrentLineLength() ) );
	}

	private void ScrollToViewCursor()
	{
		// Adjust vertical scroll
		if ( _cursorBufferRow < _viewTopRow )
		{
			_viewTopRow = _cursorBufferRow;
		}
		if ( _cursorBufferRow >= _viewTopRow + _textDisplayHeight )
		{
			_viewTopRow = _cursorBufferRow - _textDisplayHeight + 1;
		}

		// Adjust horizontal scroll (basic)
		if ( _cursorBufferCol < _viewLeftCol )
		{
			_viewLeftCol = _cursorBufferCol;
		}
		if ( _cursorBufferCol >= _viewLeftCol + _textDisplayWidth )
		{
			_viewLeftCol = _cursorBufferCol - _textDisplayWidth + 1;
		}
		_viewLeftCol = Math.Max( 0, _viewLeftCol );
	}

	private void SetStatusMessage( string message )
	{
		_statusMessage = message;
		_statusMessageTime = DateTime.UtcNow;
	}

	private void DrawScreen( TextWriter output )
	{
		StringBuilder screen = new StringBuilder();
		screen.Append( ANSI_CLEAR_SCREEN );
		screen.Append( ANSI_CURSOR_HOME );

		// 1. Menu Bar (Static for now, add F-key hints later if possible)
		string menuText = " File  Edit  Search  View  Options  Help - F2:Save ESC:Exit";
		screen.Append( menuText.PadRight( _textDisplayWidth + 2 ) ); // +2 for borders
		screen.Append( "\n" );

		// 2. Top Border with Filename
		string dirtyMarker = _isDirty ? "*" : "";
		string title = $" {_currentFilePath.ToUpper()}{dirtyMarker} ";
		if ( title.Length > _textDisplayWidth - 2 ) title = title.Substring( 0, _textDisplayWidth - 5 ) + "... ";

		int dashesTotal = _textDisplayWidth - title.Length;
		int dashesLeft = Math.Max( 0, dashesTotal / 2 );
		int dashesRight = Math.Max( 0, dashesTotal - dashesLeft );

		screen.Append( "┌" );
		screen.Append( new string( '─', dashesLeft ) );
		screen.Append( title );
		screen.Append( new string( '─', dashesRight ) );
		screen.Append( "┐\n" );

		// 3. Text Area
		for ( int i = 0; i < _textDisplayHeight; i++ )
		{
			screen.Append( "│" ); // Left border
			int bufferLineIndex = _viewTopRow + i;
			if ( bufferLineIndex < _buffer.Count )
			{
				string line = _buffer[bufferLineIndex].ToString();
				string visiblePart = "";
				if ( _viewLeftCol < line.Length )
				{
					visiblePart = line.Substring( _viewLeftCol );
				}

				for ( int j = 0; j < _textDisplayWidth; j++ )
				{
					int charIndexInVisiblePart = j;
					// Check if this is the cursor position
					if ( bufferLineIndex == _cursorBufferRow && (_viewLeftCol + j) == _cursorBufferCol )
					{
						// Simulate cursor: Use a block or underscore, or simply skip drawing the char
						// For simplicity, let's use a placeholder. A real cursor would involve inverse video or special char.
						// If we had SGR support: screen.Append("\x1B[7m"); // Inverse
						if ( charIndexInVisiblePart < visiblePart.Length )
							screen.Append( visiblePart[charIndexInVisiblePart] ); // Draw char under cursor for now
						else
							screen.Append( ' ' ); // Cursor at end of line or empty space
												  // if (had SGR: screen.Append("\x1B[0m"); // Reset inverse
					}
					else if ( charIndexInVisiblePart < visiblePart.Length )
					{
						screen.Append( visiblePart[charIndexInVisiblePart] );
					}
					else
					{
						screen.Append( ' ' );
					}
				}
			}
			else
			{
				screen.Append( new string( ' ', _textDisplayWidth ) ); // Empty line
			}
			// Scrollbar (simplified)
			char scrollChar = ' '; // Default to empty if no scrollbar part applies
			if ( i == 0 ) // Current display line is the top-most
			{
				if ( _viewTopRow > 0 ) // If there's content above the current view
					scrollChar = '↑';
			}
			else if ( i == _textDisplayHeight - 1 ) // Current display line is the bottom-most
			{
				if ( _viewTopRow + _textDisplayHeight < _buffer.Count ) // If there's content below the current view
					scrollChar = '↓';
			}
			else // Current display line is in the middle (between top and bottom)
			{
				// This makes the body of the scrollbar solid if scrolling is possible
				scrollChar = '█';
			}
			screen.Append( scrollChar ); // Append the determined scrollbar character (or space)
			screen.Append( "\n" );     // Append the right border character and then a newline
		}

		// 4. Bottom Border
		screen.Append( "└" );
		screen.Append( new string( '─', _textDisplayWidth ) );
		screen.Append( "┘\n" );

		// 5. Status Line
		string status = _statusMessage;
		if ( string.IsNullOrWhiteSpace( status ) && (DateTime.UtcNow - _statusMessageTime).TotalSeconds > 5 )
		{
			status = $"Ln {_cursorBufferRow + 1}, Col {_cursorBufferCol + 1}";
		}
		screen.Append( status.PadRight( _textDisplayWidth + 2 ) );
		screen.Append( "\n" );

		output.Write( screen.ToString() );
	}
}
