using FakeOperatingSystem;

public static class UserProfileHelper
{
	public static string GetProfilePath( string userName )
		=> FakeOSLoader.UserSystemEnabled
			? $@"C:\Documents and Settings\{userName}\"
			: @"C:\";

	public static string GetMyDocumentsPath( string userName )
		=> FakeOSLoader.UserSystemEnabled
			? $@"C:\Documents and Settings\{userName}\My Documents\"
			: @"C:\My Documents\";

	public static string GetUserRegistryHivePath( string userName )
		=> FakeOSLoader.UserSystemEnabled
			? $@"C:\Documents and Settings\{userName}\NTUSER.DAT"
			: @"C:\NTUSER.DAT";
}
