using Sandbox;
using System;

namespace FakeOperatingSystem.Experiments.Ambitious.X86;

public partial class X86Interpreter
{
	[ConCmd( "xguitest_x86_run_exec" )]
	public static void RunX86Exec( string path )
	{
		Log.Info( $"Running x86 executable from: {path}" );

		try
		{
			// Read the file from the virtual file system
			byte[] fileBytes = FileSystem.Data.ReadAllBytes( path ).ToArray();

			if ( fileBytes.Length == 0 )
			{
				Log.Error( $"Failed to read executable file: {path}" );
				return;
			}

			// Create the interpreter
			var interpreter = new X86Interpreter();

			// Load and parse the executable
			if ( interpreter.LoadExecutable( fileBytes ) )
			{
				// Optionally: register API functions or patch imports here

				// Execute the program
				interpreter.Execute();
				//Log.Info( interpreter.DumpRegisters() );
				Log.Info( "Execution completed successfully!" );
			}
			else
			{
				Log.Error( "Failed to load executable." );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error executing x86 program: {ex.Message}" );
		}
	}
}
