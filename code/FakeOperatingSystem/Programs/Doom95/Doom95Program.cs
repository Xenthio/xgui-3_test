﻿using FakeOperatingSystem;

public class Doom95Program : NativeProgram
{
	public override string FilePath => "FakeSystemRoot/Program Files/Ultimate Doom for Windows 95/doom95.exe";
	public override void Main( NativeProcess process )
	{
		// TODO: Replace with your actual Doom 95 window/panel
		var window = new Wad(); // If you have a Wad.razor for Doom
		process.RegisterWindow( window );
		// window.Show();
	}
}
