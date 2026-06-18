namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x33 — XOR r32, r/m32
public class XorHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x33;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm  = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );
		uint src;
		uint len;

		if ( mod == 3 )
		{
			src = core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			len = 2;
		}
		else
		{
			uint ea = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			src = core.ReadDword( ea );
			len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
		}

		uint result = core.Registers[destReg] ^ src;
		core.Registers[destReg] = result;
		core.ZeroFlag     = result == 0;
		core.SignFlag     = (result & 0x80000000) != 0;
		core.CarryFlag    = false;
		core.OverflowFlag = false;
		core.Registers["eip"] += len;
	}
}
