﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

public static class ResolveJBSnorroDll
{
	/// <summary>
	/// Overrides the name mismatch JBSnorro vs JBSnorro.CI. We must have a different name because otherwise I can't use this on ASDE e.g. because of name clashes.
	/// I think this is because the AssemblyName is JBSnorro and the filename is JBSnorro.CI.
	/// Previously I had built JBSnorro especially with JBSnorro.CI as AssemblyName for this project
	/// </summary>
	public static void Resolve()
	{
		// this just triggers the static ctor
	}
	// the static cctor ensures it's only called once
	static ResolveJBSnorroDll()
	{
		Assembly.LoadFrom("JBSnorro.CI.dll");
	}
}
