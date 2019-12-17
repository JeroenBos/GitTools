using AppDomainToolkit;
using System;

namespace JBSnorro.GitTools
{
	public interface IAppDomainContext : System.IDisposable
	{
		AppDomain Domain { get; }
	}
	class Adapter : IAppDomainContext, AppDomainToolkit.IDisposable
	{
		internal AppDomainContext<AssemblyTargetLoader, PathBasedAssemblyResolver> adaptee;
		public AppDomain Domain => adaptee.Domain;

		public bool IsDisposed => adaptee.IsDisposed;
		public void Dispose() => adaptee.Dispose();
	}

	// This assembly refers to stuff in the .NET framework that cannot be referenced from .NET Core
	public static class BackwardsCompatibility
	{
		public static IAppDomainContext CreateAppDomain(string appDomainBase, string assemblyPath)
		{
			var domain = AppDomainContext.Create(
				new AppDomainSetup()
				{
					ApplicationBase = appDomainBase,
					ConfigurationFile = assemblyPath + ".config"
				});
			return new Adapter { adaptee = domain };
		}
	}
	public static class RemoteFunc
	{
		public static TResult Invoke<TResult>(AppDomain domain, Func<TResult> toInvoke) => RemoteFunc.Invoke(domain, toInvoke);
		public static TResult Invoke<T, TResult>(AppDomain domain, T arg1, Func<T, TResult> toInvoke) => RemoteFunc.Invoke(domain, arg1, toInvoke);
		public static TResult Invoke<T1, T2, TResult>(AppDomain domain, T1 arg1, T2 arg2, Func<T1, T2, TResult> toInvoke) => RemoteFunc.Invoke(domain, arg1, arg2, toInvoke);
		public static TResult Invoke<T1, T2, T3, TResult>(AppDomain domain, T1 arg1, T2 arg2, T3 arg3, Func<T1, T2, T3, TResult> toInvoke) => RemoteFunc.Invoke(domain, arg1, arg2, arg3, toInvoke);
		public static TResult Invoke<T1, T2, T3, T4, TResult>(AppDomain domain, T1 arg1, T2 arg2, T3 arg3, T4 arg4, Func<T1, T2, T3, T4, TResult> toInvoke) => RemoteFunc.Invoke(domain, arg1, arg2, arg3, arg4, toInvoke);
	}
}
