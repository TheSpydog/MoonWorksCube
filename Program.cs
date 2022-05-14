using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using MoonWorks;
using MoonWorks.Graphics;

public class Program
{
	public static void Main(string[] args)
	{
		Game g = new Game(
			new WindowCreateInfo(
				"Cube",
				640,
				480,
				ScreenMode.Windowed
			),
			PresentMode.FIFORelaxed
		);

		g.SetState(new MainState(g));
		g.Run();
	}
}
