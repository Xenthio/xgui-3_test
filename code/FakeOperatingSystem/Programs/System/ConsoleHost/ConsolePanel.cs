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

	// Changed ReaderCaretPosition from a field to a property
	// This now relies on ConsoleHostReader to expose its internal caret position.
	// Ensure ConsoleHostReader has a public int property, e.g., "CaretPositionInLine".
	public int ReaderCaretPosition => reader?.CaretPositionInLine ?? 0;
	public int CaretPosition => new StringInfo( outputBuffer.ToString() ).LengthInTextElements + ReaderCaretPosition;

	public TimeSince TimeSinceStart = 0;

	// the space where the cursor will be, only shown if cursor isn't overlapping anything, character is a fullwidth whitespace
	private string CaretText => (ReaderCaretPosition >= reader?.CurrentLine.Length) ? "\u2002" : string.Empty;
	private string DisplayText => (outputBuffer?.ToString() ?? "") + (reader?.CurrentLine ?? "") + CaretText;

	private Action<string> SetWindowTitleAction; // Action to set the window title

	public ConsolePanel()
	{
		AddClass( "console-panel" );
		ScrollArea = Add.Panel( "console-area" );
		OutputLabel = ScrollArea.Add.Label( "" );

		// Writer and reader will be initialized via the Initialize method
		// Focus this panel to receive keyboard input
		Focus();
	}

	// Modified Initialize method to accept writer and reader
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
		if ( reader == null ) return; // Guard against reader not being initialized yet
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

		if ( OutputLabel == null || reader == null ) return; // Guard against reader not being initialized

		var blinkRate = 0.8f;
		var blink = (TimeSinceStart * blinkRate) % blinkRate < (blinkRate * 0.5f);

		// CaretPosition now correctly uses the ReaderCaretPosition from the reader instance
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
		if ( reader == null ) return; // Guard against reader not being initialized

		if ( e.Button == "enter" ) Submit( '\n' );
		else if ( e.Button == "backspace" ) Submit( '\b' );
		else if ( e.Button == "tab" ) Submit( '\t' );
		else if ( e.Button == "escape" ) Submit( (char)27 );
		else if ( e.Button == "up" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x48 ); // Up arrow scan code
		}
		else if ( e.Button == "down" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x50 ); // Down arrow scan code
		}
		else if ( e.Button == "left" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x4B ); // Left arrow scan code
		}
		else if ( e.Button == "right" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x4D ); // Right arrow scan code
		}
		else if ( e.Button == "home" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x47 ); // Home key scan code
		}
		else if ( e.Button == "end" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x4F ); // End key scan code
		}
		else if ( e.Button == "delete" )
		{
			Submit( (char)0x00 ); // Extended key intro
			Submit( (char)0x53 ); // Delete key scan code
		}
		else if ( e.Button == "home" ) Submit( (char)0x1B );
		else if ( e.Button == "end" ) Submit( (char)0x1B );
		else if ( e.Button == "pageup" ) Submit( (char)0x1B );
		else if ( e.Button == "pagedown" ) Submit( (char)0x1B );
		else if ( e.Button.StartsWith( "f" ) && e.Button.Length > 1 && char.IsDigit( e.Button[1] ) ) Submit( (char)0x1B );
		else if ( e.HasCtrl && e.Button == "z" ) Submit( (char)0x1A );

		e.StopPropagation = true;
	}

	public override void OnKeyTyped( char k )
	{
		base.OnKeyTyped( k );
		Submit( k ); // This will also call reader.SubmitChar(k)
	}

	void Submit( char c )
	{
		if ( reader == null ) return;
		reader.SubmitChar( c ); // ConsoleHostReader.SubmitChar should update its CurrentLine and CaretPositionInLine
		UpdateOutputDisplay();  // This will re-render the text with the new CurrentLine
		TryScrollToBottom();    // The next DrawContent will use the updated CaretPosition
	}

	private enum EscapeSequenceState { None, GotEscape, GotCSI, GotOSC }
	private EscapeSequenceState currentEscapeState = EscapeSequenceState.None;
	private StringBuilder csiParameterBuffer = new StringBuilder();
	private StringBuilder oscStringBuffer = new StringBuilder();

	public void AppendOutput( char c )
	{
		if ( reader == null ) return;

		if ( c == '\b' )
		{
			if ( outputBuffer.Length > 0 )
			{
				outputBuffer.Remove( outputBuffer.Length - 1, 1 );
			}
		}
		else if ( c == 0x1B )
		{
			currentEscapeState = EscapeSequenceState.GotEscape;
			csiParameterBuffer.Clear();
			oscStringBuffer.Clear();
			return;
		}
		else if ( c == 00 ) { return; }
		else if ( c == '\t' ) { outputBuffer.Append( "    " ); }
		else if ( c == '\v' ) { outputBuffer.Append( "\n" ); }
		else if ( c == '\u001A' ) { return; }
		else if ( c == '\u000E' ) { return; }

		else if ( currentEscapeState != EscapeSequenceState.None )
		{
			DoEscape( c );
		}
		else
		{
			outputBuffer.Append( c );
		}
		UpdateOutputDisplay();
	}

	public void DoEscape( char c )
	{
		if ( reader == null ) return;
		bool bufferModified = false;
		switch ( currentEscapeState )
		{
			case EscapeSequenceState.GotEscape:
				if ( c == '[' ) { currentEscapeState = EscapeSequenceState.GotCSI; }
				else if ( c == ']' ) { currentEscapeState = EscapeSequenceState.GotOSC; oscStringBuffer.Clear(); }
				else { currentEscapeState = EscapeSequenceState.None; }
				break;

			case EscapeSequenceState.GotCSI:
				if ( char.IsDigit( c ) || c == ';' ) { csiParameterBuffer.Append( c ); }
				else
				{
					string paramsStr = csiParameterBuffer.ToString();
					switch ( c )
					{
						case 'J':
							if ( paramsStr == "2" ) { outputBuffer.Clear(); bufferModified = true; }
							break;
						case 'H':
							if ( string.IsNullOrEmpty( paramsStr ) || paramsStr == ";" ) { outputBuffer.Clear(); bufferModified = true; }
							break;
					}
					currentEscapeState = EscapeSequenceState.None;
					csiParameterBuffer.Clear();
				}
				break;

			case EscapeSequenceState.GotOSC:
				if ( c == '\u0007' || c == 0x1B )
				{
					string oscCommand = oscStringBuffer.ToString();
					if ( oscCommand.StartsWith( "0;" ) )
					{
						SetWindowTitleAction?.Invoke( oscCommand.Substring( 2 ) );
					}
					currentEscapeState = EscapeSequenceState.None;
					oscStringBuffer.Clear();
					if ( c == 0x1B )
					{
						currentEscapeState = EscapeSequenceState.GotEscape;
					}
				}
				else { oscStringBuffer.Append( c ); }
				break;
		}
		if ( bufferModified ) { UpdateOutputDisplay(); }
	}
}
