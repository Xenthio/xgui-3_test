namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// <summary>
/// 0xFE — INC/DEC r/m8
///   /0 = INC r/m8
///   /1 = DEC r/m8
/// </summary>
public class OpcodeFEHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0xFE;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);

		uint instrLen;
		byte val;

		if ( mod == 3 )
		{
			// Register operand
			string regName = X86AddressingHelper.GetRegisterName( rm );
			val = (byte)(core.Registers[regName] & 0xFF);
			byte result;
			if ( reg == 0 ) // INC
			{
				result = (byte)(val + 1);
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				core.OverflowFlag = val == 0x7F;
				// CF not affected
			}
			else // DEC
			{
				result = (byte)(val - 1);
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				core.OverflowFlag = val == 0x80;
				// CF not affected
			}
			core.Registers[regName] = (core.Registers[regName] & 0xFFFFFF00) | result;
			instrLen = 2;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			val = core.ReadByte( addr );
			byte result;
			if ( reg == 0 ) // INC
			{
				result = (byte)(val + 1);
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				core.OverflowFlag = val == 0x7F;
			}
			else // DEC
			{
				result = (byte)(val - 1);
				core.ZeroFlag     = result == 0;
				core.SignFlag     = (result & 0x80) != 0;
				core.OverflowFlag = val == 0x80;
			}
			core.WriteByte( addr, result );
			instrLen = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		}

		core.Registers["eip"] += instrLen;
	}
}
