using FakeOperatingSystem.Experiments.Ambitious.X86.Win32; // For PELoader and ImageResourceUtils
using FakeOperatingSystem.OSFileSystem;
using Sandbox; // For Log, Texture, FileSystem, ImageFormat
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FakeOperatingSystem.Utils;

public static class IconLoader
{
	// This structure is a copy/adaptation of the one in User32Emulator
	// It's defined here to keep IconLoader self-contained or could be moved to a shared location.
	internal struct GrpIconDirEntry
	{
		public byte Width;      // Width, in pixels, of the image
		public byte Height;     // Height, in pixels, of the image
		public byte ColorCount; // Number of colors in image (0 if >=8bpp)
		public byte Reserved;   // Reserved (must be 0)
		public ushort Planes;   // Color Planes
		public ushort BitCount; // Bits per pixel
		public uint BytesInRes; // How many bytes in this resource?
		public ushort ID;       // the ID of the RT_ICON resource
	}

	/// <summary>
	/// Loads a specific icon from an executable file, attempting to find the best match for the desired dimensions.
	/// </summary>
	/// <param name="executablePath">Full path to the executable file.</param>
	/// <param name="iconGroupIdOrName">The ID (uint or ushort) or string name (e.g., "#123") of the RT_GROUP_ICON resource.</param>
	/// <param name="desiredWidth">Desired width of the icon.</param>
	/// <param name="desiredHeight">Desired height of the icon.</param>
	/// <returns>A S&box Texture if successful, otherwise null.</returns>
	public static async Task<Texture> LoadIconFromExeAsync( string executablePath, object iconGroupIdOrName, int desiredWidth, int desiredHeight )
	{
		if ( string.IsNullOrEmpty( executablePath ) )
		{
			Log.Warning( $"[IconLoader] Executable path is null or empty." );
			return null;
		}

		if ( !VirtualFileSystem.Instance.FileExists( executablePath ) )
		{
			// Attempt to resolve from game root if not a full path
			var gamePath = VirtualFileSystem.Instance.GetFullPath( executablePath );
			if ( !VirtualFileSystem.Instance.FileExists( gamePath ) )
			{
				Log.Warning( $"[IconLoader] Executable path not found: {executablePath} (also tried {gamePath})" );
				return null;
			}
			executablePath = gamePath;
		}

		byte[] fileBytes;
		try
		{
			fileBytes = await VirtualFileSystem.Instance.ReadAllBytesAsync( executablePath );
		}
		catch ( Exception ex )
		{
			Log.Error( $"[IconLoader] Error reading executable file '{executablePath}': {ex.Message}" );
			return null;
		}

		if ( fileBytes == null || fileBytes.Length == 0 )
		{
			Log.Warning( $"[IconLoader] Executable file is empty: {executablePath}" );
			return null;
		}

		var peLoader = new PELoader();
		if ( !peLoader.ParseAllResources( fileBytes, out List<PELoader.PEResourceEntry> allResources ) || !allResources.Any() )
		{
			Log.Warning( $"[IconLoader] Failed to parse resources or no resources found in '{executablePath}'." );
			return null;
		}

		uint groupIconId;
		string groupIconName = null;

		if ( iconGroupIdOrName is string nameStr )
		{
			if ( nameStr.StartsWith( "#" ) && uint.TryParse( nameStr.AsSpan( 1 ), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out uint parsedId ) )
			{
				groupIconId = parsedId;
			}
			// If PELoader is enhanced to provide string names, we would look up by string name here.
			// For now, we assume string names are not directly supported by PELoader.PEResourceEntry.Name for lookup.
			else
			{
				// Attempt to find a resource by string name if PELoader is ever updated to store them.
				// This part is speculative based on current PELoader.
				Log.Warning( $"[IconLoader] String icon group name '{nameStr}' provided. Direct string name lookup in PE resources is complex and may not be fully supported by current PELoader. This will likely fail if not in '#ID' format." );
				// As a fallback, if it's a common name, one might hardcode an ID, but that's not robust.
				// We'll let it try to find by name if PELoader's PEResourceEntry.Name could be a string.
				// However, PEResourceEntry.Name is uint. So this path is problematic for non-"#ID" strings.
				groupIconName = nameStr; // Store for potential future use if PELoader changes
				groupIconId = 0; // Will rely on finding by name if PELoader supported it, or fail.
								 // For now, this path for arbitrary string names will likely fail to find a match.
			}
		}
		else if ( iconGroupIdOrName is uint idAsUint )
		{
			groupIconId = idAsUint;
		}
		else if ( iconGroupIdOrName is ushort idAsUshort )
		{
			groupIconId = idAsUshort;
		}
		else if ( iconGroupIdOrName is int idAsInt && idAsInt >= 0 )
		{
			groupIconId = (uint)idAsInt;
		}
		else
		{
			Log.Error( $"[IconLoader] Invalid iconGroupIdOrName type: {iconGroupIdOrName?.GetType()} (Value: '{iconGroupIdOrName}')" );
			return null;
		}

		// Find the RT_GROUP_ICON resource data
		PELoader.PEResourceEntry groupIconEntry = null;
		if ( groupIconName != null )
		{
			// This part is speculative: PELoader.PEResourceEntry.Name is uint.
			// If PELoader were to store string names, this is where you'd use it.
			// For now, this will not work as intended.
			Log.Info( $"[IconLoader] Attempting to find group icon by string name '{groupIconName}' (this may not be supported by PELoader)." );
			// groupIconEntry = allResources.FirstOrDefault(r => r.Type == 14 && r.StringName == groupIconName); // Assuming StringName field existed
		}

		if ( groupIconEntry == null && groupIconId != 0 )
		{ // If not found by name or if ID was primary
			groupIconEntry = allResources.FirstOrDefault( r => r.Type == 14 && r.Name == groupIconId );
		}


		if ( groupIconEntry == null )
		{
			Log.Warning( $"[IconLoader] RT_GROUP_ICON resource not found. Executable='{Path.GetFileName( executablePath )}', GroupID/Name='{iconGroupIdOrName}' (Resolved ID: {groupIconId})" );
			return null;
		}

		var iconDirEntries = new List<GrpIconDirEntry>();
		try
		{
			using var ms = new MemoryStream( groupIconEntry.Data );
			using var reader = new BinaryReader( ms );

			ushort idReserved = reader.ReadUInt16(); // Should be 0
			ushort idType = reader.ReadUInt16();     // Should be 1 for icons
			ushort idCount = reader.ReadUInt16();    // Number of icons in the group

			if ( idReserved != 0 || idType != 1 || idCount == 0 )
			{
				Log.Warning( $"[IconLoader] Invalid GRPICONDIR header in '{Path.GetFileName( executablePath )}'. Type: {idType}, Count: {idCount}" );
				return null;
			}

			for ( int i = 0; i < idCount; i++ )
			{
				if ( ms.Position + 14 > ms.Length )
				{ // 14 bytes for GrpIconDirEntry (excluding ID, which is 2 bytes, total 16 with ID)
				  // Actually, GRPICONDIRENTRY is 14 bytes, then the ID is the RT_ICON ID.
				  // The last field of GRPICONDIRENTRY in MS docs is wID (WORD).
				  // Let's assume the struct size is 14 for the directory entry itself, and ID is the resource ID.
				  // The struct in User32Emulator is 12 bytes + uint BytesInRes + ushort ID = 18 bytes.
				  // Correct GRPICONDIRENTRY is 16 bytes.
					Log.Warning( $"[IconLoader] Unexpected end of stream while reading GRPICONDIR entries." );
					break;
				}
				iconDirEntries.Add( new GrpIconDirEntry
				{
					Width = reader.ReadByte(),      // bWidth
					Height = reader.ReadByte(),     // bHeight
					ColorCount = reader.ReadByte(), // bColorCount
					Reserved = reader.ReadByte(),   // bReserved
					Planes = reader.ReadUInt16(),   // wPlanes
					BitCount = reader.ReadUInt16(), // wBitCount
					BytesInRes = reader.ReadUInt32(),// dwBytesInRes
					ID = reader.ReadUInt16()        // nID
				} );
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"[IconLoader] Exception parsing GRPICONDIR for '{Path.GetFileName( executablePath )}', GroupID='{iconGroupIdOrName}': {ex.Message}" );
			return null;
		}

		if ( !iconDirEntries.Any() )
		{
			Log.Warning( $"[IconLoader] No icon entries found in GRPICONDIR for '{Path.GetFileName( executablePath )}', GroupID='{iconGroupIdOrName}'" );
			return null;
		}

		// Select best matching icon entry
		GrpIconDirEntry bestEntry = iconDirEntries
			.OrderBy( entry => Math.Abs( (entry.Width == 0 ? 256 : entry.Width) - desiredWidth ) + Math.Abs( (entry.Height == 0 ? 256 : entry.Height) - desiredHeight ) )
			.ThenByDescending( entry => (entry.Width == 0 ? 256 : entry.Width) >= desiredWidth && (entry.Height == 0 ? 256 : entry.Height) >= desiredHeight ) // Prefer larger or equal
			.ThenByDescending( entry => entry.BitCount )
			.ThenByDescending( entry => (entry.Width == 0 ? 256 : entry.Width) ) // Then by width
			.FirstOrDefault();

		if ( bestEntry.ID == 0 ) // Default struct if FirstOrDefault fails
		{
			Log.Warning( $"[IconLoader] Could not determine best icon entry for desired size {desiredWidth}x{desiredHeight}." );
			return null;
		}

		// Find the actual RT_ICON resource data
		PELoader.PEResourceEntry actualIconResource = allResources.FirstOrDefault( r => r.Type == 3 && r.Name == bestEntry.ID ); // RT_ICON is type 3

		if ( actualIconResource == null )
		{
			Log.Warning( $"[IconLoader] RT_ICON resource not found for ID {bestEntry.ID} (selected from group '{iconGroupIdOrName}') in '{Path.GetFileName( executablePath )}'." );
			return null;
		}

		if ( ImageResourceUtils.ParseDibAndConvertToRgba( actualIconResource.Data, true, out int iconWidth, out int iconVisualHeight, out byte[] iconRgbaData ) )
		{
			try
			{
				var texture = Texture.Create( iconWidth, iconVisualHeight, ImageFormat.RGBA8888 )
									 .WithData( iconRgbaData )
									 .WithName( $"exe_icon_{Path.GetFileNameWithoutExtension( executablePath )}_{iconGroupIdOrName}_{bestEntry.ID}" )
									 .Finish();
				Log.Info( $"[IconLoader] Successfully loaded icon {iconWidth}x{iconVisualHeight} (ID: {bestEntry.ID}) from group '{iconGroupIdOrName}' in '{Path.GetFileName( executablePath )}'." );
				return texture;
			}
			catch ( Exception ex )
			{
				Log.Error( $"[IconLoader] Failed to create S&box texture for icon ID {bestEntry.ID} from '{Path.GetFileName( executablePath )}': {ex.Message}" );
				return null;
			}
		}
		else
		{
			Log.Warning( $"[IconLoader] Failed to parse DIB data for icon ID {bestEntry.ID} from '{Path.GetFileName( executablePath )}'." );
			return null;
		}
	}
}
