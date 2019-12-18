namespace JBSnorro.GitTools
{
	/// <summary>
	/// Represents the return value of the script, to be interpreted by 'git bisect run'. 
	/// </summary>
	public enum ExitCodes
	{
		/// <summary> Indicates the current code is good (in the context of a git bisect). </summary>
		Good = 0,
		/// <summary> Indicates the current code is bad (in the context of a git bisect). </summary>
		Bad = 1,
		/// <summary> Indicates the current code cannot be tested (in the context of a git bisect). </summary>
		Skip = 125,
		/// <summary> Indicates the current git bisect run should be aborted. </summary>
		Abort = 255
	}
}
