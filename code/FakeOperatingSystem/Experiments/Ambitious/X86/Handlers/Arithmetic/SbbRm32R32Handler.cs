namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

/// 0x19 — SBB r/m32, r32
public class SbbRm32R32Handler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) => opcode == 0x19;

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte modrm = core.ReadByte( eip + 1 );
		byte mod = (byte)(modrm >> 6);
		byte reg = (byte)((modrm >> 3) & 0x7);
		byte rm = (byte)(modrm & 0x7);

		string srcReg = X86AddressingHelper.GetRegisterName( reg );
		uint src = core.Registers[srcReg];
		uint borrow = core.CarryFlag ? 1u : 0u;

		if ( mod == 3 )
		{
			string destReg = X86AddressingHelper.GetRegisterName( rm );
			uint dst = core.Registers[destReg];
			ulong fullResult = (ulong)dst - (ulong)src - (ulong)borrow;
			uint result = (uint)(fullResult & 0xFFFFFFFF);
			core.CarryFlag = fullResult > 0xFFFFFFFF;
			core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80000000) != 0;
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80000000) != 0;
			core.Registers[destReg] = result;
			core.Registers["eip"] += 2;
		}
		else
		{
			uint addr = X86AddressingHelper.CalculateEffectiveAddress( core, modrm, eip );
			uint dst = core.ReadDword( addr );
			ulong fullResult = (ulong)dst - (ulong)src - (ulong)borrow;
			uint result = (uint)(fullResult & 0xFFFFFFFF);
			core.CarryFlag = fullResult > 0xFFFFFFFF;
			core.OverflowFlag = ((dst ^ src) & (dst ^ result) & 0x80000000) != 0;
			core.ZeroFlag = result == 0;
			core.SignFlag = (result & 0x80000000) != 0;
			core.WriteDword( addr, result );
			uint len = X86AddressingHelper.GetInstructionLength( modrm, core, eip );
			core.Registers["eip"] += len;
		}
	}
}
