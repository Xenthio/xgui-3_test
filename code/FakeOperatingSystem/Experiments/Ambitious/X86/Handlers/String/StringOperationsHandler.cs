namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class StringOperationsHandler : IInstructionHandler
{
	// Track whether we're in a REP prefix state
	private static bool _hasRepPrefix = false;
	private static bool _isRepne = false; // REPNE vs REPE

	public bool CanHandle( byte opcode ) =>
		opcode == 0xF2 || // REPNE/REPNZ prefix
		opcode == 0xF3 || // REP/REPE/REPZ prefix
		opcode == 0xA4 || // MOVSB
		opcode == 0xA5 || // MOVSD
		opcode == 0xAA || // STOSB
		opcode == 0xAB || // STOSD
		opcode == 0xAC || // LODSB
		opcode == 0xAD || // LODSD
		opcode == 0xAE || // SCASB
		opcode == 0xAF;   // SCASD

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		// Handle REP prefixes
		if ( opcode == 0xF2 || opcode == 0xF3 )
		{
			_hasRepPrefix = true;
			_isRepne = (opcode == 0xF2);
			core.Registers["eip"]++;
			return;
		}

		bool continueRep = true;

		switch ( opcode )
		{
			case 0xA4: // MOVSB
				if ( _hasRepPrefix )
				{
					while ( core.Registers["ecx"] != 0 )
					{
						ExecuteMovsb( core );
						core.Registers["ecx"]--;
					}
				}
				else
				{
					ExecuteMovsb( core );
				}
				break;

			case 0xA5: // MOVSD
				if ( _hasRepPrefix )
				{
					while ( core.Registers["ecx"] != 0 )
					{
						ExecuteMovsd( core );
						core.Registers["ecx"]--;
					}
				}
				else
				{
					ExecuteMovsd( core );
				}
				break;

			case 0xAC: // LODSB
				ExecuteLodsb( core );
				break;

			case 0xAA: // STOSB
				ExecuteStosb( core );
				break;

			case 0xAE: // SCASB
				ExecuteScasb( core );
				// For REPNE/REPE, we might need to terminate the loop
				if ( _hasRepPrefix )
				{
					if ( (_isRepne && core.ZeroFlag) || (!_isRepne && !core.ZeroFlag) )
					{
						continueRep = false;
					}
				}
				break;

				// Handle other string operations similarly
		}

		// Always advance EIP by 1 (the instruction length for these string ops)
		core.Registers["eip"]++;

		// Reset REP state
		_hasRepPrefix = false;
	}

	private void ExecuteLodsb( X86Core core )
	{
		// Load byte from DS:SI into AL
		uint esi = core.Registers["esi"];
		byte value = core.ReadByte( esi );

		// Store in AL (preserve other bytes of EAX)
		core.Registers["eax"] = (core.Registers["eax"] & 0xFFFFFF00) | value;

		// Increment/decrement ESI based on Direction Flag
		if ( core.DirectionFlag )
			core.Registers["esi"]--;
		else
			core.Registers["esi"]++;

		Log.Info( $"LODSB: Loaded 0x{value:X2} from [0x{esi:X8}] into AL" );
	}

	private void ExecuteStosb( X86Core core )
	{
		// Store AL to ES:DI
		uint edi = core.Registers["edi"];
		byte al = (byte)(core.Registers["eax"] & 0xFF);

		core.WriteByte( edi, al );

		// Increment/decrement EDI based on Direction Flag
		if ( core.DirectionFlag )
			core.Registers["edi"]--;
		else
			core.Registers["edi"]++;

		Log.Info( $"STOSB: Stored 0x{al:X2} to [0x{edi:X8}]" );
	}

	private void ExecuteScasb( X86Core core )
	{
		// Compare AL with byte at ES:DI
		uint edi = core.Registers["edi"];
		byte al = (byte)(core.Registers["eax"] & 0xFF);
		byte destValue = core.ReadByte( edi );

		// Perform comparison and set flags
		byte result = (byte)(al - destValue);
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80) != 0;
		core.CarryFlag = al < destValue;

		// Increment/decrement EDI based on Direction Flag
		if ( core.DirectionFlag )
			core.Registers["edi"]--;
		else
			core.Registers["edi"]++;

		Log.Info( $"SCASB: Compared AL=0x{al:X2} with [0x{edi:X8}]=0x{destValue:X2}, ZF={core.ZeroFlag}" );
	}

	private void ExecuteMovsb( X86Core core )
	{
		// Copy byte from [ESI] to [EDI]
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		byte value = core.ReadByte( esi );
		core.WriteByte( edi, value );

		// Adjust ESI/EDI based on Direction Flag
		int delta = core.DirectionFlag ? -1 : 1;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);

		Log.Info( $"MOVSB: Copied 0x{value:X2} from [0x{esi:X8}] to [0x{edi:X8}]" );
	}

	private void ExecuteMovsd( X86Core core )
	{
		uint esi = core.Registers["esi"];
		uint edi = core.Registers["edi"];
		uint value = core.ReadDword( esi );
		core.WriteDword( edi, value );

		core.LogVerbose( $"MOVSD: Copied 0x{value:X8} from [0x{esi:X8}] to [0x{edi:X8}]" );

		int delta = core.DirectionFlag ? -4 : 4;
		core.Registers["esi"] = (uint)(esi + delta);
		core.Registers["edi"] = (uint)(edi + delta);
	}
}

