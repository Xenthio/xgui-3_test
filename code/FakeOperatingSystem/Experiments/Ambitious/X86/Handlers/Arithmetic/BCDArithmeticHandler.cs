namespace FakeOperatingSystem.Experiments.Ambitious.X86.Handlers;

public class BCDArithmeticHandler : IInstructionHandler
{
	public bool CanHandle( byte opcode ) =>
		opcode == 0x27 || // DAA - Decimal Adjust AL after Addition
		opcode == 0x2F || // DAS - Decimal Adjust AL after Subtraction
		opcode == 0x37 || // AAA - ASCII Adjust After Addition
		opcode == 0x3F || // AAS - ASCII Adjust AL After Subtraction
		opcode == 0xD4 || // AAM - ASCII Adjust AX After Multiply
		opcode == 0xD5;   // AAD - ASCII Adjust AX Before Division

	public void Execute( X86Core core )
	{
		uint eip = core.Registers["eip"];
		byte opcode = core.ReadByte( eip );

		switch ( opcode )
		{
			case 0x3F: // AAS - ASCII Adjust AL After Subtraction
				{
					// Get current value of AX (both AL and AH)
					uint eax = core.Registers["eax"];
					byte al = (byte)(eax & 0xFF);
					byte ah = (byte)((eax >> 8) & 0xFF);

					// Perform ASCII adjustment
					if ( (al & 0x0F) > 9 || core.CarryFlag )
					{
						al = (byte)((al - 6) & 0x0F);
						ah = (byte)(ah - 1);
						core.CarryFlag = true;
						core.OverflowFlag = true;
					}
					else
					{
						core.CarryFlag = false;
						core.OverflowFlag = false;
					}

					// Update EAX with new AL and AH values
					core.Registers["eax"] = (core.Registers["eax"] & 0xFFFF0000) | ((uint)ah << 8) | al;

					// Set flags
					core.ZeroFlag = al == 0;
					core.SignFlag = (al & 0x80) != 0;

					Log.Info( $"AAS: Adjusted AL from BCD subtraction, result=0x{al:X2}, flags: ZF={core.ZeroFlag}, SF={core.SignFlag}, CF={core.CarryFlag}" );

					core.Registers["eip"] += 1;
				}
				break;

			// Other BCD arithmetic operations would go here

			default:
				Log.Warning( $"Unimplemented BCD arithmetic instruction: 0x{opcode:X2}" );
				core.Registers["eip"] += 1;
				break;
		}
	}
}
