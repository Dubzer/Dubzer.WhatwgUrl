namespace Dubzer.WhatwgUrl;

internal static class Consts
{
	private const int OneKibibyteInBytes = 1024;

	/// <summary>
	/// Max stack allocated arrays length for types
	/// </summary>
	internal static class MaxLengthOnStack
	{
		public const int Byte = OneKibibyteInBytes;

		public const int Char = OneKibibyteInBytes / sizeof(char);

		// Rune is just a `uint` in a trench coat.
		// However, runtime does not guarantee that Rune will take exactly 4 bytes due to memory aligning and CLR shenanigans.
		// This should not be a problem in our case since we will exceed estimate by 2 times at most.
		// 1KiB * 2 = 2KiB which is still far from the stack capacity limit.
		public const int Rune = OneKibibyteInBytes / sizeof(uint);
	}
}
