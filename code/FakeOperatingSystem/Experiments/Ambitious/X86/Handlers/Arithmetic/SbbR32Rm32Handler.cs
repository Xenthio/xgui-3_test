namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x1B — SBB r32, r/m32
public class SbbR32Rm32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x1B;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string destReg = X86AddressingHelper.GetRegisterName( reg );

		uint src;
		if ( mod == 3 )
		{
			src = core.Registers[X86AddressingHelper.GetRegisterName( rm )];
			core.Registers["eip"] += 2;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			src = core.ReadDword( addr );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
		}

		uint borrow = core.CarryFlag ? 1u : 0u;
		uint dst = core.Registers[destReg];
		ulong fullResult = (ulong)dst - (ulong)src - (ulong)borrow;
		uint result = (uint)(fullResult & 0xFFFFFFFF);
		core.CarryFlag = fullResult > 0xFFFFFFFF;
		core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80000000) != 0;
		core.ZeroFlag = result == 0;
		core.SignFlag = (result & 0x80000000) != 0;
		core.Registers[destReg] = result;
		core.LogMaths( $"SBB r32, r/m32: {dst:X8} - {src:X8} - {borrow} = {result:X8}" );
	}
}
