using FakeOperatingSystem.Console;
using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.Globalization;
using System.IO;
using System.Text;

// Ensure FakeOperatingSystem and FakeOperatingSystem.Console namespaces are available if ConsoleHostWriter/Reader are in them.
// using FakeOperatingSystem;
// using FakeOperatingSystem.Console;


[StyleSheet] // This will look for ConsolePanel.scss or ConsolePanel.cs.scss
public partial class ConsolePanel : Panel
{
	public Label OutputLabel { get; private set; }
	public Panel ScrollArea { get; private set; }

	// --- Fields to hold writer and reader instances ---
	private StringBuilder outputBuffer = new();
	private ConsoleHostWriter writer;
	private ConsoleHostReader reader;

	public int ReaderCaretPosition => reader?.CaretPositionInLine ?? 0;
	public int CaretPosition => new StringInfo( outputBuffer.ToString() ).LengthInTextElements + ReaderCaretPosition;

	public TimeSince TimeSinceStart = 0;

	private string CaretText => (ReaderCaretPosition >= reader?.CurrentLine.Length) ? "\u2002" : string.Empty;
	private string DisplayText => (outputBuffer?.ToString() ?? "") + (reader?.CurrentLine ?? "") + CaretText;

	private Action<string> SetWindowTitleAction;

	public ConsolePanel()
	{
		AddClass( "console-panel" );
		ScrollArea = Add.Panel( "console-area" );
		OutputLabel = ScrollArea.Add.Label( "" );
		Focus();
	}

	public void Initialize( ConsoleHostWriter writer, ConsoleHostReader reader, Action<string> setTitleAction )
	{
		this.writer = writer;
		this.reader = reader;
		SetWindowTitleAction = setTitleAction;
	}


	public TextWriter GetOutputWriter() => writer;
	public TextReader GetInputReader() => reader;

	public void SetText( string text )
	{
		if ( OutputLabel != null )
		{
			OutputLabel.Text = text;
		}
	}

	public void TryScrollToBottom()
	{
		ScrollArea?.TryScrollToBottom();
	}

	public bool PreferScrollToBottom
	{
		get => ScrollArea?.PreferScrollToBottom ?? false;
		set
		{
			if ( ScrollArea != null )
			{
				ScrollArea.PreferScrollToBottom = value;
			}
		}
	}

	void UpdateOutputDisplay()
	{
		if ( reader == null ) return;
		SetText( DisplayText );
	}

	public override void Tick()
	{
		base.Tick();
		PreferScrollToBottom = true;
	}

	public override bool HasContent => true;
	public override void DrawContent( ref RenderState state )
	{
		base.DrawContent( ref state );

		if ( OutputLabel == null || reader == null ) return;

		var blinkRate = 0.8f;
		var blink = (TimeSinceStart * blinkRate) % blinkRate < (blinkRate * 0.5f);

		var caretRect = OutputLabel.GetCaretRect( CaretPosition );
		caretRect.Left = MathX.FloorToInt( caretRect.Left );
		caretRect.Width = 9;
		var caretHeight = (OutputLabel.ComputedStyle.FontSize?.Value ?? 16) / 4;

		caretRect.Top = MathX.FloorToInt( caretRect.Top + caretRect.Height - caretHeight );
		caretRect.Height = caretHeight;

		var color = OutputLabel.ComputedStyle.FontColor ?? Color.White;
		color = color.WithAlpha( blink ? 1.0f : 0f );

		Graphics.DrawRoundedRectangle( caretRect, color );
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		base.OnButtonTyped( e );
		if ( reader == null ) return;

		if ( e.Button == "enter" ) Submit( '\n' );
		else if ( e.Button == "backspace" ) Submit( '\b' );
		else if ( e.Button == "tab" ) Submit( '\t' );
		else if ( e.Button == "escape" ) Submit( (char)27 );
		else if ( e.Button == "up" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x48 );
		}
		else if ( e.Button == "down" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x50 );
		}
		else if ( e.Button == "left" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x4B );
		}
		else if ( e.Button == "right" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x4D );
		}
		else if ( e.Button == "home" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x47 );
		}
		else if ( e.Button == "end" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x4F );
		}
		else if ( e.Button == "delete" )
		{
			Submit( (char)0x00 );
			Submit( (char)0x53 );
		}
		// Note: Duplicate "home" and "end" were here, removed the single ESC versions
		else if ( e.Button == "pageup" ) Submit( (char)0x1B ); // Placeholder, could be 0x00, 0x49
		else if ( e.Button == "pagedown" ) Submit( (char)0x1B ); // Placeholder, could be 0x00, 0x51
		else if ( e.Button.StartsWith( "f" ) && e.Button.Length > 1 && char.IsDigit( e.Button[1] ) ) Submit( (char)0x1B );
		else if ( e.HasCtrl && e.Button == "z" ) Submit( (char)0x1A );

		e.StopPropagation = true;
	}

	public override void OnKeyTyped( char k )
	{
		base.OnKeyTyped( k );
		Submit( k );
	}

	void Submit( char c )
	{
		if ( reader == null ) return;
		reader.SubmitChar( c );
		UpdateOutputDisplay();
		TryScrollToBottom();
	}

	private enum EscapeSequenceState { None, GotEscape, GotCSI, GotOSC }
	private EscapeSequenceState currentEscapeState = EscapeSequenceState.None;
	private StringBuilder csiParameterBuffer = new StringBuilder();
	private StringBuilder oscStringBuffer = new StringBuilder();
	private bool csiGotQuestionMark = false;


	public void AppendOutput( char c )
	{
		if ( reader == null && currentEscapeState == EscapeSequenceState.None && c != 0x1B )
		{
			// If reader is null, we are likely in a program like 'edit'
			// that directly writes to StandardOutput.
			// We should append directly to outputBuffer if not in an escape sequence.
			outputBuffer.Append( c );
			UpdateOutputDisplay(); // Update display with direct output
			return;
		}

		if ( c == 0x1B ) // Always handle ESC first
		{
			currentEscapeState = EscapeSequenceState.GotEscape;
			csiParameterBuffer.Clear();
			oscStringBuffer.Clear();
			csiGotQuestionMark = false;
			return;
		}

		if ( currentEscapeState != EscapeSequenceState.None )
		{
			DoEscape( c );
		}
		else // Not in an escape sequence
		{
			if ( c == '\b' )
			{
				if ( outputBuffer.Length > 0 )
				{
					outputBuffer.Remove( outputBuffer.Length - 1, 1 );
				}
			}
			else if ( c == '\t' ) { outputBuffer.Append( "    " ); } // Or handle tab stops
			else if ( c == '\r' ) { /* Often ignored, or move cursor to start of line if implemented */ }
			// else if (c == '\n') { outputBuffer.Append(c); } // Newline handled by default append
			else if ( c == 00 ) { return; } // Null char
			else if ( c == '\u001A' ) { return; } // EOF
			else if ( c == '\u000E' ) { return; } // Shift out
			else
			{
				outputBuffer.Append( c );
			}
			UpdateOutputDisplay();
		}
	}

	public void DoEscape( char c )
	{
		bool bufferModified = false;
		switch ( currentEscapeState )
		{
			case EscapeSequenceState.GotEscape:
				if ( c == '[' ) { currentEscapeState = EscapeSequenceState.GotCSI; csiGotQuestionMark = false; }
				else if ( c == ']' ) { currentEscapeState = EscapeSequenceState.GotOSC; oscStringBuffer.Clear(); }
				else { currentEscapeState = EscapeSequenceState.None; /* Invalid sequence, char is lost or could be appended */ }
				break;

			case EscapeSequenceState.GotCSI:
				if ( char.IsDigit( c ) || c == ';' )
				{
					csiParameterBuffer.Append( c );
				}
				else if ( c == '?' && csiParameterBuffer.Length == 0 )
				{
					csiGotQuestionMark = true; // Mark that we saw '?' for DEC private modes
				}
				else // End of parameter sequence, this char is the command
				{
					string paramsStr = csiParameterBuffer.ToString();
					// Log.Info($"CSI Command: {c}, Params: {paramsStr}, QuestionMark: {csiGotQuestionMark}");

					if ( csiGotQuestionMark ) // DEC Private Mode sequences like CSI ? P m h/l
					{
						if ( c == 'l' || c == 'h' ) // Common DECSET/DECRST terminators
						{
							// Example: ?25h (show cursor), ?25l (hide cursor)
							// We are just consuming them for now so they don't print.
							// Actual visual effect (hiding S&box caret) would require more logic.
							// Log.Info($"Consumed DEC Private Mode: ?{paramsStr}{c}");
						}
						// Consume other DEC private sequences too if they end in a letter
					}
					else // Standard CSI sequences
					{
						switch ( c )
						{
							case 'J': // Erase in Display
								if ( paramsStr == "2" ) { outputBuffer.Clear(); bufferModified = true; }
								// else if (paramsStr == "0") { /* Clear from cursor to end of screen */ }
								// else if (paramsStr == "1") { /* Clear from cursor to beginning of screen */ }
								break;
							case 'H': // Cursor Position
									  // If paramsStr is empty or "1;1", effectively moves to home.
									  // For simplicity, cmd.exe's CLS uses ESC[2J ESC[H.
									  // If we clear all on ESC[2J, ESC[H doesn't need to do much more to outputBuffer.
									  // Actual cursor for programs like edit.com is handled by their own logic.
									  // This H is more for terminal's own cursor if it had one separate from input line.
								if ( string.IsNullOrEmpty( paramsStr ) || paramsStr == ";" || paramsStr == "1;1" )
								{
									// For programs like 'edit', they will re-render.
									// For 'cmd' itself, this might mean clearing outputBuffer if not already done by '2J'.
									// If '2J' cleared, this 'H' is just for cursor, which we don't explicitly model here for outputBuffer.
								}
								break;
							case 'm': // Select Graphic Rendition (SGR)
									  // Examples: 0m (reset), 7m (inverse)
									  // We are just consuming them for now.
									  // Actual styling would require ConsolePanel to handle rich text.
									  // Log.Info($"Consumed SGR: {paramsStr}m");
								break;
								// Add other CSI commands as needed: 'K' (Erase in Line), 'A' (Cursor Up), etc.
						}
					}
					currentEscapeState = EscapeSequenceState.None;
					csiParameterBuffer.Clear();
					csiGotQuestionMark = false;
				}
				break;

			case EscapeSequenceState.GotOSC:
				if ( c == '\u0007' || (c == 0x1B && oscStringBuffer.Length > 0 && oscStringBuffer[oscStringBuffer.Length - 1] == '\\') ) // BEL or ESC \ (ST)
				{
					string oscCommand = oscStringBuffer.ToString();
					if ( oscCommand.StartsWith( "0;" ) ) // Set window title
					{
						SetWindowTitleAction?.Invoke( oscCommand.Substring( 2 ) );
					}
					// else if (oscCommand.StartsWith("2;")) // Also set window title (alternative)
					// {
					// SetWindowTitleAction?.Invoke(oscCommand.Substring(2));
					// }
					currentEscapeState = EscapeSequenceState.None;
					oscStringBuffer.Clear();
				}
				else if ( c == 0x1B ) // Standalone ESC might also terminate an OSC sequence improperly
				{
					currentEscapeState = EscapeSequenceState.None; // Or go to GotEscape if it's part of ST (ESC \)
					oscStringBuffer.Clear();
				}
				else
				{
					oscStringBuffer.Append( c );
				}
				break;
		}
		if ( bufferModified ) { UpdateOutputDisplay(); }
	}
}
