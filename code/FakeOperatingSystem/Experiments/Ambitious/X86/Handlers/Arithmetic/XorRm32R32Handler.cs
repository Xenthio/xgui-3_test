namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x31 — XOR r/m32, r32
public class XorRm32R32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x31;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);

		uint src = core.Registers[X86AddressingHelper.GetRegisterName( reg )];

		if ( mod == 3 )
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint result = core.Registers[destReg] ^ src;
			core.Registers[destReg] = result;
			core.ZeroFlag     = result == 0;
			core.SignFlag     = (result & 0x80000000) != 0;
			core.CarryFlag    = false;
			core.OverflowFlag = false;
			core.Registers["eip"] += 2;
		}
		else
		{
			uint ea     = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint len    = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			uint result = core.ReadDword( ea ) ^ src;
			core.WriteDword( ea, result );
			core.ZeroFlag     = result == 0;
			core.SignFlag     = (result & 0x80000000) != 0;
			core.CarryFlag    = false;
			core.OverflowFlag = false;
			core.Registers["eip"] += len;
		}
	}
}
