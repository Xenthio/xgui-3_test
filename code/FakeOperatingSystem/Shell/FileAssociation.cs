using System.Collections.Generic;

namespace FakeOperatingSystem.Shell;

/// <summary>
/// Represents a file type association in the virtual file system
/// </summary>
public class FileAssociation
{
	/// <summary>
	/// The file extension this association applies to (e.g., ".txt")
	/// </summary>
	public string Extension { get; set; }

	/// <summary>
	/// A friendly name for the file type (e.g., "Text Document")
	/// </summary>
	public string FriendlyName { get; set; }

	/// <summary>
	/// The icon name to use for this file type
	/// </summary>
	public string IconName { get; set; }

	/// <summary>
	/// The default program to use for opening this file type
	/// </summary>
	public string DefaultProgram { get; set; }

	/// <summary>
	/// If the file type should be shown in the shell's "Create New" context menu
	/// </summary>
	public bool ShouldShowInShellCreateNew { get; set; } = false;

	/// <summary>
	/// The actions that can be performed on this file type
	/// </summary>
	public Dictionary<string, FileAction> Actions { get; set; } = new Dictionary<string, FileAction>();
	public static Dictionary<string, FileAssociation> Associations { get; set; } = new Dictionary<string, FileAssociation>();

	/// <summary>
	/// Create a new file association
	/// </summary>
	public FileAssociation( string extension, string friendlyName, string iconName, string defaultProgram, bool shouldShowInShellCreateNew = false )
	{
		Extension = extension.StartsWith( "." ) ? extension : "." + extension;
		FriendlyName = friendlyName;
		IconName = iconName;
		DefaultProgram = defaultProgram;
		Associations[Extension] = this;
		ShouldShowInShellCreateNew = shouldShowInShellCreateNew;

		// Default "open" action
		if ( !string.IsNullOrEmpty( defaultProgram ) )
		{
			Actions["open"] = new FileAction( "Open", defaultProgram, "\"%1\"" );
		}
	}

	/// <summary>
	/// Add an action for this file type
	/// </summary>
	public void AddAction( string verb, string displayName, string program, string arguments = "\"%1\"" )
	{
		Actions[verb] = new FileAction( displayName, program, arguments );
	}

	/// <summary>
	/// Execute the default action for this file type on the given file
	/// </summary>
	public bool Execute( string filePath )
	{
		return ExecuteAction( "open", filePath );
	}

	/// <summary>
	/// Execute a specific action for this file type on the given file
	/// </summary>
	public bool ExecuteAction( string verb, string filePath )
	{
		if ( !Actions.TryGetValue( verb, out var action ) )
		{
			Log.Warning( $"No '{verb}' action found for {Extension} files" );
			return false;
		}

		return action.Execute( filePath );
	}
}

/// <summary>
/// Represents an action that can be performed on a file
/// </summary>
public class FileAction
{
	/// <summary>
	/// The display name for this action (shown in context menu)
	/// </summary>
	public string DisplayName { get; set; }

	/// <summary>
	/// The program to execute for this action
	/// </summary>
	public string Program { get; set; }

	/// <summary>
	/// The command line arguments (typically containing %1 for the file path)
	/// </summary>
	public string Arguments { get; set; }

	public FileAction( string displayName, string program, string arguments )
	{
		DisplayName = displayName;
		Program = program;
		Arguments = arguments;
	}

	/// <summary>
	/// Execute this action on the given file
	/// </summary>
	public bool Execute( string filePath )
	{
		return true;
	}
}
