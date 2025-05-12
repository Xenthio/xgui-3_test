using FakeOperatingSystem;

namespace XGUI;

public class ProcessListOption : ListOption
{
	public BaseProcess Process;
	public override void SetPropertyObject( string name, object value )
	{
		switch ( name )
		{
			case "ownerprocess":
				Process = value as BaseProcess;
				break;
			default:
				base.SetPropertyObject( name, value );
				break;
		}
	}
}
