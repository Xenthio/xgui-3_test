namespace FakeOperatingSystem.Experiments.Ambitious.X86;
/*
public partial class X86Interpreter
{
	[ConCmd( "xguitest_x86_run_exec" )]
	public static void RunX86Exec( string path )
	{

		// TODO: Fix this
		*//*Log.Info( $"Running x86 executable from: {path}" );

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
			interpreter.OnHaltWithMessageBox += ( title, message, icon, buttons ) =>
			{
				MessageBoxUtility.ShowCustom( message, title, icon, buttons );
			};

			// Load and parse the executable
			if ( interpreter.LoadExecutable( fileBytes, path ) )
			{
				interpreter.StartProcess();
			}
			else
			{
				Log.Error( "Failed to load executable." );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error executing x86 program: {ex.Message}" );
		}*//*
	}
}
*/
